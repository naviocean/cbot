# Tasks backlog — HvnMagnet (HMPD)

| ID | Task | Status |
| --- | --- | --- |
| T0 | Spec docs (PRD/ARCH/ADR/PLAN/README/PROJECT_ROOT) | **Done** |
| T1 | Solution + csproj + Common link | **Done** |
| T2 | HvnMagnet.cs orchestrator (from VH shell) | **Done** |
| T3 | SignalEngine F1–F5 + PROFILE | **Done** |
| T4 | SignalEngine E1–E3 HVN touch/reject | **Done** |
| T5 | SignalEngine E4 delta + E5 shape + E6 HTF | **Done** |
| T6 | Structure target + E7 Min R | **Done** |
| T7 | ExecuteSignal SL/TP/size/BE/trail | **Done** |
| T8 | Logging PASS/OPEN/CLOSE + debug rejects | **Done** |
| T9 | M1/M2 failed acceptance + max bars | **Done** (default off) |
| T10 | Release build `.algo` | **Done** (compile) |
| T15 | E7 only when Structure TP | **Done** |
| T16 | OnStart recover BE/Trail + tradesToday | **Done** |
| T17 | Optional Require Close In Zone | **Done** (default off) |
| T11 | Backtest research baseline → `5-reports/` | Pending |
| T12 | Ablation A–D table | Pending |
| T13 | Demo/micro soak checklist | Pending |
| T14 | Live risk preset (daily $ + DD) | Pending |

## Priority order

`T1 → T2/T3 parallelizable after scaffold → T4 → T5 → T6 → T7 → T8 → T10 → T11 → T12`

## Notes

- Prefer **no** Common API change until SignalEngine needs shared Top-N helper.  
- Store BT HTML/notes under `../5-reports/` with date prefix.  
- Do not open PR to merge into VacuumHunter.
