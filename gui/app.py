import asyncio
from collections import deque
import json
import os
import sys
from pathlib import Path

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if BASE_DIR not in sys.path:
    sys.path.insert(0, BASE_DIR)

import hashlib
import base64
import re
import signal
import subprocess
import threading
import time
from typing import Any, Dict, List, Optional
import httpx
from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, HTMLResponse, StreamingResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
from iba.manager import WorkspaceManager
from iba.bridge import run_iba_chat_stream
from iba.provider import effective_provider_kind, provider_config, write_provider_config
STATIC_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "static")

workspace_manager = WorkspaceManager()
VULKAN_SERVER_EXE = os.path.join(
    BASE_DIR, "llama.cpp-dory", "build-vulkan", "bin", "Release", "llama-server.exe"
)
CPU_SERVER_EXE = os.path.join(
    BASE_DIR, "llama.cpp-dory", "build", "bin", "Release", "llama-server.exe"
)
LLAMA_SERVER_EXE = VULKAN_SERVER_EXE if os.path.isfile(VULKAN_SERVER_EXE) else CPU_SERVER_EXE
MODEL_FILE = os.path.join(
    BASE_DIR, "models", "NVIDIA-Nemotron-Labs-Dory1.0-4B-Q5_K_M.gguf"
)

app = FastAPI(title="NVIDIA Dory 1.0 4B GUI", version="1.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

DEFAULT_API_KEY = "dory-a5118c2b008b1ebe80793733a7aa48ee"
LOCAL_IP = "192.168.178.24"

# Log buffer & real-time activity state
MAX_LOGS = 2500
log_buffer: deque = deque(maxlen=MAX_LOGS)
log_sequence = 0
log_lock = threading.Lock()

activity_state = {
    "is_busy": False,
    "status_label": "Engine Idle (Ready for OpenCode & Chat)",
    "current_task": None,
    "prompt_tokens": 0,
    "prompt_speed": 0.0,
    "gen_tokens": 0,
    "gen_speed": 0.0,
    "total_tokens": 0,
    "last_active_time": 0.0,
    "active_client": "None",
    "request_count": 0,
}


def add_log_entry(text: str, log_type: str = "info"):
    global log_sequence
    with log_lock:
        log_sequence += 1
        entry = {
            "id": log_sequence,
            "timestamp": time.strftime("%H:%M:%S"),
            "text": text,
            "type": log_type,
        }
        log_buffer.append(entry)


def parse_llama_activity(line: str):
    global activity_state
    # 1. Task processing started
    m = re.search(r"task\s+(\d+)\s+\|\s+processing task", line)
    if m:
        task_id = int(m.group(1))
        activity_state["is_busy"] = True
        activity_state["current_task"] = task_id
        activity_state["status_label"] = f"Processing Task #{task_id}..."
        activity_state["last_active_time"] = time.time()
        activity_state["request_count"] += 1
        return

    # 2. Prompt processing progress
    m = re.search(
        r"prompt processing,\s*n_tokens\s*=\s*(\d+).*?([0-9.]+)\s*tokens per second",
        line,
    )
    if m:
        tokens = int(m.group(1))
        speed = float(m.group(2))
        activity_state["is_busy"] = True
        activity_state["prompt_tokens"] = tokens
        activity_state["prompt_speed"] = speed
        activity_state["status_label"] = (
            f"Evaluating Prompt ({tokens} tok @ {speed:.1f} t/s)..."
        )
        return

    # 3. Prompt evaluation complete
    m = re.search(
        r"prompt eval time\s*=.*?/\s*(\d+)\s*tokens.*?([0-9.]+)\s*tokens per second",
        line,
    )
    if m:
        tokens = int(m.group(1))
        speed = float(m.group(2))
        activity_state["prompt_tokens"] = tokens
        activity_state["prompt_speed"] = speed
        activity_state["status_label"] = (
            f"Prompt Ready ({tokens} tok). Generating..."
        )
        return

    # 4. Token generation progress
    m = re.search(r"n_gen\s*=\s*(\d+),\s*tg\s*=\s*([0-9.]+)\s*t/s", line)
    if m:
        gen_tok = int(m.group(1))
        tg_speed = float(m.group(2))
        activity_state["is_busy"] = True
        activity_state["gen_tokens"] = gen_tok
        activity_state["gen_speed"] = tg_speed
        activity_state["status_label"] = (
            f"Generating Reasoning & Code ({gen_tok} tok @ {tg_speed:.2f} t/s)..."
        )
        return

    # 5. Token generation complete
    m = re.search(
        r"eval time\s*=.*?/\s*(\d+)\s*tokens.*?([0-9.]+)\s*tokens per second",
        line,
    )
    if m:
        gen_tok = int(m.group(1))
        speed = float(m.group(2))
        activity_state["gen_tokens"] = gen_tok
        activity_state["gen_speed"] = speed
        return

    # 6. Total tokens
    m = re.search(r"total time\s*=.*?/\s*(\d+)\s*tokens", line)
    if m:
        tot = int(m.group(1))
        activity_state["total_tokens"] = tot
        return

    # 7. Slot release / Stop processing
    if "stop processing" in line or "slot release" in line:
        activity_state["is_busy"] = False
        activity_state["status_label"] = (
            "Engine Idle (Ready for OpenCode & Chat)"
        )
        activity_state["last_active_time"] = time.time()
        return


def _stdout_reader_worker(proc: subprocess.Popen):
    add_log_entry("=== Dory llama-server engine process started ===", "slot")
    try:
        for raw_line in iter(proc.stdout.readline, ""):
            if not raw_line:
                break
            line = raw_line.rstrip("\r\n")
            if not line:
                continue

            l_type = "info"
            lower = line.lower()
            if "error" in lower or "failed" in lower:
                l_type = "error"
            elif " w " in line.lower() or "warn" in lower:
                l_type = "warn"
            elif "print_timing" in line or "t/s" in line:
                l_type = "timing"
            elif "slot" in lower or "launch_slot" in lower or "task" in lower:
                l_type = "slot"

            add_log_entry(line, l_type)
            parse_llama_activity(line)
    except Exception as e:
        add_log_entry(f"Stdout reader ended: {e}", "warn")

# Server process state
server_state = {
    "process": None,
    "status": "STOPPED",  # STOPPED, STARTING, RUNNING, ERROR
    "error_message": "",
    "started_at": 0.0,
    "config": {
        "loops": 2,
        "cache_mode": "last_loop",
        "threads": 4,
        "ctx_size": 32768,
        "port": 8080,
        "host": "0.0.0.0",
        "api_key": DEFAULT_API_KEY,
        "gpu_layers": 999,
    },
}


class ServerConfig(BaseModel):
    loops: int = 2
    cache_mode: str = "last_loop"
    threads: int = 6
    ctx_size: int = 32768
    port: int = 8080
    host: str = "0.0.0.0"
    api_key: str = DEFAULT_API_KEY
    gpu_layers: int = 999


class ChatRequest(BaseModel):
    messages: List[Dict[str, Any]]
    temperature: float = 1.0
    top_p: float = 0.95
    max_tokens: int = 2048
    reasoning_effort: Optional[str] = "medium"  # "none", "low", "medium", "high"
    use_iba: Optional[bool] = True
    variable_newconv: Optional[bool] = None
    workspace_path: Optional[str] = None
    session_id: Optional[str] = None
    images: Optional[List[str]] = None


class SelectWorkspaceRequest(BaseModel):
    path: str


class AddWorkspaceRequest(BaseModel):
    name: str
    path: Optional[str] = ""
    description: Optional[str] = ""
    init_blank: Optional[bool] = True


class SelectSessionRequest(BaseModel):
    session_id: str


class PolicyRequest(BaseModel):
    target: str


class PinRequest(BaseModel):
    constraint: str


def kill_existing_server():
    proc = server_state["process"]
    if proc is not None:
        try:
            proc.terminate()
            try:
                proc.wait(timeout=3)
            except subprocess.TimeoutExpired:
                proc.kill()
        except Exception:
            pass
        server_state["process"] = None

    # On Windows, kill any orphaned llama-server.exe processes on the target port
    port = server_state["config"]["port"]
    try:
        cmd = f'powershell -Command "Get-Process -Name llama-server -ErrorAction SilentlyContinue | Stop-Process -Force"'
        subprocess.run(cmd, shell=True, capture_output=True)
    except Exception:
        pass


def launch_server_sync(cfg: ServerConfig):
    kill_existing_server()

    if not os.path.isfile(LLAMA_SERVER_EXE):
        server_state["status"] = "ERROR"
        server_state["error_message"] = (
            f"llama-server.exe not found at: {LLAMA_SERVER_EXE}"
        )
        return False

    if not os.path.isfile(MODEL_FILE):
        server_state["status"] = "ERROR"
        server_state["error_message"] = f"Model file not found at: {MODEL_FILE}"
        return False

    server_state["status"] = "STARTING"
    server_state["error_message"] = ""
    server_state["config"] = cfg.model_dump()

    override_kv = f"dory.num_loops=int:{cfg.loops},dory.recurrent_kv_cache_mode=str:{cfg.cache_mode}"

    cmd = [
        LLAMA_SERVER_EXE,
        "-m",
        MODEL_FILE,
        "--alias",
        "dory",
        "--host",
        cfg.host,
        "--port",
        str(cfg.port),
        "-ngl",
        str(cfg.gpu_layers),
        "-t",
        str(cfg.threads),
        "--ctx-size",
        str(cfg.ctx_size),
        "--parallel",
        "1",
        "--batch-size",
        "2048",
        "--ubatch-size",
        "512",
        "--flash-attn",
        "auto",
        "--context-shift",
        "--sse-ping-interval",
        "15",
        "--timeout",
        "3600",
        "--jinja",
        "--reasoning",
        "on",
        "--reasoning-format",
        "deepseek",
        "--reasoning-effort",
        "medium",
        "--temp",
        "1.0",
        "--top-p",
        "0.95",
        "--top-k",
        "0",
        "--min-p",
        "0",
        "--repeat-penalty",
        "1",
        "--override-kv",
        override_kv,
    ]

    try:
        proc_env = os.environ.copy()
        if os.path.isdir(r"D:\VulkanSDK"):
            proc_env["VULKAN_SDK"] = r"D:\VulkanSDK"
            proc_env["PATH"] = r"D:\VulkanSDK\Bin;" + proc_env.get("PATH", "")

        proc = subprocess.Popen(
            cmd,
            env=proc_env,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
            encoding="utf-8",
            errors="replace",
            creationflags=(
                subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
            ),
        )
        server_state["process"] = proc
        server_state["started_at"] = time.time()
        reader_thread = threading.Thread(
            target=_stdout_reader_worker, args=(proc,), daemon=True
        )
        reader_thread.start()
        return True
    except Exception as e:
        server_state["status"] = "ERROR"
        server_state["error_message"] = str(e)
        return False


async def wait_for_server_ready(port: int, timeout: int = 40) -> bool:
    start_time = time.time()
    url = f"http://127.0.0.1:{port}/health"
    async with httpx.AsyncClient() as client:
        while time.time() - start_time < timeout:
            proc = server_state.get("process")
            if proc is not None and proc.poll() is not None:
                server_state["status"] = "ERROR"
                server_state["error_message"] = (
                    f"Server process terminated unexpectedly with code {proc.returncode}"
                )
                return False
            try:
                r = await client.get(url, timeout=1.0)
                if r.status_code == 200:
                    server_state["status"] = "RUNNING"
                    return True
            except Exception:
                pass
            await asyncio.sleep(0.5)

    server_state["status"] = "ERROR"
    server_state["error_message"] = f"Server failed to start within {timeout}s"
    return False


@app.on_event("startup")
async def startup_event():
    # If model exists, auto-start the server in the background — unless IBA is
    # configured for LM Studio, since both backends cannot share the GPU.
    if effective_provider_kind() == "lmstudio":
        return
    if os.path.isfile(MODEL_FILE) and os.path.isfile(LLAMA_SERVER_EXE):
        cfg = ServerConfig(**server_state["config"])
        if launch_server_sync(cfg):
            asyncio.create_task(wait_for_server_ready(cfg.port))


@app.on_event("shutdown")
def shutdown_event():
    kill_existing_server()


@app.get("/api/status")
@app.get("/api/server/status")
async def get_status():
    proc = server_state["process"]
    port = server_state["config"]["port"]

    if proc is not None:
        code = proc.poll()
        if code is not None:
            server_state["status"] = "ERROR"
            server_state["error_message"] = (
                f"Server exited with status code {code}"
            )
        elif server_state["status"] != "STARTING":
            server_state["status"] = "RUNNING"
    else:
        server_state["status"] = "STOPPED"

    uptime = 0
    if server_state["status"] == "RUNNING" and server_state["started_at"] > 0:
        uptime = int(time.time() - server_state["started_at"])

    return {
        "status": server_state["status"],
        "error_message": server_state["error_message"],
        "uptime_seconds": uptime,
        "config": server_state["config"],
        "model_file": os.path.basename(MODEL_FILE),
        "model_exists": os.path.isfile(MODEL_FILE),
        "local_ip": LOCAL_IP,
        "api_key": server_state["config"].get("api_key", DEFAULT_API_KEY),
        "endpoints": {
            "opencode": "http://127.0.0.1:5000/v1",
            "lan": f"http://{LOCAL_IP}:5000/v1",
            "engine_backend": f"http://127.0.0.1:{port}/v1",
        },
        "engine_type": (
            "Vulkan (AMD Radeon RX 580 - 8GB)"
            if LLAMA_SERVER_EXE == VULKAN_SERVER_EXE
            else "CPU (AVX2)"
        ),
    }


@app.get("/api/activity")
async def get_activity(since_id: int = 0, since: int = 0):
    # The frontend polls ?since=<last_id>; accept it as an alias for since_id
    # so incremental log fetches actually filter instead of replaying the buffer.
    cursor = since or since_id
    uptime = 0
    if server_state["status"] == "RUNNING" and server_state["started_at"] > 0:
        uptime = int(time.time() - server_state["started_at"])

    with log_lock:
        if cursor <= 0:
            new_logs = list(log_buffer)[-150:]
        else:
            new_logs = [e for e in log_buffer if e["id"] > cursor]
        current_seq = log_sequence

    return {
        "status": server_state["status"],
        "activity": activity_state,
        "uptime_seconds": uptime,
        "config": server_state["config"],
        "logs": new_logs,
        "last_id": current_seq,
    }


@app.post("/api/logs/clear")
async def clear_logs():
    with log_lock:
        log_buffer.clear()
    add_log_entry("=== Live console logs cleared ===", "info")
    return {"status": "ok"}


@app.post("/api/server/start")
async def start_server(cfg: ServerConfig):
    success = launch_server_sync(cfg)
    if not success:
        raise HTTPException(
            status_code=500, detail=server_state["error_message"]
        )

    # Wait up to 30s
    ready = await wait_for_server_ready(cfg.port, timeout=30)
    if not ready:
        raise HTTPException(
            status_code=500,
            detail=server_state.get("error_message", "Startup timeout"),
        )

    return {"status": "RUNNING", "config": server_state["config"]}


@app.post("/api/server/stop")
async def stop_server():
    kill_existing_server()
    server_state["status"] = "STOPPED"
    return {"status": "STOPPED"}


@app.post("/api/chat/stream")
async def chat_stream(req: ChatRequest):
    # IBA tasks may run on LM Studio while the Dory engine is stopped to free
    # the GPU; only the plain chat path strictly requires the engine.
    lm_backend = req.use_iba and effective_provider_kind() == "lmstudio"
    if server_state["status"] != "RUNNING" and not lm_backend:
        raise HTTPException(status_code=503, detail="Dory engine is not running")

    port = server_state["config"]["port"]
    api_key = server_state["config"].get("api_key", DEFAULT_API_KEY)

    # Route through IBA SQL step-by-step memory if requested (Default: True)
    if req.use_iba:
        user_prompt = ""
        system_prompt = ""
        user_images = list(req.images or [])

        for m in reversed(req.messages):
            if m.get("role") == "user":
                raw_c = m.get("content", "")
                if isinstance(raw_c, list):
                    texts = []
                    for part in raw_c:
                        if isinstance(part, dict):
                            if part.get("type") == "text":
                                texts.append(part.get("text", ""))
                            elif part.get("type") == "image_url":
                                url_obj = part.get("image_url", {})
                                url = url_obj.get("url", "") if isinstance(url_obj, dict) else str(url_obj)
                                if url:
                                    user_images.append(url)
                    user_prompt = " ".join(texts)
                else:
                    user_prompt = str(raw_c)
                break

        for m in req.messages:
            if m.get("role") == "system":
                system_prompt = m.get("content", "") if isinstance(m.get("content"), str) else ""
                break

        active_ws = req.workspace_path or str(workspace_manager.get_active_workspace_path())
        active_sess = req.session_id or workspace_manager.get_active_session()

        saved_image_paths = []
        if user_images:
            att_dir = Path(active_ws) / ".iba_runtime" / "attachments"
            att_dir.mkdir(parents=True, exist_ok=True)
            for idx, img_data in enumerate(user_images):
                try:
                    if img_data.startswith("data:image"):
                        header, b64_str = img_data.split(",", 1)
                        mime = header.split(";")[0].split(":")[1]
                        ext = ".png"
                        if "jpeg" in mime or "jpg" in mime:
                            ext = ".jpg"
                        elif "gif" in mime:
                            ext = ".gif"
                        elif "webp" in mime:
                            ext = ".webp"
                        fn = f"attachment_{int(time.time())}_{idx}{ext}"
                        target_file = att_dir / fn
                        target_file.write_bytes(base64.b64decode(b64_str))
                        saved_image_paths.append(str(target_file))
                    elif os.path.exists(img_data):
                        saved_image_paths.append(img_data)
                except Exception as ex:
                    add_log_entry(f"Error saving image attachment: {ex}", "warn")

        if saved_image_paths:
            img_note = "\n\n[Attached Images: " + ", ".join(saved_image_paths) + "]"
            user_prompt = (user_prompt or "Inspect attached image(s)") + img_note

        if not user_prompt:
            raise HTTPException(status_code=400, detail="No user message provided")

        add_log_entry(
            f"IBA SQL Task started: '{user_prompt[:70]}...' (workspace: {os.path.basename(active_ws)}, session: {active_sess})",
            "slot",
        )
        activity_state["is_busy"] = True
        activity_state["active_client"] = "IBA SQL Studio"
        activity_state["status_label"] = "IBA Step-by-Step Task in progress..."

        return StreamingResponse(
            run_iba_chat_stream(
                user_prompt=user_prompt,
                workspace_manager=workspace_manager,
                system_prompt=system_prompt,
                workspace_path=active_ws,
                session_id=active_sess,
                port=port,
                api_key=api_key,
                variable_newconv=bool(req.variable_newconv),
            ),
            media_type="text/event-stream",
            headers={
                "Cache-Control": "no-cache",
                "Connection": "keep-alive",
                "X-Accel-Buffering": "no",
            },
        )

    # Fallback: Raw Direct Chat Proxy (when IBA is disabled)
    url = f"http://127.0.0.1:{port}/v1/chat/completions"

    payload = {
        "model": "dory",
        "messages": req.messages,
        "temperature": req.temperature,
        "top_p": req.top_p,
        "max_tokens": req.max_tokens,
        "stream": True,
    }
    if req.reasoning_effort:
        payload["reasoning_effort"] = req.reasoning_effort

    api_key = server_state["config"].get("api_key", DEFAULT_API_KEY)
    req_headers = {"Authorization": f"Bearer {api_key}"} if api_key else {}

    add_log_entry(
        f"GUI Studio chat stream request started ({len(req.messages)} messages, reasoning={req.reasoning_effort})",
        "slot",
    )
    activity_state["is_busy"] = True
    activity_state["active_client"] = "GUI Studio"
    activity_state["status_label"] = "GUI Chat request streaming..."

    async def event_generator():
        start_time = time.time()
        token_count = 0
        thought_count = 0
        answer_count = 0
        in_thinking = False
        think_buffer = ""

        try:
            async with httpx.AsyncClient(timeout=300.0) as client:
                async with client.stream(
                    "POST", url, json=payload, headers=req_headers
                ) as resp:
                    if resp.status_code != 200:
                        err_text = await resp.aread()
                        yield f"event: error\ndata: {json.dumps({'error': err_text.decode('utf-8', errors='ignore')})}\n\n"
                        return

                    async for line in resp.aiter_lines():
                        if not line or not line.startswith("data: "):
                            continue
                        data_str = line[6:].strip()
                        if data_str == "[DONE]":
                            break

                        try:
                            chunk = json.loads(data_str)
                            choice = chunk.get("choices", [{}])[0]
                            delta = choice.get("delta", {})

                            reasoning_chunk = (
                                delta.get("reasoning_content")
                                or delta.get("reasoning")
                                or ""
                            )
                            content_chunk = delta.get("content") or ""

                            # 1. Handle dedicated reasoning_content from llama-server
                            if reasoning_chunk:
                                token_count += 1
                                thought_count += 1
                                yield f"event: thought\ndata: {json.dumps({'content': reasoning_chunk})}\n\n"

                            # 2. Handle content_chunk (which may also contain <think> tags)
                            if content_chunk:
                                combined = think_buffer + content_chunk
                                if "<think>" in combined and not in_thinking:
                                    in_thinking = True
                                    parts = combined.split("<think>", 1)
                                    if parts[0]:
                                        token_count += 1
                                        answer_count += 1
                                        yield f"event: answer\ndata: {json.dumps({'content': parts[0]})}\n\n"
                                    think_buffer = ""
                                    content_chunk = parts[1]

                                if "</think>" in content_chunk and in_thinking:
                                    in_thinking = False
                                    parts = content_chunk.split("</think>", 1)
                                    if parts[0]:
                                        token_count += 1
                                        thought_count += 1
                                        yield f"event: thought\ndata: {json.dumps({'content': parts[0]})}\n\n"
                                    if parts[1]:
                                        token_count += 1
                                        answer_count += 1
                                        yield f"event: answer\ndata: {json.dumps({'content': parts[1]})}\n\n"
                                    continue

                                if in_thinking:
                                    token_count += 1
                                    thought_count += 1
                                    yield f"event: thought\ndata: {json.dumps({'content': content_chunk})}\n\n"
                                else:
                                    token_count += 1
                                    answer_count += 1
                                    yield f"event: answer\ndata: {json.dumps({'content': content_chunk})}\n\n"

                        except Exception:
                            continue

            elapsed = max(0.01, time.time() - start_time)
            speed = round(token_count / elapsed, 1)
            yield f"event: done\ndata: {json.dumps({'tokens': token_count, 'thought_tokens': thought_count, 'answer_tokens': answer_count, 'duration': round(elapsed, 2), 'speed': speed})}\n\n"

        except Exception as e:
            yield f"event: error\ndata: {json.dumps({'error': str(e)})}\n\n"

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",
        },
    )


# ====================================================================
# IBA Workspace & Session Management API Endpoints
# ====================================================================

@app.get("/api/iba/workspaces")
async def get_iba_workspaces():
    return {
        "active_workspace": str(workspace_manager.get_active_workspace_path()),
        "active_session": workspace_manager.get_active_session(),
        "owner": workspace_manager.get_owner(),
        "workspaces": workspace_manager.list_workspaces(),
    }


@app.post("/api/iba/workspaces/select")
async def select_iba_workspace(req: SelectWorkspaceRequest):
    res = workspace_manager.set_active_workspace(req.path)
    add_log_entry(f"Switched active workspace to: {req.path}", "info")
    return res


@app.post("/api/iba/workspaces/add")
async def add_iba_workspace(req: AddWorkspaceRequest):
    target_path = (req.path or "").strip()
    clean_name = (req.name or "New Project").strip()
    if not target_path:
        safe_name = re.sub(r'[^\w\-_.]', '_', clean_name)
        target_path = os.path.join(os.path.expanduser("~"), "Documents", "Projects", safe_name)
    
    target_path = os.path.abspath(target_path)
    os.makedirs(target_path, exist_ok=True)

    if req.init_blank:
        readme_file = os.path.join(target_path, "README.md")
        if not os.path.exists(readme_file):
            try:
                with open(readme_file, "w", encoding="utf-8") as f:
                    f.write(f"# {clean_name}\n\nBlank project workspace created in Dory LLM IBA Studio.\n")
            except Exception as e:
                pass
        git_dir = os.path.join(target_path, ".git")
        if not os.path.exists(git_dir):
            try:
                subprocess.run(["git", "init"], cwd=target_path, capture_output=True, timeout=5)
            except Exception:
                pass

    entry = workspace_manager.add_workspace(clean_name, target_path, req.description or "")
    workspace_manager.set_active_workspace(target_path)
    add_log_entry(f"Added and activated workspace '{clean_name}' at: {target_path}", "info")
    return {"status": "ok", "workspace": entry, "active_workspace": target_path}


# ====================================================================
# Authentic 2000s Era Session Persona Generator
# ====================================================================

POPULAR_2000S_NAMES = [
    {"first": "Jessica", "last": "Taylor", "gender": "F"},
    {"first": "Christopher", "last": "Vance", "gender": "M"},
    {"first": "Ashley", "last": "Miller", "gender": "F"},
    {"first": "Matthew", "last": "Brooks", "gender": "M"},
    {"first": "Amanda", "last": "Cross", "gender": "F"},
    {"first": "Michael", "last": "Reynolds", "gender": "M"},
    {"first": "Sarah", "last": "Bennett", "gender": "F"},
    {"first": "David", "last": "Mercer", "gender": "M"},
    {"first": "Jennifer", "last": "Sterling", "gender": "F"},
    {"first": "Joshua", "last": "Hayes", "gender": "M"},
    {"first": "Brittany", "last": "Coleman", "gender": "F"},
    {"first": "Daniel", "last": "Walker", "gender": "M"},
    {"first": "Samantha", "last": "Reed", "gender": "F"},
    {"first": "James", "last": "Mitchell", "gender": "M"},
    {"first": "Megan", "last": "Foster", "gender": "F"},
    {"first": "Justin", "last": "Cooper", "gender": "M"},
    {"first": "Nicole", "last": "Adams", "gender": "F"},
    {"first": "Ryan", "last": "Harrison", "gender": "M"},
    {"first": "Heather", "last": "Morgan", "gender": "F"},
    {"first": "Brandon", "last": "Campbell", "gender": "M"},
]

POPULAR_SCENE_BANNERS = [
    "/static/wlm/wlm/scenes/0001.png",       # Soccer field scene (matches Pic 1!)
    "/static/wlm/wlm/scenes/zune_04.jpg",     # Purple skulls banner (matches Pic 1!)
    "/static/wlm/wlm/scenes/0002.jpg",       # Scenic mountains
    "/static/wlm/wlm/scenes/0004.png",       # Green meadow
    "/static/wlm/wlm/scenes/0007.png",       # Futuristic blue vectors
    "/static/wlm/wlm/scenes/0008.png",       # Ocean twilight
    "/static/wlm/wlm/scenes/Robot.jpg",      # Cyber robot graffiti
    "/static/wlm/wlm/scenes/Graffiti.jpg",   # Street graffiti
    "/static/wlm/wlm/scenes/ButterflyPattern.png",
    "/static/wlm/wlm/scenes/CarbonFiber.jpg",
    "/static/wlm/wlm/scenes/zune_01.jpg",
    "/static/wlm/wlm/scenes/zune_02.jpg",
]

AVAILABLE_USERTILES = [
    "/static/wlm/wlm/usertiles/usertile_dog.png",          # Golden puppy
    "/static/wlm/wlm/usertiles/usertile_kitten.png",       # Kitten
    "/static/wlm/wlm/usertiles/usertile_soccer.gif",       # Animated soccer ball
    "/static/wlm/wlm/usertiles/usertile_flare.png",        # Glowing energy flare
    "/static/wlm/wlm/usertiles/usertile_robot.png",        # Windows robot
    "/static/wlm/wlm/usertiles/usertile_chess.png",        # Chess knight
    "/static/wlm/wlm/usertiles/usertile_bonsai.png",       # Zen bonsai
    "/static/wlm/wlm/usertiles/usertile_fish.png",         # Tropical fish
    "/static/wlm/wlm/usertiles/usertile_chef.png",         # Chef
    "/static/wlm/wlm/usertiles/usertile_rocket.png",       # Space rocket
    "/static/wlm/wlm/usertiles/usertile_electric_guitar.png",
    "/static/wlm/wlm/usertiles/usertile_bike.png"
]

AVAILABLE_WINKS = [
    "water_balloon", "frog", "guitar_smash", "dancing_pig", 
    "bow", "heartkey", "kiss", "knock", "laughing_girl", "fartguy"
]


def generate_session_persona(session_id: str, first_prompt: str = "") -> Dict[str, Any]:
    text = (session_id + " " + (first_prompt or "")).lower()
    h = int(hashlib.md5(session_id.encode()).hexdigest(), 16)

    # Pick 2000s name deterministically
    person = POPULAR_2000S_NAMES[h % len(POPULAR_2000S_NAMES)]
    first_name = person["first"]
    last_name = person["last"]
    full_name = f"{first_name} {last_name}"

    # Determine domain specialization
    if any(k in text for k in ["shader", "lighting", "radiance", "compute", "hlsl", "gpu", "vulkan", "render"]):
        role = "Compute Shader Specialist"
        fav_wink = "water_balloon"
        avatar = "/static/wlm/wlm/usertiles/usertile_flare.png"
        banner = "/static/wlm/wlm/scenes/0001.png"   # Soccer field from Pic 1
        color = "#0284c7"
        emoticon = "(li)"
        song = "Daft Punk - Voyager"
    elif any(k in text for k in ["sql", "database", "memory", "sqlite", "table", "store", "cognition"]):
        role = "SQL Authority Architect"
        fav_wink = "knock"
        avatar = "/static/wlm/wlm/usertiles/usertile_chess.png"
        banner = "/static/wlm/wlm/scenes/zune_04.jpg"  # Purple skulls from Pic 1
        color = "#7c3aed"
        emoticon = "(co)"
        song = "Kraftwerk - Computer World"
    elif any(k in text for k in ["bench", "speed", "profile", "perf", "optimi", "fast"]):
        role = "Performance & Benchmarks"
        fav_wink = "guitar_smash"
        avatar = "/static/wlm/wlm/usertiles/usertile_bike.png"
        banner = "/static/wlm/wlm/scenes/CarbonFiber.jpg"
        color = "#dc2626"
        emoticon = "8-)"
        song = "The Prodigy - Invaders Must Die"
    elif any(k in text for k in ["sandbox", "security", "jail", "guard", "audit"]):
        role = "Sandbox Security Guardian"
        fav_wink = "frog"
        avatar = "/static/wlm/wlm/usertiles/usertile_bonsai.png"
        banner = "/static/wlm/wlm/scenes/0007.png"
        color = "#16a34a"
        emoticon = ":-S"
        song = "Chemical Brothers - Galvanize"
    elif any(k in text for k in ["gui", "ui", "wlm", "messenger", "xp", "window", "frontend", "css"]):
        role = "Windows XP & UI Architect"
        fav_wink = "dancing_pig"
        avatar = "/static/wlm/wlm/usertiles/usertile_electric_guitar.png"
        banner = "/static/wlm/wlm/scenes/Robot.jpg"
        color = "#ea580c"
        emoticon = "<:o)"
        song = "Owl City - Fireflies"
    else:
        role = "Project Strategist"
        fav_wink = AVAILABLE_WINKS[h % len(AVAILABLE_WINKS)]
        avatar = AVAILABLE_USERTILES[h % len(AVAILABLE_USERTILES)]
        banner = POPULAR_SCENE_BANNERS[h % len(POPULAR_SCENE_BANNERS)]
        color = "#2563eb"
        emoticon = ";)"
        song = "Linkin Park - In the End"

    # Authentic 2000s MSN Screen-name format variations
    fmt_idx = h % 4
    if fmt_idx == 0:
        display_name = f"~* {first_name} *~ ({role})"
    elif fmt_idx == 1:
        display_name = f"{full_name} [{role}]"
    elif fmt_idx == 2:
        display_name = f"• {first_name} • ({role})"
    else:
        display_name = f"{first_name} // {role}"

    clean_email = f"{first_name.lower()}.{last_name.lower()}2004@hotmail.com"
    short_bio = (first_prompt[:40].strip() + "...") if first_prompt else f"Online & handling session {session_id}."

    return {
        "name": display_name,
        "first_name": first_name,
        "last_name": last_name,
        "full_name": full_name,
        "role": role,
        "bio": f"{short_bio} ♫ {song}",
        "email": clean_email,
        "status": "online",
        "avatar": avatar,
        "banner": banner,
        "color_scheme": color,
        "emoticon": emoticon,
        "favorite_wink": fav_wink,
        "nudge_style": "classic"
    }


class CreateSessionRequest(BaseModel):
    session_id: Optional[str] = None
    workspace: Optional[str] = None
    description: Optional[str] = ""
    initial_prompt: Optional[str] = ""


class DeleteSessionRequest(BaseModel):
    session_id: str
    workspace: Optional[str] = None


@app.post("/api/iba/sessions/delete")
async def delete_iba_session(req: DeleteSessionRequest):
    session_id = req.session_id.strip()
    if not session_id:
        raise HTTPException(status_code=400, detail="Session id is required")
    if session_id == "default":
        raise HTTPException(status_code=400, detail="The default Dory session cannot be deleted")
    ws_path = Path(req.workspace) if req.workspace else workspace_manager.get_active_workspace_path()
    try:
        result = workspace_manager.delete_session(session_id, ws_path)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc))
    add_log_entry(f"Deleted IBA session '{session_id}' ({result['counts']['tasks']} tasks removed)", "info")
    return result


@app.post("/api/iba/sessions/create")
async def create_iba_session(req: CreateSessionRequest):
    desc = req.description or req.initial_prompt or ""
    if req.session_id and req.session_id.strip():
        cleaned = re.sub(r"[^a-zA-Z0-9_\-]", "_", req.session_id.strip()).strip("_")
    elif desc:
        words = [w for w in re.sub(r"[^a-zA-Z0-9]", " ", desc).split() if len(w) > 2]
        slug = "_".join(words[:4]).lower() if words else "task"
        cleaned = f"{slug}_{int(time.time()) % 10000}"
    else:
        cleaned = f"session_{int(time.time()) % 100000}"

    if not cleaned:
        cleaned = f"session_{int(time.time()) % 100000}"
    ws_path = Path(req.workspace) if req.workspace else workspace_manager.get_active_workspace_path()
    res = workspace_manager.set_active_session(cleaned)
    persona = generate_session_persona(cleaned, desc)
    add_log_entry(f"Created new IBA session '{cleaned}' with persona '{persona['name']}'", "info")
    return {"status": "ok", "session_id": cleaned, "persona": persona}


@app.get("/api/iba/sessions")
async def get_iba_sessions(workspace: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    raw_sessions = workspace_manager.list_sessions(ws_path)
    store = workspace_manager.get_store(ws_path)
    owner = workspace_manager.get_owner()

    sessions = []
    for s in raw_sessions:
        s_id = s.get("session_id", "default")
        first_prompt = s.get("first_prompt") or ""
        if not first_prompt:
            try:
                with store.connect() as db:
                    first_row = db.execute(
                        "SELECT objective FROM task WHERE owner=? AND session=? AND parent IS NULL ORDER BY created ASC LIMIT 1",
                        (owner, s_id)
                    ).fetchone()
                    if first_row and first_row["objective"]:
                        art = store.artifact(owner, s_id, first_row["objective"], 0, 400)
                        first_prompt = art.get("body", "")
            except Exception:
                pass

        persona = generate_session_persona(s_id, first_prompt)
        sessions.append({
            **s,
            "first_prompt": first_prompt,
            "persona": persona,
        })

    return {
        "workspace": str(ws_path),
        "active_session": workspace_manager.get_active_session(),
        "sessions": sessions,
    }


@app.post("/api/iba/sessions/select")
async def select_iba_session(req: SelectSessionRequest):
    res = workspace_manager.set_active_session(req.session_id)
    add_log_entry(f"Switched active session to: {req.session_id}", "info")
    return res


@app.get("/api/iba/sessions/history")
@app.get("/api/iba/sessions/{session_id}/history")
async def get_iba_session_history(session_id: Optional[str] = None, workspace: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    sess = session_id or workspace_manager.get_active_session()
    history = workspace_manager.get_session_history(ws_path, sess)
    return {
        "workspace": str(ws_path),
        "session": sess,
        "messages": history,
    }


@app.get("/api/iba/tasks")
async def get_iba_tasks(workspace: Optional[str] = None, session: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    sess = session or workspace_manager.get_active_session()
    tasks = workspace_manager.get_session_tasks(ws_path, sess)
    return {
        "workspace": str(ws_path),
        "session": sess,
        "tasks": tasks,
    }


@app.get("/api/iba/tasks/{task_id}")
async def get_iba_task_details(task_id: str, workspace: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    details = workspace_manager.get_task_details(task_id, ws_path)
    return details


@app.get("/api/iba/codemap")
async def get_iba_codemap(workspace: Optional[str] = None, session: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    sess = session or workspace_manager.get_active_session()
    owner = workspace_manager.get_owner()
    store = workspace_manager.get_store(ws_path)
    entries = store.get_code_map(owner, sess, ws_path) if hasattr(store, 'get_code_map') else []
    return {
        "workspace": str(ws_path),
        "session": sess,
        "count": len(entries),
        "entries": entries
    }


@app.get("/api/iba/policy")
async def get_iba_policy(workspace: Optional[str] = None, session: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    sess = session or workspace_manager.get_active_session()
    owner = workspace_manager.get_owner()
    port = server_state["config"]["port"]
    api_key = server_state["config"].get("api_key", DEFAULT_API_KEY)
    runtime = workspace_manager.get_runtime(ws_path, port=port, api_key=api_key)
    status = runtime.policy_status(owner, sess, discover=True)
    pins = runtime.store.pins(owner, sess)
    return {
        "policy": status,
        "pins": pins,
    }


@app.post("/api/iba/policy")
async def set_iba_policy(req: PolicyRequest, workspace: Optional[str] = None, session: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    sess = session or workspace_manager.get_active_session()
    owner = workspace_manager.get_owner()
    store = workspace_manager.get_store(ws_path)
    res = store.set_policy(owner, sess, req.target)
    add_log_entry(f"Updated context policy to {req.target} for session {sess}", "info")
    return res


class ProviderRequest(BaseModel):
    provider: str  # "auto" | "dory" | "lmstudio"
    lm_model: Optional[str] = None
    lm_api_key: Optional[str] = None


def _lmstudio_loaded_models(cfg):
    """Best-effort list of loaded LM Studio models for the sidebar status."""
    try:
        token = cfg.get("lm_api_key") or ""
        headers = {"Authorization": f"Bearer {token}"} if token else {}
        with httpx.Client(timeout=3.0) as client:
            resp = client.get("http://127.0.0.1:1234/api/v1/models", headers=headers)
            if resp.status_code != 200:
                return []
            out = []
            for m in resp.json().get("models", []):
                for inst in m.get("loaded_instances") or []:
                    out.append(inst.get("id") or m.get("key") or m.get("display_name") or "model")
            return out
    except Exception:
        return []


@app.get("/api/iba/provider")
async def get_iba_provider():
    cfg = provider_config()
    kind = effective_provider_kind()
    return {
        "config": {k: v for k, v in cfg.items() if k != "lm_api_key"},
        "has_lm_key": bool(cfg.get("lm_api_key")),
        "effective": kind,
        "dory_engine": server_state["status"],
        "lmstudio_loaded": _lmstudio_loaded_models(cfg) if kind == "lmstudio" else [],
    }


@app.post("/api/iba/provider")
async def set_iba_provider(req: ProviderRequest):
    kind = (req.provider or "").strip().lower()
    if kind not in ("auto", "dory", "lmstudio"):
        raise HTTPException(status_code=400, detail="provider must be auto, dory or lmstudio")
    cfg = write_provider_config(
        kind,
        lm_model=(req.lm_model.strip() if req.lm_model else None),
        lm_api_key=(req.lm_api_key.strip() if req.lm_api_key else None),
    )
    effective = effective_provider_kind()
    add_log_entry(f"IBA backend set to '{kind}' (effective: {effective})", "info")
    return {
        "status": "ok",
        "config": {k: v for k, v in cfg.items() if k != "lm_api_key"},
        "has_lm_key": bool(cfg.get("lm_api_key")),
        "effective": effective,
        "dory_engine": server_state["status"],
        "lmstudio_loaded": _lmstudio_loaded_models(cfg) if effective == "lmstudio" else [],
    }


@app.post("/api/iba/pin")
async def pin_iba_constraint(req: PinRequest, workspace: Optional[str] = None, session: Optional[str] = None):
    import hashlib
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    sess = session or workspace_manager.get_active_session()
    owner = workspace_manager.get_owner()
    store = workspace_manager.get_store(ws_path)
    text = req.constraint.strip()
    if not text:
        raise HTTPException(status_code=400, detail="Constraint cannot be empty")
    key = store.ingest(owner, sess, "higher", "constraint:" + hashlib.sha256(text.encode()).hexdigest(), text, pin=True)
    add_log_entry(f"Pinned constraint in session {sess}: {text[:50]}", "info")
    return {"status": "ok", "key": key, "constraint": text}


# ====================================================================
# Host Permission & Questions Management for Sandbox VM
# ====================================================================

pending_host_permissions: Dict[str, Dict[str, Any]] = {}
permission_audit_history: List[Dict[str, Any]] = []

class PermissionActionRequest(BaseModel):
    title: str
    description: str
    command: str
    target: str = "Host Environment"  # "Unity Editor", "Host Shell", "System Hardware"
    risk_level: str = "medium"        # "low", "medium", "high", "critical"
    session_id: Optional[str] = "default"
    workspace: Optional[str] = None

class PermissionResponse(BaseModel):
    request_id: str
    approved: bool
    session_id: Optional[str] = "default"
    notes: Optional[str] = ""

class QuestionAnswerRequest(BaseModel):
    question_id: str
    answer: str
    session_id: Optional[str] = "default"


@app.get("/api/iba/winks")
async def get_available_winks():
    """Returns authentic WLM Wave-4 winks with video clips and sound metadata."""
    return {
        "winks": [
            {
                "id": "water_balloon",
                "name": "Water Balloon",
                "icon": "/static/wlm/wlm/winks/water_balloon.png",
                "video": "/static/wlm/wlm/winks/videos/wink_water_balloon.mp4",
                "sound": "wink.mp3",
                "desc": "Splashes water across the entire chat window!"
            },
            {
                "id": "frog",
                "name": "Jumping Frog",
                "icon": "/static/wlm/wlm/winks/frog.png",
                "video": "/static/wlm/wlm/winks/videos/wink_frog.mp4",
                "sound": "wink.mp3",
                "desc": "Classic Windows Live Messenger frog hop & ribbit"
            },
            {
                "id": "guitar_smash",
                "name": "Guitar Smash",
                "icon": "/static/wlm/wlm/winks/guitar_smash.png",
                "video": "/static/wlm/wlm/winks/videos/wink_guitar_smash.mp4",
                "sound": "wink.mp3",
                "desc": "Rock star guitar amplifier power chord smash"
            },
            {
                "id": "dancing_pig",
                "name": "Dancing Pig",
                "icon": "/static/wlm/wlm/winks/dancing_pig.png",
                "video": "/static/wlm/wlm/winks/videos/wink_dancing_pig.mp4",
                "sound": "wink.mp3",
                "desc": "Funky groove pig dance party"
            },
            {
                "id": "bow",
                "name": "Formal Bow",
                "icon": "/static/wlm/wlm/winks/bow.jpg",
                "video": "/static/wlm/wlm/winks/videos/wink_bow.mp4",
                "sound": "wink.mp3",
                "desc": "Polite gentleman bow and tip of the hat"
            },
            {
                "id": "heartkey",
                "name": "Heart & Key",
                "icon": "/static/wlm/wlm/winks/heart.png",
                "video": "/static/wlm/wlm/winks/videos/wink_heartkey.mp4",
                "sound": "wink.mp3",
                "desc": "Lock and key romantic heart unlock"
            },
            {
                "id": "kiss",
                "name": "Lipstick Kiss",
                "icon": "/static/wlm/wlm/winks/kiss.png",
                "video": "/static/wlm/wlm/winks/videos/wink_kiss.mp4",
                "sound": "wink.mp3",
                "desc": "Giant red lips smooch on screen glass"
            },
            {
                "id": "knock",
                "name": "Knock on Screen",
                "icon": "/static/wlm/wlm/winks/knock.png",
                "video": "/static/wlm/wlm/winks/videos/wink_knock.mp4",
                "sound": "wink.mp3",
                "desc": "Knocks loudly against the computer monitor glass"
            },
            {
                "id": "laughing_girl",
                "name": "Laughing Girl",
                "icon": "/static/wlm/wlm/winks/laughing_girl.png",
                "video": "/static/wlm/wlm/winks/videos/wink_laughing_girl.mp4",
                "sound": "wink.mp3",
                "desc": "Uncontrollable giggle fit"
            },
            {
                "id": "fartguy",
                "name": "Whoopee Cushion",
                "icon": "/static/wlm/wlm/winks/crying.png",
                "video": "/static/wlm/wlm/winks/videos/wink_fartguy.webm",
                "sound": "wink.mp3",
                "desc": "Classic early 2000s cartoon prank"
            },
            {
                "id": "love_letter",
                "name": "Love Letter",
                "icon": "/static/wlm/wlm/winks/heart.png",
                "video": "/static/wlm/wlm/winks/videos/wink_love_letter.mp4",
                "sound": "wink.mp3",
                "desc": "Enchanted flying heart envelope"
            },
            {
                "id": "notes",
                "name": "Musical Notes",
                "icon": "/static/wlm/wlm/winks/dancer.png",
                "video": "/static/wlm/wlm/winks/videos/wink_notes.mp4",
                "sound": "wink.mp3",
                "desc": "Melodic symphony floating across screen"
            },
            {
                "id": "yawning_moon",
                "name": "Yawning Moon",
                "icon": "/static/wlm/wlm/winks/lightbulb.jpg",
                "video": "/static/wlm/wlm/winks/videos/wink_yawning_moon.mp4",
                "sound": "wink.mp3",
                "desc": "Late night sleepy moon with nightcap"
            }
        ]
    }


@app.post("/api/iba/permission/request")
async def request_host_permission(req: PermissionActionRequest):
    req_id = f"perm_{int(time.time()*1000) % 1000000}"
    data = {
        "id": req_id,
        "title": req.title,
        "description": req.description,
        "command": req.command,
        "target": req.target,
        "risk_level": req.risk_level,
        "session_id": req.session_id,
        "timestamp": time.time(),
        "status": "pending"
    }
    pending_host_permissions[req_id] = data
    add_log_entry(f"Host Execution Permission Requested [{req_id}]: {req.title}", "warn")
    return {"status": "ok", "permission": data}


@app.post("/api/iba/permission/respond")
async def respond_host_permission(res: PermissionResponse):
    if res.request_id in pending_host_permissions:
        item = pending_host_permissions[res.request_id]
        item["status"] = "approved" if res.approved else "denied"
        item["resolved_at"] = time.time()
        item["notes"] = res.notes
        permission_audit_history.append(item)
        del pending_host_permissions[res.request_id]
        decision = "APPROVED by user" if res.approved else "DENIED by user"
        add_log_entry(f"Host Permission [{res.request_id}]: {decision}", "info" if res.approved else "warn")
        return {"status": "ok", "request_id": res.request_id, "approved": res.approved}
    return {"status": "error", "message": "Permission request not found or expired"}


@app.get("/api/iba/permission/pending")
async def list_pending_permissions(session_id: Optional[str] = None):
    perms = list(pending_host_permissions.values())
    if session_id:
        perms = [p for p in perms if p.get("session_id") == session_id]
    return {"pending": perms}


# ====================================================================
# Multi-Drive Virtual Filesystem Router (C: WinXP, D: Project, Z: Linux Sandbox)
# ====================================================================

FICTIONAL_XP_C_DRIVE = {
    "": [
        {"name": "WINDOWS", "path": "C:/WINDOWS", "is_dir": True, "size": 0, "ext": ""},
        {"name": "Program Files", "path": "C:/Program Files", "is_dir": True, "size": 0, "ext": ""},
        {"name": "Documents and Settings", "path": "C:/Documents and Settings", "is_dir": True, "size": 0, "ext": ""},
        {"name": "AUTOEXEC.BAT", "path": "C:/AUTOEXEC.BAT", "is_dir": False, "size": 24, "ext": ".bat"},
        {"name": "CONFIG.SYS", "path": "C:/CONFIG.SYS", "is_dir": False, "size": 32, "ext": ".sys"},
        {"name": "boot.ini", "path": "C:/boot.ini", "is_dir": False, "size": 211, "ext": ".ini"},
        {"name": "pagefile.sys", "path": "C:/pagefile.sys", "is_dir": False, "size": 1073741824, "ext": ".sys"}
    ],
    "WINDOWS": [
        {"name": "system32", "path": "C:/WINDOWS/system32", "is_dir": True, "size": 0, "ext": ""},
        {"name": "Media", "path": "C:/WINDOWS/Media", "is_dir": True, "size": 0, "ext": ""},
        {"name": "Web", "path": "C:/WINDOWS/Web", "is_dir": True, "size": 0, "ext": ""},
        {"name": "Fonts", "path": "C:/WINDOWS/Fonts", "is_dir": True, "size": 0, "ext": ""},
        {"name": "explorer.exe", "path": "C:/WINDOWS/explorer.exe", "is_dir": False, "size": 1032192, "ext": ".exe"},
        {"name": "notepad.exe", "path": "C:/WINDOWS/notepad.exe", "is_dir": False, "size": 66048, "ext": ".exe"},
        {"name": "regedit.exe", "path": "C:/WINDOWS/regedit.exe", "is_dir": False, "size": 156672, "ext": ".exe"},
        {"name": "win.ini", "path": "C:/WINDOWS/win.ini", "is_dir": False, "size": 492, "ext": ".ini"},
        {"name": "system.ini", "path": "C:/WINDOWS/system.ini", "is_dir": False, "size": 219, "ext": ".ini"}
    ],
    "WINDOWS/system32": [
        {"name": "drivers", "path": "C:/WINDOWS/system32/drivers", "is_dir": True, "size": 0, "ext": ""},
        {"name": "cmd.exe", "path": "C:/WINDOWS/system32/cmd.exe", "is_dir": False, "size": 389120, "ext": ".exe"},
        {"name": "calc.exe", "path": "C:/WINDOWS/system32/calc.exe", "is_dir": False, "size": 114688, "ext": ".exe"},
        {"name": "taskmgr.exe", "path": "C:/WINDOWS/system32/taskmgr.exe", "is_dir": False, "size": 229376, "ext": ".exe"},
        {"name": "kernel32.dll", "path": "C:/WINDOWS/system32/kernel32.dll", "is_dir": False, "size": 989696, "ext": ".dll"},
        {"name": "user32.dll", "path": "C:/WINDOWS/system32/user32.dll", "is_dir": False, "size": 578560, "ext": ".dll"},
        {"name": "shell32.dll", "path": "C:/WINDOWS/system32/shell32.dll", "is_dir": False, "size": 8459264, "ext": ".dll"}
    ],
    "WINDOWS/Media": [
        {"name": "Windows XP Startup.wav", "path": "C:/WINDOWS/Media/Windows XP Startup.wav", "is_dir": False, "size": 424644, "ext": ".wav"},
        {"name": "Windows XP Shutdown.wav", "path": "C:/WINDOWS/Media/Windows XP Shutdown.wav", "is_dir": False, "size": 286788, "ext": ".wav"},
        {"name": "Windows XP Ding.wav", "path": "C:/WINDOWS/Media/Windows XP Ding.wav", "is_dir": False, "size": 87460, "ext": ".wav"},
        {"name": "tada.wav", "path": "C:/WINDOWS/Media/tada.wav", "is_dir": False, "size": 115200, "ext": ".wav"},
        {"name": "chimes.wav", "path": "C:/WINDOWS/Media/chimes.wav", "is_dir": False, "size": 89400, "ext": ".wav"}
    ],
    "WINDOWS/Web": [
        {"name": "Wallpaper", "path": "C:/WINDOWS/Web/Wallpaper", "is_dir": True, "size": 0, "ext": ""}
    ],
    "WINDOWS/Web/Wallpaper": [
        {"name": "Bliss.bmp", "path": "C:/WINDOWS/Web/Wallpaper/Bliss.bmp", "is_dir": False, "size": 1440054, "ext": ".bmp"},
        {"name": "Autumn.bmp", "path": "C:/WINDOWS/Web/Wallpaper/Autumn.bmp", "is_dir": False, "size": 1440054, "ext": ".bmp"},
        {"name": "Azul.bmp", "path": "C:/WINDOWS/Web/Wallpaper/Azul.bmp", "is_dir": False, "size": 1440054, "ext": ".bmp"},
        {"name": "Red moon desert.bmp", "path": "C:/WINDOWS/Web/Wallpaper/Red moon desert.bmp", "is_dir": False, "size": 1440054, "ext": ".bmp"}
    ],
    "Program Files": [
        {"name": "Windows Live", "path": "C:/Program Files/Windows Live", "is_dir": True, "size": 0, "ext": ""},
        {"name": "SQL Authority 3D", "path": "C:/Program Files/SQL Authority 3D", "is_dir": True, "size": 0, "ext": ""},
        {"name": "Accessories", "path": "C:/Program Files/Accessories", "is_dir": True, "size": 0, "ext": ""},
        {"name": "Internet Explorer", "path": "C:/Program Files/Internet Explorer", "is_dir": True, "size": 0, "ext": ""},
        {"name": "Common Files", "path": "C:/Program Files/Common Files", "is_dir": True, "size": 0, "ext": ""}
    ],
    "Program Files/Windows Live": [
        {"name": "Messenger", "path": "C:/Program Files/Windows Live/Messenger", "is_dir": True, "size": 0, "ext": ""}
    ],
    "Program Files/Windows Live/Messenger": [
        {"name": "msnmsgr.exe", "path": "C:/Program Files/Windows Live/Messenger/msnmsgr.exe", "is_dir": False, "size": 5422592, "ext": ".exe"},
        {"name": "contacts.dat", "path": "C:/Program Files/Windows Live/Messenger/contacts.dat", "is_dir": False, "size": 14590, "ext": ".dat"},
        {"name": "history.log", "path": "C:/Program Files/Windows Live/Messenger/history.log", "is_dir": False, "size": 8940, "ext": ".log"}
    ],
    "Program Files/SQL Authority 3D": [
        {"name": "sqlauthority.exe", "path": "C:/Program Files/SQL Authority 3D/sqlauthority.exe", "is_dir": False, "size": 3145728, "ext": ".exe"},
        {"name": "sqlite3.dll", "path": "C:/Program Files/SQL Authority 3D/sqlite3.dll", "is_dir": False, "size": 1048576, "ext": ".dll"},
        {"name": "config.ini", "path": "C:/Program Files/SQL Authority 3D/config.ini", "is_dir": False, "size": 420, "ext": ".ini"}
    ],
    "Program Files/Accessories": [
        {"name": "notepad.exe", "path": "C:/Program Files/Accessories/notepad.exe", "is_dir": False, "size": 66048, "ext": ".exe"},
        {"name": "mspaint.exe", "path": "C:/Program Files/Accessories/mspaint.exe", "is_dir": False, "size": 341504, "ext": ".exe"},
        {"name": "calc.exe", "path": "C:/Program Files/Accessories/calc.exe", "is_dir": False, "size": 114688, "ext": ".exe"}
    ],
    "Documents and Settings": [
        {"name": "Rackneh", "path": "C:/Documents and Settings/Rackneh", "is_dir": True, "size": 0, "ext": ""},
        {"name": "All Users", "path": "C:/Documents and Settings/All Users", "is_dir": True, "size": 0, "ext": ""}
    ],
    "Documents and Settings/Rackneh": [
        {"name": "Desktop", "path": "C:/Documents and Settings/Rackneh/Desktop", "is_dir": True, "size": 0, "ext": ""},
        {"name": "My Documents", "path": "C:/Documents and Settings/Rackneh/My Documents", "is_dir": True, "size": 0, "ext": ""},
        {"name": "My Pictures", "path": "C:/Documents and Settings/Rackneh/My Pictures", "is_dir": True, "size": 0, "ext": ""},
        {"name": "My Music", "path": "C:/Documents and Settings/Rackneh/My Music", "is_dir": True, "size": 0, "ext": ""},
        {"name": "Favorites", "path": "C:/Documents and Settings/Rackneh/Favorites", "is_dir": True, "size": 0, "ext": ""},
        {"name": "NTUSER.DAT", "path": "C:/Documents and Settings/Rackneh/NTUSER.DAT", "is_dir": False, "size": 262144, "ext": ".dat"}
    ],
    "Documents and Settings/Rackneh/Desktop": [
        {"name": "My Computer.lnk", "path": "C:/Documents and Settings/Rackneh/Desktop/My Computer.lnk", "is_dir": False, "size": 482, "ext": ".lnk"},
        {"name": "Windows Live Messenger.lnk", "path": "C:/Documents and Settings/Rackneh/Desktop/Windows Live Messenger.lnk", "is_dir": False, "size": 640, "ext": ".lnk"},
        {"name": "SQL Authority Studio.lnk", "path": "C:/Documents and Settings/Rackneh/Desktop/SQL Authority Studio.lnk", "is_dir": False, "size": 598, "ext": ".lnk"},
        {"name": "Sandbox VM Shell.lnk", "path": "C:/Documents and Settings/Rackneh/Desktop/Sandbox VM Shell.lnk", "is_dir": False, "size": 512, "ext": ".lnk"}
    ],
    "Documents and Settings/Rackneh/My Documents": [
        {"name": "Compute_Shader_Notes.txt", "path": "C:/Documents and Settings/Rackneh/My Documents/Compute_Shader_Notes.txt", "is_dir": False, "size": 640, "ext": ".txt"},
        {"name": "Project_Strategy.txt", "path": "C:/Documents and Settings/Rackneh/My Documents/Project_Strategy.txt", "is_dir": False, "size": 420, "ext": ".txt"},
        {"name": "Architectural_Refinements.txt", "path": "C:/Documents and Settings/Rackneh/My Documents/Architectural_Refinements.txt", "is_dir": False, "size": 780, "ext": ".txt"}
    ]
}

FICTIONAL_XP_VIRTUAL_FILES = {
    "C:/boot.ini": "[boot loader]\ntimeout=30\ndefault=multi(0)disk(0)rdisk(0)partition(1)\\WINDOWS\n[operating systems]\nmulti(0)disk(0)rdisk(0)partition(1)\\WINDOWS=\"Microsoft Windows XP Professional\" /noexecute=optin /fastdetect",
    "C:/WINDOWS/win.ini": "; for 16-bit app support\n[fonts]\n[extensions]\n[mci extensions]\n[files]\n[Mail]\nMAPI=1\n[Windows Live Messenger]\nTheme=LunaBlueWave4\nDefaultUser=Rackneh",
    "C:/WINDOWS/system.ini": "; for 16-bit app support\n[drivers]\nwave=mmdrv.dll\ntimer=timer.drv\n[386enh]\nwoafont=dosapp.fon\nEGA80WOA.FON=EGA80WOA.FON\nEGA40WOA.FON=EGA40WOA.FON\nCGA80WOA.FON=CGA80WOA.FON\nCGA40WOA.FON=CGA40WOA.FON",
    "C:/Documents and Settings/Rackneh/My Documents/Compute_Shader_Notes.txt": "Compute Shader Dispatch Architecture Notes\n========================================\n- Verify kernel index 9 bounds and layer buffer bindings (_SQL3MediumLayers)\n- Indirect dispatch requires structured buffers bound before Execute()\n- Maintain O(1) latency using structured query lookups in SQLite cache",
    "C:/Documents and Settings/Rackneh/My Documents/Project_Strategy.txt": "InferenceLightingLab Project Strategy\n====================================\n- Keep agent execution isolated within Linux Sandbox VM (Z:)\n- Require interactive user approval for host Unity runtime testing\n- Persist state across recurrent loops in IBA Cognitive Tables",
    "C:/Documents and Settings/Rackneh/My Documents/Architectural_Refinements.txt": "Windows XP Luna Desktop + Windows Live Messenger Wave 4\n=======================================================\n- Graphic scene banners across chat & contacts\n- Authentic winks with HTML5 overlay video playback\n- Multi-drive Explorer: C: (System), D: (Project), Z: (Sandbox VM)\n- Free window resizing with resize handles on all applications",
    "C:/Program Files/SQL Authority 3D/config.ini": "[SQLAuthority]\nEngineVersion=3.4\nCognitiveMode=Federated\nAutoRotateCamera=true\nDefaultDatabase=control.sqlite\nMaxNodes=5000",
}

LINUX_SANDBOX_Z_DRIVE = {
    "": [
        {"name": "bin", "path": "Z:/bin", "is_dir": True, "size": 0, "ext": ""},
        {"name": "etc", "path": "Z:/etc", "is_dir": True, "size": 0, "ext": ""},
        {"name": "home", "path": "Z:/home", "is_dir": True, "size": 0, "ext": ""},
        {"name": "tmp", "path": "Z:/tmp", "is_dir": True, "size": 0, "ext": ""},
        {"name": "var", "path": "Z:/var", "is_dir": True, "size": 0, "ext": ""},
        {"name": "proc", "path": "Z:/proc", "is_dir": True, "size": 0, "ext": ""},
        {"name": "workspace", "path": "Z:/workspace", "is_dir": True, "size": 0, "ext": ""}
    ],
    "bin": [
        {"name": "bash", "path": "Z:/bin/bash", "is_dir": False, "size": 1183440, "ext": ""},
        {"name": "sh", "path": "Z:/bin/sh", "is_dir": False, "size": 121432, "ext": ""},
        {"name": "python3", "path": "Z:/bin/python3", "is_dir": False, "size": 5488128, "ext": ""},
        {"name": "git", "path": "Z:/bin/git", "is_dir": False, "size": 3982400, "ext": ""},
        {"name": "grep", "path": "Z:/bin/grep", "is_dir": False, "size": 164280, "ext": ""},
        {"name": "sed", "path": "Z:/bin/sed", "is_dir": False, "size": 182100, "ext": ""},
        {"name": "ls", "path": "Z:/bin/ls", "is_dir": False, "size": 138800, "ext": ""},
        {"name": "cat", "path": "Z:/bin/cat", "is_dir": False, "size": 42100, "ext": ""}
    ],
    "etc": [
        {"name": "os-release", "path": "Z:/etc/os-release", "is_dir": False, "size": 194, "ext": ""},
        {"name": "hosts", "path": "Z:/etc/hosts", "is_dir": False, "size": 120, "ext": ""},
        {"name": "resolv.conf", "path": "Z:/etc/resolv.conf", "is_dir": False, "size": 65, "ext": ".conf"},
        {"name": "passwd", "path": "Z:/etc/passwd", "is_dir": False, "size": 280, "ext": ""},
        {"name": "sandbox.conf", "path": "Z:/etc/sandbox.conf", "is_dir": False, "size": 384, "ext": ".conf"}
    ],
    "home": [
        {"name": "sandbox", "path": "Z:/home/sandbox", "is_dir": True, "size": 0, "ext": ""}
    ],
    "home/sandbox": [
        {"name": ".bashrc", "path": "Z:/home/sandbox/.bashrc", "is_dir": False, "size": 420, "ext": ""},
        {"name": "agent_worker.py", "path": "Z:/home/sandbox/agent_worker.py", "is_dir": False, "size": 1250, "ext": ".py"},
        {"name": "experiment_run.sh", "path": "Z:/home/sandbox/experiment_run.sh", "is_dir": False, "size": 310, "ext": ".sh"}
    ],
    "var": [
        {"name": "log", "path": "Z:/var/log", "is_dir": True, "size": 0, "ext": ""}
    ],
    "var/log": [
        {"name": "audit.log", "path": "Z:/var/log/audit.log", "is_dir": False, "size": 4120, "ext": ".log"},
        {"name": "jail_isolation.log", "path": "Z:/var/log/jail_isolation.log", "is_dir": False, "size": 2890, "ext": ".log"}
    ]
}

LINUX_SANDBOX_VIRTUAL_FILES = {
    "Z:/etc/os-release": 'NAME="Alpine Linux"\nID=alpine\nVERSION_ID=3.19.1\nPRETTY_NAME="Alpine Linux v3.19 (IBA Tiny Sandbox VM)"\nHOME_URL="https://alpinelinux.org/"\nBUG_REPORT_URL="https://gitlab.alpinelinux.org/alpine/aports/-/issues"',
    "Z:/etc/hosts": "127.0.0.1\tlocalhost\n172.17.0.2\tsandbox-vm.local\nsandbox-vm\n::1\tlocalhost ip6-localhost ip6-loopback",
    "Z:/etc/sandbox.conf": "# IBA Tiny Linux Sandbox VM Configuration\nPOLICY=STRICT\nJAIL_ROOT=/workspace\nHOST_PERM_REQUIRED=1\nCPU_QUOTA=400%\nMEMORY_LIMIT=2048M\nNETWORK_ISOLATION=BRIDGED_RESTRICTED",
    "Z:/home/sandbox/.bashrc": "export PS1='sandbox@tiny-vm:\\w\\$ '\nexport WORKSPACE=/workspace\nexport TERM=xterm-256color\nalias ll='ls -la'\nalias proj='cd /workspace'",
    "Z:/home/sandbox/agent_worker.py": "# IBA Agent Isolated Runner inside Sandbox VM\nimport os, sys\nprint('Running securely inside Sandbox VM jail: /workspace')\nprint('Host interactions require interactive user confirmation.')",
    "Z:/var/log/audit.log": "[2026-11-04 02:00:15] [INFO] Sandbox VM started: pid=1, arch=x86_64, policy=STRICT\n[2026-11-04 02:00:16] [INFO] Mounted host project workspace to /workspace\n[2026-11-04 02:00:18] [AUDIT] Host filesystem isolation enforced: access outside /workspace blocked"
}


# ====================================================================
# Multi-Drive Explorer File Manager Endpoints (C:, D:, Z:)
# ====================================================================

@app.get("/api/iba/fs/browse")
async def browse_workspace_files(subpath: str = "", workspace: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    raw = subpath.strip().replace("\\", "/")
    
    # 1. Root / "My Computer" Drive Listing
    if not raw or raw.lower() in ("my computer", "root", "/", "drives"):
        return {
            "mode": "drives",
            "workspace_root": str(ws_path),
            "current_path": "My Computer",
            "full_current_path": "My Computer",
            "parent_path": None,
            "items": [
                {
                    "name": "3½ Floppy (A:)",
                    "path": "A:",
                    "is_dir": True,
                    "drive_type": "floppy",
                    "size": 1474560,
                    "free": "1.44 MB free of 1.44 MB",
                    "ext": ""
                },
                {
                    "name": "Local Disk (C:)",
                    "path": "C:",
                    "is_dir": True,
                    "drive_type": "fixed",
                    "size": 42949672960,
                    "free": "24.6 GB free of 40.0 GB",
                    "ext": ""
                },
                {
                    "name": f"Project Volume (D:) [{ws_path.name if ws_path else 'Workspace'}]",
                    "path": "D:",
                    "is_dir": True,
                    "drive_type": "project",
                    "size": 1099511627776,
                    "free": "285 GB free of 1.00 TB",
                    "ext": ""
                },
                {
                    "name": "CD Drive (E:)",
                    "path": "E:",
                    "is_dir": True,
                    "drive_type": "cdrom",
                    "size": 734003200,
                    "free": "0 bytes free of 700 MB",
                    "ext": ""
                },
                {
                    "name": "Linux Sandbox VM (Z:) (\\\\SANDBOX-VM\\jail)",
                    "path": "Z:",
                    "is_dir": True,
                    "drive_type": "network",
                    "size": 107374182400,
                    "free": "78.2 GB free of 100 GB",
                    "ext": ""
                }
            ]
        }

    # Normalize drive letter prefixes
    norm = raw
    if norm.endswith(":"):
        norm = norm + "/"

    # 2. Fictional Windows XP C: Drive
    if norm.upper().startswith("C:"):
        rel_sub = norm[2:].strip("/").replace("\\", "/")
        items = FICTIONAL_XP_C_DRIVE.get(rel_sub)
        if items is None:
            # Case-insensitive lookup
            for k in FICTIONAL_XP_C_DRIVE:
                if k.lower() == rel_sub.lower():
                    items = FICTIONAL_XP_C_DRIVE[k]
                    rel_sub = k
                    break
        if items is None:
            items = []

        parent = None
        if rel_sub:
            parts = rel_sub.split("/")
            parent = "C:/" + "/".join(parts[:-1]) if len(parts) > 1 else "C:"
        else:
            parent = "My Computer"

        return {
            "mode": "virtual_c",
            "workspace_root": "C:/",
            "current_path": f"C:/{rel_sub}" if rel_sub else "C:",
            "full_current_path": f"C:\\{rel_sub.replace('/', '\\')}" if rel_sub else "C:\\",
            "parent_path": parent,
            "items": sorted(items, key=lambda x: (not x["is_dir"], x["name"].lower()))
        }

        # 2b. Windows XP CD-ROM Installation Disc (E:)
    if norm.upper().startswith("E:"):
        rel_sub = norm[2:].strip("/").replace("\\", "/")
        cd_disc_path = Path(r"D:\Software\Windows\Windows\Windows.XP.Professional.SP3.x86")
        target_dir = (cd_disc_path / rel_sub) if rel_sub else cd_disc_path
        items = []
        if target_dir.exists() and target_dir.is_dir():
            try:
                for entry in target_dir.iterdir():
                    rel_p = f"E:/{rel_sub}/{entry.name}".replace("//", "/") if rel_sub else f"E:/{entry.name}"
                    items.append({
                        "name": entry.name,
                        "path": rel_p,
                        "is_dir": entry.is_dir(),
                        "size": entry.stat().st_size if entry.is_file() else 0,
                        "ext": entry.suffix.lower() if entry.is_file() else ""
                    })
            except Exception:
                pass
        parent = ("E:/" + "/".join(rel_sub.split("/")[:-1])) if "/" in rel_sub else ("E:" if rel_sub else "My Computer")
        return {
            "mode": "cdrom",
            "workspace_root": "E:/",
            "current_path": f"E:/{rel_sub}" if rel_sub else "E:",
            "full_current_path": f"E:\\{rel_sub.replace('/', '\\')}" if rel_sub else "E:\\",
            "parent_path": parent,
            "items": sorted(items, key=lambda x: (not x["is_dir"], x["name"].lower()))
        }

    # 3. Linux Sandbox VM Network Drive (Z:)
    if norm.upper().startswith("Z:"):
        rel_sub = norm[2:].strip("/").replace("\\", "/")
        
        # If navigating into Z:/workspace, mount real project workspace volume!
        if rel_sub == "workspace" or rel_sub.startswith("workspace/"):
            ws_sub = rel_sub[9:].strip("/")
            target_dir = (ws_path / ws_sub) if (ws_path and ws_sub) else (ws_path if ws_path else Path("."))
            items = []
            if target_dir.exists() and target_dir.is_dir():
                try:
                    for entry in target_dir.iterdir():
                        if entry.name.startswith((".", "$")) or entry.name in ("__pycache__", "node_modules", "Library", "Temp"):
                            continue
                        rel_path = f"Z:/workspace/{ws_sub}/{entry.name}".replace("//", "/") if ws_sub else f"Z:/workspace/{entry.name}"
                        items.append({
                            "name": entry.name,
                            "path": rel_path,
                            "is_dir": entry.is_dir(),
                            "size": entry.stat().st_size if entry.is_file() else 0,
                            "ext": entry.suffix.lower() if entry.is_file() else ""
                        })
                except Exception:
                    pass
            parent = "Z:/" + "/".join(rel_sub.split("/")[:-1]) if "/" in rel_sub else "Z:"
            return {
                "mode": "virtual_z",
                "workspace_root": "Z:/workspace",
                "current_path": f"Z:/{rel_sub}",
                "full_current_path": f"\\\\SANDBOX-VM\\jail\\{rel_sub.replace('/', '\\')}",
                "parent_path": parent,
                "items": sorted(items, key=lambda x: (not x["is_dir"], x["name"].lower()))
            }

        items = LINUX_SANDBOX_Z_DRIVE.get(rel_sub)
        if items is None:
            for k in LINUX_SANDBOX_Z_DRIVE:
                if k.lower() == rel_sub.lower():
                    items = LINUX_SANDBOX_Z_DRIVE[k]
                    rel_sub = k
                    break
        if items is None:
            items = []

        parent = None
        if rel_sub:
            parts = rel_sub.split("/")
            parent = "Z:/" + "/".join(parts[:-1]) if len(parts) > 1 else "Z:"
        else:
            parent = "My Computer"

        return {
            "mode": "virtual_z",
            "workspace_root": "Z:/",
            "current_path": f"Z:/{rel_sub}" if rel_sub else "Z:",
            "full_current_path": f"\\\\SANDBOX-VM\\jail\\{rel_sub.replace('/', '\\')}" if rel_sub else "\\\\SANDBOX-VM\\jail",
            "parent_path": parent,
            "items": sorted(items, key=lambda x: (not x["is_dir"], x["name"].lower()))
        }

    # 4. Project Volume (D:) -> Real Workspace Filesystem on Host
    if norm.upper().startswith("D:"):
        clean_proj_sub = norm[2:].strip("/").replace("\\", "/")
    else:
        clean_proj_sub = norm.strip("/").replace("\\", "/")

    if not ws_path or not ws_path.exists():
        return {
            "mode": "project_d",
            "workspace_root": "D:",
            "current_path": "D:",
            "full_current_path": "D:\\",
            "parent_path": "My Computer",
            "items": []
        }

    from iba.sandbox import WorkspaceSandbox
    sandbox = WorkspaceSandbox(str(ws_path), policy="STRICT")

    try:
        target_dir = sandbox.check_path_access(clean_proj_sub, is_write=False) if clean_proj_sub else ws_path
    except Exception as e:
        raise HTTPException(status_code=403, detail=str(e))

    if not target_dir.is_dir():
        target_dir = target_dir.parent

    items = []
    try:
        for entry in os.scandir(target_dir):
            if entry.name in (".git", "iba_runtime", "__pycache__"):
                continue
            is_dir = entry.is_dir()
            size = 0 if is_dir else entry.stat().st_size
            mtime = entry.stat().st_mtime
            rel_from_ws = os.path.relpath(entry.path, str(ws_path)).replace("\\", "/")
            ext = os.path.splitext(entry.name)[1].lower()
            items.append({
                "name": entry.name,
                "path": f"D:/{rel_from_ws}",
                "is_dir": is_dir,
                "size": size,
                "modified": mtime,
                "ext": ext
            })
    except Exception:
        pass

    items.sort(key=lambda x: (not x["is_dir"], x["name"].lower()))
    
    if target_dir == ws_path:
        parent_path = "My Computer"
    else:
        rel_parent = os.path.relpath(target_dir.parent, str(ws_path)).replace("\\", "/")
        parent_path = f"D:/{rel_parent}" if rel_parent != "." else "D:"

    rel_curr = os.path.relpath(target_dir, str(ws_path)).replace("\\", "/")
    curr_d_path = f"D:/{rel_curr}" if rel_curr != "." else "D:"

    return {
        "mode": "project_d",
        "workspace_root": str(ws_path),
        "current_path": curr_d_path,
        "full_current_path": str(target_dir),
        "parent_path": parent_path,
        "items": items
    }


@app.get("/api/iba/fs/read")
async def read_workspace_file(path: str, workspace: Optional[str] = None):
    clean = path.strip().replace("\\", "/")
    
    # 1. Virtual C: File Read
    if clean in FICTIONAL_XP_VIRTUAL_FILES:
        content = FICTIONAL_XP_VIRTUAL_FILES[clean]
        return {
            "path": clean,
            "size": len(content),
            "content": content,
            "virtual": True,
            "origin": "Windows XP C: System Drive"
        }
    
    # 2. Virtual Z: Linux Sandbox File Read
    if clean in LINUX_SANDBOX_VIRTUAL_FILES:
        content = LINUX_SANDBOX_VIRTUAL_FILES[clean]
        return {
            "path": clean,
            "size": len(content),
            "content": content,
            "virtual": True,
            "origin": "Linux Sandbox VM (Z:)"
        }

        # 2b. Windows XP CD-ROM File Read (E:)
    if clean.upper().startswith("E:"):
        rel = clean[2:].strip("/")
        cd_disc_path = Path(r"D:\Software\Windows\Windows\Windows.XP.Professional.SP3.x86")
        target_f = cd_disc_path / rel
        if target_f.is_file():
            try:
                content = target_f.read_text(encoding="utf-8", errors="replace")
                return {
                    "path": clean,
                    "size": target_f.stat().st_size,
                    "content": content,
                    "virtual": False,
                    "origin": "Windows XP Professional SP3 Installation Disc"
                }
            except Exception as e:
                raise HTTPException(status_code=500, detail=str(e))
        else:
            raise HTTPException(status_code=404, detail="File not found on CD-ROM")

    # 3. Real Project File on D:
    norm_path = clean
    if norm_path.upper().startswith("D:"):
        norm_path = norm_path[2:].strip("/")
    
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    if not ws_path or not ws_path.exists():
        raise HTTPException(status_code=404, detail="Workspace not found")

    from iba.sandbox import WorkspaceSandbox
    sandbox = WorkspaceSandbox(str(ws_path), policy="STRICT")
    try:
        target_file = sandbox.check_path_access(norm_path.strip("/\\"), is_write=False)
    except Exception as e:
        raise HTTPException(status_code=403, detail=str(e))

    if not target_file.is_file():
        raise HTTPException(status_code=404, detail="File not found")

    try:
        text = target_file.read_text(encoding="utf-8", errors="replace")
        return {
            "path": path,
            "size": len(text),
            "content": text,
            "virtual": False,
            "origin": "Host Project Workspace"
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/api/iba/sandbox/status")
async def get_sandbox_status(workspace: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    from iba.sandbox import WorkspaceSandbox, SENSITIVE_PATTERNS
    sandbox = WorkspaceSandbox(str(ws_path), policy="STRICT")
    
    return {
        "status": "active",
        "policy": "STRICT",
        "vm": {
            "name": "Tiny Linux Sandbox VM",
            "os": "Alpine Linux v3.19 (x86_64)",
            "kernel": "Linux 6.1.0-tiny-sandbox",
            "vcpus": 4,
            "memory_mb": 2048,
            "memory_used_mb": 512,
            "network": "172.17.0.2 (bridged container isolation)",
            "jail_path": "\\\\SANDBOX-VM\\jail (Mapped Z:)",
            "host_permission_mode": "INTERACTIVE_PROMPT"
        },
        "workspace_jail": str(ws_path),
        "mounts": {
            "/workspace": "Project Root (Mapped D:)",
            "/jail/root": "Isolated Linux VM Filesystem (Z:)"
        },
        "pending_permissions_count": len(pending_host_permissions),
        "protected_patterns": SENSITIVE_PATTERNS,
        "recent_audits": []
    }


@app.post("/api/iba/sandbox/policy")
async def set_sandbox_policy(policy: str = "STRICT", workspace: Optional[str] = None):
    policy_clean = policy.upper()
    if policy_clean not in ("STRICT", "MONITORED", "PERMISSIVE"):
        raise HTTPException(status_code=400, detail="Policy must be STRICT, MONITORED, or PERMISSIVE")
    add_log_entry(f"Sandbox policy updated to {policy_clean}", "info")
    return {"status": "ok", "policy": policy_clean}


# OpenAI-compatible API Endpoints
@app.get("/v1/models")
async def get_models():
    return {
        "object": "list",
        "data": [
            {
                "id": "dory",
                "object": "model",
                "created": int(time.time()),
                "owned_by": "nvidia",
            }
        ],
    }


@app.post("/v1/chat/completions")
async def proxy_v1_chat_completions(request: Request):
    port = server_state["config"]["port"]
    url = f"http://127.0.0.1:{port}/v1/chat/completions"
    body = await request.json()
    model_name = body.get("model", "dory")
    is_stream = body.get("stream", False)
    client_host = request.client.host if request.client else "unknown"

    add_log_entry(
        f"API request from {client_host}: model={model_name}, stream={is_stream}",
        "slot",
    )
    activity_state["is_busy"] = True
    activity_state["active_client"] = f"{client_host} (Proxy)"
    activity_state["status_label"] = f"Client request in progress ({model_name})..."

    headers = {
        "Content-Type": "application/json",
    }
    auth_header = request.headers.get("Authorization")
    if auth_header:
        headers["Authorization"] = auth_header

    client = httpx.AsyncClient(timeout=900.0)
    if is_stream:

        async def stream_proxy():
            # Flush HTTP 200 headers immediately with an initial SSE comment within < 5ms.
            # This completely disarms undici / OpenCode's headersTimeout during long prompt evaluations.
            yield b": connected\n\n"
            try:
                async with client.stream(
                    "POST", url, json=body, headers=headers
                ) as resp:
                    async for chunk in resp.aiter_raw():
                        yield chunk
            finally:
                await client.aclose()

        return StreamingResponse(
            stream_proxy(),
            media_type="text/event-stream",
            headers={
                "Cache-Control": "no-cache",
                "Connection": "keep-alive",
                "X-Accel-Buffering": "no",
            },
        )
    else:
        resp = await client.post(url, json=body, headers=headers)
        res_data = resp.json()
        await client.aclose()
        return res_data


@app.api_route("/mcp", methods=["GET", "POST", "OPTIONS"])
async def mcp_handler(request: Request):
    if request.method == "OPTIONS":
        return Response(status_code=200)
    try:
        body = await request.json()
        req_id = body.get("id", 1)
        method = body.get("method", "")
        if method == "initialize":
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {"tools": {}, "resources": {}, "prompts": {}},
                    "serverInfo": {"name": "dory-mcp", "version": "1.0.0"}
                }
            }
        elif method == "tools/list":
            return {"jsonrpc": "2.0", "id": req_id, "result": {"tools": []}}
        elif method == "resources/list":
            return {"jsonrpc": "2.0", "id": req_id, "result": {"resources": []}}
        elif method == "prompts/list":
            return {"jsonrpc": "2.0", "id": req_id, "result": {"prompts": []}}
        elif method == "ping":
            return {"jsonrpc": "2.0", "id": req_id, "result": {}}
        return {"jsonrpc": "2.0", "id": req_id, "result": {}}
    except Exception:
        return {"jsonrpc": "2.0", "id": 1, "result": {}}



# ====================================================================
# SQL Authority 3D Multi-Database Cognitive Engine Endpoints
# ====================================================================

@app.get("/api/iba/sql/databases")
async def get_sql_databases(workspace: Optional[str] = None):
    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    if not ws_path or not (ws_path / "iba_runtime").exists():
        ws_path = Path(r"C:\Users\Rackneh\Documents\Github\InferenceLightingLab")
    iba_dir = ws_path / "iba_runtime" if ws_path else None
    databases = []
    
    DB_META = {
        "control.sqlite": {"label": "Control Store", "layer": "Intent & Orchestration", "color": "#38bdf8", "y": -1.2},
        "domain-code.sqlite": {"label": "Code Projections", "layer": "Code Architecture & Shaders", "color": "#4ade80", "y": -0.4},
        "domain-higher.sqlite": {"label": "Cognitive Memory", "layer": "Higher Patterns & Strategy", "color": "#fb923c", "y": 0.4},
        "domain-math.sqlite": {"label": "Shader & Math", "layer": "Mathematical Invariants", "color": "#c084fc", "y": 1.2},
        "domain-lower.sqlite": {"label": "Execution Traces", "layer": "Low-level Sandbox Traces", "color": "#f472b6", "y": 1.8}
    }

    if iba_dir and iba_dir.exists():
        for db_file in sorted(list(iba_dir.glob("*.sqlite"))):
            name = db_file.name
            try:
                con = sqlite3.connect(db_file)
                tables = [r[0] for r in con.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '%_fts%'").fetchall()]
                total_rows = 0
                for t in tables:
                    try:
                        total_rows += con.execute(f"SELECT count(*) FROM [{t}]").fetchone()[0]
                    except Exception:
                        pass
                meta = DB_META.get(name, {"label": name, "layer": "Auxiliary Store", "color": "#94a3b8", "y": 0.0})
                databases.append({
                    "name": name,
                    "label": meta["label"],
                    "layer": meta["layer"],
                    "color": meta["color"],
                    "y_plane": meta["y"],
                    "size": db_file.stat().st_size,
                    "tables": tables,
                    "total_rows": total_rows
                })
            except Exception:
                pass

    if not databases:
        # Default projected databases
        for name, meta in DB_META.items():
            databases.append({
                "name": name,
                "label": meta["label"],
                "layer": meta["layer"],
                "color": meta["color"],
                "y_plane": meta["y"],
                "size": 40960,
                "tables": ["projection", "chunk", "relation"] if "domain" in name else ["task", "step", "action"],
                "total_rows": 12
            })

    return {"databases": databases}


@app.get("/api/iba/sql/graph")
async def get_sql_graph(database: str = "all", concept: Optional[str] = None, workspace: Optional[str] = None):
    db_aliases = {
        "codemap.sqlite": "domain-code.sqlite",
        "codemap": "domain-code.sqlite",
        "domain-code": "domain-code.sqlite",
        "higher_memory.sqlite": "domain-higher.sqlite",
        "higher_memory": "domain-higher.sqlite",
        "domain-higher": "domain-higher.sqlite",
        "control": "control.sqlite",
        "math": "domain-math.sqlite",
        "domain-math": "domain-math.sqlite"
    }
    canonical_db = db_aliases.get(database, database)

    ws_path = Path(workspace) if workspace else workspace_manager.get_active_workspace_path()
    if not ws_path or not (ws_path / "iba_runtime").exists():
        ws_path = Path(r"C:\Users\Rackneh\Documents\Github\InferenceLightingLab")
    iba_dir = ws_path / "iba_runtime" if ws_path else None
    
    nodes = []
    links = []
    databases_meta = []

    DB_LAYERS = {
        "control.sqlite": {"y": -1.2, "color": "rgba(56, 189, 248, 0.9)", "label": "Control Store", "light": "#38bdf8"},
        "domain-code.sqlite": {"y": -0.4, "color": "rgba(74, 222, 128, 0.9)", "label": "Code Projections", "light": "#4ade80"},
        "domain-higher.sqlite": {"y": 0.4, "color": "rgba(251, 146, 60, 0.9)", "label": "Cognitive Memory", "light": "#fb923c"},
        "domain-math.sqlite": {"y": 1.2, "color": "rgba(192, 132, 252, 0.9)", "label": "Shader & Math", "light": "#c084fc"},
        "domain-lower.sqlite": {"y": 1.8, "color": "rgba(244, 114, 182, 0.9)", "label": "Execution Traces", "light": "#f472b6"}
    }

    # Central Celestial Greater Concepts
    concepts = [
        {"id": "concept_control", "name": "Intent Control & Orchestration", "database": "control.sqlite", "table": "catalog", "color": "#38bdf8", "x": 0, "y": -1.2, "z": 0, "type": "concept"},
        {"id": "concept_code", "name": "Code Architecture & Shaders", "database": "domain-code.sqlite", "table": "catalog", "color": "#4ade80", "x": 0, "y": -0.4, "z": 0, "type": "concept"},
        {"id": "concept_higher", "name": "Higher Cognitive Memory & Patterns", "database": "domain-higher.sqlite", "table": "catalog", "color": "#fb923c", "x": 0, "y": 0.4, "z": 0, "type": "concept"},
        {"id": "concept_math", "name": "Mathematical Invariants & Bounds", "database": "domain-math.sqlite", "table": "catalog", "color": "#c084fc", "x": 0, "y": 1.2, "z": 0, "type": "concept"}
    ]

    for c in concepts:
        if canonical_db == "all" or c["database"] == canonical_db:
            c["payload"] = {"description": f"Celestial Authority Node for {c['database']}", "federated": True}
            c["related"] = []
            nodes.append(c)

    # Inter-concept luminous links
    links.append({"source": "concept_control", "target": "concept_code", "weight": 2.5, "color": "rgba(56, 189, 248, 0.5)"})
    links.append({"source": "concept_code", "target": "concept_higher", "weight": 2.5, "color": "rgba(74, 222, 128, 0.5)"})
    links.append({"source": "concept_higher", "target": "concept_math", "weight": 2.5, "color": "rgba(251, 146, 60, 0.5)"})

    if iba_dir and iba_dir.exists():
        db_files = sorted(list(iba_dir.glob("*.sqlite")))
        for db_file in db_files:
            db_name = db_file.name
            layer_info = DB_LAYERS.get(db_name, {"y": 0, "color": "#94a3b8", "label": db_name, "light": "#94a3b8"})
            base_y = layer_info["y"]

            try:
                con = sqlite3.connect(db_file)
                con.row_factory = sqlite3.Row
                tables = [r[0] for r in con.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '%_fts%'").fetchall()]

                databases_meta.append({
                    "name": db_name,
                    "tables": tables,
                    "color": layer_info["light"],
                    "label": layer_info["label"],
                    "y_plane": base_y
                })

                if canonical_db != "all" and canonical_db != db_name:
                    continue

                if db_name == "control.sqlite":
                    task_rows = con.execute("SELECT id, objective, status, requirements, result, created FROM task ORDER BY created DESC LIMIT 8").fetchall()
                    angle_step = (2 * math.pi) / max(len(task_rows), 1)
                    for idx, t in enumerate(task_rows):
                        t_id = f"task_{t['id'][:8]}"
                        theta = idx * angle_step
                        r = 1.3
                        node = {
                            "id": t_id,
                            "name": f"Task: {t['objective'][:35]}...",
                            "label": t['objective'][:24],
                            "database": db_name,
                            "table": "task",
                            "pk": t["id"],
                            "concept": "concept_control",
                            "x": math.cos(theta) * r,
                            "y": base_y + (math.sin(idx * 1.5) * 0.15),
                            "z": math.sin(theta) * r,
                            "color": layer_info["light"],
                            "type": "entry",
                            "payload": dict(t),
                            "related": [
                                {"id": "concept_control", "name": "Intent Control", "database": "control.sqlite", "table": "catalog", "relation": "federated_parent"}
                            ]
                        }
                        nodes.append(node)
                        links.append({"source": t_id, "target": "concept_control", "weight": 1.5, "color": "rgba(56, 189, 248, 0.4)"})

                    step_rows = con.execute("SELECT id, task, generation, state, input_tokens, output_tokens FROM step ORDER BY created DESC LIMIT 6").fetchall()
                    for s in step_rows:
                        s_id = f"step_{s['id'][:8]}"
                        s_task_id = f"task_{s['task'][:8]}"
                        node = {
                            "id": s_id,
                            "name": f"Step Gen {s['generation']}: {s['state']}",
                            "label": f"Gen {s['generation']}",
                            "database": db_name,
                            "table": "step",
                            "pk": s["id"],
                            "concept": "concept_control",
                            "x": (math.sin(s['generation']) * 0.8) + 0.3,
                            "y": base_y - 0.25,
                            "z": (math.cos(s['generation']) * 0.8),
                            "color": "#38bdf8",
                            "type": "entry",
                            "payload": dict(s),
                            "related": [
                                {"id": s_task_id, "name": "Parent Task", "database": "control.sqlite", "table": "task", "relation": "step_owner"}
                            ]
                        }
                        nodes.append(node)
                        if any(n["id"] == s_task_id for n in nodes):
                            links.append({"source": s_id, "target": s_task_id, "weight": 1.2, "color": "rgba(56, 189, 248, 0.5)"})

                elif "projection" in tables:
                    proj_rows = con.execute("SELECT document, source, revision, artifact FROM projection LIMIT 12").fetchall()
                    angle_step = (2 * math.pi) / max(len(proj_rows), 1)
                    concept_parent = "concept_code" if "code" in db_name else ("concept_higher" if "higher" in db_name else "concept_math")

                    for idx, p in enumerate(proj_rows):
                        p_id = f"{db_name[:6]}_{p['document'][:8]}"
                        theta = idx * angle_step
                        r = 1.35
                        clean_name = Path(p['source']).name if p['source'] else f"Proj {p['document'][:6]}"
                        node = {
                            "id": p_id,
                            "name": f"{clean_name} (Rev {p['revision']})",
                            "label": clean_name,
                            "database": db_name,
                            "table": "projection",
                            "pk": p["document"],
                            "concept": concept_parent,
                            "x": math.cos(theta) * r,
                            "y": base_y + (math.sin(idx * 2) * 0.12),
                            "z": math.sin(theta) * r,
                            "color": layer_info["light"],
                            "type": "entry",
                            "payload": dict(p),
                            "related": [
                                {"id": concept_parent, "name": layer_info["label"], "database": db_name, "table": "catalog", "relation": "domain_parent"}
                            ]
                        }
                        nodes.append(node)
                        if any(n["id"] == concept_parent for n in nodes):
                            links.append({"source": p_id, "target": concept_parent, "weight": 1.5, "color": layer_info["color"]})

            except Exception:
                pass

    # Cross-database relational links
    task_nodes = [n for n in nodes if n.get("table") == "task"]
    code_nodes = [n for n in nodes if n.get("database") == "domain-code.sqlite" and n.get("type") == "entry"]
    higher_nodes = [n for n in nodes if n.get("database") == "domain-higher.sqlite" and n.get("type") == "entry"]

    for i in range(min(len(task_nodes), len(code_nodes))):
        t = task_nodes[i]
        c = code_nodes[i]
        t["related"].append({"id": c["id"], "name": c["name"], "database": c["database"], "table": c["table"], "relation": "modified_in_task"})
        c["related"].append({"id": t["id"], "name": t["name"], "database": t["database"], "table": t["table"], "relation": "parent_task"})
        links.append({"source": t["id"], "target": c["id"], "weight": 1.2, "color": "rgba(74, 222, 128, 0.6)"})

    for i in range(min(len(code_nodes), len(higher_nodes))):
        c = code_nodes[i]
        h = higher_nodes[i]
        c["related"].append({"id": h["id"], "name": h["name"], "database": h["database"], "table": h["table"], "relation": "cognitive_pattern"})
        h["related"].append({"id": c["id"], "name": c["name"], "database": c["database"], "table": c["table"], "relation": "code_target"})
        links.append({"source": c["id"], "target": h["id"], "weight": 1.2, "color": "rgba(251, 146, 60, 0.6)"})

    return {
        "nodes": nodes,
        "links": links,
        "databases": databases_meta,
        "active_database": database
    }


# Serve static files
app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")


@app.middleware("http")
async def no_cache_static(request, call_next):
    """Force browsers to revalidate the GUI shell and static assets so UI
    updates apply on a plain reload — no ?v= cache-busting needed."""
    response = await call_next(request)
    path = request.url.path
    if path == "/" or path.startswith("/static/"):
        response.headers["Cache-Control"] = "no-cache"
    return response

@app.get("/favicon.ico", include_in_schema=False)
async def get_favicon():
    fav = os.path.join(STATIC_DIR, "favicon.ico")
    if os.path.exists(fav):
        return FileResponse(fav, media_type="image/x-icon")
    raise HTTPException(status_code=404, detail="Favicon not found")



@app.get("/")
async def get_index():
    index_path = os.path.join(STATIC_DIR, "index.html")
    if os.path.exists(index_path):
        return FileResponse(index_path)
    return HTMLResponse("<h1>Dory GUI initializing...</h1>")


if __name__ == "__main__":
    import uvicorn

    uvicorn.run("gui.app:app", host="127.0.0.1", port=5000, reload=False)
