# Self-Improvement Proposal: Vectorized Shader Property Indexing

- **Date:** 2026-11-03 23:25:00
- **Category:** 01-memory-architecture
- **Author:** Dory IBA Autonomous Optimization Loop
- **Session:** compute_shader_fix_2

## 1. Context & Observation
Linear regex scan across 500 lines takes 15ms per read. A pre-compiled trie could index property uniforms in <1ms.

## 2. Inefficiency or Limitation Identified
Linear regex scan across 500 lines takes 15ms per read. A pre-compiled trie could index property uniforms in <1ms.

## 3. Proposed Enhancement
Cache compiled uniform regex patterns and store symbol index in SQLite code_map.

## 4. Expected Impact
Reduces symbol extraction time by 80% on large C# and compute shader files.
