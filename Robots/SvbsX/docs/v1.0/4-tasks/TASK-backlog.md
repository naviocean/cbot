# TASK backlog — SVBS-X v1.0

**Last updated:** 2026-07-12  
**Status:** v1.1 code + docs aligned · walk-forward reports still pending

---

## P0 — Block release

| ID | Task | Owner | Status |
| --- | --- | --- | --- |
| T0.1 | Scaffold cBot project + Common links | dev | done |
| T0.2 | SessionClock UTC windows + params | dev | done |
| T0.3 | Freeze Asia/London session profiles | dev | done |
| T0.4 | SignalEngine break → accept (RetestHold) | dev | done |
| T0.5 | Volume surge + POC migration filters | dev | done |
| T0.6 | SL cap/skip + risk % sizing | dev | done |
| T0.7 | Market entry single position | dev | done |
| T0.8 | BE @ 1R + trail @ 1.5R full size | dev | done |
| T0.9 | **No partial close** (code review) | dev | done |
| T0.10 | Session flat + time stop | dev | done |
| T0.11 | Daily max trades + daily loss soft-stop | dev | done |
| T0.12 | Reject/open/close journal | dev | done |

## P1 — Quality

| ID | Task | Status |
| --- | --- | --- |
| T1.1 | Continuation accept mode param | done |
| T1.2 | ExitMode FixedRR research | done |
| T1.3 | News filter wire-up | pending |
| T1.4 | Chart visualize VAH/VAL/POC | pending |
| T1.5 | Developing POC cache perf | pending |
| T1.6 | Spread guard tuned for broker | pending |

## P2 — Research

| ID | Task | Status |
| --- | --- | --- |
| T2.1 | Backtest report E-Fixed | pending |
| T2.2 | Backtest report E-Trail | pending |
| T2.3 | Ablation table | pending |
| T2.4 | Walk-forward summary | pending |
| T2.5 | HVN targets experiment (v1.1+) | deferred |

## Done

| ID | Task | Status |
| --- | --- | --- |
| D1 | Strategy design SVBS-X | done |
| D2 | PRD + ARCH + ADRs + PLAN | done |
| D3 | Owner: no partial; BE+trail OK | done |
| D4 | Target XAU personal locked | done |
| D5 | cBot implement + Release build | done |
| D6 | `CVolumeProfile.BuildRange` | done |
| D7 | v1.1: sessions toggles, BreakConfirm, ATR SL, XAU size, daily $, simplify exit | done |
| D8 | Docs sync v1.1 (README/PRD/ARCH/ADR-003/004) | done |

---

## Blockers

| Blocker | Impact |
| --- | --- |
| Broker UTC vs session table not audited on live chart | Wrong windows kill edge |
| No walk-forward report yet | Do not scale live risk |

---

## Definition of ready for coding

- [x] PRD locked with E/F/X codes  
- [x] No partial rule in ADR-001  
- [x] Plan phased  
- [x] Implemented (user: implement)  
