"""
Test script for Tool Calling with NVIDIA Nemotron Dory 1.0 4B.
Verifies that Dory can receive tool definitions, reason, and trigger tool calls.
"""

import json
import os
import subprocess
import time
import urllib.request

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
LLAMA_SERVER = os.path.join(
    BASE_DIR, "llama.cpp-dory", "build", "bin", "Release", "llama-server.exe"
)
MODEL_FILE = os.path.join(
    BASE_DIR, "models", "NVIDIA-Nemotron-Labs-Dory1.0-4B-Q5_K_M.gguf"
)
API_KEY = "dory-a5118c2b008b1ebe80793733a7aa48ee"
PORT = 8080


def ensure_server_running():
    try:
        req = urllib.request.Request(
            f"http://127.0.0.1:{PORT}/health",
            headers={"Authorization": f"Bearer {API_KEY}"},
        )
        with urllib.request.urlopen(req, timeout=2) as r:
            if r.status == 200:
                print("[+] llama-server is already running.")
                return None
    except Exception:
        pass

    print("[*] Starting llama-server for tool testing...")
    cmd = [
        LLAMA_SERVER,
        "-m",
        MODEL_FILE,
        "--alias",
        "dory",
        "--host",
        "0.0.0.0",
        "--port",
        str(PORT),
        "--api-key",
        API_KEY,
        "-t",
        "6",
        "--ctx-size",
        "4096",
        "--parallel",
        "1",
        "--batch-size",
        "512",
        "--ubatch-size",
        "512",
        "--flash-attn",
        "off",
        "--no-context-shift",
        "--jinja",
        "--reasoning",
        "on",
        "--temp",
        "0.7",
        "--top-p",
        "0.95",
        "--top-k",
        "0",
        "--min-p",
        "0",
        "--repeat-penalty",
        "1",
        "--override-kv",
        "dory.num_loops=int:2,dory.recurrent_kv_cache_mode=str:per_loop",
    ]

    proc = subprocess.Popen(
        cmd,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=subprocess.CREATE_NEW_PROCESS_GROUP
        if os.name == "nt"
        else 0,
    )

    print("[*] Waiting for server to initialize model...")
    for _ in range(40):
        time.sleep(1)
        try:
            req = urllib.request.Request(
                f"http://127.0.0.1:{PORT}/health",
                headers={"Authorization": f"Bearer {API_KEY}"},
            )
            with urllib.request.urlopen(req, timeout=2) as r:
                if r.status == 200:
                    print("[+] Server ready!")
                    return proc
        except Exception:
            pass

    print("[-] Server failed to respond.")
    return proc


def test_tool_call():
    proc = ensure_server_running()

    tools = [
        {
            "type": "function",
            "function": {
                "name": "run_bash_command",
                "description": "Execute a bash shell command in the operating system environment",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "command": {
                            "type": "string",
                            "description": "The command line string to run",
                        }
                    },
                    "required": ["command"],
                },
            },
        },
        {
            "type": "function",
            "function": {
                "name": "read_file",
                "description": "Read the content of a file from disk",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "path": {
                            "type": "string",
                            "description": "Absolute or relative file path",
                        }
                    },
                    "required": ["path"],
                },
            },
        },
    ]

    messages = [
        {
            "role": "user",
            "content": "Use the available tools to list all files in directory '/workspace/project'.",
        }
    ]

    payload = {
        "model": "dory",
        "messages": messages,
        "tools": tools,
        "tool_choice": "auto",
        "temperature": 0.7,
        "top_p": 0.95,
        "max_tokens": 512,
        "chat_template_kwargs": {"force_nonempty_content": True},
    }

    print("\n[*] Sending tool calling request to http://127.0.0.1:8080/v1/chat/completions...")
    req = urllib.request.Request(
        f"http://127.0.0.1:{PORT}/v1/chat/completions",
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {API_KEY}",
        },
    )

    start = time.time()
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            elapsed = time.time() - start
            print(f"[+] Response received in {elapsed:.2f}s!")

            choice = data["choices"][0]
            msg = choice["message"]

            print("\n================== RESPONSE DETAILS ==================")
            print(f"Finish Reason: {choice.get('finish_reason')}")
            print(f"Role: {msg.get('role')}")

            if msg.get("reasoning_content"):
                print(f"\n[Reasoning Trace ({len(msg['reasoning_content'])} chars)]:")
                print(msg["reasoning_content"][:300] + "...")

            print(f"\n[Content]: {msg.get('content')}")

            tool_calls = msg.get("tool_calls")
            if tool_calls:
                print(f"\n[SUCCESS: Model Generated {len(tool_calls)} Tool Call(s)]:")
                for i, tc in enumerate(tool_calls):
                    fn = tc.get("function", {})
                    print(f"  {i+1}. Function: {fn.get('name')}")
                    print(f"     Arguments: {fn.get('arguments')}")
                    print(f"     Call ID:   {tc.get('id')}")
            else:
                print("\n[!] No tool_calls array in message.")
                print("Full message:", json.dumps(msg, indent=2))

    except Exception as e:
        print("[-] Error during tool call request:", e)


if __name__ == "__main__":
    test_tool_call()
