"""Exact token accounting and stateless inference against Dory llama-server or LM Studio."""
from contextlib import contextmanager
from pathlib import Path
from urllib.parse import urlparse
import json
import os
import re
import httpx
import time
from .protocol import outcome_schema


def is_local_url(url):
    return urlparse(url or "http://127.0.0.1:8080").hostname in ("localhost", "127.0.0.1", "::1")


def provider_config_path():
    return Path.home() / ".dory_llm" / "provider.json"


def provider_config():
    """Resolve which inference backend IBA should use.

    Sources, highest precedence first:
      env IBA_PROVIDER / IBA_LM_MODEL / LM_STUDIO_API_KEY
      ~/.dory_llm/provider.json  {"provider": "auto|dory|lmstudio",
                                  "lm_model": "...", "lm_api_key": "..."}
    Only one backend can hold the GPU at a time; "dory" and "lmstudio" are
    mutually exclusive, "auto" keeps the historical Dory-first fallback.
    """
    cfg = {"provider": "auto", "lm_model": "", "lm_api_key": ""}
    cfg_path = provider_config_path()
    try:
        if cfg_path.exists():
            cfg.update(json.loads(cfg_path.read_text(encoding="utf-8")))
    except Exception:
        pass
    if os.environ.get("IBA_PROVIDER"):
        cfg["provider"] = os.environ["IBA_PROVIDER"].strip().lower()
    if os.environ.get("IBA_LM_MODEL"):
        cfg["lm_model"] = os.environ["IBA_LM_MODEL"].strip()
    if os.environ.get("LM_STUDIO_API_KEY"):
        cfg["lm_api_key"] = os.environ["LM_STUDIO_API_KEY"].strip()
    return cfg


def write_provider_config(provider, lm_model=None, lm_api_key=None):
    """Persist the IBA backend selection to ~/.dory_llm/provider.json."""
    cfg = provider_config()
    cfg["provider"] = provider
    if lm_model is not None:
        cfg["lm_model"] = lm_model
    if lm_api_key is not None:
        cfg["lm_api_key"] = lm_api_key
    path = provider_config_path()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(cfg, indent=2), encoding="utf-8")
    return cfg


def effective_provider_kind():
    """'dory' | 'lmstudio' — the backend that will actually be used."""
    kind = provider_config().get("provider", "auto")
    if kind in ("dory", "lmstudio"):
        return kind
    # auto: Dory wins when its engine responds, else LM Studio.
    try:
        with httpx.Client(timeout=1.0) as client:
            if client.get("http://127.0.0.1:8080/props").status_code == 200:
                return "dory"
    except Exception:
        pass
    try:
        with httpx.Client(timeout=1.0) as client:
            if client.get("http://127.0.0.1:1234/api/v1/models").status_code in (200, 401):
                return "lmstudio"
    except Exception:
        pass
    return "dory"


class DoryLlamaServer:
    """Direct provider connecting to Dory llama-server on port 8080 or port 5000."""
    def __init__(self, base_url="http://127.0.0.1:8080", model="dory", api_key=""):
        self.base_url = (base_url or "http://127.0.0.1:8080").rstrip("/")
        self.model = model or "dory"
        self.api_key = api_key

    def inference_kind(self):
        return "dory"

    def inference_slots(self):
        return 1

    @contextmanager
    def session(self):
        # Verify connectivity and query loaded model props
        headers = {}
        if self.api_key:
            headers["Authorization"] = f"Bearer {self.api_key}"

        props_url = f"{self.base_url}/props"
        ctx_size = 32768
        alias = self.model
        try:
            with httpx.Client(timeout=4.0) as client:
                resp = client.get(props_url, headers=headers)
                if resp.status_code == 200:
                    data = resp.json()
                    ctx_size = data.get("default_generation_settings", {}).get("n_ctx", 32768)
                    alias = data.get("model_alias") or self.model
        except Exception:
            pass

        yield _LoadedDoryServer(self.base_url, alias, ctx_size, self.api_key)


class _LoadedDoryServer:
    def __init__(self, base_url: str, identifier: str, context_length: int, api_key: str = ""):
        self.base_url = base_url
        self.identifier = identifier
        self.context_length = context_length
        self.api_key = api_key
        self.headers = {"Content-Type": "application/json"}
        if self.api_key:
            self.headers["Authorization"] = f"Bearer {self.api_key}"

    def _format_messages_text(self, messages: list) -> str:
        # Standard ChatML formatting matching llama-server jinja template
        text = ""
        for m in messages:
            role = m.get("role", "user")
            content = m.get("content", "")
            text += f"<|im_start|>{role}\n{content}<|im_end|>\n"
        text += "<|im_start|>assistant\n"
        return text

    def count(self, messages: list) -> int:
        formatted = self._format_messages_text(messages)
        try:
            with httpx.Client(timeout=5.0) as client:
                resp = client.post(
                    f"{self.base_url}/tokenize",
                    json={"content": formatted},
                    headers=self.headers,
                )
                if resp.status_code == 200:
                    data = resp.json()
                    tokens = data.get("tokens", [])
                    return len(tokens)
        except Exception:
            pass
        # Fallback character approximation if tokenize endpoint is unreachable
        return max(1, len(formatted) // 4)

    def predict(self, messages: list, max_tokens: int, on_thought=None, on_content=None,
                reasoning_budget=None, reasoning_budget_message=None) -> dict:
        url = f"{self.base_url}/v1/chat/completions"
        payload = {
            "model": self.identifier,
            "messages": messages,
            "temperature": 0.2,
            "top_p": 0.95,
            "max_tokens": max_tokens,
            "stream": True,
            # Dory's template understands this hint (it appends an efficiency cue
            # to the last user message). Other templates ignore unknown kwargs.
            "chat_template_kwargs": {"medium_effort": True},
        }
        if reasoning_budget is not None:
            # Hard stop for runaway analysis: llama-server forces the thinking
            # end tag once the budget is spent, then the model must answer.
            payload["reasoning_budget_tokens"] = max(32, int(reasoning_budget))
            if reasoning_budget_message:
                payload["reasoning_budget_message"] = reasoning_budget_message

        full_content = []
        full_reasoning = []
        in_think_tag = False
        think_buffer = ""
        token_count = 0
        finish_reason = "stop"

        with httpx.Client(timeout=300.0) as client:
            with client.stream("POST", url, json=payload, headers=self.headers) as resp:
                if resp.status_code != 200:
                    err = resp.read().decode("utf-8", errors="replace")
                    raise RuntimeError(f"Engine inference error ({resp.status_code}): {err}")

                for line in resp.iter_lines():
                    if not line or not line.startswith("data: "):
                        continue
                    data_str = line[6:].strip()
                    if data_str == "[DONE]":
                        break
                    try:
                        chunk = json.loads(data_str)
                        choice = chunk.get("choices", [{}])[0]
                        finish = choice.get("finish_reason")
                        if finish:
                            finish_reason = finish
                        delta = choice.get("delta", {})

                        reasoning_chunk = (
                            delta.get("reasoning_content")
                            or delta.get("reasoning")
                            or ""
                        )
                        content_chunk = delta.get("content") or ""

                        # 1. Native reasoning_content from llama-server
                        if reasoning_chunk:
                            token_count += 1
                            full_reasoning.append(reasoning_chunk)
                            if on_thought:
                                on_thought(reasoning_chunk)

                        # 2. Content chunks (handling any embedded <think> tags)
                        if content_chunk:
                            token_count += 1
                            combined = think_buffer + content_chunk
                            if "<think>" in combined and not in_think_tag:
                                in_think_tag = True
                                parts = combined.split("<think>", 1)
                                if parts[0]:
                                    full_content.append(parts[0])
                                    if on_content:
                                        on_content(parts[0])
                                think_buffer = ""
                                content_chunk = parts[1]

                            if "</think>" in content_chunk:
                                in_think_tag = False
                                parts = content_chunk.split("</think>", 1)
                                if parts[0]:
                                    full_reasoning.append(parts[0])
                                    if on_thought:
                                        on_thought(parts[0])
                                if full_content:
                                    leaked = "".join(full_content)
                                    full_reasoning.append(leaked)
                                    full_content.clear()
                                if parts[1]:
                                    full_content.append(parts[1])
                                    if on_content:
                                        on_content(parts[1])
                                continue

                            if in_think_tag:
                                full_reasoning.append(content_chunk)
                                if on_thought:
                                    on_thought(content_chunk)
                            else:
                                full_content.append(content_chunk)
                                if on_content:
                                    on_content(content_chunk)
                    except Exception:
                        continue

        content_str = "".join(full_content)
        if "</think>" in content_str:
            pre_t, post_t = content_str.rsplit("</think>", 1)
            if pre_t:
                full_reasoning.append(pre_t)
            content_str = post_t.strip()
        reasoning_str = "".join(full_reasoning)
        stop_reason = "stop" if finish_reason in ("stop", "stopStringFound", "eosFound") else finish_reason

        return {
            "content": content_str,
            "reasoning": reasoning_str,
            "stop_reason": stop_reason,
            "output_tokens": token_count,
        }


class LMStudio:
    """LM Studio provider connecting via the lmstudio Python SDK."""
    def __init__(self, base_url="http://127.0.0.1:1234", model="", api_key=""):
        parsed = urlparse(base_url or "http://127.0.0.1:1234")
        if parsed.hostname not in ("localhost", "127.0.0.1", "::1"):
            raise ValueError("Durable local IBA requires a loopback LM Studio address")
        self.host = parsed.netloc
        self.model = model
        self.api_key = api_key

    def inference_kind(self):
        return "lmstudio"

    def inference_slots(self):
        return 2

    @contextmanager
    def session(self):
        try:
            import lmstudio as lms
        except ImportError as exc:
            raise RuntimeError("Install requirements-iba.txt or lmstudio SDK, start LM Studio and load a model.") from exc
        # IBA prompts are large and local inference is slow; the SDK's default
        # sync timeout kills long predictions with an empty-message
        # LMStudioTimeoutError. Give it a generous ceiling instead.
        try:
            lms.set_sync_api_timeout(1800)
        except Exception:
            pass
        with lms.Client(self.host, api_token=self.api_key or None) as client:
            models = list(client.llm.list_loaded())
            if self.model:
                wanted = self.model.lower()
                matches = [m for m in models
                           if m.identifier == self.model
                           or wanted in m.identifier.lower()
                           or m.identifier.lower() in wanted]
            else:
                matches = list(models)
            # Auto-load the configured model when nothing usable is resident —
            # LM Studio unloads idle instances, and JIT loading only happens on
            # the REST chat path, not the SDK respond() path used here.
            if not matches and self.model:
                wanted = self.model.lower()
                keys_to_try = [self.model]
                try:
                    for dl in client.llm.list_downloaded():
                        key = getattr(dl, "key", "") or ""
                        if key and (key.lower() == wanted or wanted in key.lower() or key.lower() in wanted):
                            keys_to_try.append(key)
                except Exception:
                    pass
                for key in keys_to_try:
                    try:
                        client.llm.load_new_instance(key, config={"context_length": 32768})
                        break
                    except Exception:
                        continue
                try:
                    models = list(client.llm.list_loaded())
                    matches = [m for m in models
                               if m.identifier == self.model
                               or wanted in m.identifier.lower()
                               or m.identifier.lower() in wanted]
                except Exception:
                    pass
            if len(matches) != 1:
                available = ", ".join(m.identifier for m in models) or "(none)"
                raise RuntimeError("Load the configured LM Studio model. Loaded instances: " + available)
            yield _LoadedLMStudio(lms, matches[0])


class _LoadedLMStudio:
    def __init__(self, lms, model):
        self.lms, self.model = lms, model
        self.context_length = model.get_context_length()
        self.identifier = model.identifier

    def chat(self, messages):
        return self.lms.Chat.from_history({"messages": messages})

    def count(self, messages):
        return len(self.model.tokenize(self.model.apply_prompt_template(self.chat(messages))))

    def predict(self, messages, max_tokens, on_thought=None, on_content=None,
                reasoning_budget=None, reasoning_budget_message=None):
        public_fragments = []
        reasoning_fragments = []

        def receive(fragment):
            if fragment.reasoning_type == "none":
                public_fragments.append(fragment.content)
                if on_content:
                    on_content(fragment.content)
            else:
                reasoning_fragments.append(fragment.content)
                if on_thought:
                    on_thought(fragment.content)

        # In Variable NewConv mode the last message is a tool-result turn, not
        # the STATE JSON — scan backwards for the newest parseable state so the
        # grammar schema still reflects allowed kinds, requirements and evidence.
        conv_state = None
        for m in reversed(messages):
            if m.get("role") == "user":
                try:
                    conv_state = json.loads(m["content"])
                    break
                except Exception:
                    continue
        result = self.model.respond(
            self.chat(messages),
            config={
                "temperature": 0.2,
                "maxTokens": max_tokens,
                "contextOverflowPolicy": "stopAtLimit",
            },
            response_format=outcome_schema(conv_state or {"allowed_kinds": ["action", "finish", "wait"]}),
            on_prediction_fragment=receive,
        )
        return {
            "content": "".join(public_fragments),
            "reasoning": "".join(reasoning_fragments),
            "stop_reason": result.stats.stop_reason,
            "output_tokens": result.stats.predicted_tokens_count,
        }


class LMStudioHTTP:
    """Stateless OpenAI-compatible provider for LM Studio's REST server.

    Unlike the SDK path, each predict is an independent HTTP request — LM
    Studio's JIT loader manages model residency, so an idle unload can never
    strand a half-open SDK session or kill a prediction mid-stream.
    """
    def __init__(self, base_url="http://127.0.0.1:1234", model="", api_key=""):
        parsed = urlparse(base_url or "http://127.0.0.1:1234")
        if parsed.hostname not in ("localhost", "127.0.0.1", "::1"):
            raise ValueError("Durable local IBA requires a loopback LM Studio address")
        self.base_url = (base_url or "http://127.0.0.1:1234").rstrip("/")
        self.model = model
        self.api_key = api_key
        self.headers = {"Content-Type": "application/json"}
        if self.api_key:
            self.headers["Authorization"] = f"Bearer {self.api_key}"

    def inference_kind(self):
        return "lmstudio"

    def inference_slots(self):
        # This GPU sustains two concurrent Gemma inferences.
        return 2

    def _find_model(self):
        with httpx.Client(timeout=5.0) as client:
            resp = client.get(f"{self.base_url}/api/v1/models", headers=self.headers)
            resp.raise_for_status()
            wanted = (self.model or "").lower()
            for m in resp.json().get("models", []):
                if m.get("type") != "llm":
                    continue
                key = (m.get("key") or "").lower()
                if not wanted or key == wanted or wanted in key or key in wanted:
                    return m
        return None

    @contextmanager
    def session(self):
        info = self._find_model()
        if not info:
            raise RuntimeError("LM Studio model not found: " + (self.model or "(any)"))
        ctx = int(info.get("max_context_length") or 32768)
        for inst in info.get("loaded_instances") or []:
            ic = inst.get("config", {}).get("context_length") if isinstance(inst, dict) else None
            if ic:
                ctx = min(ctx, int(ic))
        # Cap reported context at 32K — IBA policies target 32K anyway and a
        # smaller reported window keeps prompt budgets honest on this GPU.
        yield _LoadedLMStudioHTTP(self.base_url, info["key"], min(ctx, 32768), self.api_key)


class _LoadedLMStudioHTTP:
    def __init__(self, base_url, identifier, context_length, api_key=""):
        self.base_url = base_url
        self.identifier = identifier
        self.context_length = context_length
        self.headers = {"Content-Type": "application/json"}
        if api_key:
            self.headers["Authorization"] = f"Bearer {api_key}"

    def count(self, messages):
        # No REST tokenize endpoint; ~3 chars/token is conservative for code/JSON
        # so budget checks trim slightly early rather than overflow.
        total = sum(len(m.get("content") or "") for m in messages)
        return max(1, total // 3)

    def predict(self, messages, max_tokens, on_thought=None, on_content=None,
                reasoning_budget=None, reasoning_budget_message=None):
        url = f"{self.base_url}/v1/chat/completions"
        payload = {
            "model": self.identifier,
            "messages": messages,
            "temperature": 0.2,
            "top_p": 0.95,
            "max_tokens": max_tokens,
            "stream": True,
        }
        full_content = []
        full_reasoning = []
        token_count = 0
        finish_reason = "stop"
        with httpx.Client(timeout=httpx.Timeout(1800.0, connect=10.0)) as client:
            with client.stream("POST", url, json=payload, headers=self.headers) as resp:
                if resp.status_code != 200:
                    err = resp.read().decode("utf-8", errors="replace")
                    raise RuntimeError(f"LM Studio inference error ({resp.status_code}): {err[:500]}")
                for line in resp.iter_lines():
                    if not line or not line.startswith("data: "):
                        continue
                    data_str = line[6:].strip()
                    if data_str == "[DONE]":
                        break
                    try:
                        chunk = json.loads(data_str)
                    except Exception:
                        continue
                    choice = (chunk.get("choices") or [{}])[0]
                    if choice.get("finish_reason"):
                        finish_reason = choice["finish_reason"]
                    delta = choice.get("delta") or {}
                    usage = chunk.get("usage") or {}
                    if usage.get("completion_tokens"):
                        token_count = usage["completion_tokens"]
                    r = delta.get("reasoning_content") or delta.get("reasoning") or ""
                    c = delta.get("content") or ""
                    if r:
                        full_reasoning.append(r)
                        if on_thought:
                            on_thought(r)
                    if c:
                        full_content.append(c)
                        if on_content:
                            on_content(c)
        return {
            "content": "".join(full_content),
            "reasoning": "".join(full_reasoning),
            "stop_reason": finish_reason if finish_reason in ("stop", "stopStringFound", "eosFound", "length") else "stop",
            "output_tokens": token_count,
        }


class DynamicProvider:
    """Provider facade that re-resolves the configured backend on every
    session(). Runtime calls session() once per outer step, so a
    switch_backend action takes effect at the next step boundary without
    restarting the chat."""
    def __init__(self, port: int = 8080, api_key: str = ""):
        self.port = port
        self.api_key = api_key
        self._kind_cache = (0.0, "dory")

    def inference_kind(self):
        """Configured backend kind, cached briefly — effective_provider_kind
        can probe HTTP endpoints and run() asks once per step."""
        now = time.time()
        if now - self._kind_cache[0] > 5.0:
            try:
                self._kind_cache = (now, effective_provider_kind())
            except Exception:
                self._kind_cache = (now, "dory")
        return self._kind_cache[1]

    def inference_slots(self):
        """Concurrent inferences this backend's GPU can sustain."""
        return 2 if self.inference_kind() == "lmstudio" else 1

    @contextmanager
    def session(self):
        with _resolve_provider(self.port, self.api_key).session() as model:
            yield model


def get_default_provider(port: int = 8080, api_key: str = ""):
    """Return a provider that follows ~/.dory_llm/provider.json dynamically."""
    return DynamicProvider(port, api_key)


def _evict_lmstudio():
    """Best-effort VRAM eviction: unload any resident LM Studio instances.
    Called when the config explicitly pins 'dory' so a stale in-flight
    LMStudio session cannot JIT-reload a model next to llama-server."""
    try:
        unload_lmstudio_models()
    except Exception:
        pass


def _resolve_provider(port: int = 8080, api_key: str = ""):
    """Concrete provider for the currently configured backend."""
    cfg = provider_config()
    kind = cfg.get("provider", "auto")

    if kind == "lmstudio":
        token = cfg.get("lm_api_key") or (api_key if api_key.startswith("sk-lm") else "")
        return LMStudioHTTP("http://127.0.0.1:1234", model=cfg.get("lm_model", ""), api_key=token)

    # kind == "dory" or "auto": prefer llama-server when it responds.
    try:
        with httpx.Client(timeout=1.0) as client:
            r = client.get(f"http://127.0.0.1:{port}/props")
            if r.status_code == 200:
                if kind == "dory":
                    _evict_lmstudio()
                return DoryLlamaServer(f"http://127.0.0.1:{port}", "dory", api_key)
    except Exception:
        pass

    if kind != "dory":
        # Check port 1234 (LM Studio)
        try:
            with httpx.Client(timeout=1.0) as client:
                r = client.get("http://127.0.0.1:1234/api/v1/models")
                if r.status_code in (200, 401):
                    token = cfg.get("lm_api_key") or (api_key if api_key.startswith("sk-lm") else "")
                    return LMStudioHTTP("http://127.0.0.1:1234", model=cfg.get("lm_model", ""), api_key=token)
        except Exception:
            pass

    # Default to DoryLlamaServer on configured port
    return DoryLlamaServer(f"http://127.0.0.1:{port}", "dory", api_key)


# ---------------------------------------------------------------------------
# GPU-exclusive backend switching
#
# Dory (llama-server) and LM Studio cannot share the GPU. switch_backend()
# ejects whichever model is resident BEFORE starting the target, then flips
# ~/.dory_llm/provider.json so the next provider.session() resolves the new
# backend. The Dory engine lifecycle goes through the running GUI's own
# /api/server/* endpoints so logs and status stay in one place.
# ---------------------------------------------------------------------------

GUI_BASE = os.environ.get("IBA_GUI_URL", "http://127.0.0.1:5000").rstrip("/")


def _lm_headers():
    token = provider_config().get("lm_api_key") or os.environ.get("LM_STUDIO_API_KEY") or ""
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    return headers


def lmstudio_instances():
    """All currently loaded LM Studio model instances: [{model, instance_id}]."""
    out = []
    with httpx.Client(timeout=5.0) as client:
        resp = client.get("http://127.0.0.1:1234/api/v1/models", headers=_lm_headers())
        resp.raise_for_status()
        for m in resp.json().get("models", []):
            if m.get("type") != "llm":
                continue
            for inst in m.get("loaded_instances") or []:
                iid = inst.get("id") if isinstance(inst, dict) else inst
                out.append({"model": m.get("key"), "instance_id": iid})
    return out


def unload_lmstudio_models():
    """Eject every loaded LM Studio instance so VRAM is fully released."""
    unloaded = []
    with httpx.Client(timeout=10.0) as client:
        for inst in lmstudio_instances():
            iid = inst["instance_id"]
            try:
                resp = client.post(
                    "http://127.0.0.1:1234/api/v1/models/unload",
                    json={"instance_id": iid}, headers=_lm_headers())
                if resp.status_code < 300:
                    unloaded.append(iid)
            except Exception:
                pass
    return unloaded


def load_lmstudio_model():
    """Best-effort explicit load of the configured model. JIT loading on the
    next chat completion is the fallback, so failures here are non-fatal."""
    cfg = provider_config()
    key = cfg.get("lm_model") or ""
    if not key:
        return {"loaded": False, "note": "no lm_model configured"}
    try:
        with httpx.Client(timeout=180.0) as client:
            resp = client.post(
                "http://127.0.0.1:1234/api/v1/models/load",
                json={"model": key},
                headers=_lm_headers())
        return {"loaded": resp.status_code < 300, "status": resp.status_code}
    except Exception as exc:
        return {"loaded": False, "error": str(exc)}


def stop_dory_engine():
    with httpx.Client(timeout=10.0) as client:
        resp = client.post(f"{GUI_BASE}/api/server/stop")
        resp.raise_for_status()
    return resp.json()


def start_dory_engine():
    """Restart the Dory llama-server through the GUI using its live config."""
    with httpx.Client(timeout=10.0) as client:
        status = client.get(f"{GUI_BASE}/api/status")
        status.raise_for_status()
        cfg = status.json().get("config") or {}
    with httpx.Client(timeout=60.0) as client:
        resp = client.post(f"{GUI_BASE}/api/server/start", json=cfg)
        if resp.status_code != 200:
            raise RuntimeError(f"Dory engine start failed ({resp.status_code}): {resp.text[:300]}")
    return resp.json()


def switch_backend(target, reason=""):
    """Switch the IBA inference backend with strict GPU exclusivity.

    Returns a receipt dict; 'backend_switched' tells the engine to end the
    current conversation so the next step opens a session on the new backend.
    """
    target = (target or "").strip().lower()
    if target not in ("dory", "lmstudio"):
        raise ValueError('backend must be "dory" or "lmstudio"')
    current = effective_provider_kind()
    report = {"from": current, "to": target, "reason": reason, "steps": []}
    if current == target:
        report.update(status="already_active", effective=current)
        return report
    try:
        if target == "dory":
            report["steps"].append("unloading LM Studio models")
            report["unloaded"] = unload_lmstudio_models()
            report["steps"].append("starting Dory engine")
            start_dory_engine()
            write_provider_config("dory")
        else:
            report["steps"].append("stopping Dory engine")
            stop_dory_engine()
            report["steps"].append("loading LM Studio model")
            report["lm_load"] = load_lmstudio_model()
            write_provider_config("lmstudio")
        effective = effective_provider_kind()
        report.update(
            status="switched" if effective == target else "unverified",
            effective=effective,
            backend_switched=True,
        )
        return report
    except Exception as exc:
        report.update(status="failed", error=str(exc))
        return report
