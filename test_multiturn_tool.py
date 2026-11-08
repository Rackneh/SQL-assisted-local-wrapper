import json
import urllib.request

API_KEY = "dory-a5118c2b008b1ebe80793733a7aa48ee"

tools = [
    {
        "type": "function",
        "function": {
            "name": "read_file",
            "description": "Read file content from disk",
            "parameters": {
                "type": "object",
                "properties": {
                    "file_path": {
                        "type": "string",
                        "description": "Path of file to read",
                    }
                },
                "required": ["file_path"],
            },
        },
    }
]

messages = [
    {"role": "user", "content": "Please read file /etc/hosts with the tool."},
    {
        "role": "assistant",
        "content": "",
        "tool_calls": [
            {
                "id": "BfOkOfUDxlorEPic9zThOayY0fxFucGO",
                "type": "function",
                "function": {
                    "name": "read_file",
                    "arguments": '{"file_path": "/etc/hosts"}',
                },
            }
        ],
    },
    {
        "role": "tool",
        "tool_call_id": "BfOkOfUDxlorEPic9zThOayY0fxFucGO",
        "content": "127.0.0.1 localhost\n192.168.1.50 myserver.local",
    },
]

payload = {
    "model": "dory",
    "messages": messages,
    "tools": tools,
    "max_tokens": 128,
    "temperature": 0.7,
}

req = urllib.request.Request(
    "http://127.0.0.1:8080/v1/chat/completions",
    data=json.dumps(payload).encode("utf-8"),
    headers={
        "Content-Type": "application/json",
        "Authorization": f"Bearer {API_KEY}",
    },
)

with urllib.request.urlopen(req, timeout=120) as resp:
    data = json.loads(resp.read().decode("utf-8"))
    msg = data["choices"][0]["message"]
    print("=== Multi-Turn Tool Result ===")
    print("Finish reason:", data["choices"][0].get("finish_reason"))
    print("Content:", msg.get("content"))
