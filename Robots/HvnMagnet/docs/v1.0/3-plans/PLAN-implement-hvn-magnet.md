# PLAN — Implement HvnMagnet (HMPD) v1.0

## Goal

Ship a compilable cBot that matches [PRD-hmpd.md](../1-prds/PRD-hmpd.md), reusing Common engines, skeleton-adapted from VacuumHunter.

## Principles

1. **Spec first** — PRD is source of truth for F/E/X codes.  
2. **Pure SignalEngine** — no broker calls inside evaluate.  
3. **Copy-adapt VH** — do not refactor VacuumHunter mid-flight.  
4. **Ablation-ready** — every filter is a parameter + reject code.  
5. **Quality over frequency** — Max Trades/Day = 3; no chase of 4–8/day.

## Dependencies

| Dependency | Status |
| --- | --- |
| `Common/VolumeProfile.cs` HVN list + strength | Exists |
| `Common/ProfileData.cs` Hvn finders | Exists |
| `Common/TickDeltaEngine.cs` | Exists |
| `Common/RiskManager.cs` equity OnTick | Exists (VH v1.2) |
| VacuumHunter as orchestrator template | Exists |

## Phases

### Phase 0 — Scaffold (0.5d)

| Step | Action | Done when |
| --- | --- | --- |
| 0.1 | Create `Robots/HvnMagnet/HvnMagnet.sln` + csproj linking `../../../Common/*.cs` | Solution opens |
| 0.2 | Copy `VacuumHunter.cs` → `HvnMagnet.cs`; rename class, label, namespaces | Compiles stub |
| 0.3 | Copy `SignalEngine.cs`; strip LVN logic to stubs | Compiles |

**Verify:** `dotnet build -c Release` succeeds (or cTrader build).

### Phase 1 — SignalEngine HVN path (1–2d)

| Step | Action | Done when |
| --- | --- | --- |
| 1.1 | `SignalContext` / `SignalResult` fields per ARCH (HVN, TargetPrice, TargetLabel) | Types ready |
| 1.2 | F1–F5 identical pattern to VH | Reject codes |
| 1.3 | E2 eligible HVN: strength, width, top-N | Unit table or debug |
| 1.4 | E1 touch expanded HVN band | |
| 1.5 | E3 rejection candle (reuse VH geometry) | |
| 1.6 | E6 HTF vs POC | |
| 1.7 | E5 shape allow/block + neutral block | |
| 1.8 | E4 delta required path | |
| 1.9 | Structure target resolver + **E7 Min R** | Skip near-POC traps |
| 1.10 | Side arbitration by imbalance | |

**Verify:** Synthetic contexts produce expected PASS/REJECT codes for at least:

- No touch → `E1_NO_TOUCH`  
- Weak HVN → `E2_WEAK_HVN`  
- Bad shape long on Bearish → `E5_SHAPE_*`  
- HTF below POC long → `E6_HTF_*`  
- Low R → `E7_RR`  
- Full pass long → `PASS:LONG`

### Phase 2 — Orchestrator (1d)

| Step | Action | Done when |
| --- | --- | --- |
| 2.1 | Parameters map PRD §7 (HVN buffer UI names) | Params visible |
| 2.2 | OnStart wire VP composite defaults (3d, HVN thr) | |
| 2.3 | OnTick: delta + Risk.OnTick + trail | |
| 2.4 | OnBar: BuildComposite + Evaluate + ExecuteSignal | |
| 2.5 | SL from HVN edges; TP modes RR/Structure/Fixed | |
| 2.6 | OPEN/CLOSE/day summary logs | |
| 2.7 | Optional M1 failed acceptance (param off) | Stub OK |

**Verify:** Chart attach XAU M15; Visualize Profile; debug rejects appear; no trade when sessions off.

### Phase 3 — Research baseline run (ops)

| Step | Action | Done when |
| --- | --- | --- |
| 3.1 | Backtest every-tick, London+NY, research preset | Report in `5-reports/` |
| 3.2 | Ablation A–D (see below) | Table filled |
| 3.3 | Walk-forward or at least IS/OOS split | Note freeze params |

### Ablation matrix

| ID | Config |
| --- | --- |
| A | HTF on; Delta off; Shape off; MinR on |
| B | A + Shape on |
| C | B + Delta on (product default) |
| D | C + Require Hvn Poc Side on |

Metrics: trades, PF, MaxDD%, expectancy R, avg R.

**Pass criteria for “code complete + research ok”:** C not clearly worse than A on DD **and** PF (or clearly better DD with acceptable PF). If delta only hurts, keep toggle but document.

### Phase 4 — Live readiness gate

| Gate | Requirement |
| --- | --- |
| G1 | PRD codes match logs |
| G2 | Risk$ OPEN ≈ Risk% (and room) |
| G3 | Demo/micro ≥ 2 weeks, param frozen |
| G4 | Daily loss / DD caps set if prop |
| G5 | Max combined risk if co-running VH |

---

## Critical files to create/modify

| Path | Action |
| --- | --- |
| `Robots/HvnMagnet/HvnMagnet/HvnMagnet.cs` | Create |
| `Robots/HvnMagnet/HvnMagnet/SignalEngine.cs` | Create |
| `Robots/HvnMagnet/HvnMagnet/HvnMagnet.csproj` | Create |
| `Robots/HvnMagnet/HvnMagnet.sln` | Create |
| `Common/*` | Prefer **no** change; only if Hvn Top-N helper must live in ProfileData |

## Reuse reference (read, don’t rewrite)

| Path | What to copy |
| --- | --- |
| `Robots/VacuumHunter/VacuumHunter/VacuumHunter.cs` | Params, OnStart/OnBar/OnTick, ExecuteSignal shell |
| `Robots/VacuumHunter/VacuumHunter/SignalEngine.cs` | Filter order, rejection math, delta checks |
| `Robots/VacuumHunter/VacuumHunter/VacuumHunter.csproj` | Common include pattern |

## Out of scope this plan

- Partial / runner  
- M5 primary TF  
- Auto news calendar  
- Merging with VH  
- Prop firm challenge automation beyond equity gates  

## Rollback

If HvnMagnet misbehaves in production: `Enable Trading=false` or remove instance. VacuumHunter untouched.

## Effort estimate

| Phase | Estimate |
| --- | --- |
| 0 Scaffold | 0.5 day |
| 1 SignalEngine | 1–2 days |
| 2 Orchestrator | 1 day |
| 3 Research ops | 2–5 days calendar |
| 4 Live gate | ops / not pure code |

**Total engineering:** ~3–4 focused days to compilable bot + first BT report.
