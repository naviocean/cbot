# ADR-003: No partial close in v1.0

## Context

Discretionary HMPD narrative includes “partial + runner.” VacuumHunter historically tried soft TP1 partial then removed it (v1.1) for simpler fills, less race risk, and cleaner RR measurement.

## Decision

v1.0 uses **single full-size TP** at broker + optional BE/Trail in R. Partial close / runner is **out of scope** until edge is validated.

## Consequences

- ✅ Same exit contract as current VacuumHunter — reuse patterns  
- ✅ Cleaner backtest attribution  
- ❌ No scale-out psychology for discretionary-style runners  

Revisit as v1.1 only if Structure TP + trail still leave large give-back with proven edge.

## Status

Accepted — 2026-07-12
