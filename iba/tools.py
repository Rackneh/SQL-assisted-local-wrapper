"""Small, scoped tool bundles for the durable runtime.

Tools return data, never model instructions. All results are committed to an
artifact by the runtime before another invocation can observe them.
"""
import ast
import hashlib
import json
import operator
import os
from pathlib import Path
import re
import tempfile
import time
import subprocess

from .store import encode, uid
from .sandbox import WorkspaceSandbox, SandboxViolationError


TOOL_HELP = {
    "search_evidence": {"query": "string", "database": "optional database id", "limit": "integer <=64"},
    "read_evidence": {"id": "integer evidence id"},
    "read_document": {"id": "document id", "after": "last chunk ordinal; default -1", "limit": "integer <=32"},
    "read_artifact": {"id": "artifact id", "offset": "character offset", "limit": "characters <=32768"},
    "databases": {},
    "create_database": {"name": "unique concept/task name", "kind": "knowledge|math|code|higher|lower"},
    "remember": {"database": "database id", "source": "stable source key", "text": "finding; preserve uncertainty", "evidence": "array of original evidence ids"},
    "read_file": {"path": "workspace relative UTF-8 file", "offset": "character offset (default 0)", "limit": "with 'line': LINES to return (default 80, max 300); with 'offset': characters (default ~4000, max 6000 for large files)", "line": "target line number; returns a window of 'limit' lines starting there"},
    "find_files": {"pattern": "filename or pattern substring to locate files across workspace", "limit": "integer <=50"},
    "search_text": {"pattern": "text or regex to find inside source files (case-insensitive)", "path": "optional workspace relative file or directory", "glob": "optional filename filter such as *.cs", "limit": "integer <=50"},
    "list_directory": {"path": "workspace relative directory", "after": "optional last name", "limit": "integer <=200"},
    "ingest_file": {"path": "workspace relative UTF-8 file", "database": "database id (default code)"},
    "write_file": {"path": "workspace relative file", "text": "complete UTF-8 content", "expected_hash": "required: SHA256 from read_file, or literal string absent to create a new file"},
    "edit_file": {"path": "workspace relative file", "old_text": "exact existing snippet to replace — copy verbatim including whitespace", "new_text": "replacement snippet (empty string deletes old_text)", "expected_hash": "required: SHA256 from read_file", "replace_all": "optional boolean, default false — set true only to replace every occurrence"},
    "calculate": {"expression": "exact rational arithmetic + - * / ** with parentheses; no functions"},
    "list_checks": {},
    "run_check": {"name": "name of a host-configured test/build command; no arbitrary shell input"},
    "scope_document": {"id": "document to require exhaustive chunk coverage before this task can finish"},
    "record_coverage": {"id": "evidence chunk id fully present in this invocation", "finding": "specific analysis of that chunk"},
    "coverage_status": {},
    "query_code_map": {"path": "optional file path substring", "query": "optional symbol or keyword to find in mental map"},
    "propose_improvement": {
        "title": "short descriptive title of improvement",
        "observation": "what inefficiency, redundant query, or optimization was observed",
        "proposal": "concrete proposal for how things could be done more efficiently",
        "category": "optional: memory-architecture | execution-efficiency | codebase-cognition | general-workflows",
        "expected_impact": "optional: expected token, latency, or reliability gain"
    },
    "switch_backend": {
        "backend": '"dory" (deep local reasoning engine) or "lmstudio" (faster general model)',
        "reason": "optional: why this backend is needed for the current step"
    },
}


def calculate(expression):
    """Bounded exact rational evaluator, never eval()."""
    from fractions import Fraction
    if not isinstance(expression, str) or len(expression) > 2048:
        raise ValueError("Expression must contain at most 2048 characters")
    tree = ast.parse(expression, mode="eval")
    if sum(1 for _ in ast.walk(tree)) > 128:
        raise ValueError("Expression is too complex")
    operations = {ast.Add: operator.add, ast.Sub: operator.sub, ast.Mult: operator.mul, ast.Div: operator.truediv}
    def walk(node):
        if isinstance(node, ast.Expression):
            return walk(node.body)
        if isinstance(node, ast.Constant) and type(node.value) in (int,float):
            value = Fraction(ast.get_source_segment(expression, node))
        elif isinstance(node, ast.UnaryOp) and isinstance(node.op, (ast.UAdd,ast.USub)):
            value = walk(node.operand) * (-1 if isinstance(node.op,ast.USub) else 1)
        elif isinstance(node, ast.BinOp):
            a,b = walk(node.left), walk(node.right)
            if isinstance(node.op, ast.Pow):
                if b.denominator != 1 or abs(b) > 64:
                    raise ValueError("Only integer powers between -64 and 64 are supported")
                value = a ** int(b)
            elif type(node.op) in operations:
                value = operations[type(node.op)](a,b)
            else:
                raise ValueError("Unsupported operator")
        else:
            raise ValueError("Only numeric arithmetic is allowed")
        if max(value.numerator.bit_length(),value.denominator.bit_length()) > 16384:
            raise ValueError("Result too large")
        return value
    value = walk(tree)
    return {"expression":expression,"result":str(value),"status":"verified","verifier":"exact-rational-v1"}


class Tools:
    def __init__(self, store, owner, session, workspace, checks=None, task=None):
        self.store,self.owner,self.session = store,owner,session
        self.workspace = Path(workspace).resolve()
        self.checks = checks or {}
        self.task=task
        self.visible_evidence=set()

    def resolve(self, path):
        target = (self.workspace / path).resolve()
        target.relative_to(self.workspace)
        # Runtime metadata is not editable through model file tools.
        if target == self.store.root or self.store.root in target.parents:
            raise ValueError("Use database tools for IBA storage")
        parts={part.lower() for part in target.relative_to(self.workspace).parts}
        if parts.intersection({'.git','iba_memory','iba_runtime'}) or target.suffix.lower() in ('.sqlite','.sqlite3','.db'):
            raise ValueError("Repository internals and database files are not editable workspace documents")
        return target

    @staticmethod
    def _preserve_eol(text, target):
        """Convert '\n'-normalized text back to the file's dominant line ending
        so writes don't silently convert a CRLF file to LF (or vice versa)."""
        try:
            raw = target.read_bytes()
        except OSError:
            return text
        crlf = raw.count(b"\r\n")
        lf_only = raw.count(b"\n") - crlf
        if crlf > lf_only:
            return text.replace("\r\n", "\n").replace("\r", "\n").replace("\n", "\r\n")
        return text

    @staticmethod
    def _preprocessor_conditions(lines):
        """Map 1-based line number -> innermost enclosing preprocessor
        condition (e.g. 'UNITY_EDITOR', '!UNITY_EDITOR' inside #else)."""
        condmap = {}
        stack = []
        for i, line in enumerate(lines, 1):
            s = line.strip()
            m = re.match(r'#\s*(?:if|ifdef|ifndef)\b\s*(.*)', s)
            if m:
                stack.append(m.group(1).strip())
            elif re.match(r'#\s*elif\b', s):
                if stack:
                    rest = s.split(None, 1)
                    stack[-1] = rest[1].strip() if len(rest) > 1 else stack[-1]
            elif re.match(r'#\s*else\b', s):
                if stack:
                    stack[-1] = '!' + stack[-1]
            elif re.match(r'#\s*endif\b', s):
                if stack:
                    stack.pop()
            if stack:
                condmap[i] = stack[-1]
        return condmap

    @staticmethod
    def _quote_warning(text):
        """Heuristic: an odd count of unescaped double quotes almost always
        means a stray quote broke a string literal (e.g. `foo");`). Warn rather
        than block — rare false positives exist (char literal '\"')."""
        n = 0
        i = 0
        while i < len(text):
            if text[i] == "\\":
                i += 2
                continue
            if text[i] == '"':
                n += 1
            i += 1
        if n % 2:
            return ("odd number of unescaped double-quotes in the written text — likely a "
                    "stray quote breaking a string literal; re-read the edited region and fix it")
        return None

    @staticmethod
    def _file_text_hash(path):
        """SHA-256 of file content exactly as read_file reports it: text-mode
        read (universal newlines, errors=replace) re-encoded as UTF-8. Must be
        used by BOTH read_file and write_file — hashing raw bytes instead makes
        the expected_hash check impossible to satisfy on CRLF files."""
        with path.open("r", encoding="utf-8", errors="replace") as f:
            return hashlib.sha256(f.read().encode("utf-8")).hexdigest()

    def _file_index(self):
        """Cached workspace-relative file list, used for path suggestions."""
        if getattr(self, '_files_cache', None) is None:
            files = []
            skip = {'.git','iba_memory','iba_runtime','library','temp','logs','obj','build',
                    'node_modules','.vs','bin','backup','backups'}
            for root, dirs, names in os.walk(self.workspace):
                dirs[:] = [d for d in dirs if d.lower() not in skip]
                for n in names:
                    files.append(os.path.relpath(os.path.join(root, n), self.workspace).replace('\\', '/'))
            self._files_cache = files
        return self._files_cache

    _INDEX_SKIP = {'.git','iba_memory','iba_runtime','library','temp','logs','obj','build',
                   'node_modules','.vs','bin','backup','backups'}

    def _dir_index(self):
        """Cached workspace-relative directory list."""
        if getattr(self, '_dirs_cache', None) is None:
            dirs = []
            for root, dirnames, _ in os.walk(self.workspace):
                dirnames[:] = [d for d in dirnames if d.lower() not in self._INDEX_SKIP]
                for d in dirnames:
                    dirs.append(os.path.relpath(os.path.join(root, d), self.workspace).replace('\\', '/'))
            self._dirs_cache = dirs
        return self._dirs_cache

    def _resolve_near_miss(self, requested, kind="file"):
        """Decisive fuzzy path correction, or None when ambiguous/too far.

        Small models see 'Similar files' suggestions but cannot re-emit a long
        path verbatim — they regenerate it and corrupt characters. When exactly
        one workspace path is a clearly-better match, return it so the caller
        can read the intended file and report the correction.
        """
        try:
            import difflib
            want = str(requested or "").replace("\\", "/").strip().lstrip("./")
            if not want:
                return None
            if kind == "dir":
                pool = [d for d in self._dir_index() if not d.lower().endswith('.meta')]
            else:
                pool = [f for f in self._file_index() if not f.lower().endswith('.meta')]
            if not pool:
                return None
            want_dir, _, want_base = want.rpartition("/")
            # Stage 1: cheap basename shortlist, biased toward the same folder.
            same_dir = [f for f in pool if f.rpartition("/")[0].lower() == want_dir.lower()]
            shortlist = list(dict.fromkeys(
                same_dir + difflib.get_close_matches(
                    want_base.lower(),
                    [f.rpartition("/")[2].lower() for f in pool],
                    n=12, cutoff=0.45)))
            # get_close_matches returns basenames, not paths — map back.
            base_to_paths = {}
            for f in pool:
                base_to_paths.setdefault(f.rpartition("/")[2].lower(), []).append(f)
            candidates = []
            for s in shortlist:
                if "/" in s:
                    candidates.append(s)
                else:
                    candidates.extend(base_to_paths.get(s, []))
            candidates = list(dict.fromkeys(candidates))
            if not candidates:
                return None
            # Stage 2: full-path similarity; require a decisive winner.
            scored = sorted(
                ((difflib.SequenceMatcher(None, want.lower(), c.lower()).ratio(), c)
                 for c in candidates),
                key=lambda x: x[0], reverse=True)
            best_ratio, best = scored[0]
            second_ratio = scored[1][0] if len(scored) > 1 else 0.0
            if best_ratio < 0.84 or (second_ratio >= 0.80 and best_ratio - second_ratio < 0.04):
                return None
            return best
        except Exception:
            return None

    def _path_hint(self, requested, kind="file"):
        """' Similar files: ...' suffix suggesting near-miss paths on a miss."""
        try:
            import difflib
            want = str(requested or "").replace("\\", "/").strip().lstrip("./")
            if not want:
                return ""
            if kind == "dir":
                files = self._dir_index()
                noun = "directories"
            else:
                files = self._file_index()
                noun = "files"
            files = [f for f in files if not f.lower().endswith('.meta')]
            if not files:
                return ""
            want_dir, _, want_base = want.rpartition("/")
            same_dir = [f for f in files
                        if f.rpartition("/")[0].lower() == want_dir.lower()]
            pool = same_dir or files
            hits = difflib.get_close_matches(
                want_base.lower(),
                [f.rpartition("/")[2].lower() for f in pool],
                n=3, cutoff=0.55)
            if not hits:
                return ""
            out, seen = [], set()
            hitset = set(hits)
            for f in pool:
                if f.rpartition("/")[2].lower() in hitset and f not in seen:
                    seen.add(f)
                    out.append(f)
                if len(out) >= 3:
                    break
            return f" Similar {noun}: " + ", ".join(repr(p) for p in out) + "."
        except Exception:
            return ""

    def execute(self, name, args):
        if name not in TOOL_HELP or not isinstance(args,dict):
            raise ValueError("Unknown tool or invalid arguments")
        if set(args) - set(TOOL_HELP[name]):
            raise ValueError("Unknown arguments: " + ", ".join(sorted(set(args)-set(TOOL_HELP[name]))))
        if name == "search_evidence":
            return self.store.search(self.owner,self.session,args["query"],args.get("database"),args.get("limit",16))
        if name == "read_evidence":
            return self.store.evidence(self.owner,self.session,args["id"])
        if name == "read_document":
            return self.store.document(self.owner,self.session,args["id"],args.get("after",-1),args.get("limit",16))
        if name == "read_artifact":
            return self.store.artifact(self.owner,self.session,args["id"],args.get("offset",0),args.get("limit",8192))
        if name == "databases":
            return self.store.databases()
        if name == 'coverage_status':
            return self.store.coverage_status(self.owner,self.session,self.task)
        if name == 'scope_document':
            doc=self.store.document(self.owner,self.session,args['id'],limit=1)
            if not doc['current']:
                raise ValueError('Cannot require a superseded document')
            with self.store.connect(True) as db:
                if db.execute('SELECT count(*) FROM task_document WHERE task=?',(self.task,)).fetchone()[0]>=64:
                    raise ValueError('Decompose tasks that require more than 64 documents')
                db.execute('INSERT OR IGNORE INTO task_document VALUES(?,?,?)',(self.task,doc['id'],doc['total_chunks']))
            return self.store.coverage_status(self.owner,self.session,self.task)
        if name == 'record_coverage':
            eid=int(args['id'])
            if eid not in self.visible_evidence:
                raise ValueError('Read the whole evidence chunk into context before recording coverage')
            evidence=self.store.evidence(self.owner,self.session,eid)
            if not evidence['current'] or not isinstance(args.get('finding'),str) or not args['finding'].strip():
                raise ValueError('Coverage needs current evidence and a finding')
            with self.store.connect(True) as db:
                if not db.execute('SELECT 1 FROM task_document WHERE task=? AND document=?',(self.task,evidence['document'])).fetchone():
                    raise ValueError('Use scope_document first')
                db.execute('INSERT OR REPLACE INTO coverage VALUES(?,?,?)',(self.task,eid,args['finding']))
                db.execute('INSERT OR IGNORE INTO dependency VALUES(?,?)',(self.task,eid))
            return {'evidence':eid,'status':'analyzed','verification':'model_asserted'}
        if name == 'list_checks':
            return {'checks':list(self.checks)}
        if name == 'run_check':
            argv = self.checks.get(args['name'])
            if not isinstance(argv,list) or not argv or any(not isinstance(a,str) for a in argv):
                avail = ', '.join(sorted(self.checks)) or '(none configured)'
                raise ValueError(f"No such host-configured check '{args['name']}'. "
                                 f"Available checks: {avail}. Call list_checks to see them — do not guess names.")
            with tempfile.TemporaryFile(mode='w+b') as output:
                process = subprocess.Popen(argv,cwd=str(self.workspace),stdout=output,stderr=subprocess.STDOUT,
                    shell=False,**({'creationflags':subprocess.CREATE_NO_WINDOW} if os.name=='nt' else {'start_new_session':True}))
                timed_out=False
                try:
                    code=process.wait(timeout=300)
                except subprocess.TimeoutExpired:
                    timed_out=True
                    if os.name=='nt':
                        subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True,check=False)
                    else:
                        import signal
                        os.killpg(process.pid,signal.SIGKILL)
                    code=process.wait(timeout=10)
                output.seek(0)
                import io
                reader=io.TextIOWrapper(output,encoding='utf-8',errors='replace')
                try:
                    document=self.store.ingest_stream(self.owner,self.session,'code','check-log:'+uid(),reader)
                finally:
                    reader.detach()
            receipt={'check':args['name'],'argv':argv,'exit_code':code,'timed_out':timed_out,
                     'status':'passed' if code==0 and not timed_out else 'failed','log_document':document}
            receipt['document']=self.store.ingest(self.owner,self.session,'code','check-receipt:'+uid(),encode(receipt))
            return receipt
        if name == "create_database":
            kind = args.get("kind","knowledge")
            if kind not in ("knowledge","math","code","higher","lower"):
                raise ValueError("Unsupported database kind")
            return self.store.create_database(args["name"],kind)
        if name == "remember":
            sources = [self.store.evidence(self.owner,self.session,k) for k in args.get("evidence",[])]
            if any(not s["current"] for s in sources):
                raise ValueError("Finding references stale evidence")
            body = encode({"text":args["text"],"evidence":[s["id"] for s in sources],"status":"asserted"})
            key = self.store.ingest(self.owner,self.session,args["database"],args["source"],body,references=[s["id"] for s in sources])
            return {"document":key,"status":"asserted"}
        if name == "switch_backend":
            from .provider import switch_backend
            return switch_backend(args.get("backend"), args.get("reason", ""))
        if name == "calculate":
            result = calculate(args["expression"])
            result["document"] = self.store.ingest(self.owner,self.session,"math","calculation:"+args["expression"],encode(result))
            return result
        if name == "find_files":
            import fnmatch
            pattern = args.get("pattern", "*")
            limit = min(50, max(1, int(args.get("limit", 20))))
            matches = []
            for root, dirs, files in os.walk(self.workspace):
                dirs[:] = [d for d in dirs if d.lower() not in ('.git', 'iba_memory', 'iba_runtime', 'library', 'temp', 'logs', 'obj', 'build')]
                for f in files:
                    if pattern.lower() in f.lower() or fnmatch.fnmatch(f.lower(), pattern.lower()):
                        rel = os.path.relpath(os.path.join(root, f), self.workspace).replace('\\', '/')
                        matches.append(rel)
                        if len(matches) >= limit:
                            break
                if len(matches) >= limit:
                    break
            out = {"pattern": pattern, "matches": matches, "count": len(matches)}
            if not matches:
                out["hint"] = ("No files matched this pattern. Try a shorter substring, "
                               "a different extension, or find_files with '*' to list candidates.")
            return out
        if name == "search_text":
            import fnmatch
            pattern = args.get("pattern")
            if not isinstance(pattern, str) or not pattern.strip():
                raise ValueError("Pattern must be a non-empty string")
            limit = min(50, max(1, int(args.get("limit", 20))))
            glob_pattern = (args.get("glob") or "").lower().strip()
            resolved_from = None
            if args.get("path"):
                base = self.resolve(args["path"])
                if not base.exists():
                    near = self._resolve_near_miss(args["path"], "file") or self._resolve_near_miss(args["path"], "dir")
                    if near is not None:
                        resolved_from = args["path"]
                        base = self.resolve(near)
                    else:
                        raise ValueError(f"Path not found in workspace: {args['path']}" + self._path_hint(args['path']))
            else:
                base = self.workspace
            skip_dirs = {'.git', 'iba_memory', 'iba_runtime', 'library', 'temp', 'logs', 'obj', 'build',
                         'node_modules', '.vs', 'bin', 'backup', 'backups'}
            text_exts = {'.cs', '.hlsl', '.shader', '.compute', '.cginc', '.shadergraph', '.json', '.md',
                         '.py', '.js', '.ts', '.tsx', '.jsx', '.txt', '.yaml', '.yml', '.xml', '.asmdef',
                         '.uss', '.uxml', '.prop', '.ini', '.toml'}
            files = []
            if base.is_file():
                files = [base]
            elif base.is_dir():
                for root, dirs, names in os.walk(base):
                    dirs[:] = [d for d in dirs if d.lower() not in skip_dirs]
                    for name_ in names:
                        lower = name_.lower()
                        if glob_pattern:
                            if not fnmatch.fnmatch(lower, glob_pattern):
                                continue
                        elif Path(name_).suffix.lower() not in text_exts:
                            continue
                        if name_.lower().endswith('.meta'):
                            continue
                        files.append(Path(root) / name_)
            matches = []
            scanned = 0
            try:
                matcher = re.compile(pattern, re.IGNORECASE)
            except re.error:
                matcher = None
            for file in files:
                try:
                    if file.stat().st_size > 2_000_000:
                        continue
                    text = file.read_text(encoding="utf-8", errors="replace")
                except Exception:
                    continue
                scanned += 1
                lines = text.splitlines()
                file_hits = []
                for line_no, line in enumerate(lines, 1):
                    hit = matcher.search(line) if matcher else pattern.lower() in line.lower()
                    if hit:
                        file_hits.append((line_no, line))
                    if len(file_hits) >= limit * 4:
                        break
                if not file_hits:
                    continue
                # Annotate matches inside preprocessor conditionals (e.g.
                # #if UNITY_EDITOR) — a symbol that exists in the file but is
                # compiled out for builds is the classic "works in editor,
                # fails during build" trap.
                condmap = None
                if file.suffix.lower() in ('.cs', '.hlsl', '.compute', '.shader', '.cginc'):
                    condmap = self._preprocessor_conditions(lines)
                rel = os.path.relpath(str(file), self.workspace).replace('\\', '/')
                for line_no, line in file_hits:
                    m = {"path": rel, "line": line_no, "text": line.strip()[:240]}
                    if condmap:
                        ctx = condmap.get(line_no)
                        if ctx:
                            m["context"] = "#if " + ctx
                    matches.append(m)
                    if len(matches) >= limit * 4:
                        break
                if len(matches) >= limit * 4:
                    break
            # Prefer live sources over archived copies when the match list is bounded.
            matches.sort(key=lambda m: ("backup" in m["path"].lower() or "claude outputs" in m["path"].lower(), m["path"], m["line"]))
            out = {"pattern": pattern, "matches": matches[:limit], "count": len(matches),
                   "resolved_from": resolved_from,
                   "files_scanned": scanned, "truncated": len(matches) > limit}
            if not matches:
                scope = f" in '{args['path']}'" if args.get("path") else ""
                out["hint"] = (f"Zero matches{scope}. Do NOT repeat this search — drop the 'path' "
                               "filter to scan the whole workspace, or search a parent directory.")
            return out
        if name == "list_directory":
            target = self.resolve(args.get("path","."))
            resolved_from = None
            if not target.is_dir():
                near = self._resolve_near_miss(args.get("path","."), "dir")
                if near is not None:
                    resolved_from = args.get("path",".")
                    target = self.resolve(near)
                else:
                    raise ValueError(f"Directory not found: {args.get('path', '.')}" + self._path_hint(args.get("path", "."), "dir"))
            limit = min(200,max(1,int(args.get("limit",100))))
            # Sort names only, never file bodies. Large directories are paged.
            names = sorted(p.name for p in target.iterdir() if p.name > args.get("after", ""))
            page = names[:limit]
            return {"names":page,"next_after":page[-1] if len(names)>limit else None,
                    "resolved_from": resolved_from}
        if name == "query_code_map":
            query = (args.get("query") or "").lower().strip()
            target_file = (args.get("path") or "").lower().strip()
            cmap = []
            if hasattr(self.store, 'get_code_map'):
                cmap = self.store.get_code_map(self.owner, self.session, self.workspace, limit=50)
            if target_file:
                cmap = [c for c in cmap if target_file in c["path"].lower()]
            if query:
                cmap = [c for c in cmap if query in c["path"].lower() or query in c["summary"].lower() or any(query in str(v).lower() for v in c.get("symbols", {}).values())]
            out = {
                "files": [
                    {
                        "path": c["path"],
                        "total_lines": c["total_lines"],
                        "summary": c["summary"],
                        "symbols": c["symbols"],
                        "stale_on_disk": c.get("stale_on_disk", False)
                    } for c in cmap
                ],
                "count": len(cmap)
            }
            if not cmap:
                out["hint"] = ("Code map has no entries matching that path/query — the file may not "
                               "be indexed yet (read_file indexes it) or the symbol name differs. "
                               "Try read_file on the file directly.")
            return out
        if name == "propose_improvement":
            category_raw = (args.get("category") or "memory-architecture").lower().strip()
            category_map = {
                "memory": "01-memory-architecture",
                "memory-architecture": "01-memory-architecture",
                "execution": "02-execution-efficiency",
                "execution-efficiency": "02-execution-efficiency",
                "codebase": "03-codebase-cognition",
                "codebase-cognition": "03-codebase-cognition",
                "workflow": "04-general-workflows",
                "general-workflows": "04-general-workflows",
            }
            subfolder = category_map.get(category_raw, "01-memory-architecture")
            base_dir = Path(r"C:\Users\Rackneh\Documents\Github\Dory LLM\Self-Improvement-Proposals") / subfolder
            base_dir.mkdir(parents=True, exist_ok=True)

            title = (args.get("title") or "Autonomous System Improvement").strip()
            slug = re.sub(r'[^a-z0-9]+', '-', title.lower()).strip('-')[:50] or "proposal"
            date_str = time.strftime("%Y-%m-%d")
            file_name = f"{date_str}_{slug}.md"
            target_file = base_dir / file_name

            observation = (args.get("observation") or "").strip()
            proposal = (args.get("proposal") or "").strip()
            impact = (args.get("expected_impact") or "Increased system performance and memory reliability").strip()

            doc_content = f"""# Self-Improvement Proposal: {title}

- **Date:** {time.strftime("%Y-%m-%d %H:%M:%S")}
- **Category:** {subfolder}
- **Author:** Dory IBA Autonomous Optimization Loop
- **Session:** {self.session}

## 1. Context & Observation
{observation}

## 2. Inefficiency or Limitation Identified
{observation}

## 3. Proposed Enhancement
{proposal}

## 4. Expected Impact
{impact}
"""
            target_file.write_text(doc_content, encoding="utf-8")

            # Persist into durable store so IBA remembers its own proposal
            try:
                self.store.ingest(self.owner, self.session, "higher", f"self_improvement:{file_name}", doc_content)
            except Exception:
                pass

            return {
                "status": "recorded",
                "path": str(target_file),
                "file": file_name,
                "category": subfolder,
                "title": title,
                "message": "Self-improvement proposal noted. Proceeding immediately with next task objective."
            }
        target = self.resolve(args["path"])
        resolved_from = None
        if name == "read_file":
            if not target.is_file():
                near = self._resolve_near_miss(args["path"], "file")
                if near is not None:
                    resolved_from = args["path"]
                    target = self.resolve(near)
                else:
                    raise ValueError(f"File not found: {args['path']}" + self._path_hint(args["path"]))
            with target.open("r", encoding="utf-8", errors="replace") as f:
                content = f.read()

            digest = hashlib.sha256(content.encode("utf-8")).hexdigest()
            total_chars = len(content)
            lines = content.splitlines(keepends=True)
            total_lines = len(lines)

            # Smart defaults: read piecemeal for large files
            is_large_file = total_lines > 300 or total_chars > 12000

            # Line-window reads: 'line' + 'limit' means a window of LINES.
            # Hard cap keeps the model from swallowing whole files in one call.
            MAX_WINDOW_LINES = 300
            DEFAULT_WINDOW_LINES = 80
            next_line = None

            if "line" in args and args["line"] is not None:
                target_line = max(1, min(total_lines, int(args["line"])))
                window = int(args.get("limit", DEFAULT_WINDOW_LINES))
                window = max(1, min(MAX_WINDOW_LINES, window))
                start_idx = target_line - 1
                end_idx = min(total_lines, start_idx + window)
                offset = sum(len(l) for l in lines[:start_idx])
                body = "".join(lines[start_idx:end_idx])
                next_offset = offset + len(body) if end_idx < total_lines else None
                next_line = end_idx + 1 if end_idx < total_lines else None
            else:
                offset = max(0, int(args.get("offset", 0)))
                # Char-based reads: tighter cap on large files (~150 lines max).
                if is_large_file:
                    default_limit = 4000
                    hard_cap = 6000
                else:
                    default_limit = 8192
                    hard_cap = 16384
                limit = min(hard_cap, max(1, int(args.get("limit", default_limit))))
                if offset >= total_chars and total_chars > 0:
                    raise ValueError(f"Offset {offset} is beyond end of file (file is {total_chars} chars, {total_lines} lines). Use offset: 0 or line: 1-{total_lines}.")
                body = content[offset:offset + limit]
                more = (offset + limit) < total_chars
                next_offset = (offset + limit) if more else None

            # Add guidance for large files
            guidance = ""
            if is_large_file and "line" in args and args.get("line") is not None:
                guidance = (f"\n\n[Large file: {total_lines} lines. Returned lines {start_idx+1}-{end_idx} of {total_lines}. "
                            f"Sample the end with line={total_lines} before drilling into sections.]")
            elif is_large_file and offset == 0 and not args.get("line"):
                guidance = (f"\n\n[Large file: {total_lines} lines, {total_chars} chars. "
                            f"Prefer 'line' windows (default 80 lines, max 300). "
                            f"First chunk shown; sample the tail with line={total_lines} or offset={max(0, total_chars - 4000)}.]")
            elif is_large_file and next_offset is not None:
                guidance = f"\n\n[More content available. Use offset={next_offset} or a 'line' window to continue.]"

            # Progressively build & update mental map in SQL database (code update-aware)
            mental_map_summary = None
            try:
                try:
                    rel_p = str(target.relative_to(self.workspace)).replace("\\", "/")
                except Exception:
                    rel_p = str(args["path"]).replace("\\", "/")
                if hasattr(self.store, 'update_code_map'):
                    mmap = self.store.update_code_map(self.owner, self.session, self.workspace, rel_p, content=content)
                    if mmap:
                        mental_map_summary = mmap.get("summary")
            except Exception:
                pass

            rel_resolved = None
            if resolved_from is not None:
                try:
                    rel_resolved = str(target.relative_to(self.workspace)).replace("\\", "/")
                except Exception:
                    rel_resolved = None
            return {
                "path": args["path"],
                "resolved_path": rel_resolved,
                "note": (f"Requested path did not exist; '{rel_resolved}' is the closest match and was read instead."
                         if resolved_from is not None else None),
                "text": body + guidance,
                "hash": digest,
                "offset": offset,
                "next_offset": next_offset,
                "next_line": next_line,
                "total_chars": total_chars,
                "total_lines": total_lines,
                "mental_map": mental_map_summary
            }
        if name == "ingest_file":
            namespace = args.get("database","code")
            if not target.is_file():
                near = self._resolve_near_miss(args["path"], "file")
                if near is not None:
                    resolved_from = args["path"]
                    target = self.resolve(near)
                else:
                    raise ValueError(f"File not found: {args['path']}" + self._path_hint(args["path"]))
            def checkpoint():
                if self.task and self.store.task(self.owner,self.session,self.task)['status']!='running':
                    raise ValueError('Import stopped; staged chunks are not published')
            with target.open(encoding="utf-8") as file:
                key = self.store.ingest_stream(self.owner,self.session,namespace,"file:"+str(target.relative_to(self.workspace)),file,checkpoint=checkpoint)
            manifest = self.store.document(self.owner,self.session,key,limit=1)
            return {"document":key,"artifact":manifest["artifact"],"chunks":manifest["total_chunks"],
                    "resolved_from": resolved_from,
                    "coverage":"indexed; use read_document to visit every chunk"}
        if name == "write_file":
            if not args.get('expected_hash'):
                raise ValueError('expected_hash is required: use the literal string absent for a new file, or the SHA256 from read_file for an existing file')
            text = args.get("text")
            if not isinstance(text,str):
                raise ValueError("write_file requires 'text' — the COMPLETE new file content as a string")
            existing = self._file_text_hash(target) if target.exists() else "absent"
            if args.get("expected_hash") != existing:
                raise ValueError("File changed; read it again before writing")
            # Guard against fragment writes: models sometimes send only the
            # region they edited as 'text', silently truncating the file. A
            # legitimate rewrite keeps most of the content, so reject drastic
            # shrinkage and make the model resend the complete file.
            old_size = target.stat().st_size if target.exists() else 0
            new_size = len(text.encode("utf-8"))
            if old_size > 4000 and new_size < old_size * 0.5:
                raise ValueError(
                    f"Refusing to shrink '{args['path']}' from {old_size} to {new_size} bytes — "
                    "'text' must be the COMPLETE file content, not just the edited region. "
                    "For small changes use edit_file instead: it takes 'old_text' (the exact snippet "
                    "to replace) and 'new_text' (the replacement) and does not require resending "
                    "the whole file. Only use write_file when you truly intend to replace everything.")
            # Preserve the file's dominant line ending: model text arrives with
            # '\n', and writing it raw would convert a CRLF file to LF wholesale.
            if target.exists():
                text = self._preserve_eol(text, target)
            target.parent.mkdir(parents=True,exist_ok=True)
            temp_path = None
            try:
                with tempfile.NamedTemporaryFile(mode="wb",delete=False,dir=str(target.parent)) as file:
                    temp_path = file.name
                    file.write(text.encode("utf-8"))
                    file.flush()
                    os.fsync(file.fileno())
                current = self._file_text_hash(target) if target.exists() else "absent"
                if current != existing:
                    raise ValueError("Concurrent file modification; write cancelled")
                os.replace(temp_path,target)
                temp_path = None
                # Update mental map immediately with new code
                try:
                    rel_p = str(target.relative_to(self.workspace)).replace("\\", "/")
                except Exception:
                    rel_p = str(args["path"]).replace("\\", "/")
                if hasattr(self.store, 'update_code_map'):
                    self.store.update_code_map(self.owner, self.session, self.workspace, rel_p, content=text)
            finally:
                if temp_path:
                    os.unlink(temp_path)
            wr = {"path":args["path"],"hash":self._file_text_hash(target),"bytes":target.stat().st_size}
            warn = self._quote_warning(text)
            if warn:
                wr["warning"] = warn
            return wr
        if name == "edit_file":
            resolved_from = None
            if not target.is_file():
                near = self._resolve_near_miss(args["path"], "file")
                if near is not None:
                    resolved_from = args["path"]
                    target = self.resolve(near)
                else:
                    raise ValueError(f"File not found: {args['path']}" + self._path_hint(args["path"]))
            old_text = args.get("old_text")
            new_text = args.get("new_text")
            if not isinstance(old_text, str) or not old_text:
                raise ValueError("edit_file requires 'old_text' — the exact existing snippet to replace")
            if not isinstance(new_text, str):
                raise ValueError("edit_file requires 'new_text' — the replacement snippet (empty string deletes old_text)")
            expected = args.get("expected_hash")
            if not expected:
                raise ValueError("edit_file requires 'expected_hash' — the SHA256 from read_file")
            existing = self._file_text_hash(target)
            if expected != existing:
                raise ValueError("File changed; read it again before editing")
            with target.open("r", encoding="utf-8", errors="replace") as f:
                content = f.read()
            count = content.count(old_text)
            spans = None
            if count == 0:
                # Whitespace-insensitive fallback: models routinely drop or
                # flatten line breaks/indentation when copying a snippet into
                # old_text (or the file wraps lines they joined). Compare with
                # ALL whitespace stripped, then map the match span back to the
                # original text so the replacement keeps real file formatting.
                old_ns = "".join(c for c in old_text if not c.isspace())
                if old_ns:
                    ns_chars = [(c, i) for i, c in enumerate(content) if not c.isspace()]
                    ns_text = "".join(c for c, _ in ns_chars)
                    spans = []
                    pos = 0
                    while True:
                        j = ns_text.find(old_ns, pos)
                        if j < 0:
                            break
                        spans.append((ns_chars[j][1], ns_chars[j + len(old_ns) - 1][1] + 1))
                        pos = j + 1
                    count = len(spans)
            if count == 0:
                raise ValueError(
                    "old_text not found — copy it verbatim from a fresh read_file, including exact "
                    "indentation and line breaks. The file may use different whitespace than expected.")
            replace_all = bool(args.get("replace_all"))
            if count > 1 and not replace_all:
                raise ValueError(
                    f"old_text matches {count} locations — include more surrounding context to make the "
                    "match unique, or set replace_all:true to change every occurrence")
            if spans is not None:
                new_content = content
                for s, e in sorted(spans, reverse=True):
                    new_content = new_content[:s] + new_text + new_content[e:]
            else:
                new_content = content.replace(old_text, new_text) if replace_all else content.replace(old_text, new_text, 1)
            out_text = self._preserve_eol(new_content, target)
            temp_path = None
            try:
                with tempfile.NamedTemporaryFile(mode="wb",delete=False,dir=str(target.parent)) as file:
                    temp_path = file.name
                    file.write(out_text.encode("utf-8"))
                    file.flush()
                    os.fsync(file.fileno())
                current = self._file_text_hash(target)
                if current != existing:
                    raise ValueError("Concurrent file modification; edit cancelled")
                os.replace(temp_path,target)
                temp_path = None
                try:
                    rel_p = str(target.relative_to(self.workspace)).replace("\\", "/")
                except Exception:
                    rel_p = str(args["path"]).replace("\\", "/")
                if hasattr(self.store, 'update_code_map'):
                    self.store.update_code_map(self.owner, self.session, self.workspace, rel_p, content=new_content)
            finally:
                if temp_path:
                    os.unlink(temp_path)
            out = {"path":args["path"],"hash":self._file_text_hash(target),"bytes":target.stat().st_size,
                   "replacements":count if replace_all else 1}
            warn = self._quote_warning(new_text)
            if warn:
                out["warning"] = warn
            if resolved_from is not None:
                try:
                    out["resolved_path"] = str(target.relative_to(self.workspace)).replace("\\", "/")
                except Exception:
                    pass
            return out
        raise ValueError("Unimplemented tool")
