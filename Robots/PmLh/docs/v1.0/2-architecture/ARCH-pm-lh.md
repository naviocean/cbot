# Architecture — PmLh (PM-LH) v1.0

## 1. Layering

```text
┌──────────────────────────────────────────────────────────────┐
│  PmLh (Robot)                                                │
│  Params · OnStart / OnBar (signal) / OnTick (risk + trail)   │
└─────────────────────────────┬────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
  SignalEngine         CVolumeProfile          CTickDeltaEngine
  (pure entry)         + ProfileData           (optional E4)
        │                     │
        │              PocMigrationTracker
        │              (POC ring + score M)
        └──────────► CRiskManager · CSessionFilter · CNewsFilter
                     CMarketCondition · CTrailingManager · CLogger
```

**Principles**

1. Engines have **no** strategy side-effects (no orders inside SignalEngine / tracker).  
2. Risk flatten runs **only** in `CRiskManager.OnTick` when init’d with `Robot`. **Do not** flatten from `OnBar`.  
3. Template: copy `Robots/VacuumHunter` orchestrator; replace LVN-reject path with migration + highway path.  
4. Prefer **no** Common changes; if rolling POC helper is reusable, optional small addition to `VolumeProfile` with ADR note.

## 2. Runtime flow

### OnStart

1. Logger; VP configure composite (lookback, bin, LVN/HVN thr).  
2. `PocMigrationTracker.Configure(N, K, …)`.  
3. Delta engine (always construct; filter may be off).  
4. `RiskManager.Init(this, Symbol, label)` + equity/daily flags.  
5. Session toggles; news; spread; trailing; ATR series.  
6. HTF bars series if E6 may be used.  
7. Warm-up: build composite + fill POC buffer as far as history allows (or warm on first bars).

### OnTick

```text
TickDeltaEngine.OnTick(bid, ask, time)
RiskManager.OnTick()                 ← equity gates + optional ClosePosition
if BE or Trail enabled → TrailingManager.OnTick
```

### OnBar (signal; no risk flatten)

```text
idx = Bars.Count - 2                 // last closed
Build structure profile (composite and/or rolling per LvnSource)
PocMigrationTracker.OnClosedBar(idx) // append poc[t], compute M
Build SignalContext
SignalEngine.Evaluate(ctx)
  if PASS && EnableTrading → ExecuteSignal
Log reject/pass when Debug
```

### ExecuteSignal

```text
entry ≈ Ask/Bid (side)
SL = LVN edge ± ATR×buffer, floored by MinSlAtrMult
if MaxSl on and |entry-SL| > MaxSlAtr×ATR → abort (should be E8 pre-check)
TP = entry ± RR × |entry-SL|         // ONLY mode v1
risk$ = min(Balance×Risk%, daily room)
volume = CalculateVolumeFromRiskMoney
Configure BE/Trail distances from SL_dist × R params
ExecuteMarketOrder(SL, TP)
Log OPEN: M, LVN, mode, RR, risk$, volume
```

## 3. PocMigrationTracker

### Responsibility

- Maintain `poc[]` ring for last `max(N, K+streakOf, warm)` bars.  
- On each closed bar: compute rolling profile POC for window ending at `idx`, push.  
- Expose: `IsWarm`, `PocNow`, `M`, `Delta`, `Direction` (Bull/Bear/Flat), `StreakOk`, `StreakSame` / `StreakOpp` / `StreakFlat`, `FailCode`.
- Streak counts **non-zero** 1-bar POC steps only; optional **Strong M bypass**.
- Implemented: `PmLh/PocMigrationTracker.cs` — `Push(poc, atr)` each closed bar (orchestrator builds rolling VP via `BuildRange`).

### Rolling POC build options

| Option | Pros | Cons |
| --- | --- | --- |
| A. Full rebuild last N bars each bar | Simple, correct | Costly on M5 long history |
| B. Incremental bin add/remove | Fast | More code risk |

**v1:** Option A via `CVolumeProfile.BuildRange` (updateLastProfile=false). Optimize to B only if BT too slow.

## 4. SignalEngine contract

### Input `SignalContext`

| Field | Source |
| --- | --- |
| StructProfile | Composite or dual structure `ProfileData` |
| RollProfile | Optional rolling snapshot for LvnSource=Rolling |
| Bar OHLC | Closed signal bar |
| Atr | ATR value |
| Migration M, Delta, Direction, StreakOk, IsWarm, PocNow | Tracker |
| MaxPricePocAtr, RequireLvnSide | Price/LVN align gates |
| HtfClose / HtfPoc | Optional E6 |
| BuyImbalance / SellImbalance / DeltaTickCount | Delta |
| SessionOk, NewsOk, SpreadOk, EquityOk | Filters |
| TradesToday, MaxTradesPerDay, HasOpenPosition | Ops |
| Entry mode + all thresholds | Params snapshot |
| Filter toggles E4–E8 | Params |

### Output `SignalResult`

| Field | Meaning |
| --- | --- |
| IsValid | Pass/fail |
| Side | Long / Short / None |
| Reason | PASS:… or REJECT:… |
| Lvn | Entry volume node |
| MigrationM | Score used |
| SlPrice | Suggested SL (orchestrator may recompute identically) |
| TpPrice | entry ± RR×R (orchestrator sets entry at fill) |
| Imbalance / Shape | Diagnostics |

### Evaluate pipeline (order)

```text
F1 → F2 → F3 → F3_EQUITY → F4 → F5
→ E0 profiles / warm
→ E_MIG (direction D; flat/tiny/noise fail)
→ E_PRICE_POC (wrong-side distance vs PocNow)
→ Collect eligible LVNs (E2) from LvnSource
→ E1 interaction for mode + side D
→ E_LVN_DWELL / E_NO_PRIOR_BREAK as mode requires
→ E_LVN_SIDE if RequireLvnSide
→ E3 acceptance
→ E4 delta if on
→ E5 shape if on
→ E6 HTF if on
→ E7 expand if on
→ E8 max SL if on
→ PASS:D
```

**Only side D** is evaluated (migration-aligned).

## 5. SL / TP computation (shared pure helpers)

```text
Long:
  raw = Lvn.Low - Atr * LvnBuffer
  floor = entry - Atr * MinSlAtrMult
  sl = min(raw, floor)           // farther from entry
  // if MaxSl: require entry-sl <= Atr*MaxSlAtrMult

  slDist = entry - sl
  tp = entry + RR * slDist

Short: mirror
```

Orchestrator and SignalEngine should use the **same formula** (static helper on SignalEngine or small `RiskGeometry` local class) to keep E8 and live SL consistent.

## 6. State

| State | Owner |
| --- | --- |
| Trades today / day key | Robot |
| Last signal snapshot (optional journal) | Robot |
| Open position | Platform + label filter |
| POC ring | PocMigrationTracker |
| Equity HWM / day start | RiskManager |

No multi-position grid state.

## 7. File layout (target)

```text
Robots/PmLh/
  PmLh.sln
  docs/                          # this tree
  PmLh/
    PmLh.csproj                  # links ../../../Common/*.cs
    PmLh.cs
    SignalEngine.cs
    PocMigrationTracker.cs       # optional separate file
```

## 8. Testing approach

| Level | What |
| --- | --- |
| Synthetic unit-style | Feed `SignalContext` fixtures → expect codes (no broker) |
| Chart visual | Visualize profile; debug rejects |
| cTrader BT | Every tick / open prices as available; dump report to `5-reports/` |
| Ablation | Toggle E4–E7, Entry Mode, LvnSource, N/K/M_min |

## 9. Non-goals in architecture v1

- Shared mutex service with VH  
- Partial fills / OCO multi-TP  
- Tick-based entry  
- POC-based exit manager
