"""SQLite is the authority; domain databases are replayable projections.

Each workspace owns one control store. Owner/session filters are mandatory at
the public boundary. Connections are short lived so callers can use worker
threads without sharing mutable sqlite connection state.
"""
from __future__ import annotations

from contextlib import contextmanager, nullcontext, closing
import hashlib
import io
import json
import logging
from pathlib import Path
import re
import sqlite3
import time
import uuid


def uid():
    return uuid.uuid4().hex


def encode(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


def target_tokens(value):
    text = str(value).lower().strip()
    if text == "auto":
        return None
    if text.endswith("k"):
        text = str(int(text[:-1]) * 1024)
    tokens = int(text)
    if not 4096 <= tokens <= 1048576:
        raise ValueError("Context target must be auto or 4096–1048576 tokens (16k, 32k, 64k).")
    return tokens


SCHEMA = """
CREATE TABLE IF NOT EXISTS policy(owner TEXT, session TEXT, target INTEGER,
 revision INTEGER NOT NULL DEFAULT 1, PRIMARY KEY(owner,session));
CREATE TABLE IF NOT EXISTS task(id TEXT PRIMARY KEY, owner TEXT NOT NULL,
 session TEXT NOT NULL, parent TEXT REFERENCES task(id), objective TEXT NOT NULL,
 requirements TEXT NOT NULL DEFAULT '[]', status TEXT NOT NULL DEFAULT 'ready',
 allow_decompose INTEGER NOT NULL DEFAULT 1,
 reason TEXT NOT NULL DEFAULT '', result TEXT, generation INTEGER NOT NULL DEFAULT 0,
 lease TEXT, expires REAL, created REAL NOT NULL, updated REAL NOT NULL);
CREATE INDEX IF NOT EXISTS task_scope ON task(owner,session,created);
CREATE INDEX IF NOT EXISTS task_parent ON task(parent,status);
CREATE INDEX IF NOT EXISTS task_recovery ON task(status,expires);
CREATE TABLE IF NOT EXISTS step(id TEXT PRIMARY KEY, task TEXT NOT NULL REFERENCES task(id),
 generation INTEGER NOT NULL, state TEXT NOT NULL, packet TEXT, response TEXT,
 reasoning TEXT, policy TEXT NOT NULL, input_tokens INTEGER, output_tokens INTEGER,
 created REAL NOT NULL, UNIQUE(task,generation));
CREATE TABLE IF NOT EXISTS action(step TEXT PRIMARY KEY REFERENCES step(id),
 name TEXT NOT NULL, args TEXT NOT NULL, state TEXT NOT NULL, result TEXT);
CREATE TABLE IF NOT EXISTS artifact(id TEXT PRIMARY KEY, owner TEXT NOT NULL,
 session TEXT NOT NULL, kind TEXT NOT NULL, size INTEGER NOT NULL, hash TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS artifact_chunk(artifact TEXT REFERENCES artifact(id),
 ordinal INTEGER, body TEXT NOT NULL, PRIMARY KEY(artifact,ordinal));
CREATE TABLE IF NOT EXISTS document(id TEXT PRIMARY KEY, owner TEXT NOT NULL,
 session TEXT NOT NULL, namespace TEXT NOT NULL, source TEXT NOT NULL,
 revision INTEGER NOT NULL, artifact TEXT REFERENCES artifact(id), current INTEGER NOT NULL,
 UNIQUE(owner,session,namespace,source,revision));
CREATE TABLE IF NOT EXISTS evidence_link(document TEXT REFERENCES document(id),
 evidence INTEGER REFERENCES evidence(id), relation TEXT NOT NULL,
 PRIMARY KEY(document,evidence,relation));
CREATE TABLE IF NOT EXISTS invalid_document(document TEXT PRIMARY KEY REFERENCES document(id),reason TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS ingestion(document TEXT PRIMARY KEY REFERENCES document(id),state TEXT NOT NULL,updated REAL NOT NULL);
CREATE INDEX IF NOT EXISTS document_scope ON document(owner,session,namespace,source,current);
CREATE TABLE IF NOT EXISTS evidence(id INTEGER PRIMARY KEY, document TEXT REFERENCES document(id),
 ordinal INTEGER NOT NULL, body TEXT NOT NULL, source_offset INTEGER NOT NULL DEFAULT 0, UNIQUE(document,ordinal));
CREATE VIRTUAL TABLE IF NOT EXISTS evidence_fts USING fts5(body,content=evidence,content_rowid=id);
CREATE TRIGGER IF NOT EXISTS evidence_insert AFTER INSERT ON evidence BEGIN
 INSERT INTO evidence_fts(rowid,body) VALUES(new.id,new.body); END;
CREATE TRIGGER IF NOT EXISTS evidence_delete AFTER DELETE ON evidence BEGIN
 INSERT INTO evidence_fts(evidence_fts,rowid,body) VALUES('delete',old.id,old.body); END;
CREATE TABLE IF NOT EXISTS pin(owner TEXT, session TEXT, evidence INTEGER REFERENCES evidence(id),
 PRIMARY KEY(owner,session,evidence));
CREATE TABLE IF NOT EXISTS dependency(task TEXT REFERENCES task(id), evidence INTEGER REFERENCES evidence(id),
 PRIMARY KEY(task,evidence));
CREATE TABLE IF NOT EXISTS task_input(task TEXT REFERENCES task(id),artifact TEXT REFERENCES artifact(id),created REAL NOT NULL);
CREATE TABLE IF NOT EXISTS request(owner TEXT,session TEXT,request_id TEXT,task TEXT REFERENCES task(id),PRIMARY KEY(owner,session,request_id));
CREATE TABLE IF NOT EXISTS task_document(task TEXT REFERENCES task(id),document TEXT REFERENCES document(id),
 total INTEGER NOT NULL,PRIMARY KEY(task,document));
CREATE TABLE IF NOT EXISTS task_check(task TEXT REFERENCES task(id),name TEXT NOT NULL,
 PRIMARY KEY(task,name));
CREATE TABLE IF NOT EXISTS coverage(task TEXT REFERENCES task(id),evidence INTEGER REFERENCES evidence(id),
 finding TEXT NOT NULL,PRIMARY KEY(task,evidence));
CREATE TABLE IF NOT EXISTS event(seq INTEGER PRIMARY KEY AUTOINCREMENT, owner TEXT NOT NULL,
 session TEXT NOT NULL, task TEXT, kind TEXT NOT NULL, payload TEXT NOT NULL, created REAL NOT NULL);
CREATE INDEX IF NOT EXISTS event_scope ON event(owner,session,seq);
CREATE INDEX IF NOT EXISTS event_task_kind ON event(task,kind,seq);
CREATE TABLE IF NOT EXISTS catalog(id TEXT PRIMARY KEY, name TEXT UNIQUE NOT NULL, kind TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS projection_status(namespace TEXT PRIMARY KEY REFERENCES catalog(id),
 watermark INTEGER NOT NULL DEFAULT 0, error TEXT, checked REAL NOT NULL);
CREATE TABLE IF NOT EXISTS code_map(owner TEXT NOT NULL, session TEXT NOT NULL,
 path TEXT NOT NULL, sha256 TEXT NOT NULL, mtime REAL NOT NULL,
 total_lines INTEGER NOT NULL, total_chars INTEGER NOT NULL,
 symbols TEXT NOT NULL, summary TEXT NOT NULL, updated REAL NOT NULL,
 PRIMARY KEY(owner, session, path));
CREATE INDEX IF NOT EXISTS code_map_scope ON code_map(owner, session, updated);
PRAGMA user_version=2;
"""


class Conflict(ValueError):
    pass


class Store:
    def __init__(self, root):
        self.root = Path(root).resolve()
        self.root.mkdir(parents=True, exist_ok=True)
        self.path = self.root / "control.sqlite"
        with self.connect() as db:
            version = db.execute("PRAGMA user_version").fetchone()[0]
            if version > 2:
                raise RuntimeError("IBA database was created by a newer runtime")
            db.execute("PRAGMA journal_mode=WAL")
            db.executescript(SCHEMA)
            if 'allow_decompose' not in {r['name'] for r in db.execute('PRAGMA table_info(task)')}:
                db.execute('ALTER TABLE task ADD COLUMN allow_decompose INTEGER NOT NULL DEFAULT 1')
            if 'source_offset' not in {r['name'] for r in db.execute('PRAGMA table_info(evidence)')}:
                db.execute('ALTER TABLE evidence ADD COLUMN source_offset INTEGER NOT NULL DEFAULT 0')
                db.execute('UPDATE evidence SET source_offset=ordinal*4096')
            if 'reasoning' not in {r['name'] for r in db.execute('PRAGMA table_info(step)')}:
                db.execute('ALTER TABLE step ADD COLUMN reasoning TEXT')
            for name in ("higher", "lower", "math", "code"):
                db.execute("INSERT OR IGNORE INTO catalog VALUES(?,?,?)", (name, name, name))

    @contextmanager
    def connect(self, write=False):
        db = sqlite3.connect(str(self.path), timeout=30, isolation_level=None)
        db.row_factory = sqlite3.Row
        db.execute("PRAGMA foreign_keys=ON")
        db.execute("PRAGMA synchronous=FULL")
        try:
            if write:
                db.execute("BEGIN IMMEDIATE")
            yield db
            if write:
                db.commit()
        except BaseException:
            if write:
                db.rollback()
            raise
        finally:
            db.close()

    @staticmethod
    def emit(db, owner, session, task, kind, payload):
        db.execute("INSERT INTO event(owner,session,task,kind,payload,created) VALUES(?,?,?,?,?,?)",
                   (owner, session, task, kind, encode(payload), time.time()))

    def policy(self, owner, session, parent_session=None):
        with self.connect(True) as db:
            parent = db.execute("SELECT target FROM policy WHERE owner=? AND session=?",
                                (owner, parent_session)).fetchone() if parent_session else None
            db.execute("INSERT OR IGNORE INTO policy(owner,session,target) VALUES(?,?,?)",
                       (owner, session, parent[0] if parent else 32768))
            return dict(db.execute("SELECT * FROM policy WHERE owner=? AND session=?", (owner, session)).fetchone())

    def set_policy(self, owner, session, value, revision=None):
        target = target_tokens(value)
        self.policy(owner, session)
        with self.connect(True) as db:
            row = db.execute("SELECT revision FROM policy WHERE owner=? AND session=?", (owner, session)).fetchone()
            if revision is not None and row[0] != revision:
                raise Conflict("Context changed on another device; refresh before updating.")
            db.execute("UPDATE policy SET target=?,revision=revision+1 WHERE owner=? AND session=?", (target, owner, session))
            self.emit(db, owner, session, None, "context_policy", {"target": target, "revision": row[0] + 1})
        return self.policy(owner, session)

    @staticmethod
    def put_artifact(db, owner, session, kind, text):
        text = text if isinstance(text, str) else encode(text)
        key = uid()
        db.execute("INSERT INTO artifact VALUES(?,?,?,?,?,?)",
                   (key, owner, session, kind, len(text), hashlib.sha256(text.encode()).hexdigest()))
        db.executemany("INSERT INTO artifact_chunk VALUES(?,?,?)",
                       ((key, i // 4096, text[i:i+4096]) for i in range(0, len(text), 4096)))
        return key

    def artifact(self, owner, session, key, offset=0, limit=8192):
        offset, limit = max(0, int(offset)), min(32768, max(1, int(limit)))
        with self.connect() as db:
            row = db.execute("SELECT * FROM artifact WHERE id=? AND owner=? AND session=?", (key, owner, session)).fetchone()
            if not row:
                raise ValueError("Artifact not found in this conversation")
            chunks = db.execute("SELECT body FROM artifact_chunk WHERE artifact=? AND ordinal BETWEEN ? AND ? ORDER BY ordinal",
                                (key, offset // 4096, (offset + limit - 1) // 4096)).fetchall()
            body = "".join(r[0] for r in chunks)[offset % 4096:offset % 4096 + limit]
            return {**dict(row), "body": body, "offset": offset, "end_offset": offset + len(body),
                    "next_offset": offset + len(body) if offset + len(body) < row["size"] else None}

    def create_task(self, owner, session, objective, requirements=(), parent=None, request_id=None, allow_decompose=True, required_checks=()):
        if type(allow_decompose) is not bool:
            raise ValueError('allow_decompose must be a boolean')
        key = uid()
        with self.connect(True) as db:
            if request_id:
                existing=db.execute('SELECT r.task,a.hash FROM request r JOIN task t ON t.id=r.task JOIN artifact a ON a.id=t.objective WHERE r.owner=? AND r.session=? AND r.request_id=?',(owner,session,request_id)).fetchone()
                if existing:
                    if existing['hash']!=hashlib.sha256(objective.encode()).hexdigest():
                        raise Conflict('An input request ID was reused with different content')
                    return existing[0]
            if parent and not db.execute("SELECT 1 FROM task WHERE id=? AND owner=? AND session=?", (parent, owner, session)).fetchone():
                raise ValueError("Parent task not found")
            objective_id = self.put_artifact(db, owner, session, "objective", objective)
            db.execute("INSERT INTO task(id,owner,session,parent,objective,requirements,created,updated,allow_decompose) VALUES(?,?,?,?,?,?,?,?,?)",
                       (key, owner, session, parent, objective_id, encode(list(requirements)), time.time(), time.time(),int(allow_decompose)))
            self.emit(db, owner, session, key, "task_created", {"parent": parent, "objective": objective_id})
            db.executemany('INSERT OR IGNORE INTO task_check VALUES(?,?)',((key,name) for name in required_checks))
            if request_id:
                db.execute('INSERT INTO request VALUES(?,?,?,?)',(owner,session,request_id,key))
        return key

    def require_check(self, owner, session, task, name):
        with self.connect(True) as db:
            row = db.execute('SELECT status FROM task WHERE id=? AND owner=? AND session=?', (task,owner,session)).fetchone()
            if not row:
                raise ValueError('Task not found')
            if row['status'] not in ('ready','paused','waiting'):
                raise Conflict('Pause the task before changing its completion checks')
            db.execute('INSERT OR IGNORE INTO task_check VALUES(?,?)',(task,name))
            self.emit(db,owner,session,task,'check_required',{'name':name})

    def completion_checks(self, owner, session, task):
        self.task(owner,session,task)
        with self.connect() as db:
            names = [r[0] for r in db.execute('SELECT name FROM task_check WHERE task=? ORDER BY name',(task,))]
            if not names:
                return []
            # Event sequence orders completed effects, including child actions.
            tree = '''WITH RECURSIVE tree(id) AS (
                SELECT ? UNION ALL SELECT t.id FROM task t JOIN tree ON t.parent=tree.id)
                SELECT e.seq,a.name,a.args,a.result FROM event e JOIN tree ON tree.id=e.task
                JOIN action a ON a.step=json_extract(e.payload,'$.step')
                WHERE e.kind='action_completed' '''
            write = db.execute(tree + "AND a.name='write_file' ORDER BY e.seq DESC LIMIT 1",(task,)).fetchone()
            latest_write = write['seq'] if write else 0
            rows = {name:db.execute(tree + "AND a.name='run_check' AND json_extract(a.args,'$.name')=? ORDER BY e.seq DESC LIMIT 1",(task,name)).fetchone() for name in names}
        checks = []
        for name in names:
            row = rows[name]
            status = 'pending'
            artifact = None
            if row:
                artifact = row['result']
                receipt = self.artifact(owner,session,artifact,limit=32768)
                if receipt['next_offset'] is None:
                    result = json.loads(receipt['body'])
                    status = 'passed' if result.get('status')=='passed' and result.get('exit_code')==0 and not result.get('timed_out') else 'failed'
                if row['seq'] < latest_write:
                    status = 'stale'
            checks.append({'name':name,'status':status,'receipt':artifact})
        return checks

    def task(self, owner, session, key):
        with self.connect() as db:
            row = db.execute("SELECT * FROM task WHERE id=? AND owner=? AND session=?", (key, owner, session)).fetchone()
            if not row:
                raise ValueError("Task not found in this conversation")
            return dict(row)

    def tasks(self, owner, session, before=None, limit=50):
        with self.connect() as db:
            return [dict(r) for r in db.execute("SELECT * FROM task WHERE owner=? AND session=? AND created<? ORDER BY created DESC LIMIT ?",
                    (owner, session, float(before or time.time()+1), min(100, max(1, int(limit)))))]

    def delete_session(self, owner, session):
        """Remove every trace of one chat session from this workspace store.

        FTS projections stay in sync through the evidence_delete trigger.
        The host must not call this for the 'default' session.
        """
        if session == "default":
            raise ValueError("The default session cannot be deleted")
        with self.connect(True) as db:
            task_ids = [r["id"] for r in db.execute(
                "SELECT id FROM task WHERE owner=? AND session=?", (owner, session))]
            docs = [r["id"] for r in db.execute(
                "SELECT id FROM document WHERE owner=? AND session=?", (owner, session))]
            arts = [r["id"] for r in db.execute(
                "SELECT id FROM artifact WHERE owner=? AND session=?", (owner, session))]
            for t in task_ids:
                db.execute("DELETE FROM action WHERE step IN (SELECT id FROM step WHERE task=?)", (t,))
                db.execute("DELETE FROM step WHERE task=?", (t,))
                db.execute("DELETE FROM task_input WHERE task=?", (t,))
                db.execute("DELETE FROM task_document WHERE task=?", (t,))
                db.execute("DELETE FROM task_check WHERE task=?", (t,))
                db.execute("DELETE FROM dependency WHERE task=?", (t,))
                db.execute("DELETE FROM coverage WHERE task=?", (t,))
                db.execute("DELETE FROM request WHERE task=?", (t,))
            db.execute("DELETE FROM task WHERE owner=? AND session=?", (owner, session))
            db.execute("DELETE FROM evidence_link WHERE document IN (SELECT id FROM document WHERE owner=? AND session=?)", (owner, session))
            db.execute("DELETE FROM evidence WHERE document IN (SELECT id FROM document WHERE owner=? AND session=?)", (owner, session))
            db.executemany("DELETE FROM invalid_document WHERE document=?", [(d,) for d in docs])
            db.executemany("DELETE FROM ingestion WHERE document=?", [(d,) for d in docs])
            db.execute("DELETE FROM document WHERE owner=? AND session=?", (owner, session))
            db.execute("DELETE FROM pin WHERE owner=? AND session=?", (owner, session))
            db.execute("DELETE FROM code_map WHERE owner=? AND session=?", (owner, session))
            db.execute("DELETE FROM event WHERE owner=? AND session=?", (owner, session))
            db.execute("DELETE FROM policy WHERE owner=? AND session=?", (owner, session))
            db.executemany("DELETE FROM artifact_chunk WHERE artifact=?", [(a,) for a in arts])
            db.execute("DELETE FROM artifact WHERE owner=? AND session=?", (owner, session))
            return {"tasks": len(task_ids), "documents": len(docs), "artifacts": len(arts)}

    def control(self, owner, session, key, command, reason=None):
        if command not in ("pause", "resume", "cancel"):
            raise ValueError("Use pause, resume or cancel")
        with self.connect(True) as db:
            row = db.execute("SELECT * FROM task WHERE id=? AND owner=? AND session=?", (key, owner, session)).fetchone()
            if not row:
                raise ValueError("Task not found")
            if row["status"] in ("completed", "cancelled"):
                raise Conflict("Task is already terminal")
            descendants = [r[0] for r in db.execute("WITH RECURSIVE tree(id) AS (SELECT ? UNION ALL SELECT t.id FROM task t JOIN tree ON t.parent=tree.id) SELECT id FROM tree",(key,))]
            if command == "resume":
                blocked=db.execute("WITH RECURSIVE a AS (SELECT * FROM task WHERE id=? UNION ALL SELECT t.* FROM task t JOIN a ON a.parent=t.id) SELECT 1 FROM a WHERE id!=? AND status NOT IN ('ready','running') LIMIT 1",(key,key)).fetchone()
                if blocked:
                    raise Conflict('Resume the parent task first')
                for task_id in descendants:
                    if db.execute("SELECT 1 FROM task WHERE id=? AND lease IS NOT NULL AND expires>?",(task_id,time.time())).fetchone():
                        raise Conflict("The current invocation is still stopping; wait before resuming")
                    if db.execute("SELECT 1 FROM action a JOIN step s ON s.id=a.step WHERE s.task=? AND a.state='issued'", (task_id,)).fetchone():
                        raise Conflict("An action has an uncertain outcome. Inspect and reconcile its receipt before retrying.")
            status = {"pause": "paused", "resume": "ready", "cancel": "cancelled"}[command]
            # Do not erase a live lease: in-flight work acknowledges the stop at its next boundary.
            for task_id in descendants:
                db.execute("""UPDATE task SET status=?,reason=CASE WHEN ?='resume' THEN
                    CASE WHEN reason IN ('User pause','Step allowance reached') THEN '' ELSE reason END
                    ELSE ? END,updated=? WHERE id=? AND status NOT IN ('completed','cancelled')""",
                    (status, command, reason or 'User '+command, time.time(), task_id))
            self.emit(db, owner, session, key, "task_" + status, {})

    def reply(self, owner, session, key, text):
        with self.connect(True) as db:
            row=db.execute("SELECT * FROM task WHERE id=? AND owner=? AND session=?",(key,owner,session)).fetchone()
            if not row or row['status']!='waiting':
                raise ValueError("Task is not waiting for input")
            # Keep the question attached to the reply so the next packet shows
            # the model what was asked, not just the bare answer.
            question = (row['reason'] or '').strip()
            body = text if not question else f'[Waiting question: {question[:400]}]\nUser answered: {text}'
            artifact=self.put_artifact(db,owner,session,'user_reply',body)
            db.execute("INSERT INTO task_input VALUES(?,?,?)",(key,artifact,time.time()))
            db.execute("UPDATE task SET status='ready',reason='' WHERE id=?",(key,))
            self.emit(db,owner,session,key,'user_reply',{'artifact':artifact})

    def recover(self, all_leases=False):
        """Only expired leases are recovered, so another live worker is never stolen.
        With all_leases=True every lease is treated as dead — only safe at process
        start, before any in-process worker can exist."""
        with self.connect(True) as db:
            if all_leases:
                rows = db.execute("SELECT * FROM task WHERE lease IS NOT NULL").fetchall()
            else:
                rows = db.execute("SELECT * FROM task WHERE lease IS NOT NULL AND expires<?",(time.time(),)).fetchall()
            for row in rows:
                reason = "Worker lease expired; resume to retry the unfinished inference"
                if db.execute("SELECT 1 FROM action a JOIN step s ON s.id=a.step WHERE s.task=? AND a.state='issued'",(row['id'],)).fetchone():
                    reason = "Action outcome is uncertain; reconcile the receipt before resuming"
                status = row['status'] if row['status'] in ('cancelled','stale') else 'paused'
                db.execute("UPDATE task SET status=?,reason=?,lease=NULL,expires=NULL WHERE id=?",(status,reason,row['id']))
                db.execute("UPDATE step SET state='interrupted' WHERE task=? AND state='running'",(row['id'],))
                self.emit(db,row['owner'],row['session'],row['id'],'task_recovered',{'reason':reason})
            # A 'running' step under a leaseless task can only be an orphaned
            # worker (live workers always hold a claim during inference).
            db.execute("""UPDATE step SET state='interrupted' WHERE state='running' AND task IN
                (SELECT id FROM task WHERE lease IS NULL)""")
            return len(rows)

    def reconcile(self, owner, session, step, note):
        """Operator-only receipt acknowledgement. It never re-executes an action."""
        if not isinstance(note,str) or not note.strip():
            raise ValueError("A reconciliation note describing the observed outcome is required")
        with self.connect(True) as db:
            row = db.execute("SELECT t.* FROM task t JOIN step s ON s.task=t.id JOIN action a ON a.step=s.id WHERE s.id=? AND t.owner=? AND t.session=? AND a.state='issued'",(step,owner,session)).fetchone()
            if not row:
                raise ValueError("Uncertain action not found")
            if row['lease'] and row['expires']>time.time():
                raise Conflict("Cannot reconcile an action while its worker lease is live")
            artifact = self.put_artifact(db,owner,session,'reconciled_action',{'operator_note':note,'verification':'operator_observed'})
            db.execute("UPDATE action SET state='completed',result=? WHERE step=?",(artifact,step))
            self.emit(db,owner,session,row['id'],'action_reconciled',{'step':step,'artifact':artifact})

    def claim(self, owner, session, key, ttl=1800):
        with self.connect(True) as db:
            now = time.time()
            row = db.execute("SELECT * FROM task WHERE id=? AND owner=? AND session=?", (key, owner, session)).fetchone()
            if not row or row["status"] not in ("ready", "running"):
                raise Conflict("Task is not runnable")
            if db.execute("WITH RECURSIVE a AS (SELECT * FROM task WHERE id=? UNION ALL SELECT t.* FROM task t JOIN a ON a.parent=t.id) SELECT 1 FROM a WHERE id!=? AND status NOT IN ('ready','running') LIMIT 1",(key,key)).fetchone():
                raise Conflict('Parent task is not runnable')
            if row["lease"] and row["expires"] > now:
                raise Conflict("Task already has a worker")
            if db.execute("SELECT 1 FROM action a JOIN step s ON s.id=a.step WHERE s.task=? AND a.state='issued'", (key,)).fetchone():
                raise Conflict("Uncertain action receipt; automatic replay is disabled")
            token = uid()
            db.execute("UPDATE task SET lease=?,expires=?,generation=generation+1,status='running',updated=? WHERE id=?", (token, now+ttl, now, key))
            return token

    @staticmethod
    def fence(db, key, token):
        row = db.execute("SELECT * FROM task WHERE id=?", (key,)).fetchone()
        if not row or row["lease"] != token or row["expires"] < time.time():
            raise Conflict("Expired or replaced worker lease")
        return row

    def start_step(self, key, token, packet, policy, input_tokens, evidence_ids=()):
        with self.connect(True) as db:
            row = self.fence(db, key, token)
            if row["status"] != "running":
                raise Conflict("Task stopped")
            for eid in evidence_ids:
                if not db.execute('SELECT 1 FROM evidence e JOIN document d ON d.id=e.document WHERE e.id=? AND d.owner=? AND d.session=? AND d.current=1 AND NOT EXISTS(SELECT 1 FROM invalid_document i WHERE i.document=d.id)',(eid,row['owner'],row['session'])).fetchone():
                    raise Conflict('Context evidence changed before inference; rebuild the packet')
                db.execute('INSERT OR IGNORE INTO dependency VALUES(?,?)',(key,eid))
            sid = uid()
            artifact = self.put_artifact(db, row["owner"], row["session"], "context_packet", packet)
            # Columns: id, task, generation, state, packet, response, policy,
            # input_tokens, output_tokens, created, reasoning.
            db.execute("INSERT INTO step(id,task,generation,state,packet,response,policy,input_tokens,output_tokens,created,reasoning) VALUES(?,?,?,?,?,?,?,?,?,?,?)", (sid, key, row["generation"], "running", artifact, None, encode(policy), input_tokens, None, time.time(), None))
            return sid

    def update_step_reasoning(self, owner, session, sid, text):
        """Shadow-persist in-flight reasoning so a reopened client can render
        the partial thought stream of a still-running step. Only touches
        steps that are still 'running' — finish_step writes the final copy."""
        if not text:
            return False
        with self.connect(True) as db:
            row = db.execute(
                """SELECT s.id FROM step s JOIN task t ON t.id=s.task
                   WHERE s.id=? AND t.owner=? AND t.session=? AND s.state='running'""",
                (sid, owner, session)).fetchone()
            if not row:
                return False
            raw = self.put_artifact(db, owner, session, "model_reasoning", text)
            db.execute("UPDATE step SET reasoning=? WHERE id=? AND state='running'", (raw, sid))
            return True

    def finish_step(self, key, token, sid, response, status="ready", reason="", result=None, output_tokens=None, transaction=None, reasoning=None):
        with (nullcontext(transaction) if transaction is not None else self.connect(True)) as db:
            row = self.fence(db, key, token)
            reason = self._bounded_reason(db,row,reason)
            raw = self.put_artifact(db, row["owner"], row["session"], "model_response", response)
            reasoning_raw = None
            if reasoning:
                reasoning_raw = self.put_artifact(db, row["owner"], row["session"], "model_reasoning", reasoning)
            db.execute("UPDATE step SET state=?,response=?,reasoning=?,output_tokens=? WHERE id=? AND task=?", (status, raw, reasoning_raw, output_tokens, sid, key))
            # A user stop always wins over a late model result.
            status = row["status"] if row["status"] in ("paused", "cancelled", "stale") else status
            db.execute("UPDATE task SET status=?,reason=?,result=COALESCE(?,result),lease=NULL,expires=NULL,updated=? WHERE id=?", (status, reason, result, time.time(), key))
            self.emit(db, row["owner"], row["session"], key, "step_"+status, {"step": sid, "reason": reason})

    def release(self, key, token, reason):
        with self.connect(True) as db:
            row = self.fence(db, key, token)
            reason = self._bounded_reason(db,row,reason)
            status = row["status"] if row["status"] in ("paused", "cancelled", "stale") else "paused"
            db.execute("UPDATE task SET status=?,reason=?,lease=NULL,expires=NULL WHERE id=?", (status, reason, key))
            self.emit(db, row["owner"], row["session"], key, "task_paused", {"reason": reason})

    def _bounded_reason(self, db, task, reason):
        if len(reason) <= 2048:
            return reason
        artifact = self.put_artifact(db,task['owner'],task['session'],'runtime_error',reason)
        return reason[:1024] + '\n[Full error in artifact ' + artifact + '; use read_artifact to page it.]'

    def ingest(self, owner, session, namespace, source, text, pin=False, references=()):
        return self.ingest_stream(owner,session,namespace,source,io.StringIO(text),pin,references)

    def ingest_stream(self, owner, session, namespace, source, reader, pin=False, references=(), checkpoint=None):
        """Stream one immutable revision; publish it atomically after reading EOF.

        Memory is bounded by chunk size even for a corpus larger than RAM.
        Readers see either the old complete revision or the new complete revision.
        """
        # For seekable sources, skip unchanged input before indexing it again.
        # This scan is outside the writer transaction and uses bounded memory.
        known_digest=None
        if hasattr(reader,'seekable') and reader.seekable():
            start=reader.tell(); digest=hashlib.sha256()
            for block in iter(lambda:reader.read(65536),''):
                digest.update(block.encode())
            reader.seek(start); known_digest=digest.hexdigest()
            with self.connect() as db:
                unchanged=db.execute("SELECT d.id FROM document d JOIN artifact a ON a.id=d.artifact WHERE d.owner=? AND d.session=? AND d.namespace=? AND d.source=? AND d.current=1 AND a.hash=? AND NOT EXISTS(SELECT 1 FROM invalid_document i WHERE i.document=d.id)",
                    (owner,session,namespace,source,known_digest)).fetchone()
                if unchanged:
                    return unchanged[0]
        with self.connect(True) as db:
            if not db.execute("SELECT 1 FROM catalog WHERE id=?", (namespace,)).fetchone():
                raise ValueError("Unknown database")
            old = db.execute("SELECT d.*,a.hash FROM document d JOIN artifact a ON a.id=d.artifact WHERE d.owner=? AND d.session=? AND d.namespace=? AND d.source=? AND d.current=1", (owner,session,namespace,source)).fetchone()
            for eid in references:
                if not db.execute("SELECT 1 FROM evidence e JOIN document d ON d.id=e.document LEFT JOIN invalid_document i ON i.document=d.id WHERE e.id=? AND d.owner=? AND d.session=? AND d.current=1 AND i.document IS NULL",(eid,owner,session)).fetchone():
                    raise ValueError("Finding references missing, stale or foreign evidence")
            revision = db.execute('SELECT COALESCE(max(revision),0)+1 FROM document WHERE owner=? AND session=? AND namespace=? AND source=?',(owner,session,namespace,source)).fetchone()[0]
            key,artifact = uid(),uid()
            db.execute("INSERT INTO artifact VALUES(?,?,?,'source',0,'')", (artifact,owner,session))
            db.execute("INSERT INTO document VALUES(?,?,?,?,?,?,?,0)", (key,owner,session,namespace,source,revision,artifact))
            db.execute("INSERT INTO ingestion VALUES(?,'staging',?)",(key,time.time()))
            hasher,size,ordinal,overlap = hashlib.sha256(),0,0,''
            while True:
                body = reader.read(4096)
                if not body:
                    break
                hasher.update(body.encode())
                size += len(body)
                db.execute("INSERT INTO artifact_chunk VALUES(?,?,?)", (artifact,ordinal,body))
                eid = db.execute("INSERT INTO evidence(document,ordinal,body,source_offset) VALUES(?,?,?,?)", (key,ordinal,overlap+body,size-len(body)-len(overlap))).lastrowid
                ordinal += 1
                overlap=body[-256:]
                if ordinal%128==0:
                    # Staged chunks are invisible to retrieval. Yield the writer
                    # so context changes and stop commands work during huge imports.
                    db.execute('UPDATE ingestion SET updated=? WHERE document=?',(time.time(),key))
                    db.commit()
                    if checkpoint:
                        checkpoint()
                    db.execute('BEGIN IMMEDIATE')
            digest = hasher.hexdigest()
            if known_digest is not None and known_digest!=digest:
                raise ValueError('Source changed while importing; previous published revision remains current')
            if old and old["hash"] == digest:
                db.execute('DELETE FROM evidence WHERE document=?',(key,))
                db.execute('DELETE FROM ingestion WHERE document=?',(key,))
                db.execute('DELETE FROM document WHERE id=?',(key,))
                db.execute('DELETE FROM artifact_chunk WHERE artifact=?',(artifact,))
                db.execute('DELETE FROM artifact WHERE id=?',(artifact,))
                return old["id"]
            # A concurrent importer may have published while we yielded. Never
            # supersede a revision we did not observe at the start.
            latest=db.execute('SELECT id FROM document WHERE owner=? AND session=? AND namespace=? AND source=? AND current=1',(owner,session,namespace,source)).fetchone()
            if (latest[0] if latest else None)!=(old['id'] if old else None):
                raise Conflict('Source changed concurrently; retry this import')
            for eid in references:
                if not db.execute('SELECT 1 FROM evidence e JOIN document d ON d.id=e.document WHERE e.id=? AND d.current=1 AND NOT EXISTS(SELECT 1 FROM invalid_document i WHERE i.document=d.id)',(eid,)).fetchone():
                    raise Conflict('Referenced evidence changed during import')
            db.execute("UPDATE artifact SET size=?,hash=? WHERE id=?", (size,digest,artifact))
            if old:
                db.execute("UPDATE document SET current=0 WHERE id=?", (old["id"],))
                # Derived findings and their consumers are transitively stale.
                db.execute("""CREATE TEMP TABLE affected_doc AS WITH RECURSIVE stale(id) AS (
                    SELECT ? UNION SELECT l.document FROM evidence_link l JOIN evidence e ON e.id=l.evidence JOIN stale ON stale.id=e.document)
                    SELECT id FROM stale""",(old["id"],))
                db.execute("INSERT OR IGNORE INTO invalid_document SELECT id,'Source revision changed' FROM affected_doc")
                db.execute("""WITH RECURSIVE affected_task(id) AS (
                    SELECT task FROM dependency WHERE evidence IN (SELECT id FROM evidence WHERE document IN (SELECT id FROM affected_doc))
                    UNION SELECT task FROM task_document WHERE document IN (SELECT id FROM affected_doc)
                    UNION SELECT t.parent FROM task t JOIN affected_task a ON a.id=t.id WHERE t.parent IS NOT NULL)
                    UPDATE task SET status='stale',reason='Source revision changed' WHERE id IN (SELECT id FROM affected_task) AND status!='cancelled'""")
                db.execute("DELETE FROM pin WHERE evidence IN (SELECT id FROM evidence WHERE document=?)", (old["id"],))
            db.execute("UPDATE document SET current=1 WHERE id=?",(key,))
            db.execute("UPDATE ingestion SET state='published',updated=? WHERE document=?",(time.time(),key))
            if pin:
                db.execute('INSERT INTO pin SELECT ?,?,id FROM evidence WHERE document=?',(owner,session,key))
            for eid in references:
                db.execute("INSERT INTO evidence_link VALUES(?,?,'derived_from')",(key,eid))
            self.emit(db, owner,session,None,"source_published", {"document":key,"namespace":namespace,"revision":revision})
        self.try_project(namespace)
        return key

    def search(self, owner, session, query, namespace=None, limit=16):
        terms = re.findall(r"\w+", str(query), re.UNICODE)[:24]
        if not terms:
            return []
        match = " OR ".join('"' + t + '"' for t in terms)
        with self.connect() as db:
            return [dict(r) for r in db.execute("""SELECT e.id,e.document,e.ordinal,e.body,e.source_offset,d.source,d.revision,d.namespace
                FROM evidence_fts f JOIN evidence e ON e.id=f.rowid JOIN document d ON d.id=e.document
                WHERE evidence_fts MATCH ? AND d.owner=? AND d.session=? AND d.current=1
                AND NOT EXISTS(SELECT 1 FROM invalid_document i WHERE i.document=d.id)
                AND (? IS NULL OR d.namespace=?) ORDER BY bm25(evidence_fts),e.id LIMIT ?""",
                (match,owner,session,namespace,namespace,min(64,max(1,int(limit)))))]

    def evidence(self, owner, session, key):
        with self.connect() as db:
            row = db.execute("SELECT e.*,d.source,d.revision,d.namespace,(d.current=1 AND NOT EXISTS(SELECT 1 FROM invalid_document i WHERE i.document=d.id)) AS current FROM evidence e JOIN document d ON d.id=e.document WHERE e.id=? AND d.owner=? AND d.session=?", (int(key),owner,session)).fetchone()
            if not row:
                raise ValueError("Evidence not found")
            return dict(row)

    def document(self, owner, session, key, after=-1, limit=16):
        with self.connect() as db:
            doc = db.execute("SELECT * FROM document WHERE id=? AND owner=? AND session=?", (key,owner,session)).fetchone()
            if not doc:
                raise ValueError("Document not found")
            rows = [dict(r) for r in db.execute("SELECT id,ordinal,body,source_offset FROM evidence WHERE document=? AND ordinal>? ORDER BY ordinal LIMIT ?", (key,int(after),min(32,max(1,int(limit))))) ]
            total = db.execute("SELECT count(*) FROM evidence WHERE document=?",(key,)).fetchone()[0]
            return {**dict(doc),"chunks":rows,"total_chunks":total,"next_after":rows[-1]["ordinal"] if rows and rows[-1]["ordinal"]+1<total else None}

    def graph(self, owner, session, after=0, limit=100):
        with self.connect() as db:
            rows = [dict(r) for r in db.execute("SELECT e.id,e.document,e.ordinal,substr(e.body,1,240) AS label,d.namespace,d.source,d.revision FROM evidence e JOIN document d ON d.id=e.document WHERE d.owner=? AND d.session=? AND d.current=1 AND e.id>? AND NOT EXISTS(SELECT 1 FROM invalid_document i WHERE i.document=d.id) ORDER BY e.id LIMIT ?",(owner,session,int(after),min(200,max(1,int(limit)))))]
            links=[]
            for row in rows:
                links.extend(dict(r) for r in db.execute("SELECT document,evidence,relation FROM evidence_link WHERE document=? LIMIT 128",(row["document"],)))
            return {"nodes":rows,"links":links,"next_after":rows[-1]["id"] if rows else None}

    def coverage_status(self, owner, session, task):
        self.task(owner,session,task)
        with self.connect() as db:
            return [dict(r) for r in db.execute("""SELECT td.document,td.total,d.current,
                (SELECT count(*) FROM coverage c JOIN evidence e ON e.id=c.evidence WHERE c.task=td.task AND e.document=td.document) AS covered,
                (SELECT min(e.ordinal) FROM evidence e WHERE e.document=td.document AND NOT EXISTS(SELECT 1 FROM coverage c WHERE c.task=td.task AND c.evidence=e.id)) AS next_uncovered
                FROM task_document td JOIN document d ON d.id=td.document WHERE td.task=?""",(task,))]

    def pins(self, owner, session):
        with self.connect() as db:
            return [dict(r) for r in db.execute("SELECT e.id,e.body FROM pin p JOIN evidence e ON e.id=p.evidence WHERE p.owner=? AND p.session=? ORDER BY e.id LIMIT 129", (owner,session))]

    def events(self, owner, session, after=0, limit=100):
        with self.connect() as db:
            return [dict(r) for r in db.execute("SELECT * FROM event WHERE owner=? AND session=? AND seq>? ORDER BY seq LIMIT ?", (owner,session,int(after),min(200,max(1,int(limit)))))]

    def databases(self):
        with self.connect() as db:
            return [dict(r) for r in db.execute("""SELECT c.*,coalesce(p.watermark,0) AS watermark,
                p.error AS projection_error,coalesce(p.checked,0) AS checked,
                (SELECT coalesce(max(seq),0) FROM event) AS ledger_sequence
                FROM catalog c LEFT JOIN projection_status p ON p.namespace=c.id ORDER BY c.name""")]

    def steps(self, owner, session, task, after=0, limit=100):
        self.task(owner,session,task)
        with self.connect() as db:
            rows = [dict(r) for r in db.execute("""SELECT s.*,a.name AS action_name,
                a.args AS action_arguments,a.state AS action_state,a.result AS action_result
                FROM step s LEFT JOIN action a ON a.step=s.id WHERE s.task=? AND s.generation>?
                ORDER BY s.generation LIMIT ?""",(task,int(after),min(100,max(1,int(limit)))))]
            return rows

    def try_project(self, namespace):
        """Projection failure must not turn an accepted source write into a failure."""
        try:
            self.project(namespace)
            return True
        except (OSError, sqlite3.Error) as exc:
            logging.getLogger(__name__).warning('IBA projection %s deferred: %s',namespace,exc)
            with self.connect(True) as db:
                db.execute("""INSERT INTO projection_status(namespace,error,checked) VALUES(?,?,?)
                    ON CONFLICT(namespace) DO UPDATE SET error=excluded.error,checked=excluded.checked""",
                    (namespace,str(exc),time.time()))
            return False

    def maintain_projections(self, max_batches=8):
        """Bounded replay, oldest checked pending domain first; retry on next run."""
        attempted = set()
        for _ in range(max(1,min(1000,int(max_batches)))):
            pending = [d for d in self.databases() if d['id'] not in attempted
                       and (d['projection_error'] or d['watermark'] < d['ledger_sequence'])]
            if not pending:
                break
            namespace = min(pending,key=lambda d:d['checked'])['id']
            if not self.try_project(namespace):
                attempted.add(namespace)
        return self.databases()

    def create_database(self, name, kind="knowledge"):
        if not isinstance(name,str) or not 1 <= len(name.strip()) <= 128:
            raise ValueError("Database name must contain 1–128 characters")
        with self.connect(True) as db:
            db.execute("INSERT OR IGNORE INTO catalog VALUES(?,?,?)", (uid(),name.strip(),kind))
            return dict(db.execute("SELECT * FROM catalog WHERE name=?", (name.strip(),)).fetchone())

    def project(self, namespace):
        """Incremental idempotent projection. Watermark commits with projected rows.

        Readers use control.sqlite for current/stale status. Domain files expose
        their watermark and never claim an atomic multi-file transaction.
        """
        if namespace not in {d["id"] for d in self.databases()}:
            raise ValueError("Unknown database")
        path = self.root / ("domain-" + namespace + ".sqlite")
        with closing(sqlite3.connect(str(path), timeout=30)) as dest, dest, self.connect() as src:
            dest.execute("PRAGMA journal_mode=WAL")
            dest.execute("CREATE TABLE IF NOT EXISTS projection(document TEXT PRIMARY KEY,owner TEXT,session TEXT,source TEXT,revision INTEGER,artifact TEXT)")
            dest.execute("CREATE TABLE IF NOT EXISTS chunk(document TEXT,ordinal INTEGER,evidence INTEGER,body TEXT,PRIMARY KEY(document,ordinal))")
            dest.execute("CREATE TABLE IF NOT EXISTS relation(document TEXT,evidence INTEGER,kind TEXT,PRIMARY KEY(document,evidence,kind))")
            dest.execute("CREATE TABLE IF NOT EXISTS watermark(id INTEGER PRIMARY KEY CHECK(id=1),seq INTEGER)")
            dest.execute("BEGIN IMMEDIATE")
            after = dest.execute("SELECT seq FROM watermark WHERE id=1").fetchone()
            rows = src.execute("SELECT * FROM event WHERE seq>? ORDER BY seq LIMIT 500", (after[0] if after else 0,)).fetchall()
            for row in rows:
                payload = json.loads(row["payload"])
                if row["kind"] == "source_published" and payload["namespace"] == namespace:
                    d = src.execute("SELECT * FROM document WHERE id=?", (payload["document"],)).fetchone()
                    dest.execute("INSERT OR IGNORE INTO projection VALUES(?,?,?,?,?,?)", (d["id"],d["owner"],d["session"],d["source"],d["revision"],d["artifact"]))
                    dest.executemany("INSERT OR IGNORE INTO chunk VALUES(?,?,?,?)", ((d['id'],r['ordinal'],r['id'],r['body']) for r in src.execute("SELECT * FROM evidence WHERE document=?",(d['id'],))))
                    dest.executemany("INSERT OR IGNORE INTO relation VALUES(?,?,?)", ((r['document'],r['evidence'],r['relation']) for r in src.execute("SELECT * FROM evidence_link WHERE document=?",(d['id'],))))
            if rows:
                dest.execute("INSERT OR REPLACE INTO watermark VALUES(1,?)", (rows[-1]["seq"],))
            watermark = rows[-1]['seq'] if rows else (after[0] if after else 0)
        with self.connect(True) as db:
            db.execute("""INSERT INTO projection_status VALUES(?,?,NULL,?)
                ON CONFLICT(namespace) DO UPDATE SET watermark=max(watermark,excluded.watermark),
                error=NULL,checked=excluded.checked""",(namespace,watermark,time.time()))
        return str(path)

    def update_code_map(self, owner, session, workspace, path, content=None):
        """Update or refresh the mental map for a script file in the workspace.
        Code update-aware: detects external edits via sha256 and mtime.
        Invalidates stale mental map revisions in durable memory.
        """
        import os
        from .manager import extract_script_map
        ws_path = Path(workspace) if workspace else self.root.parent
        target_path = (ws_path / path).resolve()

        if not target_path.exists() or not target_path.is_file():
            with self.connect(True) as db:
                db.execute("DELETE FROM code_map WHERE owner=? AND session=? AND path=?", (owner, session, path))
            return None

        mtime = target_path.stat().st_mtime
        if content is None:
            try:
                with open(target_path, "r", encoding="utf-8", errors="replace") as f:
                    content = f.read()
            except Exception:
                return None

        sha256 = hashlib.sha256(content.encode("utf-8", errors="replace")).hexdigest()

        # Check existing row in code_map
        with self.connect() as db:
            row = db.execute(
                "SELECT sha256, mtime, summary, symbols FROM code_map WHERE owner=? AND session=? AND path=?",
                (owner, session, path)
            ).fetchone()
            if row and row["sha256"] == sha256:
                syms = row["symbols"]
                if isinstance(syms, str):
                    try:
                        syms = json.loads(syms)
                    except Exception:
                        pass
                return {
                    "path": path,
                    "sha256": sha256,
                    "mtime": mtime,
                    "summary": row["summary"],
                    "symbols": syms,
                    "status": "unchanged"
                }

        # New or modified content: extract structural symbols & summary
        symbols, summary = extract_script_map(path, content)
        lines = content.splitlines()
        total_lines = len(lines)
        total_chars = len(content)

        with self.connect(True) as db:
            db.execute("""
                INSERT INTO code_map(owner, session, path, sha256, mtime, total_lines, total_chars, symbols, summary, updated)
                VALUES(?,?,?,?,?,?,?,?,?,?)
                ON CONFLICT(owner, session, path) DO UPDATE SET
                    sha256=excluded.sha256,
                    mtime=excluded.mtime,
                    total_lines=excluded.total_lines,
                    total_chars=excluded.total_chars,
                    symbols=excluded.symbols,
                    summary=excluded.summary,
                    updated=excluded.updated
            """, (owner, session, path, sha256, mtime, total_lines, total_chars, json.dumps(symbols), summary, time.time()))

        # Ingest into durable store under code namespace with source="map:" + path
        # Store.ingest automatically marks old revision current=0 and cascades invalidation
        try:
            map_text = f"FILE: {path}\nLINES: {total_lines}\nSUMMARY: {summary}\nSYMBOLS:\n" + json.dumps(symbols, indent=2)
            self.ingest(owner, session, "code", "map:" + path, map_text)
        except Exception:
            pass

        return {
            "path": path,
            "sha256": sha256,
            "mtime": mtime,
            "total_lines": total_lines,
            "total_chars": total_chars,
            "summary": summary,
            "symbols": symbols,
            "status": "updated"
        }

    def get_code_map(self, owner, session, workspace=None, limit=50):
        """Return the current active mental map of code files for this session."""
        ws_path = Path(workspace) if workspace else self.root.parent
        results = []
        with self.connect() as db:
            rows = db.execute(
                "SELECT path, sha256, mtime, total_lines, total_chars, symbols, summary, updated FROM code_map WHERE owner=? AND session=? ORDER BY updated DESC LIMIT ?",
                (owner, session, limit)
            ).fetchall()
            for r in rows:
                p = r["path"]
                full_path = (ws_path / p).resolve()
                is_stale = False
                if not full_path.exists():
                    is_stale = True
                else:
                    try:
                        cur_mtime = full_path.stat().st_mtime
                        if abs(cur_mtime - r["mtime"]) > 0.001:
                            is_stale = True
                    except Exception:
                        is_stale = True
                syms = r["symbols"]
                if isinstance(syms, str):
                    try:
                        syms = json.loads(syms)
                    except Exception:
                        pass
                results.append({
                    "path": p,
                    "sha256": r["sha256"],
                    "total_lines": r["total_lines"],
                    "total_chars": r["total_chars"],
                    "summary": r["summary"],
                    "symbols": syms,
                    "updated": r["updated"],
                    "stale_on_disk": is_stale
                })
        return results

