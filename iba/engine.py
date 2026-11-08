"""Every loop iteration is a new model conversation built from durable state."""
from __future__ import annotations

import json
import re
import threading
import time
from concurrent.futures import ThreadPoolExecutor

from .store import Store, Conflict, encode, uid
from .tools import Tools, TOOL_HELP
from .protocol import outcome_schema, visible_evidence, compact_outcome_contract, TOOL_ARGUMENTS


INSTRUCTIONS = """You are IBA, an autonomous local engineering assistant. Work one concrete tool action at a time.

EVERY STEP you receive two user messages: first a SELF-HANDOFF briefing written by your
previous step (mission, what was tried, latest results, leads, outstanding work), then a
STATE JSON with authoritative evidence IDs, requirements, and allowed kinds. The briefing
is your own note-to-self — continue from it instead of restarting your analysis.

DECISION POLICY
1. Act first, do not narrate. If the request names a file and line, make read_file with {"line":N} the first action. If it names a symbol, property, shader, or error, make search_text the first action and try both Symbol and _Symbol (Unity errors often omit a leading underscore).
2. Think only to choose the next tool. Your reasoning must be at most 3 short sentences and end with "Next: <tool> <target>". Never copy the error, stack trace, code, or task text into your reasoning.
3. One action per step — but when several INDEPENDENT lookups are needed (reading multiple files, searching several patterns), batch them: make "arguments" a list of up to 4 argument objects for the SAME read-only tool and they run in parallel as one action. Do not reply with a plan, a restatement, or wait unless you truly need user input.
4. Investigate before editing: read the exact declaration and binding sites, then edit_file with old_text (the exact snippet to change) and new_text (its replacement) plus the expected_hash from read_file. Use write_file only for new files or complete rewrites — never send a fragment as 'text'.
5. Verify locally when a run_check is available; otherwise re-read the changed region and report the exact change.
6. If an action fails or returns empty, change the arguments or approach. Never repeat an identical action. Never guess high character offsets; use line or offset 0. If read_file reports "File not found", use the "Similar files" suggestions in the error or copy the exact path from earlier search_text results — paths and filenames must match exactly (watch for dropped/added letters like SQLV3 vs SQL3).
7. Use the SQL memory system (search_evidence, read_evidence, remember) to store and retrieve findings. Query code_map for mental map of the codebase.
8. For large files (>300 lines), read piecemeal: with "line" + "limit" the limit is a LINE count (default 80, hard max 300). First sample the start (line:1) and end (line:<total_lines>) in ~80-line windows, then read targeted sections around what matters. Never attempt whole-file reads. When you already know several independent files or sections to inspect (e.g. the LEADS list), batch their reads into ONE action with "arguments" as a list — it is much faster than one file per step.
9. Backend switching: you run on ONE inference backend at a time. If the current backend is struggling with deep multi-step reasoning, architecture analysis, or repeated parse failures, call switch_backend {"backend":"dory"}. If it is slow or the task is simple mechanical work, switch_backend {"backend":"lmstudio"} is faster. The switch takes a few seconds and your next step continues on the new backend — keep working afterward.
10. Asking the user: use kind="wait" only when you truly need a decision or information only the user has. When the answer is a discrete choice, include "options":["...","..."] (1–6 short choices) so the user can click instead of typing.

DEEP REASONING (only when it matters)
- Spend extra thought only when evidence conflicts, several causes are plausible, or a previous fix failed. Reason about the observed evidence, not about the task description.
- For analysis/review tasks, take time to understand the system before suggesting improvements.

OPERATIONAL RULES
- Return exactly one valid JSON object.
- Outcome format for actions:
  {"kind":"action","text":"Inspect SQLV3RelationalTable.cs at line 259","tool":"read_file","tool_arguments":{"path":"Assets/...","line":259,"limit":120},"children":[],"evidence":[],"coverage":[]}
- Batched read example (parallel, one step):
  {"kind":"action","text":"Read three candidate files","tool":"read_file","tool_arguments":[{"path":"A.cs","line":1,"limit":80},{"path":"B.cs","line":1,"limit":80},{"path":"C.cs","line":1,"limit":80}],"children":[],"evidence":[],"coverage":[]}
- Allowed kinds: 'action', 'decompose', 'finish', 'wait'. "arguments" is normally one object; it may be a list of 1–4 objects ONLY for read-only tools (read_file, search_text, find_files, list_directory, search_evidence, read_evidence, read_document, read_artifact, query_code_map, calculate, list_checks, databases, coverage_status). Never batch write_file, edit_file, run_check, remember, ingest_file, propose_improvement or switch_backend.
Available tools and argument types:
""" + encode(TOOL_HELP)


# Reasoning is the dominant latency source on small local models. Keep routine
# diagnosis short, but allow a larger budget when the evidence is genuinely hard.
_COMPLEX_MARKERS = (
    "deadlock", "race condition", "memory leak", "regression", "optimize",
    "architecture", "refactor", "intermittent", "nondeterministic",
    "multiple", "why does", "root cause", "design", "procedural", "system",
    "improvement", "suggestion", "analysis", "review",
    # Debugging/investigation tasks deserve deep reasoning too — a shader
    # property binding bug is not "simple mechanical work".
    "error", "bug", "crash", "exception", "fail", "debug", "why",
    "shader", "kernel", "compute", "binding", "property", "dispatch",
    "null reference", "undefined", "not set", "investigate", "diagnose",
)


def _is_complex_task(text):
    text = (text or "").lower()
    return len(text) > 1000 or any(marker in text for marker in _COMPLEX_MARKERS)


def reasoning_budget(objective, step, recent_actions=()):
    complex_task = _is_complex_task(objective)
    failed = bool(recent_actions) and isinstance(recent_actions[0], dict) and recent_actions[0].get("status") == "failed"
    # Generous budgets for complex tasks — deep debugging needs room to think.
    # Simple mechanical work stays cheap.
    if step <= 2:
        budget = 1024 if complex_task else 256
    elif step <= 5:
        budget = 1792 if complex_task else 512
    else:
        budget = 3072 if complex_task else 1024
    if failed:
        budget += 512
    return max(256, min(4096, budget))


# Read-only tools that may be batched: up to 4 identical-tool calls with
# different arguments execute in parallel inside ONE durable step. Anything
# with side effects (writes, checks, memory, backend switches) stays single.
BATCH_TOOLS = {
    "read_file", "search_evidence", "read_evidence", "read_document",
    "read_artifact", "databases", "coverage_status", "find_files",
    "search_text", "list_directory", "query_code_map", "calculate",
    "list_checks",
}
MAX_BATCH_CALLS = 4


REASONING_BUDGET_MESSAGE = ("\n\nPlease wrap up your analysis and provide the next tool action or final answer.")

_PATHISH = re.compile(r"[A-Za-z0-9_./\\\-]+\.(?:cs|py|md|js|jsx|ts|tsx|hlsl|cginc|compute|shader|json|jsonc|txt|xml|ya?ml|ini|cfg|csv|html?|css|bat|ps1|shaderlab|vert|frag|glsl)\b")


def _brief_result(result, limit=240):
    """One-line summary of a tool result for the self-handoff briefing."""
    if result is None:
        return "no result recorded"
    if isinstance(result, dict):
        if 'value' in result:
            result = result['value']
        elif 'body' in result:
            result = result['body']
    if isinstance(result, str):
        return result[:limit] + ("…" if len(result) > limit else "")
    try:
        return encode(result)[:limit]
    except Exception:
        return str(result)[:limit]


def _result_payload(res):
    """Unwrap an observation() receipt down to the tool's JSON payload."""
    if isinstance(res, str):
        s = res.strip()
        if not s.startswith("{"):
            return None
        try:
            res = json.loads(s)
        except Exception:
            # Briefed results may be truncated — still detect empty lookups.
            if '"count":0' in s or '"count": 0' in s:
                return {"count": 0, "matches": []}
            return None
    if not isinstance(res, dict):
        return None
    v = res.get("value")
    if isinstance(v, dict):
        return v
    body = res.get("body")
    if isinstance(body, str):
        try:
            parsed = json.loads(body)
            return parsed if isinstance(parsed, dict) else None
        except Exception:
            return None
    return res if ("count" in res or "matches" in res or "files" in res) else None


def _is_empty_lookup(payload):
    """True when a tool-result payload is a lookup that found nothing."""
    return (isinstance(payload, dict) and payload.get("count") == 0
            and ("matches" in payload or "files" in payload or "results" in payload))


def _artifact_is_empty_lookup(db, artifact_id, limit_chunks=4):
    """True when a stored tool result is a lookup that found nothing."""
    try:
        body = "".join(
            (r["body"] if isinstance(r["body"], str) else r["body"].decode("utf-8", "replace"))
            for r in db.execute(
                "SELECT body FROM artifact_chunk WHERE artifact=? ORDER BY ordinal LIMIT ?",
                (artifact_id, limit_chunks))
        )
        j = json.loads(body)
    except Exception:
        return False
    if not isinstance(j, dict):
        return False
    return j.get("count") == 0 and ("matches" in j or "files" in j or "results" in j)


def _lookup_signature(name, args):
    """Semantic key for read-only lookups. Cosmetic argument changes — glob on
    a single-file path, limit tweaks, key order — must not let a model re-issue
    the same dead-end search as a 'new' call."""
    if not isinstance(args, dict):
        return None
    try:
        norm = dict(args)
        p = norm.get("path")
        if isinstance(p, str) and p.strip():
            norm["path"] = p.replace("\\", "/").rstrip("/").lower()
            base = norm["path"].rsplit("/", 1)[-1]
            if "." in base:
                norm.pop("glob", None)  # glob cannot filter a single file
        norm.pop("limit", None)
        for k in ("pattern", "query"):
            if k in norm:
                norm[k] = str(norm[k]).lower()
        return json.dumps(norm, sort_keys=True)
    except Exception:
        return None


def _extract_leads(result, cap=8):
    """Pull concrete file/path leads out of the newest tool result."""
    if result is None:
        return []
    try:
        text = result if isinstance(result, str) else encode(result)
    except Exception:
        return []
    seen, leads = set(), []
    for m in _PATHISH.finditer(text):
        p = m.group(0).replace('\\', '/')
        if p not in seen:
            seen.add(p)
            leads.append(p)
        if len(leads) >= cap:
            break
    return leads


def _compose_briefing(task, objective_body, state, receipts):
    """Write the note-to-self that replaces the raw repeated JSON prompt.

    Each step is a fresh conversation, so this briefing is the only narrative
    memory the next invocation gets: mission, what was tried, what the latest
    result actually contained, and what is still outstanding.
    """
    gen = int(task.get("generation") or 1)
    lines = [
        f"SELF-HANDOFF — you are step {gen} of this task. This conversation has no memory",
        "beyond this briefing and the STATE JSON message that follows it.",
        "",
        "MISSION:",
        (objective_body or "")[:1200],
        "",
    ]
    if receipts:
        lines.append("WHAT PREVIOUS STEPS DID (newest first):")
        for r in receipts[:6]:
            arg_txt = encode(r.get("arguments") or {})
            if len(arg_txt) > 160:
                arg_txt = arg_txt[:160] + "…"
            # Surface path auto-correction loudly: the model keeps re-typing
            # corrupted paths, so it must see the canonical one it should use.
            corrected = ""
            res = r.get("result")
            rp = None
            if isinstance(res, dict):
                rp = res.get("resolved_path")
                inner = res.get("value")
                if not rp and isinstance(inner, dict):
                    rp = inner.get("resolved_path")
            if rp:
                corrected = f" [path auto-corrected to: {rp} — use this exact path next time]"
            lines.append(f"- {r.get('tool')}({arg_txt}) -> {r.get('status') or 'completed'}{corrected}: {_brief_result(r.get('result'), 200)}")
        lines.append("")
    else:
        lines.append("No actions have been taken yet. Locate the relevant files or data first.")
        lines.append("")

    # Scope dead-end streak: repeated ZERO-result lookups in the SAME
    # scope — even with different patterns — mean the answer is not there.
    # Warn loudly so the model stops re-searching a dead file/scope.
    scope_zeros = {}
    for r in receipts[:6]:
        if r.get("tool") not in ("search_text", "find_files", "query_code_map", "search_evidence"):
            continue
        empty = r.get("empty")
        if empty is None:
            empty = _is_empty_lookup(_result_payload(r.get("result")))
        if not empty:
            continue
        a = r.get("arguments")
        if isinstance(a, list):
            a = a[0] if a and isinstance(a[0], dict) else {}
        scope = str(a.get("path") or "(whole workspace)") if isinstance(a, dict) else "(whole workspace)"
        scope = scope.replace("\\", "/").rstrip("/").lower()
        scope_zeros[scope] = scope_zeros.get(scope, 0) + 1
    for scope, n in sorted(scope_zeros.items(), key=lambda kv: -kv[1]):
        if n >= 2:
            lines.append(f"!! SCOPE DEAD END: '{scope}' returned ZERO results for {n} lookups")
            lines.append("   (even with different patterns). The answer is NOT in this scope —")
            lines.append("   search a DIFFERENT directory or read_file a LEAD below instead.")
            lines.append("")

    last = receipts[0] if receipts else None
    if last:
        lines.append(f"LATEST RESULT ({last.get('tool')}):")
        lines.append(_brief_result(last.get("result"), 700))
        lines.append("")
        if (last.get("status") or "completed") == "failed":
            lines.append("!! The latest action FAILED. Do not retry it with identical arguments —")
            lines.append("   change the path, pattern, or approach.")
            lines.append("")
        payload = _result_payload(last.get("result"))
        if (isinstance(payload, dict) and payload.get("count") == 0
                and last.get("tool") in ("search_text", "find_files", "query_code_map", "search_evidence")):
            lines.append("!! That lookup returned ZERO results in that scope — repeating it is pointless.")
            lines.append("   Broaden the search: drop 'path'/'glob' filters, search a parent")
            lines.append("   directory, or read_file the most likely file directly.")
            lines.append("")

    # Leads persist across steps: paths surfaced by ANY recent lookup stay
    # visible until inspected, so a good hit (e.g. gen-12 wide search) is not
    # forgotten while the model fixates on a dead-end file.
    read_paths = set()
    for r in receipts[:6]:
        if r.get("tool") != "read_file":
            continue
        a = r.get("arguments")
        items = a if isinstance(a, list) else [a]
        for it in items:
            if isinstance(it, dict) and it.get("path"):
                read_paths.add(str(it["path"]).replace("\\", "/").rstrip("/").lower())
    leads, seen_leads = [], set()
    for r in receipts[:6]:
        for p in _extract_leads(r.get("result"), 8):
            pn = p.replace("\\", "/").rstrip("/").lower()
            if pn in seen_leads or pn in read_paths:
                continue
            seen_leads.add(pn)
            leads.append(p)
            if len(leads) >= 10:
                break
        if len(leads) >= 10:
            break
    if leads:
        lines.append("LEADS TO INSPECT — files/paths found earlier but not yet read:")
        lines += [f"- {p}" for p in leads]
        lines.append("   Read one of these BEFORE searching the same scope again.")
        lines.append("")

    outstanding = []
    for c in state.get("completion_checks", []):
        if c.get("status") != "passed":
            outstanding.append(f"required check '{c.get('name')}' has not passed")
    for m in state.get("document_coverage", []):
        if m.get("next_uncovered") is not None:
            outstanding.append(f"source document still has unread sections (next offset {m['next_uncovered']})")
    children = state.get("children") or []
    pending_children = [c for c in children if c.get("status") not in ("completed", "cancelled")]
    if pending_children:
        outstanding.append(f"{len(pending_children)} child task(s) still open")
    if state.get("last_error"):
        outstanding.append(f"last error: {str(state['last_error'])[:200]}")
    if outstanding:
        lines.append("STILL OUTSTANDING:")
        lines += ["- " + o for o in outstanding]
        lines.append("")

    pins = state.get("constraints") or []
    if pins:
        lines.append(f"NOTE: {len(pins)} pinned constraint(s) apply — see STATE JSON.")
        lines.append("")

    # User replies are the whole point of a wait cycle — surface them loudly so
    # the model acts on the answer instead of re-asking the same question.
    replies = state.get("user_replies") or []
    if replies:
        lines.append("!! THE USER HAS REPLIED — read the answer below and ACT ON IT.")
        lines.append("   Do not ask the same question again; continue the mission with this input.")
        for rep in replies[:3]:
            body = rep.get("body", "") if isinstance(rep, dict) else str(rep)
            lines.append(f"- {body[:500]}")
        lines.append("")

    lines += [
        "DECIDE NOW — pick exactly ONE next action:",
        "- If the mission is fully answered, return kind=\"finish\".",
        "- If you need something only the user can provide, return kind=\"wait\".",
        "- If the work splits into independent parts, return kind=\"decompose\".",
        "- Otherwise return kind=\"action\" with the single most informative next tool call.",
        "- Build on the results above; never repeat an action with identical arguments.",
        "- The STATE JSON in the next message is authoritative for evidence IDs,",
        "  requirements, allowed kinds, and pinned constraints.",
    ]
    return "\n".join(lines)


# Inference concurrency is provider-aware: LM Studio on this GPU sustains two
# parallel requests, while Dory llama-server stays strictly serial. Each
# backend kind gets its own semaphore so a mid-run switch_backend cannot
# exceed either backend's real capacity.
_INFERENCE_SEMS = {}
_INFERENCE_SEMS_LOCK = threading.Lock()


def _inference_gate(provider):
    """Context manager yielding one inference slot for the provider's backend.

    Providers expose inference_kind()/inference_slots(); anything else
    (test stubs) is treated as a single-slot 'dory'-class backend, preserving
    the historical one-inference-at-a-time behaviour.
    """
    kind_fn = getattr(provider, "inference_kind", None)
    slots_fn = getattr(provider, "inference_slots", None)
    kind = kind_fn() if callable(kind_fn) else "dory"
    try:
        slots = max(1, min(4, int(slots_fn()))) if callable(slots_fn) else 1
    except Exception:
        slots = 1
    key = (kind, slots)
    with _INFERENCE_SEMS_LOCK:
        sem = _INFERENCE_SEMS.get(key)
        if sem is None:
            sem = threading.BoundedSemaphore(slots)
            _INFERENCE_SEMS[key] = sem
    return sem


# Peer runners: when a task tree has several runnable leaves and the backend
# allows parallel inferences, run() spawns ONE extra daemon runner per root.
# Lease-aware leaf selection plus atomic claim() split the leaves between
# runners automatically — no task is ever executed twice.
_PEER_THREADS = {}
_PEER_THREADS_LOCK = threading.Lock()

# Variable NewConv mode: how many tool actions may share one model conversation
# before the engine gracefully rolls into a fresh self-handoff conversation.
MAX_CONV_ACTIONS = 8
CONV_CONTEXT_WARN = 0.85


def _result_evidence_ids(tool, result):
    """Extract citable evidence ids produced by a tool result."""
    ids = []
    def add(e):
        if isinstance(e, dict) and isinstance(e.get('id'), int):
            ids.append({'id': e['id']})
    def extract(r):
        if tool == 'search_evidence' and isinstance(r, list):
            for e in r:
                add(e)
        elif tool == 'read_document' and isinstance(r, dict):
            for c in r.get('chunks', []):
                add(c)
        elif tool == 'read_evidence' and isinstance(r, dict):
            add(r)
    # Unwrap batched calls: {"batch": name, "results": [{"arguments":..,"result":..}]}
    if isinstance(result, dict) and isinstance(result.get('results'), list):
        for i in result['results']:
            if isinstance(i, dict):
                extract(i.get('result'))
    else:
        extract(result)
    return ids


class OutcomeRejected(ValueError):
    """A decision rejected before any tool intent or child was created."""


def observation(artifact):
    """Present complete receipts as data, without a second JSON string layer."""
    if not isinstance(artifact, dict):
        return artifact
    if 'body' in artifact and artifact.get('next_offset') is None:
        try:
            value = json.loads(artifact['body'])
            return {**{k:v for k,v in artifact.items() if k != 'body'}, 'value':value}
        except ValueError:
            pass
    return artifact


class Runtime:
    def __init__(self, store, provider, workspace, cap=65536, max_steps=40, checks=None, required_checks=()):
        if not 4096<=cap<=1048576 or not 1<=max_steps<=100000:
            raise ValueError('Invalid IBA context cap or step allowance')
        self.store,self.provider,self.workspace = store,provider,workspace
        self.cap,self.max_steps = cap,max_steps
        self.checks = checks or {}
        if not isinstance(required_checks,(list,tuple)) or any(name not in self.checks for name in required_checks):
            raise ValueError('Required checks must name host-configured checks')
        self.required_checks = tuple(required_checks)

    def policy_status(self, owner, session, discover=False):
        policy = self.store.policy(owner,session)
        status = {**policy,"requested_tokens":policy["target"],"effective_tokens":None,
                  "runtime_cap":self.cap,"applies":"next invocation","model":None}
        if discover:
            try:
                with self.provider.session() as model:
                    status.update(effective_tokens=min(policy["target"] or 32768,model.context_length,self.cap),
                                  loaded_context=model.context_length,model=model.identifier)
            except Exception as exc:
                status["reason"] = str(exc)
        if status["effective_tokens"] and status["effective_tokens"] < (policy["target"] or 32768):
            status["reason"] = "Clamped by loaded model context or runtime cap; requested setting is retained."
        return status

    def submit(self, owner, session, text, requirements=(), request_id=None, allow_decompose=True, required_checks=None):
        required_checks = self.required_checks if required_checks is None else required_checks
        if not isinstance(required_checks,(list,tuple)) or any(name not in self.checks for name in required_checks):
            raise ValueError('Required check is not configured on this host')
        self.store.policy(owner,session)
        task=self.store.create_task(owner,session,text,requirements,request_id=request_id,allow_decompose=allow_decompose,required_checks=required_checks)
        document=self.store.ingest(owner,session,"higher","request:"+task,text)
        if len(text)>8192:
            total=self.store.document(owner,session,document,limit=1)['total_chunks']
            with self.store.connect(True) as db:
                db.execute('INSERT OR IGNORE INTO task_document VALUES(?,?,?)',(task,document,total))
        return task

    def _packet(self, owner, session, task, model, system_prompt):
        policy = self.policy_status(owner,session)
        effective = min(policy["target"] or 32768,model.context_length,self.cap)
        margin = max(512, int(effective * 0.05))
        objective = self.store.artifact(owner,session,task["objective"],0,8192)
        # Complex tasks reserve more output room: reasoning budget + the JSON
        # outcome must both fit inside max_tokens (they share the pool on
        # llama-server; LM Studio only sees max_tokens).
        complex_task = _is_complex_task(objective.get("body",""))
        output_cap = 6144 if complex_task else 3072
        output = min(output_cap, max(1024, int(effective * (0.20 if complex_task else 0.15))))
        policy.update(effective_tokens=effective,loaded_context=model.context_length,
                      model=model.identifier,output_tokens=output,safety_margin=margin)
        pins = self.store.pins(owner,session)
        if len(pins)>128:
            raise ValueError("Too many pinned constraints; split this task into scoped subtasks")
        with self.store.connect() as db:
            children = [dict(r) for r in db.execute("SELECT id,status,result,reason FROM task WHERE parent=? ORDER BY created LIMIT 65", (task["id"],))]
            recent = [dict(r) for r in db.execute("SELECT a.name,a.args,a.result,a.state FROM action a JOIN step s ON s.id=a.step WHERE s.task=? ORDER BY s.created DESC LIMIT 4", (task["id"],))]
            replies = [r[0] for r in db.execute("SELECT artifact FROM task_input WHERE task=? ORDER BY created DESC LIMIT 4",(task['id'],))]
            prior = [dict(r) for r in db.execute("SELECT id,status,objective,result FROM task WHERE owner=? AND session=? AND parent IS NULL AND id!=? ORDER BY created DESC LIMIT 2",(owner,session,task['id']))]
            ancestors=[dict(r) for r in db.execute("WITH RECURSIVE a AS (SELECT * FROM task WHERE id=? UNION ALL SELECT t.* FROM task t JOIN a ON a.parent=t.id) SELECT id,objective,requirements FROM a LIMIT 17",(task['parent'],))]
            observations=[dict(r) for r in db.execute("""WITH RECURSIVE ancestors(id,parent) AS (
                SELECT id,parent FROM task WHERE id=? UNION ALL
                SELECT t.id,t.parent FROM task t JOIN ancestors a ON t.id=a.parent)
                SELECT a.name,a.args,a.result,a.state FROM action a JOIN step s ON s.id=a.step
                JOIN ancestors p ON p.id=s.task ORDER BY s.created DESC LIMIT 4""",(task['parent'],))]
        if len(ancestors)>16:
            raise ValueError('Task hierarchy exceeds 16 levels; flatten the plan')
        if len(children)>64:
            raise ValueError("Task has too many children; use a deeper hierarchy")
        receipts = [{"tool":r["name"],"arguments":json.loads(r["args"]),"status":r.get("state","completed"),"result":observation(self.store.artifact(owner,session,r["result"],0,4096))} for r in recent]
        child_results = [{**r,"result":self.store.artifact(owner,session,r["result"],0,2048) if r["result"] else None} for r in children]
        state = {"task_id":task["id"],"objective":objective,"requirements":json.loads(task["requirements"]),
                 "constraints":pins,"children":child_results,"recent_actions":receipts,"last_error":task['reason'],"evidence":[]}
        state['user_replies'] = [self.store.artifact(owner,session,r,0,4096) for r in replies]
        state['allowed_kinds'] = ['action','finish','wait'] + (['decompose'] if task['allow_decompose'] else [])
        state['ancestor_observations'] = [{"tool":r['name'],"arguments":json.loads(r['args']),"status":r.get("state","completed"),
            "result":observation(self.store.artifact(owner,session,r['result'],0,1024))} for r in observations]
        state['document_coverage'] = self.store.coverage_status(owner,session,task['id'])
        state['completion_checks'] = self.store.completion_checks(owner,session,task['id'])
        if any(c['status'] != 'passed' for c in state['completion_checks']):
            state['allowed_kinds'].remove('finish')
        state['recent_tasks'] = [{**r,'objective':self.store.artifact(owner,session,r['objective'],0,1024),
            'result':self.store.artifact(owner,session,r['result'],0,1024) if r['result'] else None} for r in prior]
        state['ancestor_contracts']=[{**r,'requirements':json.loads(r['requirements']),
            'objective':self.store.artifact(owner,session,r['objective'],0,1024)} for r in ancestors]
        def messages():
            def trim(value, limit):
                """Keep the newest observation readable, older ones compact."""
                if isinstance(value, str):
                    return value[:limit] + ("\n...[truncated]" if len(value) > limit else "")
                if isinstance(value, dict):
                    out = {}
                    for k, v in value.items():
                        if isinstance(v, str) and len(v) > limit:
                            out[k] = v[:limit] + "\n...[truncated]"
                        elif isinstance(v, list) and len(v) > 12:
                            out[k] = v[:12]
                            out[k + "_truncated"] = True
                        else:
                            out[k] = v
                    return out
                if isinstance(value, list):
                    return value[:12] + (["...[truncated]"] if len(value) > 12 else [])
                return value

            clean_actions = []
            for i, r in enumerate(receipts):
                res_val = r["result"]
                if isinstance(res_val, dict):
                    if 'value' in res_val:
                        res_val = res_val['value']
                    elif 'body' in res_val:
                        res_val = res_val['body']
                # The model needs the newest result in full; older receipts are
                # only context and must not crowd out the current observation.
                clean_actions.append({
                    "tool": r["tool"],
                    "arguments": r["arguments"],
                    "status": r.get("status", "completed"),
                    "result": trim(res_val, 4096 if i == 0 else 1200)
                })

            clean_evidence = []
            for e in state["evidence"]:
                item = dict(e)
                if isinstance(item.get("body"), str):
                    item["body"] = item["body"][:2000]
                clean_evidence.append(item)

            clean_prior = []
            for r in prior:
                try:
                    g = self.store.artifact(owner, session, r['objective'], 0, 1024).get('body', '')
                    res_text = self.store.artifact(owner, session, r['result'], 0, 1024).get('body', '') if r.get('result') else None
                    clean_prior.append({"goal": g, "status": r["status"], "result": res_text})
                except Exception:
                    pass

            clean_state = {
                "goal": objective.get("body", ""),
                "requirements": json.loads(task["requirements"]),
                "constraints": [p.get("body", p) if isinstance(p, dict) else str(p) for p in pins],
                "recent_actions": clean_actions,
                "evidence": clean_evidence,
                "allowed_kinds": state["allowed_kinds"]
            }
            if task.get("reason"):
                clean_state["last_error"] = task["reason"]
            if clean_prior:
                clean_state["recent_tasks"] = clean_prior
            if state.get("user_replies"):
                clean_state["user_replies"] = state["user_replies"]
            if hasattr(self.store, 'get_code_map'):
                try:
                    cmap = self.store.get_code_map(owner, session, getattr(self, 'workspace', None), limit=10)
                    if cmap:
                        clean_state["codebase_mental_map"] = {
                            c["path"]: (c["summary"] or "")[:400] for c in cmap if not c.get("stale_on_disk")
                        }
                except Exception:
                    pass
            
            # Add SQL memory context - search for relevant evidence
            try:
                search_results = self.store.search(owner, session, objective.get("body", ""), limit=5)
                if search_results:
                    clean_state["relevant_evidence"] = [
                        {"id": e["id"], "body": e.get("body", "")[:500], "document": e.get("document")}
                        for e in search_results[:3]
                    ]
            except Exception:
                pass

            briefing = _compose_briefing(task, objective.get("body", ""), state, receipts)
            return [
                {"role":"system","content":INSTRUCTIONS + '\n' + compact_outcome_contract() + "\nUser-configured role:\n" + system_prompt},
                {"role":"user","content":briefing},
                {"role":"user","content":encode(clean_state)},
            ]
        base = messages()
        if model.count(base)+output+margin>effective:
            raise ValueError("Required task context exceeds budget. Raise /context or split the task; required constraints were not truncated.")
        # Spend available input budget on the latest tool observations first.
        # Keep explicit artifact cursors whenever an observation does not fit.
        for receipt in receipts[:1]:
            previous=receipt['result']
            if isinstance(previous, dict) and 'id' in previous:
                complete=self.store.artifact(owner,session,previous['id'],0,8192)
                receipt['result']=observation(complete)
                if model.count(messages())+output+margin>effective:
                    receipt['result']=previous
        candidates=[]
        for manifest in state['document_coverage']:
            if manifest['next_uncovered'] is not None:
                page=self.store.document(owner,session,manifest['document'],manifest['next_uncovered']-1,1)
                candidates.extend(self.store.evidence(owner,session,e['id']) for e in page['chunks'])
        candidates += self.store.search(owner,session,objective.get("body", ""),limit=2)
        seen=set()
        for evidence in candidates:
            if evidence['id'] in seen:
                continue
            seen.add(evidence['id'])
            state["evidence"].append(evidence)
            if model.count(messages())+output+margin>effective:
                state["evidence"].pop()
        packet = messages()
        count = model.count(packet)
        # Shrink the output reservation to fit large packets rather than fail —
        # but keep at least 1024 so reasoning + outcome still have room.
        policy["output_tokens"] = max(1024, min(policy["output_tokens"], effective - count - margin))
        assert count+policy["output_tokens"]+margin<=effective, "Required task context exceeds budget"
        return packet,policy,count

    @staticmethod
    def _outcome(raw):
        if not raw or not isinstance(raw, str):
            raise OutcomeRejected("Model returned empty or non-string response; reply with a single JSON outcome object")
        text = raw.strip()
        if "</think>" in text:
            text = text.rsplit("</think>", 1)[-1].strip()

        # Strip markdown code fences if wrapped
        m = re.search(r'```(?:json)?[ \t]*\r?\n(.*?)\r?\n[ \t]*```', text, re.DOTALL | re.IGNORECASE)
        if m:
            text = m.group(1).strip()

        value = None
        try:
            val = json.loads(text)
            if isinstance(val, dict) and val.get("kind") in ("action", "decompose", "finish", "wait"):
                value = val
        except Exception:
            pass

        if value is None:
            # Fallback: find all complete JSON objects using JSONDecoder
            decoder = json.JSONDecoder()
            candidates = []
            idx = 0
            while idx < len(text):
                next_brace = text.find('{', idx)
                if next_brace == -1:
                    break
                try:
                    obj, end_idx = decoder.raw_decode(text, next_brace)
                    if isinstance(obj, dict) and obj.get("kind") in ("action", "decompose", "finish", "wait"):
                        candidates.append(obj)
                    idx = max(next_brace + 1, end_idx)
                except Exception:
                    idx = next_brace + 1
            if candidates:
                value = candidates[-1]

        if not isinstance(value, dict) or value.get("kind") not in ("action", "decompose", "finish", "wait"):
            # Small models sometimes echo a chat-message envelope instead of the
            # bare outcome object: {"content":"{\"kind\":\"action\",...}", ...}.
            # Unwrap one level of string-encoded JSON looking for a real outcome.
            envelope = None
            for src in (text, raw or ""):
                try:
                    obj = json.loads(src.strip())
                    if isinstance(obj, dict):
                        envelope = obj
                        break
                except Exception:
                    continue
            if envelope is None:
                brace = text.find('{')
                if brace != -1:
                    try:
                        obj, _ = json.JSONDecoder().raw_decode(text[brace:])
                        if isinstance(obj, dict):
                            envelope = obj
                    except Exception:
                        pass
            if isinstance(envelope, dict):
                for k in ("content", "response", "result", "message", "output", "text"):
                    inner = envelope.get(k)
                    if not (isinstance(inner, str) and "kind" in inner):
                        continue
                    inner = inner.strip()
                    m2 = re.search(r'```(?:json)?[ \t]*\r?\n(.*?)\r?\n[ \t]*```', inner, re.DOTALL | re.IGNORECASE)
                    if m2:
                        inner = m2.group(1).strip()
                    for cand_text in (inner,):
                        try:
                            cand = json.loads(cand_text)
                        except Exception:
                            try:
                                cand, _ = json.JSONDecoder().raw_decode(cand_text)
                            except Exception:
                                cand = None
                        if isinstance(cand, dict) and cand.get("kind") in ("action", "decompose", "finish", "wait"):
                            value = cand
                            break
                    if value is not None and value.get("kind") in ("action", "decompose", "finish", "wait"):
                        break
            if not isinstance(value, dict) or value.get("kind") not in ("action", "decompose", "finish", "wait"):
                raise OutcomeRejected(f"Could not parse valid IBA outcome JSON from response: {raw[:200]}. Reply with exactly one JSON object: " + compact_outcome_contract())

        if 'tool_arguments' in value:
            if 'arguments' in value:
                value.pop('arguments')
            value['arguments'] = value.pop('tool_arguments')
        if not isinstance(value.get("text"), str):
            raise OutcomeRejected('Outcome requires a "text" string field (a short status note); add it and retry')
        evidence = value.get("evidence", [])
        if not isinstance(evidence, list) or len(evidence) > 128 or any(type(e) is not int for e in evidence):
            raise OutcomeRejected("Evidence must be a bounded array of integer evidence IDs from this packet, or []")
        if value["kind"] == "action":
            tool_ok = value.get("tool") in TOOL_HELP
            args = value.get("arguments")
            args_ok = isinstance(args, dict) or (
                isinstance(args, list) and 1 <= len(args) <= MAX_BATCH_CALLS
                and all(isinstance(a, dict) for a in args))
            if not tool_ok or not args_ok:
                raise OutcomeRejected("Outcome requests an unknown tool or invalid arguments; pick a tool from the allowed list with a JSON object of arguments (or a list of 1–4 objects for batched read-only calls)")
            if isinstance(args, list) and value["tool"] not in BATCH_TOOLS:
                raise OutcomeRejected(f"{value['tool']} cannot be batched — it has side effects. Issue it as a single action with one arguments object")
            # Models sometimes emit declared tool arguments at the outcome top
            # level (e.g. "expected_hash" beside "tool_arguments" instead of
            # inside it). Hoist them into arguments rather than rejecting.
            arg_spec = TOOL_ARGUMENTS.get(value["tool"])
            if arg_spec and isinstance(args, dict):
                declared = set(arg_spec[0].keys())
                hoisted = {k: value.pop(k) for k in list(value.keys())
                           if k in declared and k not in args}
                if hoisted:
                    args.update(hoisted)
            spec = TOOL_ARGUMENTS.get(value["tool"])
            if spec:
                required = spec[1]
                items = args if isinstance(args, list) else [args]
                missing = sorted({r for it in items if isinstance(it, dict) for r in required if r not in it})
                if missing:
                    raise OutcomeRejected(
                        f"{value['tool']} is missing required argument(s): {', '.join(missing)}. "
                        "Supply them and retry — for write_file, 'text' must be the COMPLETE file content.")
        options = value.get("options")
        if options is not None:
            if value["kind"] != "wait":
                value.pop("options", None)
            elif (not isinstance(options, list) or not 1 <= len(options) <= 6
                  or any(not isinstance(o, str) or not o.strip() or len(o) > 140 for o in options)):
                raise OutcomeRejected('wait "options" must be a list of 1–6 short non-empty strings (<=140 chars each)')
            else:
                value["options"] = [o.strip() for o in options]
        return value

    def _accept(self, owner, session, key, token, sid, value, tools, prediction, callback=None):
        def commit(db,status="ready",reason="",result=None):
            self.store.finish_step(key,token,sid,prediction["content"],status,reason,result,
                                   prediction.get("output_tokens"),transaction=db,
                                   reasoning=prediction.get("reasoning", ""))
        if not set(value.get('evidence',[])).issubset(tools.visible_evidence):
            raise OutcomeRejected('Citation rejected before execution. Use only evidence IDs present in this packet: '
                                  + encode(sorted(tools.visible_evidence))
                                  + '. Do not invent IDs for tool receipts. Use evidence:[] for an uncited action, or read evidence first.')
        evidence = [self.store.evidence(owner,session,k) for k in value.get("evidence",[])]
        if any(not e["current"] for e in evidence):
            raise OutcomeRejected("Outcome relies on superseded evidence; cite only evidence marked current in this packet")
        with self.store.connect(True) as db:
            row = self.store.fence(db,key,token)
            if row["status"] != "running":
                raise Conflict("Task stopped before accepting outcome")
            for e in evidence:
                if not db.execute('SELECT 1 FROM document d WHERE d.id=? AND d.current=1 AND NOT EXISTS(SELECT 1 FROM invalid_document i WHERE i.document=d.id)',(e['document'],)).fetchone():
                    raise Conflict('Evidence changed while accepting outcome')
                db.execute("INSERT OR IGNORE INTO dependency VALUES(?,?)", (key,e["id"]))
        if value["kind"] == "action":
            name,args = value["tool"],value["arguments"]
            if callback:
                try: callback({"event":"tool_start","task":key,"step":sid,"tool":name,"args":args})
                except Exception: pass
            # Detect a no-progress loop before replaying a potentially mutating action.
            with self.store.connect(True) as db:
                prior_rows = db.execute(
                    "SELECT a.state,a.result FROM action a JOIN step s ON s.id=a.step WHERE s.task=? AND a.name=? AND a.args=? ORDER BY s.created DESC",
                    (key,name,encode(args))).fetchall()
                priors = [r["state"] for r in prior_rows]
                repeats = len(priors)
                # Auto-resolvable near-miss paths: the tool corrects them itself,
                # so path-based guards must not pre-empt the correction. Only
                # count priors that actually succeeded — failed priors were
                # pre-resolution misses that will now succeed.
                near = None
                if name in BATCH_TOOLS and isinstance(args, dict) and args.get("path"):
                    kind = "dir" if name == "list_directory" else "file"
                    near = tools._resolve_near_miss(args["path"], kind)
                    if near is None and kind == "file" and name == "search_text":
                        near = tools._resolve_near_miss(args["path"], "dir")
                    # An exact existing path "resolves" to itself — that is not
                    # a near-miss correction, so the fast-path guards below must
                    # still apply to it.
                    if near is not None and near.replace("\\", "/").strip().lstrip("./").lower() == \
                            str(args["path"]).replace("\\", "/").strip().lstrip("./").lower():
                        near = None
                if near is None and name in BATCH_TOOLS:
                    # A deterministic read-only call that already failed can never
                    # succeed with identical arguments — reject the retry instantly
                    # instead of letting the model burn steps re-issuing it.
                    # A completed lookup that returned ZERO results is equally
                    # dead-ended: identical args can never return new information.
                    last_state = priors[0] if priors else None
                    last_result = prior_rows[0]["result"] if prior_rows else None
                    empty_lookup = (
                        last_state == "completed" and last_result
                        and name in ("search_text", "find_files", "query_code_map", "search_evidence")
                        and _artifact_is_empty_lookup(db, last_result))
                    if last_state == "failed" or empty_lookup:
                        if empty_lookup and last_state != "failed":
                            raise OutcomeRejected(
                                f"Identical {name} call already returned ZERO results — do not repeat it. "
                                "Broaden the scope: drop 'path'/'glob' filters, search a parent "
                                "directory, or read_file the most likely file directly.")
                        raise OutcomeRejected(
                            f"Identical {name} call already failed — do not repeat it. "
                            "Read the error in recent_actions (it may list 'Similar files' "
                            "suggestions), then choose a corrected path/pattern or a "
                            "different tool.")
                    if repeats>=3:
                        raise OutcomeRejected("Repeated identical action without progress; revise the task before resuming — choose a different tool or arguments, or finish/wait")
                    # Semantic dedup for lookups: models re-issue the same
                    # dead-end search with cosmetic arg changes (glob added to
                    # a single-file path, limit tweaks). Compare normalized
                    # signatures against recent same-tool calls. Only lookups
                    # after the last write_file count — a write can change results.
                    if name in ("search_text", "find_files", "query_code_map", "search_evidence"):
                        sig = _lookup_signature(name, args)
                        if sig:
                            last_write = db.execute(
                                "SELECT MAX(s.created) AS m FROM step s JOIN action a ON a.step=s.id "
                                "WHERE s.task=? AND a.name='write_file'", (key,)).fetchone()["m"] or 0
                            for row in db.execute(
                                "SELECT s.created,a.state,a.result,a.args FROM action a JOIN step s ON s.id=a.step "
                                "WHERE s.task=? AND a.name=? ORDER BY s.created DESC LIMIT 20",
                                (key, name)).fetchall():
                                if (row["created"] or 0) <= last_write:
                                    break
                                try:
                                    pa = json.loads(row["args"])
                                except Exception:
                                    continue
                                if _lookup_signature(name, pa) != sig:
                                    continue
                                if row["state"] == "completed" and row["result"] and _artifact_is_empty_lookup(db, row["result"]):
                                    raise OutcomeRejected(
                                        f"An equivalent {name} call (same pattern and scope) already returned ZERO results — "
                                        "do not re-issue it with cosmetic argument changes. Broaden the search: "
                                        "drop 'path'/'glob' filters, search a parent directory, or read_file the "
                                        "most likely file directly.")
                                break
                elif near is not None and priors.count("completed") >= 3:
                    # Resolved path read 3+ times already — genuine re-read loop.
                    raise OutcomeRejected("Repeated identical action without progress; revise the task before resuming — choose a different tool or arguments, or finish/wait")
                # Path-level stuck detection: the model keeps retrying the same
                # bad path with different windows/limits. Two consecutive
                # failures on one path is enough — the path itself is wrong.
                if near is None and name in BATCH_TOOLS and isinstance(args, dict) and args.get("path"):
                    want_path = str(args["path"]).replace("\\","/").lower()
                    streak = 0
                    for r in db.execute(
                        "SELECT a.args,a.state FROM action a JOIN step s ON s.id=a.step WHERE s.task=? AND a.name=? ORDER BY s.created DESC LIMIT 12",
                        (key,name,)).fetchall():
                        try:
                            pa = json.loads(r["args"])
                        except Exception:
                            continue
                        if not isinstance(pa, dict):
                            continue
                        if str(pa.get("path","")).replace("\\","/").lower() != want_path:
                            break
                        if r["state"] == "failed":
                            streak += 1
                        else:
                            break
                    if streak >= 2:
                        raise OutcomeRejected(
                            f"Path '{args['path']}' already failed {streak} times — stop retrying it. "
                            "Use the 'Similar files' suggestions in the error or a different path entirely.")
                row = self.store.fence(db,key,token)
                if row["status"] != "running":
                    raise Conflict("Task stopped")
                db.execute("INSERT INTO action VALUES(?,?,?,'issued',NULL)", (sid,name,encode(args)))
            try:
                if isinstance(args, list):
                    # Batched read-only calls: execute in parallel, one receipt.
                    def _one(call_args):
                        try:
                            return {"arguments": call_args, "result": tools.execute(name, call_args)}
                        except Exception as exc:
                            return {"arguments": call_args, "result": {"error": str(exc), "status": "failed"}}
                    with ThreadPoolExecutor(max_workers=min(MAX_BATCH_CALLS, len(args))) as pool:
                        items = list(pool.map(_one, args))
                    result = {"batch": name, "count": len(items), "results": items}
                    is_failed = all(
                        isinstance(i.get("result"), dict)
                        and (i["result"].get("status") == "failed" or "error" in i["result"])
                        for i in items)
                else:
                    result = tools.execute(name,args)
                    is_failed = isinstance(result, dict) and (result.get("status") == "failed" or "error" in result)
                action_status = "failed" if is_failed else "completed"
            except Exception as exc:
                result = {"error":str(exc),"status":"failed"}
                action_status = "failed"
            with self.store.connect(True) as db:
                self.store.fence(db,key,token)
                result_id = self.store.put_artifact(db,owner,session,"tool_result",result)
                db.execute("UPDATE action SET state=?,result=? WHERE step=?", (action_status,result_id,sid))
                self.store.emit(db,owner,session,key,"action_completed",{"step":sid,"tool":name,"artifact":result_id,"status":action_status})
                commit(db)
            # Durable SQL memory link: record command execution and whether it worked
            mem_db = "lower" if action_status == "failed" else "higher"
            summary = f"Command {name}({encode(args)[:100]}) -> {action_status}: {encode(result)[:220]}"
            try:
                self.store.ingest(owner, session, mem_db, f"receipt:{sid}", summary, references=[])
            except Exception:
                pass
            if callback:
                try: callback({
                    "event":"tool_result",
                    "task":key,
                    "step":sid,
                    "tool":name,
                    "args":args,
                    "result":result,
                    "status":action_status,
                    "artifact":result_id
                })
                except Exception: pass
            return "ready","",None,result
        if value["kind"] == "decompose":
            if not row['allow_decompose']:
                raise OutcomeRejected('This is an execution task: decompose is forbidden. Use action to call the required tool directly, or finish if this task is complete. No child was created.')
            children = value.get("children")
            if not isinstance(children,list) or not 1<=len(children)<=8:
                raise OutcomeRejected('Decompose requires a "children" list of 1–8 objects, each with a non-empty "objective" string. Got: ' + encode(children)[:300])
            for child in children:
                if not isinstance(child,dict) or not isinstance(child.get("objective"),str) or not child["objective"].strip():
                    raise OutcomeRejected('Every decompose child needs a non-empty "objective" string')
                if not isinstance(child.get("requirements",[]),list) or any(not isinstance(x,str) for x in child.get("requirements",[])):
                    raise OutcomeRejected('Child "requirements" must be a list of strings')
                if type(child.get('allow_decompose',False)) is not bool:
                    raise OutcomeRejected('Child "allow_decompose" must be a boolean')
            # The whole child batch and parent transition are one transaction.
            with self.store.connect(True) as db:
                row = self.store.fence(db,key,token)
                for child in children:
                    cid = uid()
                    objective = self.store.put_artifact(db,owner,session,"objective",child["objective"])
                    db.execute("INSERT INTO task(id,owner,session,parent,objective,requirements,created,updated,allow_decompose) VALUES(?,?,?,?,?,?,?,?,?)",
                               (cid,owner,session,key,objective,encode(child.get("requirements",[])),time.time(),time.time(),int(child.get('allow_decompose',False))))
                    self.store.emit(db,owner,session,cid,"task_created",{"parent":key,"objective":objective})
                commit(db)
            return "ready","",None,None
        if value["kind"] == "wait":
            options = value.get("options") or []
            with self.store.connect(True) as db:
                commit(db,"waiting",value["text"])
                if options:
                    opt_art = self.store.put_artifact(db,owner,session,"wait_options",encode(options))
                    self.store.emit(db,owner,session,key,"task_waiting",
                                    {"options":opt_art,"text":value["text"]})
            if callback and options:
                try:
                    callback({"event":"task_waiting","task":key,"step":sid,
                              "text":value["text"],"options":options})
                except Exception:
                    pass
            return "waiting",value["text"],None,None
        incomplete = [c for c in self.store.completion_checks(owner,session,key) if c['status'] != 'passed']
        if incomplete:
            raise OutcomeRejected('Cannot finish: required checks have not passed after the latest file write. Run run_check for: ' + encode(incomplete))
        with self.store.connect(True) as db:
            row = self.store.fence(db,key,token)
            if db.execute("SELECT 1 FROM task WHERE parent=? AND status!='completed'", (key,)).fetchone():
                raise OutcomeRejected("Cannot finish while child tasks are incomplete; act on the children first or choose wait")
            requirements = json.loads(row["requirements"])
            missing=db.execute("""SELECT 1 FROM task_document td JOIN document d ON d.id=td.document WHERE td.task=? AND
                (d.current=0 OR td.total!=(SELECT count(*) FROM coverage c JOIN evidence e ON e.id=c.evidence WHERE c.task=td.task AND e.document=td.document)
                OR EXISTS(SELECT 1 FROM invalid_document i WHERE i.document=d.id))""",(key,)).fetchone()
            if missing:
                raise OutcomeRejected('Required document coverage is incomplete or stale; gather coverage with actions before finishing')
            coverage = value.get("coverage",[])
            covered = set()
            for entry in coverage:
                idx = entry.get("requirement")
                if type(idx) is not int or not 0<=idx<len(requirements) or not entry.get("explanation"):
                    raise OutcomeRejected("Invalid requirement coverage: cite only indexes in requirements; use coverage:[] when requirements is empty")
                if not entry.get("evidence") or not set(entry["evidence"]).issubset({e["id"] for e in evidence}):
                    raise OutcomeRejected("Requirement coverage must cite only evidence IDs from this packet's evidence list")
                covered.add(idx)
            if covered != set(range(len(requirements))):
                raise OutcomeRejected("Acceptance requirements lack evidence coverage; cite an index in requirements for each, or use coverage:[] when requirements is empty")
            result = self.store.put_artifact(db,owner,session,"task_result",value['text'])
            commit(db,"completed",result=result)
        return "completed","",result,None

    def _runnable_leaf_count(self, owner, session, root):
        """Runnable leaves in the task tree that no live worker holds."""
        with self.store.connect() as db:
            return db.execute("""WITH RECURSIVE tree(id) AS (
                SELECT id FROM task WHERE id=? UNION ALL
                SELECT t.id FROM task t JOIN tree ON t.parent=tree.id)
                SELECT count(*) FROM task t JOIN tree ON t.id=tree.id
                WHERE t.status IN ('ready','running')
                AND (t.lease IS NULL OR t.expires < ?)
                AND NOT EXISTS (SELECT 1 FROM task c WHERE c.parent=t.id AND c.status!='completed')""",
                (root, time.time())).fetchone()[0]

    def _maybe_spawn_peer(self, owner, session, root, system_prompt, callback, variable_newconv):
        """Spawn one daemon peer runner when the tree can use a second slot."""
        slots_fn = getattr(self.provider, "inference_slots", None)
        try:
            if not callable(slots_fn) or slots_fn() < 2:
                return
        except Exception:
            return
        if self._runnable_leaf_count(owner, session, root) < 2:
            return
        with _PEER_THREADS_LOCK:
            live = _PEER_THREADS.get(root)
            if live is not None and live.is_alive():
                return
            thread = threading.Thread(
                target=self._peer_main,
                args=(owner, session, root, system_prompt, callback, variable_newconv),
                daemon=True, name=f"iba-peer-{root[:8]}")
            _PEER_THREADS[root] = thread
            thread.start()

    @staticmethod
    def _peer_alive(root):
        with _PEER_THREADS_LOCK:
            t = _PEER_THREADS.get(root)
            return t is not None and t.is_alive()

    def _peer_main(self, owner, session, root, system_prompt, callback, variable_newconv):
        try:
            self.run(owner, session, root, system_prompt=system_prompt,
                     callback=callback, variable_newconv=variable_newconv, _peer=True)
        except Exception:
            pass
        finally:
            with _PEER_THREADS_LOCK:
                if _PEER_THREADS.get(root) is threading.current_thread():
                    _PEER_THREADS.pop(root, None)

    def run(self, owner, session, root, system_prompt="", callback=None, variable_newconv=False, _peer=False):
        self.store.task(owner,session,root)
        self.store.maintain_projections()
        try:
            self.store.recover()
        except Exception:
            pass
        tools = Tools(self.store,owner,session,self.workspace,self.checks)
        corrections = 0
        blocked = set()  # leaves this runner failed to claim (peer holds them)
        for _ in range(self.max_steps):
            root_task = self.store.task(owner,session,root)
            if root_task["status"] not in ("ready","running"):
                return root_task
            # Depth-first runnable leaf selection; no recursive Python model calls.
            # Leaves held by a live worker lease are skipped so a peer runner
            # automatically takes the next free leaf instead of colliding.
            with self.store.connect() as db:
                rows = db.execute("""WITH RECURSIVE tree(id,depth) AS (
                    SELECT id,0 FROM task WHERE id=? UNION ALL
                    SELECT t.id,tree.depth+1 FROM task t JOIN tree ON t.parent=tree.id)
                    SELECT t.* FROM task t JOIN tree ON t.id=tree.id
                    WHERE t.status IN ('ready','running')
                    AND (t.lease IS NULL OR t.expires < ?)
                    AND NOT EXISTS
                    (SELECT 1 FROM task c WHERE c.parent=t.id AND c.status!='completed')
                    ORDER BY tree.depth DESC,t.created LIMIT 65""", (root, time.time())).fetchall()
            row = next((r for r in rows if r["id"] not in blocked), None) if rows else None
            if not row:
                # Every runnable leaf may be leased by a live peer runner.
                # Wait for it rather than reporting a premature "running"
                # completion while its steps are still emitting events.
                if self._peer_alive(root):
                    while self._peer_alive(root):
                        time.sleep(2)
                        rt = self.store.task(owner, session, root)
                        if rt["status"] not in ("ready", "running"):
                            return rt
                    continue
                return root_task
            if not _peer:
                try:
                    self._maybe_spawn_peer(owner, session, root, system_prompt, callback, variable_newconv)
                except Exception:
                    pass
            key = row["id"]
            token,sid,prediction = None,None,{}
            try:
                with _inference_gate(self.provider):
                    try:
                        token = self.store.claim(owner,session,key)
                    except Conflict:
                        # A peer runner holds this leaf — block it for this run
                        # and loop so lease-aware selection offers the next leaf.
                        blocked.add(key)
                        continue
                    with self.provider.session() as model:
                        task = self.store.task(owner,session,key)
                        packet,policy,count = self._packet(owner,session,task,model,system_prompt)
                        tools.task=key
                        context=json.loads(packet[-1]['content'])
                        tools.visible_evidence=visible_evidence(context)
                        # Variable NewConv: keep one model conversation alive across
                        # several tool actions. Each action still gets its own durable
                        # step row; the conversation only rolls over to a fresh
                        # self-handoff packet when it ends (finish/wait/decompose),
                        # hits the action cap, or approaches the context budget.
                        conv_messages = packet
                        conv_state = context
                        conv_count = count
                        conv_actions = 0
                        value = {}
                        status = reason = result = exec_result = None
                        while True:
                            # finish_step clears the worker lease after every action,
                            # so each continued conversation turn must re-claim it.
                            # claim() also bumps generation, matching fresh-mode steps.
                            if conv_actions > 0:
                                try:
                                    token = self.store.claim(owner, session, key)
                                except Conflict:
                                    break
                            prediction = {}
                            sid = self.store.start_step(key,token,conv_messages,policy,conv_count,tools.visible_evidence)
                            if callback:
                                try:
                                    callback({"event":"step_start","task":key,"step":sid,"policy":policy})
                                except Exception:
                                    pass
                            # Shadow-persist the reasoning stream so a client
                            # that reconnects mid-step still sees the partial
                            # thought via history. Flushed at most every ~2s.
                            think_buf = []
                            think_len = [0]
                            think_at = [0.0]
                            def _th(t, _sid=sid, _buf=think_buf, _ln=think_len, _at=think_at):
                                if callback:
                                    try: callback({"event":"thought","task":key,"step":_sid,"token":t})
                                    except Exception: pass
                                _buf.append(t)
                                _ln[0] += len(t)
                                now = time.time()
                                if now - _at[0] > 2.0 and _ln[0] >= 400:
                                    _at[0] = now
                                    try:
                                        # _buf keeps the FULL stream — each flush
                                        # rewrites the whole partial reasoning.
                                        if self.store.update_step_reasoning(owner, session, _sid, "".join(_buf)):
                                            _ln[0] = 0
                                    except Exception:
                                        pass
                            def _co(t, _sid=sid):
                                if callback:
                                    try: callback({"event":"content","task":key,"step":_sid,"token":t})
                                    except Exception: pass
                            budget = reasoning_budget(
                                context.get("goal", ""),
                                int(task.get("generation") or 1) + conv_actions,
                                context.get("recent_actions", []),
                            )
                            try:
                                prediction = model.predict(
                                    conv_messages, policy["output_tokens"],
                                    on_thought=_th, on_content=_co,
                                    reasoning_budget=budget,
                                    reasoning_budget_message=REASONING_BUDGET_MESSAGE,
                                )
                            except TypeError:
                                prediction = model.predict(conv_messages,policy["output_tokens"])
                            if prediction["stop_reason"] not in ("eosFound","stopStringFound","stop"):
                                raise ValueError("Model output was interrupted or token-limited; no action was accepted")
                            raw = prediction.get("content")
                            if not isinstance(raw, str) or not raw.strip():
                                # Small models sometimes spend the whole output budget
                                # on reasoning and return nothing. Nudge once inside the
                                # same conversation with a shrunken reasoning budget
                                # before burning a durable correction.
                                conv_messages = conv_messages + [{"role":"user","content":"Your previous reply was empty. Respond with ONLY the single JSON outcome object — no reasoning and no prose."}]
                                try:
                                    prediction = model.predict(
                                        conv_messages, policy["output_tokens"],
                                        on_thought=_th, on_content=_co,
                                        reasoning_budget=max(64, budget // 2),
                                        reasoning_budget_message=REASONING_BUDGET_MESSAGE,
                                    )
                                except TypeError:
                                    prediction = model.predict(conv_messages, policy["output_tokens"])
                                if prediction["stop_reason"] not in ("eosFound","stopStringFound","stop"):
                                    raise ValueError("Model output was interrupted or token-limited; no action was accepted")
                                raw = prediction.get("content")
                                if not isinstance(raw, str) or not raw.strip():
                                    # Second empty in a row is usually a provider fluke
                                    # (output_tokens:0, immediate EOS). One more attempt
                                    # with reasoning fully suppressed before giving up.
                                    conv_messages = conv_messages + [{"role":"user","content":"Reply NOW with only the JSON outcome object. Example shape: {\"kind\":\"action\",\"tool\":\"read_file\",\"arguments\":{...}} — start with { and nothing else."}]
                                    try:
                                        prediction = model.predict(
                                            conv_messages, policy["output_tokens"],
                                            on_thought=_th, on_content=_co,
                                            reasoning_budget=32,
                                            reasoning_budget_message=REASONING_BUDGET_MESSAGE,
                                        )
                                    except TypeError:
                                        prediction = model.predict(conv_messages, policy["output_tokens"])
                                    if prediction["stop_reason"] not in ("eosFound","stopStringFound","stop"):
                                        raise ValueError("Model output was interrupted or token-limited; no action was accepted")
                                    raw = prediction.get("content")
                                if not isinstance(raw, str) or not raw.strip():
                                    raise OutcomeRejected("Model returned empty or non-string response; reply with a single JSON outcome object")
                            value = self._outcome(prediction["content"])
                            want_more = (variable_newconv and value["kind"] == "action"
                                         and conv_actions < MAX_CONV_ACTIONS)
                            status,reason,result,exec_result = self._accept(
                                owner,session,key,token,sid,value,tools,prediction,callback=callback)
                            # A backend switch ejects the model this session is
                            # using, so the conversation must end here; the next
                            # outer iteration opens a session on the new backend.
                            switched = isinstance(exec_result, dict) and exec_result.get("backend_switched")
                            if not want_more or status != "ready" or exec_result is None or switched:
                                break
                            conv_actions += 1
                            # Feed the tool result back into the same conversation,
                            # then re-append the refreshed STATE JSON so grammar
                            # backends and evidence citations stay valid.
                            result_text = encode(exec_result)
                            if len(result_text) > 8000:
                                result_text = result_text[:8000] + "…[truncated]"
                            conv_messages = conv_messages + [
                                {"role":"assistant","content":prediction["content"]},
                                {"role":"user","content":f"TOOL RESULT — {value['tool']}:\n{result_text}\n\nContinue the task: pick the next single action, or return finish/wait/decompose."},
                            ]
                            new_ids = _result_evidence_ids(value["tool"], exec_result)
                            if new_ids:
                                tools.visible_evidence.update(e['id'] for e in new_ids)
                                seen = {e.get('id') for e in conv_state.get('evidence', []) if isinstance(e, dict)}
                                conv_state['evidence'] = list(conv_state.get('evidence', [])) + [e for e in new_ids if e['id'] not in seen]
                            conv_state.setdefault('recent_actions', [])
                            conv_state['recent_actions'].insert(0, {
                                "tool": value["tool"], "arguments": value.get("arguments", {}),
                                "status": "completed", "result": _brief_result(exec_result, 1500),
                                "empty": _is_empty_lookup(_result_payload(exec_result))})
                            conv_state['recent_actions'] = conv_state['recent_actions'][:6]
                            conv_messages.append({"role":"user","content":encode(conv_state)})
                            conv_count = model.count(conv_messages)
                            # Graceful rollover: end this conversation before the
                            # packet outgrows the budget; next iteration self-hands-off.
                            if conv_count + policy["output_tokens"] + policy["safety_margin"] > int(policy["effective_tokens"] * CONV_CONTEXT_WARN):
                                break
                    corrections = 0
                    if status == "completed":
                        self.store.ingest(owner,session,"higher","task:"+key,
                            encode({"text":value["text"],"evidence":value.get("evidence",[]),"verification":"model_asserted"}),references=value.get('evidence',[]))
                    if callback:
                        try:
                            callback({"event":"step_completed","task":key,"step":sid,"status":status,"text":value["text"]})
                        except Exception:
                            pass  # Delivery failure never rolls back an accepted step.
            except OutcomeRejected as exc:
                corrections += 1
                try:
                    self.store.finish_step(key,token,sid,prediction,'ready' if corrections <= 2 else 'paused',
                                           str(exc),output_tokens=prediction.get('output_tokens'),
                                           reasoning=prediction.get('reasoning', ''))
                except Conflict:
                    return self.store.task(owner,session,root)
                if corrections <= 2:
                    continue
                return self.store.task(owner,session,root)
            except Exception as exc:
                if token:
                    try:
                        if sid:
                            self.store.finish_step(key,token,sid,prediction,"paused",str(exc),output_tokens=prediction.get('output_tokens'),
                                                   reasoning=prediction.get('reasoning', ''))
                        else:
                            self.store.release(key,token,str(exc))
                    except Conflict:
                        pass
                return self.store.task(owner,session,root)
        row = self.store.task(owner,session,root)
        if row["status"] in ("ready","running"):
            self.store.control(owner,session,root,"pause",reason="Step allowance reached")
        return self.store.task(owner,session,root)

    def response(self, owner, session, task):
        from .manager import generate_task_title
        task_title = "Autonomous Task"
        try:
            obj_art = self.store.artifact(owner, session, task["objective"], 0, 1024)
            task_title = generate_task_title(obj_art.get("body", ""))
        except Exception:
            pass

        if task["result"] and task["status"] == "completed":
            result = self.store.artifact(owner,session,task["result"],0,8192)
            text = result['body']
            if result['next_offset'] is not None:
                text+='\n[Continued; next offset '+str(result['next_offset'])+']'
            return text + f"\n\n⚡ Task Complete: {task_title}"
        with self.store.connect() as db:
            child = db.execute("""WITH RECURSIVE descendants(id) AS (
                SELECT id FROM task WHERE parent=? UNION ALL
                SELECT t.id FROM task t JOIN descendants d ON t.parent=d.id)
                SELECT t.id,t.status,t.reason FROM task t JOIN descendants d ON d.id=t.id
                WHERE t.status IN ('paused','waiting','stale') ORDER BY t.created LIMIT 1""", (task["id"],)).fetchone()
        reason = (child["reason"] if child and task['status'] in ('ready','running') else task["reason"])
        reason = reason or (child["reason"] if child else "Step allowance reached or a child needs attention")
        child_title = ""
        if child:
            try:
                ch_row = db.execute("SELECT objective FROM task WHERE id=?", (child["id"],)).fetchone()
                if ch_row:
                    ch_art = self.store.artifact(owner, session, ch_row[0], 0, 1024)
                    child_title = f"\nSubtask: {generate_task_title(ch_art.get('body', ''))}"
            except Exception:
                pass
        return f"⚡ Task: {task_title} — {task['status']}: {reason}{child_title}\nUse /task status or /task resume to continue."
