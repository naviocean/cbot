# PLAN — Implement PmLh (PM-LH) v1.0

## Goal

Ship a compilable cBot that matches [PRD-pmlh.md](../1-prds/PRD-pmlh.md), reusing Common engines, skeleton-adapted from VacuumHunter, with **research-wide params** and **exit = SL + RR TP + optional trail only**.

## Principles

1. **Spec first** — PRD is source of truth for F/E/X codes.  
2. **Pure SignalEngine** — no broker calls inside evaluate.  
3. **Copy-adapt VH** — do not refactor VacuumHunter / HvnMagnet mid-flight.  
4. **Ablation-ready** — optional filters are parameters + reject codes; **defaults research-open** (E4–E7 off).  
5. **Do not pre-cut TF/session** — chart TF + session toggles; matrix after first green build.  
6. **Exit simplicity** — ADR-002; no structure TP / POC exit / time stop in v1.

## Dependencies

| Dependency | Status |
| --- | --- |
| `Common/VolumeProfile.cs` LVN list + composite | Exists |
| `Common/ProfileData.cs` Lvn finders | Exists |
| `Common/TickDeltaEngine.cs` | Exists |
| `Common/RiskManager.cs` equity OnTick | Exists |
| VacuumHunter orchestrator template | Exists |
| Rolling POC series helper | **New** (`PocMigrationTracker`) |

## Phases

### Phase 0 — Scaffold (0.5d)

| Step | Action | Done when |
| --- | --- | --- |
| 0.1 | Create `Robots/PmLh/PmLh.sln` + csproj linking `../../../Common/*.cs` | Solution opens |
| 0.2 | Copy `VacuumHunter.cs` → `PmLh.cs`; rename class, label, namespaces | Compiles stub |
| 0.3 | Copy `SignalEngine.cs`; strip VH LVN-reject to stubs / PASS false | Compiles |
| 0.4 | Add empty `PocMigrationTracker.cs` | Compiles |

**Verify:** `dotnet build -c Release` (or cTrader build) succeeds.

### Phase 1 — PocMigrationTracker (0.5–1d)

| Step | Action | Done when |
| --- | --- | --- |
| 1.1 | Ring buffer POC; warm length = max(N, K+streakOf) | Unit-style checks |
| 1.2 | OnClosedBar: build rolling VP N bars → POC (via VolumeProfile API) | POC updates each bar |
| 1.3 | Compute Δ, M=Δ/ATR, direction, streak | Codes match PRD formulas |
| 1.4 | Expose diagnostics for logs | |

**Verify:** On historical segment, M flips when value shifts; buffer not warm until filled.

### Phase 2 — SignalEngine highway path (1–2d)

| Step | Action | Done when |
| --- | --- | --- |
| 2.1 | `SignalContext` / `SignalResult` per ARCH | Types ready |
| 2.2 | F1–F5 identical pattern to VH | Reject codes |
| 2.3 | E0 + E_MIG (flat/tiny/noise/invalid) | |
| 2.4 | E2 eligible LVN from LvnSource | Composite path first |
| 2.5 | Entry Mode `ShallowRetest` (prior break, dwell, interact) | |
| 2.6 | Entry Mode `Pierce` + `TouchOnly` | |
| 2.7 | E3 acceptance | |
| 2.8 | E4–E7 optional toggles | Default off |
| 2.9 | E8 max SL | Default on |
| 2.10 | Shared SL geometry helper (entry assumed mid for estimate) | E8 consistent |
| 2.11 | LvnSource Rolling + DualPreferComposite | |

**Verify:** Synthetic contexts produce at least:

- Flat M → `E_POC_FLAT`  
- No LVN → `E2_NO_LVN`  
- No interact → `E1_NO_INTERACT`  
- Dwell exceeded → `E_LVN_DWELL`  
- Full pass long → `PASS:LONG`

### Phase 3 — Orchestrator (1d)

| Step | Action | Done when |
| --- | --- | --- |
| 3.1 | Parameters map PRD §7 (groups: Risk, SL/TP, VP, Migration, Signal, Session, News) | Params visible |
| 3.2 | OnStart wire composite + tracker + risk + session | |
| 3.3 | OnTick: delta + Risk.OnTick + trail | |
| 3.4 | OnBar: profiles + tracker + Evaluate + ExecuteSignal | |
| 3.5 | ExecuteSignal: SL geometry + **TP RR only** + size + order | No Structure/Fixed TP modes |
| 3.6 | OPEN/CLOSE/day summary logs (include M, LVN, mode) | |
| 3.7 | Remove VH rejection-entry leftovers | |

**Verify:** Chart attach XAU; Visualize Profile; debug rejects; no trade when all sessions off; OPEN has SL+TP.

### Phase 4 — Research baseline (ops, not “done coding”)

| Step | Action | Done when |
| --- | --- | --- |
| 4.1 | Backtest starter defaults, ≥1 TF (e.g. M15), report in `5-reports/` | Report file |
| 4.2 | Ablation matrix (below) | Table filled |
| 4.3 | Optional: second TF (M5) same params | Comparison note |
| 4.4 | Optional: LvnSource A/B | Note winner/tie |

**No TF/session freeze required to close engineering Phase 0–3.**

### Ablation matrix (starter)

| ID | Config |
| --- | --- |
| A | Base: migration + ShallowRetest; E4–E7 off; trail off; BE on |
| B | A + Require Expand |
| C | A + Require HTF |
| D | A + Require Shape |
| E | A + Require Delta |
| F | A + Pierce mode |
| G | A + LvnSource=Rolling |
| H | A + Trail on |

Metrics: trades, PF, MaxDD%, expectancy (R or $), avg trades/day, win rate.

### Phase 5 — Live readiness gate (after evidence)

| Gate | Requirement |
| --- | --- |
| G1 | PRD codes match logs |
| G2 | Risk$ OPEN ≈ Risk% (and room) |
| G3 | Chosen config frozen from OOS/ablation notes |
| G4 | Demo/micro soak with **that** config |
| G5 | Daily loss / DD caps set if prop; combined risk if co-running VH/HMPD |

---

## Critical files to create/modify

| Path | Action |
| --- | --- |
| `Robots/PmLh/PmLh/PmLh.cs` | Create |
| `Robots/PmLh/PmLh/SignalEngine.cs` | Create |
| `Robots/PmLh/PmLh/PocMigrationTracker.cs` | Create |
| `Robots/PmLh/PmLh/PmLh.csproj` | Create |
| `Robots/PmLh/PmLh.sln` | Create |
| `Common/*` | Prefer **no** change; only if shared rolling helper clearly reusable |

## Reuse reference (read, don’t rewrite)

| Path | What to copy |
| --- | --- |
| `Robots/VacuumHunter/VacuumHunter/VacuumHunter.cs` | Params shell, OnStart/OnBar/OnTick, ExecuteSignal, sizing |
| `Robots/VacuumHunter/VacuumHunter/SignalEngine.cs` | Filter order, delta/shape/HTF patterns |
| `Robots/HvnMagnet/HvnMagnet/*` | Optional filter packaging / logging style |
| `Robots/VacuumHunter/VacuumHunter/VacuumHunter.csproj` | Common include pattern |

## Out of scope this plan

- Structure TP / Fixed $ TP modes  
- POC reverse exit / time stop  
- Merging with VH or HMPD  
- Auto news calendar  
- Multi-symbol portfolio  
- Declaring production TF/session from speculation  

## Rollback

If PmLh misbehaves: `Enable Trading=false` or remove instance. VacuumHunter / HvnMagnet untouched.

## Effort estimate

| Phase | Estimate |
| --- | --- |
| 0 Scaffold | 0.5 day |
| 1 Tracker | 0.5–1 day |
| 2 SignalEngine | 1–2 days |
| 3 Orchestrator | 1 day |
| 4 Research ops | 2–5 days calendar |
| 5 Live gate | ops / not pure code |

**Total engineering:** ~3–5 focused days to compilable bot + first BT report.
