# SQL-Assisted Local Wrapper

**A fully local, retro Windows XP / Windows Live Messenger studio and agent harness for [NVIDIA Nemotron Dory 1.0 4B](https://huggingface.co/nvidia/NVIDIA-Nemotron-Labs-Dory1.0-4B-GGUF) — with SQLite-backed, step-by-step memory.**

**Created by [Rackneh](https://github.com/Rackneh).** Retro Windows XP and WLM assets belong to Microsoft; see [Credits](#credits).

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4)
![Python](https://img.shields.io/badge/python-3.12-3776AB)
![FastAPI](https://img.shields.io/badge/backend-FastAPI-009688)
![llama.cpp](https://img.shields.io/badge/inference-llama.cpp-orange)
![Model](https://img.shields.io/badge/model-Dory%201.0%204B-76B900)

---

## Overview

**SQL-Assisted Local Wrapper** is a local-first wrapper around NVIDIA's open-weight **Dory 1.0 4B** model. It runs the model with a Dory-enabled `llama.cpp` server, then adds:

- A **FastAPI backend** and a browser-based studio.
- **IBA**, an autonomous engineering agent runtime that treats every step as a fresh model conversation built from durable SQLite state.
- A **SQLite memory system** for tasks, steps, evidence, documents, code maps, and pinned constraints.
- A **Windows XP desktop shell** and **Windows Live Messenger 2009-style chat client** as the interface.
- **SQL Authority Studio 3D**, an interactive visualization of the SQLite memory graph.
- An **OpenAI-compatible `/v1` API**, so tools like OpenCode, Continue, or the OpenAI SDK can use the model locally.
- A **sandboxed tool layer** for reading, searching, editing, and verifying files inside a project workspace.

> This repository contains the **wrapper, UI, agent runtime, and SQL memory layer**. The model weights and compiled `llama.cpp` binaries are not committed to the repository — see [Installation](#installation).

---

## Quick start

If the Python 3.12 environment, GGUF model, and a Dory-enabled `llama-server.exe` are already in place:

```powershell
# Only if models\NVIDIA-Nemotron-Labs-Dory1.0-4B-Q5_K_M.gguf is missing
python download_model.py

.\launch_gui.ps1
```

Open **http://127.0.0.1:5000**. The full setup is below.

---

## About Dory 1.0 4B

Dory is NVIDIA's open-weight **looped (latent-recurrent)** small language model:

- **4B parameters**, normalized-GPT backbone with interleaved sliding-window and full attention.
- **8 input layers + 20 shared recurrent layers + 8 output layers**. The recurrent block is applied multiple times per token, with the input re-injected at every loop.
- **Elastic inference compute:** the same checkpoint runs at **1, 2, or 4 loops**, trading speed for reasoning depth without retraining or swapping models.
- **Up to 256K context** in the model card; this wrapper defaults to a more practical **32K**.
- **Q5_K_M GGUF** checkpoint used here: ~**3.67 GB**.
- **English**, text-only input/output, released under the **OpenMDW-1.1** model license.
- Emits native `think` reasoning traces, which the studio can display live.

**2 loops** is the recommended default balance between speed and accuracy. Use **1 loop** for fast mechanical work and **4 loops** for harder reasoning or agentic tasks.

See [MODEL_CARD.md](MODEL_CARD.md) for the full NVIDIA model card, benchmarks, and license terms. This wrapper does not train or modify the model.

---

## Highlights

| Feature | Description |
|:---|:---|
| **100% local inference** | The model runs on your machine through `llama.cpp`. After setup, no cloud API is required. |
| **Elastic loop control** | Switch Dory between **1 loop** (fast), **2 loops** (balanced), and **4 loops** (deep reasoning) without swapping checkpoints. |
| **KV cache modes** | Choose `last_loop` for a smaller, faster cache or `per_loop` for full multi-loop fidelity. |
| **SQL as the source of truth** | Every agent step reads authoritative task/evidence state from SQLite instead of carrying a giant chat history. |
| **Codebase mental map** | Files read or written by the agent are indexed into SQLite with hashes, symbols, shader properties, and compute kernels. |
| **Pinned constraints** | Rules pinned to a session are injected into every step packet, even after 100+ steps. |
| **Dual inference backends** | Run on the local Dory server or switch to a local **LM Studio** server for another model. |
| **OpenAI-compatible API** | Use `http://127.0.0.1:5000/v1` (wrapper proxy) or `http://127.0.0.1:8080/v1` (direct engine). |
| **Retro UI** | Windows XP boot/logon/desktop, Start menu, taskbar, Notepad, Explorer, CMD, games, and a WLM-style contact list. |
| **SQL Authority 3D** | Explore the five IBA databases as an orbital 3D graph with cross-database relations. |
| **Workspace sandbox** | Path containment, sensitive-file shielding, dangerous-command blocking, and audit logging. |

---

## Architecture

```text
┌──────────────────────────────────────────────────────────────────────┐
│  Browser / Native WPF GUI                                            │
│  Windows XP desktop · WLM-style chat · SQL Authority 3D              │
└───────────────────────────────┬──────────────────────────────────────┘
                                │ HTTP / SSE
┌───────────────────────────────▼──────────────────────────────────────┐
│  FastAPI wrapper                               http://127.0.0.1:5000  │
│  · GUI + static assets                                               │
│  · /api/chat/stream (plain chat or IBA agent)                        │
│  · /v1/chat/completions (OpenAI-compatible proxy)                    │
│  · /api/iba/* (workspaces, sessions, tasks, SQL graph)               │
└───────────────┬───────────────────────────────┬──────────────────────┘
                │                               │
┌───────────────▼───────────────┐   ┌───────────▼──────────────────────┐
│  IBA agent runtime            │   │  Dory llama-server               │
│  · task / step / action loop  │   │  http://127.0.0.1:8080/v1        │
│  · tool execution + sandbox   │   │  model: dory                     │
│  · SQLite memory              │   │  loops: 1 / 2 / 4                │
└───────────────┬───────────────┘   └───────────┬──────────────────────┘
                │                               │
┌───────────────▼───────────────┐   ┌───────────▼──────────────────────┐
│  Workspace SQLite stores      │   │  GGUF model                      │
│  iba_runtime/control.sqlite   │   │  Dory1.0-4B-Q5_K_M.gguf          │
│  + domain projections         │   │  ~3.67 GB                        │
└───────────────────────────────┘   └──────────────────────────────────┘

Optional backend: LM Studio local server at http://127.0.0.1:1234
```

### Ports

| Port | Service |
|---:|:---|
| `5000` | Wrapper GUI, IBA API, and OpenAI-compatible proxy |
| `8080` | Direct `llama-server` OpenAI-compatible API |
| `1234` | Optional LM Studio local server |

---

## Requirements

### Hardware

- **Windows 10 or Windows 11 (x64)**. The launchers and native GUI are Windows-first.
- **RAM:** 8 GB minimum; **16 GB+ recommended** for 32K context.
- **GPU:** optional but strongly recommended. The reference setup uses an **AMD Radeon RX 580 8 GB via Vulkan**. CPU-only AVX2 works but is much slower.
- **Disk:** ~4 GB for the GGUF model, plus space for the `llama.cpp` build.

### Software

| Dependency | Why it is needed |
|:---|:---|
| **Python 3.12** | Backend, agent runtime, and scripts |
| **PowerShell 5+** or Command Prompt | Launchers |
| **Git** | Clone the wrapper and the Dory-enabled `llama.cpp` fork |
| **CMake + MSVC / Visual Studio Build Tools** | Build `llama-server.exe` |
| **Vulkan SDK** | Optional; required for the Vulkan GPU build |
| **.NET 9 SDK** | Optional; only for the native WPF GUI in `native-gui/` |

### Python dependencies

```powershell
py -3.12 -m venv .venv
.\.venv\Scripts\Activate.ps1

pip install fastapi "uvicorn[standard]" httpx pydantic openai huggingface_hub rich requests
```

Optional LM Studio backend support:

```powershell
pip install lmstudio
```

`sqlite3` and FTS5 are used from Python's standard library. Use the official **python.org** CPython build, which includes FTS5.

If PowerShell blocks the activation script:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

---

## Installation

### 1. Clone the repository

```powershell
git clone https://github.com/Rackneh/SQL-assisted-local-wrapper.git
cd SQL-assisted-local-wrapper
```

### 2. Create the Python environment

```powershell
py -3.12 -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install fastapi "uvicorn[standard]" httpx pydantic openai huggingface_hub rich requests
```

### 3. Download the model

The wrapper expects:

```text
models\NVIDIA-Nemotron-Labs-Dory1.0-4B-Q5_K_M.gguf
```

Download and verify it automatically:

```powershell
python download_model.py
```

Expected file size: **3,673,288,864 bytes**

Expected SHA-256:

```text
96080be49ffd67b916018b7bdef8c484762576fb52780e10ab98ac4978a660e0
```

You can also download it manually from the [official NVIDIA GGUF repository](https://huggingface.co/nvidia/NVIDIA-Nemotron-Labs-Dory1.0-4B-GGUF) and place it in `models/`.

Downloading and using the model means accepting NVIDIA's [OpenMDW-1.1 license](https://openmdw.ai/license/1-1/).

### 4. Build the Dory-enabled `llama.cpp` server

Dory support is **not in a stock `llama.cpp` release** at the time of writing. Build from the Dory-enabled fork at the pinned revision:

```powershell
# Skip the clone if llama.cpp-dory/ already exists.
git clone https://github.com/brianchmielNV/llama.cpp.git llama.cpp-dory
cd llama.cpp-dory

git fetch origin 98a74c97ea09ac72f6ceda1559808fc0a2f447ce
git checkout --detach FETCH_HEAD
```

#### Vulkan GPU build (recommended)

```powershell
cmake -S . -B build-vulkan `
  -DCMAKE_BUILD_TYPE=Release `
  -DGGML_VULKAN=ON `
  -DLLAMA_BUILD_SERVER=ON `
  -DLLAMA_BUILD_TESTS=OFF `
  -DLLAMA_BUILD_EXAMPLES=OFF

cmake --build build-vulkan --config Release --target llama-server --parallel
```

The wrapper looks for:

```text
llama.cpp-dory\build-vulkan\bin\Release\llama-server.exe
```

#### CPU build (fallback)

```powershell
cmake -S . -B build `
  -DCMAKE_BUILD_TYPE=Release `
  -DGGML_NATIVE=ON `
  -DLLAMA_BUILD_SERVER=ON `
  -DLLAMA_BUILD_TESTS=OFF `
  -DLLAMA_BUILD_EXAMPLES=OFF

cmake --build build --config Release --target llama-server --parallel
```

The wrapper looks for:

```text
llama.cpp-dory\build\bin\Release\llama-server.exe
```

> If Vulkan is unavailable, the wrapper automatically falls back to the CPU build. See [MODEL_CARD.md](MODEL_CARD.md) for the upstream build notes and Apple Silicon/Linux CUDA examples.

### 5. Launch the studio

```powershell
.\launch_gui.ps1
```

or:

```cmd
launch_gui.bat
```

The GUI opens at **http://127.0.0.1:5000**.

If the model file and `llama-server.exe` are present, the wrapper **auto-starts the engine** on startup (unless the LM Studio backend is selected). You can also start/stop it from the studio.

### Headless / manual launch

```powershell
# Standalone console mode: start only llama-server, without the GUI
.\start_server.ps1 -NoGui -Loops 2 -CacheMode last_loop

# Start the wrapper; it manages (and auto-starts) the engine for you
.\launch_gui.ps1
```

---

## Using the Studio

### First boot

The web UI presents a simulated Windows XP boot/logon sequence and then the studio desktop. The engine panel lets you configure:

- **Recurrent loops:** `1` (fast), `2` (recommended), `4` (deep reasoning).
- **KV cache mode:** `last_loop` (fastest) or `per_loop` (full cache).
- **CPU threads**, **context window** (4K–128K), and **GPU offload layers** (`999` = all layers).
- **Reasoning mode**, temperature, Top-P, and max output tokens.
- **Persona preset:** General Assistant, Senior Software Engineer, Math/Logic Reasoner, or Concise.

Click **Apply & Restart Engine** after changing engine settings.

### Plain chat vs. IBA agent mode

- **IBA mode ON (default):** The wrapper runs the autonomous agent loop. Each step is an isolated model conversation assembled from SQLite: the task, the latest evidence, pinned constraints, and a self-handoff briefing.
- **IBA mode OFF:** The wrapper proxies a normal streaming chat completion to Dory.

This is the core idea of the project: **the database — not the context window — is the agent's memory.** The model does not need to re-read the entire history to continue a long task.

### Workspaces

A workspace is a folder on your PC that the agent is allowed to inspect and edit.

- Add a workspace from the sidebar or the **Add Project** modal.
- Each workspace gets its own runtime store:

```text
<workspace>\iba_runtime\control.sqlite
```

- Selecting a different workspace switches the active SQL memory and code map.
- A new blank workspace can be created with a README and git repository.

### Sessions and the WLM contact list

Sessions behave like contacts in Windows Live Messenger:

- Each session has its own durable SQLite history.
- Right-click a contact for session actions.
- The default **IBA** contact is the built-in autonomous agent and cannot be deleted.
- New sessions can receive a generated WLM-style persona and task goal.

### Slash commands

Type these into the chat input:

| Command | Action |
|:---|:---|
| `/context 16k` / `32k` / `64k` / `auto` | Set the IBA prompt budget for the session |
| `/context` | Show the current target, effective tokens, model, and policy revision |
| `/pin <constraint>` | Store a constraint in SQLite and inject it into every future step |
| `/task status` | List recent IBA tasks in the session |
| `/task resume` | Resume paused tasks in the session |

### Pin Rule, Code Map, and Task Inspector

- **Pin Rule** stores operational constraints in SQLite. They are injected into every step packet.
- **Code Map** shows files indexed by the agent, including symbols, shader properties, compute kernels, and buffer bindings. Hash changes supersede stale knowledge.
- **Task Inspector** shows tasks, statuses, objectives, and step-level actions/results.

### SQL Authority Studio 3D

Open **SQL Authority Studio 3D** from the desktop or Start menu to explore the memory graph. It renders five federated databases:

| Database | Layer |
|:---|:---|
| `control.sqlite` | Intent, orchestration, tasks, steps, events |
| `domain-code.sqlite` | Code architecture, shader and file projections |
| `domain-higher.sqlite` | Higher-level patterns, strategy, pinned constraints |
| `domain-math.sqlite` | Mathematical invariants and bounds |
| `domain-lower.sqlite` | Low-level execution traces |

The 3D view supports orbital database planes, cross-database relational traversal, and an inspector for node payloads.

### Windows XP desktop apps

The web UI includes simulated versions of:

- Boot sequence, logon screen, desktop, Start menu, taskbar, and shutdown flow.
- **Windows Live Messenger**-style contacts and chat, with personas, emoticons, winks, sounds, and user tiles.
- **My Computer / Explorer**, **Notepad**, **Command Prompt**, **Calculator**, **Paint**, **Task Manager**, and **Sandbox Security Center**.
- **Minesweeper**, **Solitaire**, **FreeCell**, **Spider Solitaire**, and **Pinball**.

### Native WPF GUI (optional)

A parallel native desktop client lives in `native-gui/`:

```powershell
dotnet run --project native-gui\DoryNative.csproj
```

It targets **.NET 9 / Windows** and talks to the same FastAPI backend. The web GUI is the primary, best-tested interface.

---

## API and integrations

### OpenAI-compatible endpoint

Use the wrapper proxy:

```text
Base URL: http://127.0.0.1:5000/v1
Model:    dory
API key:  dory-a5118c2b008b1ebe80793733a7aa48ee   (default — change it)
```

Or connect directly to the engine:

```text
Base URL: http://127.0.0.1:8080/v1
Model:    dory
```

The proxy forwards streaming and non-streaming requests and keeps OpenCode/undici header timeouts alive during long prompt evaluations.

### Python example

```python
from openai import OpenAI

client = OpenAI(
    base_url="http://127.0.0.1:5000/v1",
    api_key="dory-a5118c2b008b1ebe80793733a7aa48ee",
)

response = client.chat.completions.create(
    model="dory",
    messages=[{"role": "user", "content": "Explain how Dory's recurrent loops work."}],
    max_tokens=2048,
    temperature=1.0,
    top_p=0.95,
)

print(response.choices[0].message.content)
```

### curl example

```bash
curl http://127.0.0.1:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer dory-a5118c2b008b1ebe80793733a7aa48ee" \
  -d '{
    "model": "dory",
    "messages": [{"role": "user", "content": "Write a haiku about GPUs"}],
    "stream": false
  }'
```

### OpenCode / Continue

A ready-to-edit configuration snippet is in [`opencode_config.json`](opencode_config.json):

```json
{
  "opencode": {
    "provider": "openai",
    "name": "NVIDIA Dory 1.0 4B",
    "model": "dory",
    "baseURL": "http://127.0.0.1:8080/v1",
    "apiKey": "dory-a5118c2b008b1ebe80793733a7aa48ee",
    "contextLength": 32768,
    "temperature": 1.0,
    "topP": 0.95,
    "supportsTools": true,
    "capabilities": {
      "tools": true,
      "toolCalling": true,
      "streaming": true,
      "reasoning": true
    }
  }
}
```

### LM Studio backend

The IBA runtime can switch to a local LM Studio server:

1. Start LM Studio and load a model.
2. Enable the local server on port `1234`.
3. In the studio, choose **Gemma (LM Studio)** as the IBA backend — or persist it in `~/.dory_llm/provider.json`:

```json
{
  "provider": "lmstudio",
  "lm_model": "your-model-id",
  "lm_api_key": ""
}
```

Environment variables override the file:

```powershell
$env:IBA_PROVIDER = "lmstudio"
$env:IBA_LM_MODEL = "your-model-id"
$env:LM_STUDIO_API_KEY = ""
```

Only one backend can hold the GPU at a time. Dory and LM Studio are mutually exclusive; switching takes a few seconds.

### CLI clients and tests

```powershell
# Terminal chat client (start the engine first)
.\.venv\Scripts\python.exe chat.py

# Direct llama.cpp CLI without a server
.\run_cli.ps1 -Loops 2 -CacheMode last_loop
.\run_cli.ps1 -Loops 4 -Prompt "Explain recurrent loops in neural networks"

# API smoke tests
.\.venv\Scripts\python.exe test_client.py
.\.venv\Scripts\python.exe test_tools.py
.\.venv\Scripts\python.exe test_parallel.py
.\.venv\Scripts\python.exe test_multiturn_tool.py
```

---

## Project layout

```text
SQL-assisted-local-wrapper/
├── gui/
│   ├── app.py                  # FastAPI backend, proxy, server manager, SQL graph API
│   └── static/
│       ├── index.html          # Studio single-page app
│       ├── app.js              # Chat, SSE, engine controls
│       ├── js/
│       │   ├── xp_core.js      # XP window manager, boot, audio, desktop shell
│       │   ├── xp_apps.js      # Explorer, Notepad, CMD, Calculator, Task Manager
│       │   ├── xp_games.js     # Minesweeper, Solitaire, FreeCell, Spider, Pinball
│       │   ├── xp_installer.js # XP setup/OOBE flow
│       │   ├── wlm_client.js   # Windows Live Messenger-style client
│       │   └── sql_authority.js# SQL Authority Studio 3D
│       ├── xp_assets/          # XP icons, wallpapers, sounds, games assets
│       └── wlm/                # WLM backgrounds, user tiles, emoticons, winks
├── iba/
│   ├── engine.py               # Agent decision loop and reasoning budgets
│   ├── store.py                # SQLite schema, tasks, steps, evidence, FTS5
│   ├── manager.py              # Workspaces, sessions, code map
│   ├── tools.py                # File/search/edit/check tools
│   ├── sandbox.py              # Workspace containment and command policy
│   ├── provider.py             # Dory llama-server and LM Studio providers
│   ├── bridge.py               # SSE bridge between FastAPI and IBA
│   └── protocol.py             # Tool schemas and outcome contract
├── native-gui/                 # Optional .NET 9 WPF client
├── llama.cpp-dory/             # Dory-enabled llama.cpp checkout/build
├── models/                     # GGUF model (gitignored)
├── Self-Improvement-Proposals/ # Agent-generated improvement proposals
├── launch_gui.bat / .ps1       # One-click web studio
├── start_server.bat / .ps1     # Engine launcher
├── run_cli.ps1                 # Direct llama.cpp CLI
├── chat.py                     # Rich terminal chat client
├── download_model.py           # Model downloader + SHA-256 verification
├── test_client.py              # OpenAI-compatible API tests
├── opencode_config.json        # OpenCode/Continue configuration
└── MODEL_CARD.md               # NVIDIA model card
```

---

## How the SQL memory works

Small local models degrade when they have to carry a long transcript: prompts grow, latency rises, and context compaction becomes unreliable. This wrapper takes a different approach. Durable state lives in SQLite, and each step is rebuilt from a small, relevant slice of that state. SQL is the memory; the model is the reasoning engine.

The design follows one rule:

> **SQLite is the authority; domain databases are replayable projections.**

Each workspace owns one control store at `iba_runtime/control.sqlite`. The schema includes:

- `task`, `step`, `action` — the durable agent plan and execution history.
- `artifact`, `artifact_chunk` — file contents and outputs, chunked for bounded reads.
- `document`, `evidence`, `evidence_fts` — ingested evidence with full-text search.
- `pin` — constraints injected into every step packet.
- `code_map` — hash-aware file/symbol map.
- `event` — audit/event stream.
- `catalog`, `projection_status` — domain projection metadata.

At each step the runtime builds a compact packet from SQLite rather than replaying the whole conversation:

1. The current task objective and requirements.
2. A self-handoff briefing from the previous step.
3. Relevant evidence IDs, pinned constraints, and recent results.
4. The allowed tool schema.

This keeps prompt size bounded, avoids context-compaction stalls on small local models, and makes long tasks resumable. The model can search memory with `search_evidence`, `read_evidence`, `read_document`, `query_code_map`, and `remember`.

The agent loop is intentionally conservative:

- One write/edit action at a time.
- Up to **4 independent read-only calls** batched per step.
- A default cap of **40 steps** per task.
- Required checks can block "finish" until verification passes.
- A `switch_backend` tool can move the task between Dory and LM Studio.

The **sandbox** enforces workspace path containment, blocks sensitive files (`.env`, keys, credentials), rejects high-risk system paths, filters dangerous commands, and writes audit records.

---

## Configuration reference

| Setting | Location |
|:---|:---|
| Workspace registry | `iba_workspaces.json` |
| Per-workspace memory | `<workspace>\iba_runtime\control.sqlite` |
| Backend selection | `~/.dory_llm/provider.json` |
| Backend env overrides | `IBA_PROVIDER`, `IBA_LM_MODEL`, `LM_STUDIO_API_KEY` |
| Default wrapper API key | `dory-a5118c2b008b1ebe80793733a7aa48ee` in `gui/app.py` |
| Engine binary lookup | `llama.cpp-dory\build-vulkan\...` then `llama.cpp-dory\build\...` |
| Model path | `models\NVIDIA-Nemotron-Labs-Dory1.0-4B-Q5_K_M.gguf` |
| Default context | 32K in the GUI; 32K in `start_server.ps1` |
| Default loops | 2 |
| Default KV cache | `last_loop` |

### Changing the API key

The default key is intentionally permissive for local use. Before exposing the wrapper on a LAN, change `DEFAULT_API_KEY` in `gui/app.py`, the value in `opencode_config.json`, and the key shown in the UI.

---

## Troubleshooting

| Problem | Fix |
|:---|:---|
| `Dory engine is not running` | Confirm the GGUF model and `llama-server.exe` exist, then click **Apply & Restart Engine**. |
| `llama-server.exe not found` | Build the Dory-enabled fork and place the binary at the expected `build-vulkan` or `build` path. |
| Engine exits immediately | Check `llama_server.log` / `llama_server_err.log`; verify Vulkan runtime/DLLs or use the CPU build. |
| `no such table: evidence_fts` | Your Python SQLite lacks FTS5. Use the official python.org CPython 3.12 build. |
| GPU error or out of memory | Lower context size, reduce GPU layers, or use `last_loop` cache. |
| LM Studio and Dory fight over the GPU | Stop one backend before starting the other; they are mutually exclusive. |
| PowerShell script blocked | `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` |
| Port 5000 or 8080 already in use | Run `kill_server.ps1` or change `-GuiPort` / `-Port`. |
| Model checksum mismatch | Re-download with `python download_model.py`; do not use a partial file. |

---

## Current limitations

This is an actively developed local project, not a production platform. Known limitations:

### Platform and packaging

- **Windows-first.** The launch scripts, binary paths, and native WPF GUI target Windows. Linux/macOS users must build and launch the components manually.
- **Not self-contained.** The GGUF model and compiled `llama.cpp` binaries are gitignored and must be downloaded/built separately.
- **Dory is not in a stock `llama.cpp` release yet.** The wrapper pins a Dory-enabled fork revision; a stock `llama-server` will not load the model correctly.
- **No installer or CI pipeline.** Setup is manual, and the test scripts are smoke tests rather than a full test suite.
- **No formal license file yet.** Until one is added, treat the wrapper code as all rights reserved by the author. Third-party assets and model weights keep their own licenses.

### Performance and capacity

- **One inference slot.** The engine runs with `--parallel 1`, so requests are serialized. The agent is not a parallel multi-user service.
- **GPU exclusivity.** Dory and LM Studio cannot both hold the GPU. Switching backends takes several seconds.
- **CPU fallback is slow.** Without a supported GPU, generation speed is limited. The reference Vulkan target is an 8 GB RX 580; larger contexts and GPU layers may exceed VRAM.
- **Bounded agent runs.** Tasks default to a maximum of 40 steps, and only up to 4 read-only calls can be batched per step. Very large refactors may need to be split into multiple sessions.
- **Context is clamped.** The IBA budget is constrained by the model/runtime; 64K or 128K settings may be reduced depending on available memory and backend.

### Security and data

- **Local trust model.** The wrapper binds to `0.0.0.0` and ships a default API key. There is no real user authentication, multi-tenant isolation, or TLS. **Do not expose it directly to the internet.**
- **Sandbox is best-effort.** It blocks known-sensitive paths and dangerous commands, but it is not a virtual machine or a hardened security boundary. Run the agent only on projects you are willing to let it modify.
- **Memory is unencrypted.** SQLite stores live inside the workspace. Deleting `iba_runtime` deletes that workspace's task history and memory.
- **Text-only model.** Dory 1.0 4B accepts text input. The UI can save image attachments, but true vision requires a backend/model that supports it.

### UI and features

- **The retro UI is a simulation.** Some XP/WLM apps, dialogs, assets, and flows are approximations or incomplete.
- **SQL Authority 3D is a visualizer**, not a general-purpose SQL client or database administrator.
- **Native WPF GUI is experimental.** The browser studio is the primary interface and receives the most testing.
- **English-only model.** Dory 1.0 4B is trained primarily on English.
- **No fine-tuning or training.** This project wraps and orchestrates the model; it does not train or modify weights.

---

## Credits

This project would not exist without the work of many people:

- **Microsoft** — for the **Windows XP** and **Windows Live Messenger (WLM)** visual elements, sounds, icons, wallpapers, and assets that inspire and populate the retro interface. All Microsoft trademarks, names, and assets remain the property of Microsoft. This project is not affiliated with, endorsed by, or sponsored by Microsoft.
- **GLM 2**, **Gemini 3.8**, and **DeepSeek 4.1 Flash** — for the AI-assisted work, code generation, debugging, and implementation support.
- **Rackneh** — for the wrapper, the integration work, and the **SQL database linking idea** that became the IBA memory system.
- **NVIDIA** — for the **NVIDIA Nemotron Dory 1.0 4B** model, model card, and GGUF release.
- **ggml-org / llama.cpp contributors** and **brianchmielNV** — for `llama.cpp` and the Dory-enabled fork.
- The open-source community behind FastAPI, Uvicorn, SQLite/FTS5, and the vendored frontend libraries.

### Asset notice

The `gui/static/xp_assets/` and `gui/static/wlm/` directories contain Windows XP and Windows Live Messenger-inspired assets used for a personal/educational retro interface. They are **not** covered by any license granted by this repository. If you fork or redistribute this project, verify that you have the right to use and redistribute those assets, or replace them with your own.
