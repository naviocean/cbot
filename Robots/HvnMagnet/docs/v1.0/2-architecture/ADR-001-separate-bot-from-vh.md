# ADR-001: Separate bot from VacuumHunter

## Context

HMPD (HVN magnet pullback) and VacuumHunter (LVN vacuum) share composite VP, delta proxy, risk, and session modules. Merging both into one robot with a “node mode” flag is tempting for less code.

## Decision

Implement **HvnMagnet as a separate cBot** under `Robots/HvnMagnet/`. Share only `RedWave.Common`. Do **not** dual-mode VacuumHunter in v1.0.

## Consequences

- ✅ Clear journals, labels, risk attribution per strategy  
- ✅ Independent defaults (delta/shape on vs off; lookback; risk %)  
- ✅ Avoid contradictory signals same bar (LVN long vs HVN short) inside one engine  
- ❌ Some orchestrator duplication (acceptable; copy-adapt from VH)  
- Later: optional shared library for “execute + risk shell” if duplication hurts  

## Status

Accepted — 2026-07-12
