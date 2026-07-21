# ADR-002: Delta filter required by default (v1)

## Context

HMPD’s product story is “not a blind HVN pullback.” VacuumHunter keeps delta **optional/off** by default because LVN rejection candle carries much of the edge. On HVN, price often balances without directional edge; mid-tick delta is a soft aggression proxy.

## Decision

v1.0 defaults: **`Require Delta Filter = true`**, with Min Delta Strength 1.2 and Min Delta Ticks 15. Parameter remains toggleable for ablation (`false` = structure-only baseline).

## Consequences

- ✅ Aligns product claim with defaults  
- ✅ Fewer chop entries in balance at HVN  
- ❌ Lower trade count; delta noise can false-reject good holds  
- ❌ Still not true footprint — document limitation in README/PRD  

Ablation plan must prove delta improves PF or DD; if not, default may flip to false in v1.1.

## Status

Accepted — 2026-07-12
