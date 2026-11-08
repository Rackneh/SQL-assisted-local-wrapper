"""
Interactive Chat Client for NVIDIA Dory 1.0 4B.
Connects to the local llama-server running at http://127.0.0.1:8080/v1.
"""

import sys
from openai import OpenAI
from rich.console import Console
from rich.markdown import Markdown
from rich.panel import Panel
from rich.prompt import Prompt

BASE_URL = "http://127.0.0.1:8080/v1"
API_KEY = "dory-a5118c2b008b1ebe80793733a7aa48ee"
MODEL_NAME = "dory"
MAX_TOKENS = 2048

console = Console()


def main():
    console.print(
        Panel.fit(
            "[bold cyan]NVIDIA Nemotron Labs Dory 1.0 4B[/bold cyan]\n"
            "[green]Connected to local llama-server: http://127.0.0.1:8080/v1[/green]\n"
            f"[dim]API Key: {API_KEY[:8]}...[/dim]\n"
            "[dim]Commands: [bold]/reset[/bold] (clear chat history), [bold]/exit[/bold] (quit)[/dim]",
            title="🐟 Dory Loop Model Chat",
            border_style="cyan",
        )
    )

    client = OpenAI(base_url=BASE_URL, api_key=API_KEY)

    # Verify server reachable
    try:
        models = client.models.list()
        available = [m.id for m in models.data]
        console.print(f"[dim]Available server models: {', '.join(available)}[/dim]\n")
    except Exception as e:
        console.print(
            f"[bold red]Cannot connect to server at {BASE_URL}![/bold red]\n"
            f"[yellow]Make sure the server is running by executing: .\\start_server.ps1 (or .\\start_server.bat)[/yellow]\n"
            f"[dim]Error: {e}[/dim]"
        )
        sys.exit(1)

    messages = [
        {
            "role": "system",
            "content": "You are Dory, a helpful, precise, and thoughtful AI assistant developed by NVIDIA.",
        }
    ]

    while True:
        try:
            user_input = Prompt.ask("\n[bold green]You[/bold green]").strip()
        except (KeyboardInterrupt, EOFError):
            console.print("\n[dim]Exiting chat. Goodbye![/dim]")
            break

        if not user_input:
            continue

        if user_input.lower() in ("/exit", "/quit"):
            console.print("[dim]Exiting chat. Goodbye![/dim]")
            break

        if user_input.lower() == "/reset":
            messages = [
                {
                    "role": "system",
                    "content": "You are Dory, a helpful, precise, and thoughtful AI assistant developed by NVIDIA.",
                }
            ]
            console.print("[yellow]Conversation history cleared.[/yellow]")
            continue

        messages.append({"role": "user", "content": user_input})
        console.print("\n[bold cyan]Dory[/bold cyan]:", end=" ")

        try:
            stream = client.chat.completions.create(
                model=MODEL_NAME,
                messages=messages,
                max_tokens=MAX_TOKENS,
                temperature=1.0,
                top_p=0.95,
                stream=True,
            )

            full_reply = ""
            in_thinking = False
            for chunk in stream:
                delta = chunk.choices[0].delta
                reasoning = (
                    getattr(delta, "reasoning_content", None)
                    or getattr(delta, "reasoning", None)
                    or ""
                )
                content = delta.content or ""

                if reasoning:
                    if not in_thinking:
                        console.print(
                            "\n[dim cyan]🧠 [Thinking]:[/dim cyan] ", end=""
                        )
                        in_thinking = True
                    console.print(f"[dim cyan]{reasoning}[/dim cyan]", end="")

                if content:
                    if in_thinking:
                        console.print(
                            "\n\n[bold cyan]Dory[/bold cyan]: ", end=""
                        )
                        in_thinking = False
                    print(content, end="", flush=True)
                    full_reply += content

            print()
            messages.append({"role": "assistant", "content": full_reply})

        except Exception as e:
            console.print(f"\n[bold red]Error generating response:[/bold red] {e}")


if __name__ == "__main__":
    main()
