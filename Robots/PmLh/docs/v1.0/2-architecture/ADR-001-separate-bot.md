# ADR-001: Separate bot from VacuumHunter / HvnMagnet

## Context

PM-LH uses LVN nodes (like VacuumHunter) and Common volume profile (like both VH and HvnMagnet). Merging into VacuumHunter as “LVN mode = vacuum | highway” is tempting for less code.

## Decision

Ship **PmLh** as a **separate** cBot / solution (`Robots/PmLh`), copy-adapt orchestrator from VacuumHunter. Do **not** merge strategies into one robot with a mode flag in v1.

## Consequences

- ✅ Clear labels, risk journals, and reject codes per edge hypothesis  
- ✅ Avoid conflicting default filters on the same LVN  
- ✅ Independent enable/disable on chart  
- ❌ Some duplicated wiring (params, OnBar shell) — acceptable  
- ❌ Co-run may open opposing ideas same day — ops/research later, not merge fix  

## Status

Accepted (v1.0-spec).
