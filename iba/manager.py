"""Workspace and session manager for IBA in Dory LLM."""
import json
import os
from pathlib import Path
import sqlite3
import time
from typing import Dict, List, Optional, Any, Tuple

from .store import Store, uid
from .engine import Runtime
from .provider import get_default_provider, DoryLlamaServer, LMStudio


import re

_LEASE_SWEEP_DONE = False

CONFIG_PATH = Path(__file__).resolve().parent.parent / "iba_workspaces.json"


def generate_task_title(objective_text: str, max_len: int = 42) -> str:
    """Generate a clean, human-readable real name from an objective."""
    if not objective_text or not objective_text.strip():
        return "Autonomous Task"
    text = objective_text.strip()
    text = re.sub(r'^(?:please\s+|can\s+you\s+|kindly\s+|hey\s+dory,?\s*|dory,?\s*|see\s+if\s+you\s+can\s+)*', '', text, flags=re.IGNORECASE).strip()
    text = re.sub(r'^(?:go\s+over\s+(?:the\s+)?error\s+(?:here\s+)?and\s+(?:see\s+if\s+you\s+can\s+)?fix\s+it:?\s*|fix\s+(?:the\s+)?error:?\s*)', '', text, flags=re.IGNORECASE).strip()

    m_prop = re.search(r'Property\s*\(([^\)]+)\)', text)
    m_shader = re.search(r'Compute shader\s*\(([A-Za-z0-9_]+)', text, re.IGNORECASE)
    if m_shader and m_prop:
        return f"Fix {m_shader.group(1)} ({m_prop.group(1)})"

    m_locate = re.search(r'(?:locate|find)\s+(?:the\s+)?(?:relevant\s+)?(?:project\s+)?files?\s*(?:\(([^\)]+)\))?', text, re.IGNORECASE)
    if m_locate and m_locate.group(1):
        files = [f.strip().split('/')[-1] for f in m_locate.group(1).split(',')[:2]]
        return f"Locate {' & '.join(files)}"

    first_line = text.split('\n')[0].strip()
    words = [w.strip(".,;:?!()[]\"'") for w in first_line.split() if w.strip(".,;:?!()[]\"'")]
    if not words:
        return "Engineering Task"
    chosen = []
    for w in words[:5]:
        if w.startswith('_') or '.' in w or w.isupper() or any(c.isupper() for c in w[1:]):
            chosen.append(w)
        else:
            chosen.append(w.capitalize())
    title = " ".join(chosen)
    if len(title) > max_len:
        title = title[:max_len].rsplit(' ', 1)[0]
    return title or "Engineering Task"


def generate_step_title(action_name: Optional[str], action_args: Optional[dict] = None) -> str:
    """Generate a clean, descriptive real name for an executed step."""
    if not action_name:
        return "Planning & Strategy"
    batch = isinstance(action_args, list)
    args = (action_args[0] if batch and action_args else action_args) or {}
    if not isinstance(args, dict):
        args = {}
    if action_name == "read_file":
        path = args.get("path", "")
        filename = path.split("/")[-1].split("\\")[-1] or path
        line_info = f" around line {args['line']}" if args.get("line") else ""
        return f"Inspect {filename}{line_info}" if filename else "Read Source File"
    if action_name == "find_files":
        pattern = args.get("pattern", "*")
        return f"Find '{pattern}'"
    if action_name == "list_directory":
        path = args.get("path", ".")
        return "List Root Directory" if path in (".", "", "/") else f"List {path.split('/')[-1]}"
    if action_name == "write_file":
        path = args.get("path", "")
        filename = path.split("/")[-1].split("\\")[-1] or path
        return f"Patch {filename}" if filename else "Write File"
    if action_name == "calculate":
        return f"Calculate {args.get('expression', '')[:20]}"
    if action_name == "run_check":
        return f"Run {args.get('name', 'Check')}"
    return f"Execute {action_name.replace('_', ' ').capitalize()}"


def extract_script_map(path: str, content: str) -> Tuple[dict, str]:
    """Extract structural symbols, shader properties, and compact mental map for a script."""
    symbols = {
        "classes": [],
        "structs": [],
        "interfaces": [],
        "methods": [],
        "shader_properties": [],
        "compute_kernels": [],
        "buffer_bindings": []
    }
    
    # Classes, structs, interfaces
    for m in re.finditer(r'\b(class|struct|interface)\s+([A-Za-z0-9_]+)', content):
        kind = m.group(1)
        name = m.group(2)
        key = "classes" if kind == "class" else ("structs" if kind == "struct" else "interfaces")
        if name not in symbols[key]:
            symbols[key].append(name)
        
    # Shader properties / uniform names (like _SQL3MediumLayers, _SQL3TableKeysRW)
    for m in re.finditer(r'(_[A-Za-z0-9_]+)', content):
        prop = m.group(1)
        if len(prop) > 3 and not prop.startswith("__") and prop not in symbols["shader_properties"]:
            symbols["shader_properties"].append(prop)
            
    # Compute buffer bindings & dispatches
    for m in re.finditer(r'\b(SetBuffer|SetTexture|Dispatch|DispatchIndirect|GraphicsBuffer|ComputeBuffer)\s*\(([^)]+)\)', content):
        call_name = m.group(1)
        call_args = m.group(2).replace('\n', ' ').strip()
        binding_str = f"{call_name}({call_args[:65]})"
        if binding_str not in symbols["buffer_bindings"]:
            symbols["buffer_bindings"].append(binding_str)

    # Key Methods
    for m in re.finditer(r'\b(public|private|protected|internal|static|void|int|float|bool|string)\s+([A-Za-z0-9_]+)\s*\([^)]*\)\s*\{', content):
        mname = m.group(2)
        if mname not in ("if", "for", "while", "switch", "catch", "using", "lock"):
            if mname not in symbols["methods"]:
                symbols["methods"].append(mname)

    # Compute shader kernels: #pragma kernel <name>
    for m in re.finditer(r'#pragma\s+kernel\s+([A-Za-z0-9_]+)', content):
        kname = m.group(1)
        if kname not in symbols["compute_kernels"]:
            symbols["compute_kernels"].append(kname)

    # Clean up and produce a concise summary
    lines = content.splitlines()
    total_lines = len(lines)
    
    parts = []
    if symbols["classes"]:
        parts.append(f"Class: {', '.join(symbols['classes'][:2])}")
    elif symbols["compute_kernels"]:
        parts.append(f"Kernels: {', '.join(symbols['compute_kernels'][:3])}")
    if symbols["shader_properties"]:
        props = symbols["shader_properties"][:6]
        parts.append(f"Props: {', '.join(props)}")
    if symbols["buffer_bindings"]:
        bindings = symbols["buffer_bindings"][:3]
        parts.append(f"Bindings: {'; '.join(bindings)}")
    elif symbols["methods"]:
        parts.append(f"Methods: {', '.join(symbols['methods'][:3])}")
        
    summary = f"[{total_lines} lines] " + (" | ".join(parts) if parts else "Script file")
    return symbols, summary


class WorkspaceManager:
    def __init__(self, config_file: Optional[Path] = None):
        self.config_file = config_file or CONFIG_PATH
        self.config = self._load_config()
        self._stores: Dict[str, Store] = {}

    def _load_config(self) -> dict:
        default_ws = Path(__file__).resolve().parent.parent.parent / "InferenceLightingLab"
        if not default_ws.exists():
            default_ws = Path(__file__).resolve().parent.parent / "workspaces" / "default"

        default_config = {
            "active_workspace": str(default_ws.resolve()),
            "active_session": "default",
            "owner": "dory-user@local",
            "workspaces": [
                {
                    "name": "InferenceLightingLab",
                    "path": str(default_ws.resolve()),
                    "description": "Active Unity Inference Lighting Lab project",
                    "created_at": time.time(),
                }
            ],
        }

        if self.config_file.exists():
            try:
                with open(self.config_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    # merge with defaults if missing keys
                    for k, v in default_config.items():
                        if k not in data:
                            data[k] = v
                    return data
            except Exception:
                pass

        self._save_config(default_config)
        return default_config

    def _save_config(self, cfg: Optional[dict] = None):
        if cfg is not None:
            self.config = cfg
        try:
            self.config_file.parent.mkdir(parents=True, exist_ok=True)
            with open(self.config_file, "w", encoding="utf-8") as f:
                json.dump(self.config, f, indent=2, ensure_ascii=False)
        except Exception:
            pass

    def get_active_workspace_path(self) -> Path:
        active = self.config.get("active_workspace")
        path = Path(active) if active else Path(__file__).resolve().parent.parent
        path.mkdir(parents=True, exist_ok=True)
        return path.resolve()

    def get_active_session(self) -> str:
        return self.config.get("active_session", "default")

    def get_owner(self) -> str:
        return self.config.get("owner", "dory-user@local")

    def get_store(self, workspace_path: Optional[Path] = None) -> Store:
        ws = (workspace_path or self.get_active_workspace_path()).resolve()
        ws_str = str(ws)
        if ws_str not in self._stores:
            runtime_dir = ws / "iba_runtime"
            runtime_dir.mkdir(parents=True, exist_ok=True)
            self._stores[ws_str] = Store(runtime_dir)
        return self._stores[ws_str]

    def get_runtime(self, workspace_path: Optional[Path] = None, port: int = 8080, api_key: str = "") -> Runtime:
        ws = (workspace_path or self.get_active_workspace_path()).resolve()
        store = self.get_store(ws)
        # First runtime request in this process: every lease still in the DB
        # belongs to a worker thread from a previous process — none can be
        # alive now, so recover them all instead of waiting out the TTL.
        global _LEASE_SWEEP_DONE
        if not _LEASE_SWEEP_DONE:
            _LEASE_SWEEP_DONE = True
            for s in list(self._stores.values()):
                try:
                    s.recover(all_leases=True)
                except Exception:
                    pass
        # Clear stale worker leases from interrupted runs. Only leases that
        # already expired are touched — a live lease belongs to a concurrent
        # worker (parallel inference peers) and must never be stolen.
        try:
            with store.connect(True) as db:
                db.execute("UPDATE task SET status='ready', lease=NULL, expires=NULL WHERE status='running' AND (lease IS NULL OR expires<?)", (time.time(),))
        except Exception:
            pass
        provider = get_default_provider(port=port, api_key=api_key)
        return Runtime(store, provider, ws)

    def list_workspaces(self) -> List[dict]:
        active_path = str(self.get_active_workspace_path())
        result = []
        for w in self.config.get("workspaces", []):
            p = Path(w["path"])
            stats = self.get_workspace_stats(p) if p.exists() else {"status": "missing"}
            result.append({
                **w,
                "is_active": (str(p.resolve()) == active_path if p.exists() else False),
                "stats": stats,
            })
        return result

    def get_workspace_stats(self, ws_path: Path) -> dict:
        runtime_dir = ws_path / "iba_runtime"
        db_path = runtime_dir / "control.sqlite"
        if not db_path.exists():
            return {
                "exists": ws_path.exists(),
                "db_initialized": False,
                "tasks_count": 0,
                "steps_count": 0,
                "sessions_count": 0,
                "db_size_kb": 0,
            }
        try:
            size_kb = round(db_path.stat().st_size / 1024, 1)
            with sqlite3.connect(f"file:{db_path.as_posix()}?mode=ro", uri=True) as db:
                tasks = db.execute("SELECT count(*) FROM task").fetchone()[0]
                steps = db.execute("SELECT count(*) FROM step").fetchone()[0]
                sessions = db.execute("SELECT count(DISTINCT session) FROM task").fetchone()[0]
            return {
                "exists": True,
                "db_initialized": True,
                "tasks_count": tasks,
                "steps_count": steps,
                "sessions_count": max(1, sessions),
                "db_size_kb": size_kb,
            }
        except Exception as e:
            return {
                "exists": True,
                "db_initialized": True,
                "error": str(e),
            }

    def set_active_workspace(self, path_str: str) -> dict:
        target = Path(path_str).resolve()
        if not target.exists():
            target.mkdir(parents=True, exist_ok=True)
        # Ensure it is in the workspaces list
        normalized = str(target)
        found = False
        for w in self.config.get("workspaces", []):
            if str(Path(w["path"]).resolve()) == normalized:
                found = True
                break
        if not found:
            self.config.setdefault("workspaces", []).append({
                "name": target.name or "Workspace",
                "path": normalized,
                "description": f"Workspace at {normalized}",
                "created_at": time.time(),
            })

        self.config["active_workspace"] = normalized
        self._save_config()
        # Ensure Store is initialized
        self.get_store(target)
        return {
            "status": "ok",
            "active_workspace": normalized,
            "stats": self.get_workspace_stats(target),
        }

    def add_workspace(self, name: str, path_str: str, description: str = "") -> dict:
        target = Path(path_str).resolve()
        target.mkdir(parents=True, exist_ok=True)
        normalized = str(target)
        # Check if already registered
        for w in self.config.get("workspaces", []):
            if str(Path(w["path"]).resolve()) == normalized:
                w["name"] = name
                if description:
                    w["description"] = description
                self.config["active_workspace"] = normalized
                self._save_config()
                self.get_store(target)
                return w

        entry = {
            "name": name or target.name or "Workspace",
            "path": normalized,
            "description": description or f"Project at {normalized}",
            "created_at": time.time(),
        }
        self.config.setdefault("workspaces", []).append(entry)
        self.config["active_workspace"] = normalized
        self._save_config()
        self.get_store(target)
        return entry

    def list_sessions(self, ws_path: Optional[Path] = None) -> List[dict]:
        store = self.get_store(ws_path)
        owner = self.get_owner()
        active_sess = self.get_active_session()

        sessions_map = {}
        with store.connect() as db:
            rows = db.execute("SELECT session, count(*) as task_count, max(created) as last_active FROM task WHERE owner=? GROUP BY session", (owner,)).fetchall()
            for r in rows:
                sessions_map[r["session"]] = {
                    "session_id": r["session"],
                    "task_count": r["task_count"],
                    "last_active": r["last_active"],
                }

        # Always ensure 'default' and the active session are in the list
        for s in ("default", active_sess):
            if s not in sessions_map:
                sessions_map[s] = {
                    "session_id": s,
                    "task_count": 0,
                    "last_active": time.time(),
                }

        result = []
        for s_id, data in sorted(sessions_map.items(), key=lambda x: x[1].get("last_active") or 0, reverse=True):
            result.append({
                **data,
                "is_active": (s_id == active_sess),
            })
        return result

    def set_active_session(self, session_id: str) -> dict:
        cleaned = session_id.strip() or "default"
        self.config["active_session"] = cleaned
        self._save_config()
        return {"status": "ok", "active_session": cleaned}

    def delete_session(self, session_id: str, ws_path: Optional[Path] = None) -> dict:
        """Drop one session (its tasks, artifacts and persona data) from a workspace."""
        if not session_id or session_id == "default":
            raise ValueError("The default session cannot be deleted")
        store = self.get_store(ws_path)
        owner = self.get_owner()
        counts = store.delete_session(owner, session_id)
        if self.config.get("active_session") == session_id:
            self.config["active_session"] = "default"
            self._save_config()
        return {"status": "ok", "deleted_session": session_id, "counts": counts}

    def get_session_tasks(self, ws_path: Optional[Path] = None, session_id: Optional[str] = None) -> List[dict]:
        store = self.get_store(ws_path)
        owner = self.get_owner()
        session = session_id or self.get_active_session()
        tasks = store.tasks(owner, session, limit=50)
        # Fetch objective text for each task and generate friendly real names
        for t in tasks:
            try:
                art = store.artifact(owner, session, t["objective"], 0, 500)
                t["objective_preview"] = art.get("body", "")
            except Exception:
                t["objective_preview"] = ""
            t["title"] = generate_task_title(t["objective_preview"])
        return tasks

    def get_task_details(self, task_id: str, ws_path: Optional[Path] = None) -> dict:
        store = self.get_store(ws_path)
        owner = self.get_owner()
        session = self.get_active_session()

        with store.connect() as db:
            task_row = db.execute("SELECT * FROM task WHERE id=?", (task_id,)).fetchone()
            if not task_row:
                return {"error": "Task not found"}
            task = dict(task_row)
            try:
                art = store.artifact(owner, session, task["objective"], 0, 500)
                task["objective_preview"] = art.get("body", "")
            except Exception:
                task["objective_preview"] = ""
            task["title"] = generate_task_title(task["objective_preview"])

            steps_rows = db.execute(
                "SELECT s.*, a.name as action_name, a.args as action_args, a.state as action_state, a.result as action_result "
                "FROM step s LEFT JOIN action a ON a.step=s.id WHERE s.task=? ORDER BY s.generation ASC",
                (task_id,),
            ).fetchall()

            steps = []
            for sr in steps_rows:
                s_dict = dict(sr)
                if s_dict.get("action_args"):
                    try:
                        s_dict["action_args"] = json.loads(s_dict["action_args"])
                    except Exception:
                        pass
                if s_dict.get("action_result"):
                    try:
                        art = store.artifact(owner, session, s_dict["action_result"], 0, 4096)
                        s_dict["action_result_body"] = art.get("body", "")
                    except Exception:
                        pass
                s_dict["step_title"] = generate_step_title(s_dict.get("action_name"), s_dict.get("action_args"))
                steps.append(s_dict)

        return {"task": task, "steps": steps}

    def get_session_history(self, ws_path: Optional[Path] = None, session_id: Optional[str] = None) -> List[dict]:
        store = self.get_store(ws_path)
        owner = self.get_owner()
        session = session_id or self.get_active_session()

        messages = []
        with store.connect() as db:
            tasks = db.execute(
                "SELECT * FROM task WHERE owner=? AND session=? AND parent IS NULL ORDER BY created ASC",
                (owner, session)
            ).fetchall()

            for t in tasks:
                t_dict = dict(t)
                tid = t_dict["id"]
                user_text = ""
                try:
                    obj_art = store.artifact(owner, session, t_dict["objective"], 0, 65536)
                    user_text = obj_art.get("body", "")
                except Exception:
                    pass

                task_title = generate_task_title(user_text)

                messages.append({
                    "role": "user",
                    "content": user_text,
                    "task_id": tid,
                    "task_title": task_title,
                    "timestamp": t_dict.get("created"),
                })

                resp_text = ""
                if t_dict.get("result"):
                    try:
                        res_art = store.artifact(owner, session, t_dict["result"], 0, 65536)
                        resp_text = res_art.get("body", "")
                    except Exception:
                        pass

                steps_query = """
                WITH RECURSIVE family(id) AS (
                    SELECT ? UNION ALL SELECT t.id FROM task t JOIN family f ON t.parent=f.id
                )
                SELECT s.*, a.name as action_name, a.args as action_args, a.state as action_state, a.result as action_result
                FROM step s
                JOIN family f ON f.id=s.task
                LEFT JOIN action a ON a.step=s.id
                ORDER BY s.created ASC
                """
                steps_rows = db.execute(steps_query, (tid,)).fetchall()
                steps = []
                reasoning_parts = []
                for idx, sr in enumerate(steps_rows):
                    s_dict = dict(sr)
                    action_args = {}
                    if s_dict.get("action_args"):
                        try:
                            action_args = json.loads(s_dict["action_args"])
                        except Exception:
                            action_args = s_dict["action_args"]
                    step_reasoning = ""
                    if s_dict.get("reasoning"):
                        try:
                            reasoning_art = store.artifact(owner, session, s_dict["reasoning"], 0, 65536)
                            step_reasoning = reasoning_art.get("body", "")
                        except Exception:
                            step_reasoning = ""
                    if step_reasoning.strip():
                        reasoning_parts.append(step_reasoning.strip())
                    action_result_body = ""
                    if s_dict.get("action_result"):
                        try:
                            art = store.artifact(owner, session, s_dict["action_result"], 0, 4096)
                            action_result_body = art.get("body", "")
                        except Exception:
                            pass

                    step_name = generate_step_title(s_dict.get("action_name"), action_args)

                    steps.append({
                        "step_id": s_dict["id"],
                        "step_number": idx + 1,
                        "step_title": step_name,
                        "task_id": s_dict["task"],
                        "action_name": s_dict.get("action_name"),
                        "action_args": action_args,
                        "action_state": s_dict.get("action_state") or ("completed" if s_dict.get("action_name") else None),
                        "action_result_body": action_result_body,
                        "reasoning": step_reasoning,
                        "input_tokens": s_dict.get("input_tokens"),
                        "output_tokens": s_dict.get("output_tokens"),
                    })

                # Group steps by the task that executed them so parallel child
                # tasks can be rendered side by side in chat history.
                grouped = {}
                group_order = []
                for s in steps:
                    gid = s.get("task_id") or tid
                    if gid not in grouped:
                        grouped[gid] = []
                        group_order.append(gid)
                    grouped[gid].append(s)
                step_groups = []
                for gid in group_order:
                    gtitle = task_title
                    if gid != tid:
                        try:
                            grows = db.execute("SELECT objective FROM task WHERE id=?", (gid,)).fetchone()
                            if grows:
                                gbody = store.artifact(owner, session, grows[0], 0, 500).get("body", "")
                                gtitle = generate_task_title(gbody) or gtitle
                        except Exception:
                            gtitle = gid[:8]
                    step_groups.append({"task_id": gid, "title": gtitle, "steps": grouped[gid]})

                # Roll up descendant activity: a decomposed root can sit in
                # 'ready'/'paused' while a child is still executing. Report the
                # family's most-active status so clients poll/resume correctly.
                fam_status = t_dict.get("status")
                try:
                    fam_rows = db.execute(
                        """WITH RECURSIVE family(id) AS (
                            SELECT ? UNION ALL SELECT t.id FROM task t JOIN family f ON t.parent=f.id)
                            SELECT status FROM task WHERE id IN (SELECT id FROM family)""",
                        (tid,)).fetchall()
                    fam_states = {r["status"] for r in fam_rows}
                    if "running" in fam_states:
                        fam_status = "running"
                    elif "ready" in fam_states and fam_status not in ("running",):
                        fam_status = "ready"
                    elif "waiting" in fam_states and fam_status in ("completed", "paused", "ready"):
                        fam_status = "waiting"
                except Exception:
                    pass

                messages.append({
                    "role": "assistant",
                    "content": resp_text,
                    "reasoning": "\n\n".join(reasoning_parts),
                    "task_id": tid,
                    "task_title": task_title,
                    "step_groups": step_groups,
                    "status": fam_status,
                    "reason": t_dict.get("reason"),
                    "steps": steps,
                    "timestamp": t_dict.get("updated") or t_dict.get("created"),
                })
        return messages
