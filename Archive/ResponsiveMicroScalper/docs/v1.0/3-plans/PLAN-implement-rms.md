# PLAN — Implement RMS v1.0

**PRD:** [PRD-rms.md](../1-prds/PRD-rms.md)  
**Approved:** 2026-07-13 (user Y)  
**Goal:** Compilable cBot matching locked formulas; first BT can calibrate Accel/Var thresholds.

## Steps

| Step | Deliverable | Verify |
| --- | --- | --- |
| 1 | Scaffold `Robots/ResponsiveMicroScalper/` + csproj linking Common | `dotnet build` |
| 2 | `SignalEngine.cs` — M/A/V, bias, regime, thresh, SL/TP, reject codes | Hand-check formulas §5 |
| 3 | `ResponsiveMicroScalper.cs` — params, OnBar entry, OnTick manage, Common wiring | Compile; dry-run log |
| 4 | Docs: PRD status → Approved/Implemented scaffold | TASK T1.* done |
| 5 | (Later) BT calibrate BaseAccel / VarMin → report in `5-reports/` | PRD §13 gates |

## Architecture

```text
OnBar(M1 closed)
  → filters (session, spread, risk, news, cooldown, max trades)
  → SignalEngine.Evaluate
  → market order + SL/TP
OnTick
  → RiskManager, TrailingManager, time exit
```

## Out of scope this plan

- BT optimization, ML regime, merge with DynamicMicroScalper
