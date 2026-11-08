"""
Test client demonstrating chat, streaming, and tool calling with NVIDIA Dory 1.0 4B.
Matches the official Hugging Face repository usage specifications.
"""

from openai import OpenAI
import requests

BASE_URL = "http://127.0.0.1:8080/v1"
API_KEY = "dory-a5118c2b008b1ebe80793733a7aa48ee"
MODEL = "dory"
MAX_TOKENS = 2048


def test_health():
    print("=== Testing Server Health ===")
    try:
        r = requests.get(
            "http://127.0.0.1:8080/health",
            headers={"Authorization": f"Bearer {API_KEY}"},
            timeout=5,
        )
        print(f"Health response status: {r.status_code}")
        print(f"Health response body: {r.text}")
        return r.status_code == 200
    except Exception as e:
        print(f"Health check failed: {e}")
        return False


def test_chat(client):
    print("\n=== Testing Chat Completion ===")
    response = client.chat.completions.create(
        model=MODEL,
        messages=[{"role": "user", "content": "Write a haiku about GPUs"}],
        max_tokens=MAX_TOKENS,
        temperature=1.0,
        top_p=0.95,
    )
    print("Response:\n" + response.choices[0].message.content)


def test_streaming(client):
    print("\n=== Testing Streaming Completion ===")
    stream = client.chat.completions.create(
        model=MODEL,
        messages=[
            {
                "role": "user",
                "content": "Explain how a transformer works in two sentences",
            }
        ],
        max_tokens=MAX_TOKENS,
        temperature=1.0,
        top_p=0.95,
        stream=True,
    )
    for chunk in stream:
        delta = chunk.choices[0].delta
        reasoning = (
            getattr(delta, "reasoning_content", None)
            or getattr(delta, "reasoning", None)
            or ""
        )
        content = delta.content or ""
        if reasoning:
            print(f"[Thought: {reasoning}]", end="", flush=True)
        if content:
            print(content, end="", flush=True)
    print()


def test_tools(client):
    print("\n=== Testing Tool Calling ===")
    tools = [
        {
            "type": "function",
            "function": {
                "name": "get_weather",
                "description": "Get the current weather for a city",
                "parameters": {
                    "type": "object",
                    "properties": {"city": {"type": "string"}},
                    "required": ["city"],
                },
            },
        }
    ]

    response = client.chat.completions.create(
        model=MODEL,
        messages=[{"role": "user", "content": "What's the weather in Santa Clara?"}],
        tools=tools,
        max_tokens=MAX_TOKENS,
        temperature=1.0,
        top_p=0.95,
        extra_body={"chat_template_kwargs": {"force_nonempty_content": True}},
    )
    print("Tool calls response:")
    print(response.choices[0].message.tool_calls)


def main():
    if not test_health():
        print(
            "\n[!] Server is not reachable at http://127.0.0.1:8080. Start the server first with .\\start_server.ps1"
        )
        return

    client = OpenAI(base_url=BASE_URL, api_key=API_KEY)
    test_chat(client)
    test_streaming(client)
    test_tools(client)


if __name__ == "__main__":
    main()
