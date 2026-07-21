# PLAN — Implement ZigZagPocPullback v1.0

## Goal

Ship a compilable cBot matching [PRD-zz-poc-pullback.md](../1-prds/PRD-zz-poc-pullback.md): confirmed ZZ pullback → zone POC|Fib → market when price in zone → SL z2±ATR · TP fixed RR.

## Principles

1. **PRD = source of truth** for rules; this PLAN = file order + defaults.  
2. **Pure `SignalEngine`** — no `ExecuteMarketOrder` inside evaluate.  
3. **Copy-adapt Fib786** orchestrator (risk/session/sizing); **PmLh** for rolling VP.  
4. **No ZigZag indicator API** — own confirmed pivot series (Depth / Deviation / Backstep), anti-repaint = next pivot exists.  
5. **Common only link** — avoid editing `Common/*` unless bug blocks POC build.

## Dependencies

| Dependency | Use |
| --- | --- |
| `Common/RiskManager.cs` | Volume from risk $ / fixed lots; optional equity gate |
| `Common/SessionFilter.cs` | Asia/London/NY/Overlap |
| `Common/VolumeProfile.cs` | `BuildRange` for rolling POC (PmLh pattern) |
| `Common/ProfileData.cs` | POC validity |
| `Common/Logger.cs` | Info/reject logs |
| `Common/PriceUtils.cs` | If needed for pip/price |
| Fib786 | Scaffold params, OnStart/OnBar, sizing, market order |
| PmLh `BuildRollingProfile` | POC rolling by time range |

## Target layout

```text
Robots/ZigZagPocPullback/
  ZigZagPocPullback.sln
  ZigZagPocPullback/
    ZigZagPocPullback.csproj   # net6.0 + cTrader.Automate + Common/*.cs link
    ZigZagPocPullback.cs       # Robot orchestrator
    SignalEngine.cs            # ZZ + zone + side + reject codes
  docs/                        # already exists
```

**Verify scaffold:**

```bash
dotnet build -c Release Robots/ZigZagPocPullback/ZigZagPocPullback/ZigZagPocPullback.csproj
```

---

## Tasks

### T1 — Scaffold

- [x] Create `ZigZagPocPullback.sln` + `ZigZagPocPullback/ZigZagPocPullback.csproj` (copy Fib786 csproj pattern: `Compile Include="..\..\..\Common\*.cs"`).  
- [x] Stub `ZigZagPocPullback.cs` (`[Robot]`, OnStart Print) + empty `SignalEngine.cs` with `SignalResult.Reject`.  
- [x] `dotnet build -c Release` green.

### T2 — ZigZag pivot series (in SignalEngine or small helper)

- [x] Inputs: `ZzDepth`, `ZzDeviation` (pips→price), `ZzBackstep` (defaults: **12 / 50 pips / 3** for XAU).  
- [x] Build alternating high/low pivots from closed OHLC (no look-ahead: pivot final only when rule says confirmed).  
- [x] Expose confirmed chain **z1, z2, z3** (newest confirmed first) and “z1 is high vs low”.  
- [x] **Confirm rule (PRD):** tradeable z1 only if a **newer pivot** exists after it (so z1 is not the live tip).  
- **Verify:** On sample series, z1 price/time never changes after listed as confirmed.

### T3 — Structure + optional filter

- [x] Preliminary: Buy = z1 bottom && z1 &lt; z3; Sell = z1 peak && z1 &gt; z3.  
- [x] `UseStructureFilter` default **false**; when true, require 2 HL (buy) / 2 LH (sell) in last 4–5 confirmed pivots.  
- [x] Reject codes: `F_ZZ_NONE`, `F_ZZ_UNCONF`, `F_SIDE`, `F_STRUCT`.

### T4 — Zone modes

- [x] Enum `ZoneMode { Poc = 0, Fib = 1 }` default **Poc**.  
- [x] **Fib:** band [38.2%, 61.8%] of |z2−z1| from extreme; expand by `BufferAtrRatio * ATR` (default buffer ratio **0.5**). In-zone if price inside expanded band.  
- [x] **POC:** rolling lookback days + `BuildRange`; invalid → `F_POC_INVALID`.  
- [x] Reject: `F_ZONE` if price not in zone (used when evaluating fill).

### T5 — SignalEngine.Evaluate + arm state

- [x] `SignalContext` + Evaluate / EvaluateSetup.  
- [x] One-shot z1 key; max pos per side.  

### T6 — Orchestrator (`ZigZagPocPullback.cs`)

- [x] Params, OnBar arm, OnTick market in zone, sizing, RR TP, session.  

### T7 — Docs + build artifact

- [x] PROJECT_ROOT + PRD DoD updated.  
- [x] `.algo` at `Robots/ZigZagPocPullback.algo`.  

### T8 — Verification (engineering done)

- [x] `dotnet build -c Release` exit 0 (2026-07-15).  
- [ ] User: cTrader BT **XAUUSD**, chart **M5**, tick if available, ≥1y; sessions London+NY; ZoneMode POC then Fib ablation.  
- [ ] Optional note under `docs/v1.0/5-reports/` after first BT.

---

## Defaults freeze (code)

| Param | Default |
| --- | --- |
| ZzDepth / Deviation / Backstep | 5 / 20 pips / 2 (fractal ZZ) |
| ZoneMode | Poc |
| PocTimeFrame | Hour |
| ProfileLookbackDays | 3 |
| BufferAtrRatio | 0.5 |
| SlAtrRatio | 1.0 |
| TpRR | 2.0 |
| RiskPercent / FixedLots | 0.5 / 0.01 |
| MaxPositionsPerSide | 5 |
| UseStructureFilter | false |
| Sessions | Asia off, London on, NY on, Overlap off |

## Out of scope (this plan)

- HTF EMA bias, partial TP, BE, trail, news hard filter, multi-symbol, pending limits, anchored VP, Common refactors.

## Critical path

`T1 → T2 → T3 → T4 → T5 → T6 → T8` (T7 parallel after T1)

## Done when

- [ ] Release build succeeds  
- [ ] Behavior matches PRD §3–8 (zone exclusive, market-in-zone, 1/z1, max 5/side, SL/TP)  
- [ ] Reject codes logged for blocked paths  
- [ ] User can run first backtest without code change for missing params  

## Estimate

~2–3 focused days (scaffold + ZZ hardest; POC reuse shortens T4).
