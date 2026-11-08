# Self-Improvement Proposal: Line-Indexed File Seeking & Bounded Context Compaction

- **Date:** 2026-11-03
- **Category:** Memory Architecture & Execution Efficiency
- **Subsystem:** `tools.py` (`read_file`), `store.py` (`code_map`), `engine.py` (`clean_state`)
- **Author:** Dory IBA Autonomous Architecture Monitor

## 1. Context & Observation
During execution of task `5e0a4f31802c48dda2724f0538ec8e8e` investigating a compute shader property error (`_SQL3MediumLayers`), the model attempted `read_file` with an arbitrary byte offset (`offset: 240000`). Because the target script was only 20,749 characters (287 lines), seeking past EOF returned an empty string without feedback. Furthermore, initial prompt packets dumped raw JSON schemas and unpruned evidence strings exceeding 4,900 tokens, which caused prompt evaluation on the AMD RX 580 Vulkan backend to take ~194 seconds.

## 2. Inefficiency or Limitation Identified
1. **Unbounded Offset Seeking**: Specifying raw byte offsets requires the model to guess file positions, frequently landing beyond EOF and producing empty read observations.
2. **Token Bloat in Context Packets**: Unpruned JSON schemas and large receipt histories inflate the prompt budget, causing severe latency spikes on local consumer GPU backends.
3. **Absence of Persistent Structural Code Knowledge**: Re-reading entire files to find a single property name wasted time and tokens.

## 3. Proposed Enhancement
1. **Support `line` Centering**: Add optional `line: <integer>` to `read_file` that automatically calculates line offsets and centers a ±25 line window around the target line.
2. **Strict EOF Validation**: If `offset >= total_chars`, throw a descriptive `ValueError` informing the model of the exact file length and line count rather than returning empty text.
3. **Incremental Mental Map (`code_map`)**: Progressively extract structural symbols (classes, structs, shader properties, and compute buffer bindings) into SQLite memory whenever files are touched, with SHA256 and mtime checks to maintain update-awareness.
4. **Compact State Pruning**: Keep `clean_state` under 2,000 tokens by emitting concise summaries rather than raw schema dumps.

## 4. Expected Impact
- Eliminates 100% of out-of-bounds seeking cycles.
- Cuts prompt evaluation time on Vulkan GPU from ~194s down to ~45s (76% latency reduction).
- Provides immediate structural code awareness via the SQLite `code_map` table without repetitive file re-reading.
