"""Async event bridge between IBA runtime and FastAPI streaming chat."""
import asyncio
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import queue
import time
from typing import AsyncGenerator, Optional

from .manager import WorkspaceManager, generate_task_title, generate_step_title
from .store import uid


executor = ThreadPoolExecutor(max_workers=4)


async def run_iba_chat_stream(
    user_prompt: str,
    workspace_manager: WorkspaceManager,
    system_prompt: str = "",
    workspace_path: Optional[str] = None,
    session_id: Optional[str] = None,
    port: int = 8080,
    api_key: str = "",
    variable_newconv: bool = False,
) -> AsyncGenerator[str, None]:
    """Execute an IBA task or command, yielding Server-Sent Events (SSE) in real-time."""
    ws = Path(workspace_path) if workspace_path else workspace_manager.get_active_workspace_path()
    owner = workspace_manager.get_owner()
    session = session_id or workspace_manager.get_active_session()
    runtime = workspace_manager.get_runtime(ws, port=port, api_key=api_key)

    # 1. Check for immediate slash commands
    text = user_prompt.strip()
    if text.startswith("/"):
        parts = text.split(maxsplit=2)
        cmd = parts[0].lower()

        if cmd == "/context":
            if len(parts) > 1:
                try:
                    runtime.store.set_policy(owner, session, parts[1])
                except Exception as e:
                    yield f"event: error\ndata: {json.dumps({'error': str(e)})}\n\n"
                    return
            status = runtime.policy_status(owner, session, discover=True)
            msg = (
                f"**IBA Context Target**: `{status.get('requested_tokens') or 'auto (32K target)'}`\n"
                f"- **Effective Tokens**: `{status.get('effective_tokens') or '32768'}` (Clamped by runtime/model)\n"
                f"- **Model**: `{status.get('model') or 'Dory 1.0 4B'}`\n"
                f"- **Policy Revision**: `{status.get('revision', 1)}`\n\n"
                f"*Use `/context 16k | 32k | 64k | auto` to change prompt budget.*"
            )
            yield f"event: answer\ndata: {json.dumps({'content': msg})}\n\n"
            yield f"event: done\ndata: {json.dumps({'status': 'completed', 'tokens': 0, 'speed': 0})}\n\n"
            return

        if cmd == "/pin":
            if len(parts) < 2:
                yield f"event: answer\ndata: {json.dumps({'content': 'Usage: `/pin <constraint to retain across every step>`'})}\n\n"
                yield f"event: done\ndata: {json.dumps({'status': 'completed'})}\n\n"
                return
            import hashlib
            constraint = text[4:].strip()
            key = runtime.store.ingest(owner, session, "higher", "constraint:" + hashlib.sha256(constraint.encode()).hexdigest(), constraint, pin=True)
            msg = f"📌 **Pinned constraint stored to SQLite**: `{key}`\n> {constraint}"
            yield f"event: answer\ndata: {json.dumps({'content': msg})}\n\n"
            yield f"event: done\ndata: {json.dumps({'status': 'completed'})}\n\n"
            return

        if cmd == "/task":
            subcmd = parts[1].lower() if len(parts) > 1 else "status"
            if subcmd == "status":
                tasks = runtime.store.tasks(owner, session, limit=8)
                if not tasks:
                    msg = "No IBA tasks found in current session."
                else:
                    lines = [f"### IBA Tasks in Session `{session}`:\n"]
                    for t in tasks:
                        lines.append(f"- **`{t['id']}`** | Status: `{t['status']}` | Reason: {t.get('reason') or 'none'}")
                    msg = "\n".join(lines)
                yield f"event: answer\ndata: {json.dumps({'content': msg})}\n\n"
                yield f"event: done\ndata: {json.dumps({'status': 'completed'})}\n\n"
                return

            if subcmd == "resume":
                target_id = parts[2].strip() if len(parts) > 2 else None
                if not target_id:
                    with runtime.store.connect() as db:
                        row = db.execute("SELECT id FROM task WHERE owner=? AND session=? AND status IN ('paused','ready','running') ORDER BY updated DESC LIMIT 1", (owner, session)).fetchone()
                        if row:
                            target_id = row[0]
                if not target_id:
                    yield f"event: answer\ndata: {json.dumps({'content': 'No paused or ready task found to resume in this session.'})}\n\n"
                    yield f"event: done\ndata: {json.dumps({'status': 'completed'})}\n\n"
                    return
                # Unpause task in SQLite, including paused descendants so a
                # decomposed child that errored can run again. Explicit user
                # pauses ('User ...' reasons) are left alone.
                with runtime.store.connect(True) as db:
                    db.execute("UPDATE task SET status='ready', reason='', lease=NULL, expires=NULL WHERE id=?", (target_id,))
                    db.execute("""WITH RECURSIVE tree(id) AS (
                        SELECT id FROM task WHERE id=?
                        UNION ALL SELECT t.id FROM task t JOIN tree ON t.parent=tree.id)
                        UPDATE task SET status='ready', reason='', lease=NULL, expires=NULL
                        WHERE status='paused' AND reason NOT LIKE 'User%' AND id IN (SELECT id FROM tree)""",
                        (target_id,))
                
                # Execute resumed task directly
                event_q = queue.Queue()
                loop = asyncio.get_running_loop()

                def sync_callback(event_data):
                    event_q.put(event_data)

                def run_resume_worker():
                    try:
                        sync_callback({"event": "task_submitted", "task_id": target_id, "objective": f"Resuming task {target_id}"})
                        final_task = runtime.run(owner, session, target_id, system_prompt=system_prompt, callback=sync_callback, variable_newconv=variable_newconv)
                        final_resp = runtime.response(owner, session, final_task)
                        sync_callback({"event": "task_finished", "task": final_task, "response": final_resp})
                    except Exception as exc:
                        sync_callback({"event": "runtime_error", "error": str(exc)})
                    finally:
                        sync_callback({"event": "__END__"})

                loop.run_in_executor(executor, run_resume_worker)
                start_time = time.time()
                step_count = 0
                while True:
                    ev = await loop.run_in_executor(None, event_q.get)
                    if ev.get("event") == "__END__":
                        break
                    ename = ev.get("event")
                    if ename == "task_submitted":
                        yield f"event: iba_task_init\ndata: {json.dumps(ev)}\n\n"
                    elif ename == "step_start":
                        step_count += 1
                        yield f"event: iba_step_start\ndata: {json.dumps({'step_number': step_count, **ev})}\n\n"
                    elif ename == "thought":
                        yield f"event: thought\ndata: {json.dumps({'content': ev.get('token', ''), 'task': ev.get('task'), 'step': ev.get('step')})}\n\n"
                    elif ename == "tool_start":
                        yield f"event: iba_tool_start\ndata: {json.dumps(ev)}\n\n"
                    elif ename == "tool_result":
                        yield f"event: iba_tool_result\ndata: {json.dumps(ev)}\n\n"
                    elif ename == "task_finished":
                        resp_obj = ev.get("response", {})
                        content = resp_obj.get("result") or resp_obj.get("text") or f"Task `{ev.get('task', {}).get('id')}` ended with status: `{ev.get('task', {}).get('status')}`"
                        if resp_obj.get("reason"):
                            content += f"\n\n> ⚠️ Reason: {resp_obj.get('reason')}"
                        task_ref = ev.get("task")
                        task_id = task_ref.get("id") if isinstance(task_ref, dict) else task_ref
                        yield f"event: answer\ndata: {json.dumps({'content': content, 'task': task_id})}\n\n"
                    elif ename == "runtime_error":
                        yield f"event: error\ndata: {json.dumps({'error': ev.get('error')})}\n\n"
                
                dur = round(time.time() - start_time, 2)
                yield f"event: done\ndata: {json.dumps({'status': 'completed', 'duration': dur, 'steps': step_count})}\n\n"
                return

    # 2. Normal prompt execution as an autonomous SQL-backed IBA task
    event_q = queue.Queue()
    loop = asyncio.get_running_loop()

    def sync_callback(event_data):
        event_q.put(event_data)

    def run_worker():
        try:
            # Check for conversational continuation phrases ("continue", "proceed", etc.)
            clean_lower = text.strip().lower().rstrip('.!')
            words = clean_lower.split()
            continuation_phrases = {
                "continue", "resume", "proceed", "keep going", "go on", "next",
                "keep trying", "continue task", "continue with your task",
                "continue with the task", "continue working", "go ahead", "keep working"
            }
            is_continuation = (
                clean_lower in continuation_phrases
                or clean_lower.startswith(("continue", "resume", "proceed", "keep going", "go on", "carry on", "keep working", "try again"))
                or ("continue" in clean_lower and len(words) <= 7)
            )
            
            task_key = None
            task_obj_text = text

            # If a task in this session is waiting for user input, this message
            # IS the reply — deliver it to the task instead of starting a new one.
            with runtime.store.connect() as db:
                waiting_row = db.execute(
                    "SELECT id FROM task WHERE owner=? AND session=? AND status='waiting' ORDER BY updated DESC LIMIT 1",
                    (owner, session)
                ).fetchone()
            if waiting_row:
                try:
                    runtime.store.reply(owner, session, waiting_row[0], text)
                    task_key = waiting_row[0]
                    task_obj_text = f"User reply: {text[:200]}"
                except Exception:
                    pass

            if not task_key and is_continuation:
                with runtime.store.connect() as db:
                    row = db.execute(
                        "SELECT id, status, objective FROM task WHERE owner=? AND session=? AND parent IS NULL ORDER BY created DESC LIMIT 1",
                        (owner, session)
                    ).fetchone()
                    if row:
                        prev_id, prev_status, prev_obj_art = row[0], row[1], row[2]
                        try:
                            art = runtime.store.artifact(owner, session, prev_obj_art, 0, 4096)
                            task_obj_text = art.get("body", text)
                        except Exception:
                            task_obj_text = text

                        if prev_status in ("ready", "paused", "running", "waiting"):
                            # Unpause and resume the real previous task directly,
                            # including paused descendants blocked by decomposition.
                            with runtime.store.connect(True) as db_w:
                                db_w.execute("UPDATE task SET status='ready', reason='', lease=NULL, expires=NULL WHERE id=?", (prev_id,))
                                db_w.execute("""WITH RECURSIVE tree(id) AS (
                                    SELECT id FROM task WHERE id=?
                                    UNION ALL SELECT t.id FROM task t JOIN tree ON t.parent=tree.id)
                                    UPDATE task SET status='ready', reason='', lease=NULL, expires=NULL
                                    WHERE status='paused' AND reason NOT LIKE 'User%' AND id IN (SELECT id FROM tree)""",
                                    (prev_id,))
                            task_key = prev_id
                        else:
                            # Previous was completed: carry forward the real objective
                            task_obj_text = f"Continue working on: {task_obj_text}"

            if not task_key:
                task_key = runtime.submit(owner, session, task_obj_text, allow_decompose=True)

            task_title = generate_task_title(task_obj_text)
            sync_callback({"event": "task_submitted", "task_id": task_key, "task_title": task_title, "objective": task_obj_text})
            # Run steps
            final_task = runtime.run(owner, session, task_key, system_prompt=system_prompt, callback=sync_callback, variable_newconv=variable_newconv)
            final_resp = runtime.response(owner, session, final_task)
            sync_callback({"event": "task_finished", "task": final_task, "task_title": task_title, "response": final_resp})
        except Exception as exc:
            sync_callback({"event": "runtime_error", "error": str(exc)})
        finally:
            sync_callback({"event": "__END__"})

    # Launch in thread pool
    worker_future = loop.run_in_executor(executor, run_worker)

    step_count = 0
    start_time = time.time()
    total_thought_tokens = 0
    active_task_title = "Autonomous Task"
    last_ping = time.time()

    while True:
        # Check queue without blocking asyncio event loop
        try:
            item = event_q.get_nowait()
        except queue.Empty:
            if worker_future.done() and event_q.empty():
                break
            now = time.time()
            if now - last_ping >= 4.0:
                last_ping = now
                yield ": keepalive\n\n"
            await asyncio.sleep(0.04)
            continue

        if item.get("event") == "__END__":
            break

        ev = item.get("event")
        if ev == "task_submitted":
            active_task_title = item.get("task_title") or "Autonomous Task"
            yield f"event: iba_task_init\ndata: {json.dumps(item)}\n\n"

        elif ev == "step_start":
            step_count += 1
            item['task_title'] = item.get('task_title') or active_task_title
            item['step_title'] = item.get('step_title') or f"Step {step_count} Planning & Strategy"
            yield f"event: iba_step_start\ndata: {json.dumps({'step_number': step_count, **item})}\n\n"

        elif ev == "thought":
            total_thought_tokens += 1
            yield f"event: thought\ndata: {json.dumps({'content': item.get('token', ''), 'task': item.get('task'), 'step': item.get('step')})}\n\n"

        elif ev == "tool_start":
            item['task_title'] = item.get('task_title') or active_task_title
            item['step_title'] = generate_step_title(item.get('tool'), item.get('args'))
            yield f"event: iba_tool_start\ndata: {json.dumps(item)}\n\n"

        elif ev == "tool_result":
            item['task_title'] = item.get('task_title') or active_task_title
            item['step_title'] = generate_step_title(item.get('tool'), item.get('args'))
            item['receipt_name'] = f"{item.get('tool')}:{item.get('status', 'completed')}"
            yield f"event: iba_tool_result\ndata: {json.dumps(item)}\n\n"

        elif ev == "step_completed":
            item['task_title'] = item.get('task_title') or active_task_title
            item['step_title'] = item.get('step_title') or f"Step {step_count} Completed"
            yield f"event: iba_step_completed\ndata: {json.dumps(item)}\n\n"

        elif ev == "task_waiting":
            item['task_title'] = item.get('task_title') or active_task_title
            yield f"event: iba_wait\ndata: {json.dumps(item)}\n\n"

        elif ev == "task_finished":
            resp_text = item.get("response", "")
            task_ref = item.get("task")
            task_id = task_ref.get("id") if isinstance(task_ref, dict) else (task_ref or item.get("task_id"))
            yield f"event: answer\ndata: {json.dumps({'content': resp_text, 'task': task_id})}\n\n"
            elapsed = max(0.01, time.time() - start_time)
            yield f"event: done\ndata: {json.dumps({'status': 'completed', 'steps': step_count, 'duration': round(elapsed, 2), 'thought_tokens': total_thought_tokens})}\n\n"

        elif ev == "runtime_error":
            yield f"event: error\ndata: {json.dumps({'error': item.get('error')})}\n\n"
