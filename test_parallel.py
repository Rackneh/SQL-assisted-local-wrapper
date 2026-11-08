"""Verify parallel IBA execution: peer runner + provider-aware inference gate."""
import json, sys, tempfile, threading, time
from contextlib import contextmanager
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from iba.store import Store
from iba.engine import Runtime, _inference_gate


class StubModel:
    context_length = 32768
    identifier = "stub"

    def __init__(self, provider):
        self.provider = provider

    def count(self, messages):
        return sum(len(m.get("content", "")) for m in messages) // 4

    def predict(self, messages, max_tokens, **kw):
        # Track concurrency
        with self.provider.lock:
            self.provider.inflight += 1
            self.provider.max_inflight = max(self.provider.max_inflight, self.provider.inflight)
        time.sleep(0.15)  # hold the "GPU" briefly
        # finish immediately so each leaf completes in one step
        outcome = {"kind": "finish", "text": "done", "evidence": [], "coverage": []}
        with self.provider.lock:
            self.provider.inflight -= 1
        return {"content": json.dumps(outcome), "reasoning": "", "stop_reason": "stop", "output_tokens": 5}


class StubProvider:
    def __init__(self, slots):
        self.slots = slots
        self.lock = threading.Lock()
        self.inflight = 0
        self.max_inflight = 0

    def inference_kind(self):
        return "lmstudio" if self.slots > 1 else "dory"

    def inference_slots(self):
        return self.slots

    @contextmanager
    def session(self):
        yield StubModel(self)


def make_tree(store, owner, session):
    """Root task with two ready children."""
    root = store.create_task(owner, session, "parent objective", [])
    kids = []
    for i in range(2):
        kid = store.create_task(owner, session, f"child {i} objective", [], parent=root)
        kids.append(kid)
    return root, kids


def run_case(slots, expect_parallel):
    tmp = Path(tempfile.mkdtemp())
    store = Store(tmp)
    owner, session = "u", "s"
    root, kids = make_tree(store, owner, session)
    provider = StubProvider(slots)
    rt = Runtime(store, provider, tmp)
    t0 = time.time()
    final = rt.run(owner, session, root, variable_newconv=False)
    elapsed = time.time() - t0
    statuses = {k: store.task(owner, session, k)["status"] for k in kids}
    print(f"slots={slots}: max_inflight={provider.max_inflight} elapsed={elapsed:.2f}s "
          f"children={statuses} root={final['status']}")
    return provider.max_inflight, statuses, final["status"]


if __name__ == "__main__":
    # 2 slots -> children should overlap (max_inflight == 2)
    mi2, st2, root2 = run_case(2, True)
    assert mi2 == 2, f"expected 2 parallel inferences, got {mi2}"
    assert all(s == "completed" for s in st2.values()), st2
    assert root2 == "completed", root2

    # 1 slot -> strictly serial (max_inflight == 1)
    mi1, st1, root1 = run_case(1, False)
    assert mi1 == 1, f"expected serial inference, got {mi1}"
    assert all(s == "completed" for s in st1.values()), st1
    assert root1 == "completed", root1

    print("PARALLEL_TEST_OK")
