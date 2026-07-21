# PRD — ClusterWickLimit

**Status:** LOCKED + implemented (**v1.1 structure scale**)  
**Platform:** cTrader Automate (cBot, C#)  
**Spec version:** **v1.1**  
**Date:** 2026-07-13  
**Code of record:** `Robots/ClusterWickLimit/ClusterWickLimit/`  

> Mọi thay đổi rule = bump version và ghi changelog.

## v1.1 scale (2026-07-13)

Micro SL/TP (~0.8–1.4$) failed on **tick data**. v1.1:

- **Primary TF:** M5 preferred (M1 OK with ATR on).  
- **Strategy pips:** 100 pips = 1.0 price.  
- **Effective distance:** `max(pips_floor, ATR × mult)` when `Use ATR Scaling`.  
- **SL band floors:** Min **250** pips (2.5$), Max **600** pips (6$), + ATR 0.8× / 2.5×.  
- **Cluster/approach/tol/body/wick** raised to structure size (see code defaults).  
- **Trail Dist 120 pips**, BE Lock **15 pips**.  
- Validate on **tick data only**; do not trust M1 opening-prices PnL.  
- **HTF bias (default on):** H1, lookback 3 bars; only Long if HTF up, only Short if HTF down; flat → no trade (`F_HTF_FLAT` / `F_HTF_BIAS`).

---

## 1. Identity

| Field | Value |
| --- | --- |
| Name | ClusterWickLimit |
| Label | `ClusterWickLimit` |
| Platform | cTrader Automate (C# / .NET 6) |
| Symbols | XAUUSD (primary) |
| Signal TF | M5 preferred; M1 + ATR OK |
| Account | Personal research; prop optional later |
| Position model | Max **1** position or **1** pending (same label); no pyramid |
| Units | Price distance on XAU; `0.01` = 1 logical pip in this PRD |

---

## 2. Goals & non-goals

### Goals

- Trade **liquidity cluster + wick reject + retest limit** on M1 XAU.  
- Testable rules (E/F/X codes) for journal and ablation.  
- Exit: **single TP by RR**; BE/trail by **R**; **no partial**.  
- Risk % sizing + daily/trade caps.

### Non-goals (v1.0)

- Partial scale-out / multi-TP.  
- HTF bias, RSI, MA filters.  
- Grid / martingale / multi-symbol.  
- Auto economic calendar download (news = injectable schedule or manual windows).  
- Forming-bar signals.

---

## 3. Market regime

| Trade when | Do not trade when |
| --- | --- |
| Session: **London and/or NY** enabled (default both on; Asia **off**) | Outside all enabled sessions |
| `range20 ≥ MinRange` | Dead tape |
| `spread ≤ MaxSpread` | Spread too wide |
| Outside news blackout ±`NewsMin` | Inside news window (high-impact) |
| No open position / pending with label | Already armed or in market |
| Trades filled today &lt; `MaxTradesDay` | Cap hit |
| Equity daily loss / DD gates OK (if enabled) | Kill-switch active |

**Fail regime (stand down expected):** clean trend days with shallow fake wicks; thin Asia clusters (session off by default).

---

## 4. Parameters (defaults locked for v1.0)

### 4.1 Cluster & volatility

| Param | Default | Description |
| --- | --- | --- |
| `LookbackBars` | 90 | Bars scanned for clusters (`shift` 1..N) |
| `MaxClusterAgeBars` | 120 | Newest touch must be ≤ this age |
| `MinTouches` | 3 | Min wick extremes in band |
| `Base Band (pips)` | **30** (0.30) | Floor for cluster band |
| `TolFactor` | 0.30 | `tol = max(BaseBand, TolFactor × range20)` |
| `RangeBars` | 20 | Window for `range20` |
| `Min Range (pips)` | **50** (0.50) | Min `range20` to trade |
| `Max Approach (pips)` | **250** (2.50) | Max \|close[1] − clusterLevel\| at arm |

```text
range20 = highest(high, RangeBars) - lowest(low, RangeBars)   // on closed bars
tol     = max(BaseBand, TolFactor × range20)
```

### 4.2 Wick confirmation

| Param | Default | Description |
| --- | --- | --- |
| `WickBodyMin` | 1.5 | Reject wick ≥ this × body |
| `Max Body (pips)` | **80** (0.80) | Max \|close − open\| (M5-friendly) |
| `ClosePosMax` | 0.35 | Short: closePos ≤ this |
| `ClosePosMin` | 0.65 | Long: closePos ≥ this |

```text
body      = abs(close - open)
// if body < tickSize → body = tickSize for ratio only
upperWick = high - max(open, close)
lowerWick = min(open, close) - low
barRange  = high - low
closePos  = (close - low) / barRange    // require barRange > 0
```

### 4.3 Entry offset

| Param | Default | Description |
| --- | --- | --- |
| `EntryOffsetK` | 0.40 | Limit outside cluster: `± EntryOffsetK × tol` |

### 4.4 SL gate (structure)

**XAU pip scale (locked):** **100 pips = 1.0 price** → `1 pip = 0.01`.  
Not broker `PipSize` (often 0.1).

| Param (UI) | Default | Price equiv | Description |
| --- | --- | --- | --- |
| `SL Buffer (pips)` | **20** | 0.20 | Beyond extreme wick of cluster touches |
| `SL Min (pips)` | **80** | 0.80 | Min \|entry − sl\|; else skip |
| `SL Max (pips)` | **140** | 1.40 | Max \|entry − sl\|; else skip |

```text
priceDistance = pips × 0.01          // 100 pips = 1.0
R_pips = |entry − sl| / 0.01
trade only if SlMinPips ≤ R_pips ≤ SlMaxPips
```

### 4.5 Exit (RR only — LOCKED)

| Param | Default | Description |
| --- | --- | --- |
| `TpRR` | 1.5 | **Single** take profit = `TpRR × R` |
| `UseTrail` | true | Enable R-based trailing |
| `BeAtRR` | 1.0 | Move SL to BE when profit ≥ this R |
| `BE Lock (pips)` | **5** | Lock beyond entry after BE (strategy pips; 100=1.0). 0 = 1 tick |
| `TrailStartRR` | 1.0 | Start trail when profit ≥ this R |
| `Trail Dist (pips)` | **40** | Trail distance behind price (strategy pips; 40 = 0.40) |

**Constraints:**

```text
TpRR > 0; TrailStartRR ≥ BeAtRR > 0
Trail Dist (pips) > 0
Partial = OFF          // permanent for v1.0
TP2 / scale-out = OFF  // permanent for v1.0
// BE lock + trail distance are absolute strategy pips, not R multiples
```

### 4.6 Pending lifecycle

| Param | Default | Description |
| --- | --- | --- |
| `PendingTtlBars` | 12 | Cancel unfilled after N new bars |
| `BreakBuffer` | 0.02 | Acceptance break beyond extreme |
| `CancelApproachMult` | 1.5 | Cancel if price leaves cluster &gt; `MaxApproach × this` |

### 4.7 Filters & risk

| Param | Default | Description |
| --- | --- | --- |
| `MaxSpread` | 0.25 | Price; broker-dependent |
| `NewsMin` | 30 | Minutes before/after red news |
| Session London | on | |
| Session NY | on | |
| Session Asia | **off** | |
| `RiskPercent` | 0.25–0.5 | Equity risk per trade (implementer default 0.5) |
| `MaxTradesDay` | 6 | **Filled** trades, not arms |
| `MaxDailyLossPct` | 1.5–2.0 | Optional equity kill |
| `MaxEquityDdPct` | (account) | Optional |

---

## 5. Entry rules (testable)

**Signal bar:** only **closed** bar `shift = 1`. Never arm on forming bar 0.

### 5.1 Cluster detection (E_CLUSTER)

**Sell-side cluster (liquidity above):**

1. Scan highs on bars `1..LookbackBars`.  
2. Group / clusterize highs so members lie within a band of width related to `tol` (implementation: density cluster or seed+merge with pairwise distance ≤ `tol` to level).  
3. `clusterHigh = median(highs in group)`.  
4. A bar **touches** if `|high[i] - clusterHigh| ≤ tol`.  
5. Valid if `touches ≥ MinTouches` and age of newest touch ≤ `MaxClusterAgeBars`.  
6. `clusterExtremeHigh = max(high of touch bars)` — for SL.

**Buy-side:** mirror with lows → `clusterLow`, `clusterExtremeLow`.

**Selection:**

- At most one sell cluster and one buy cluster: **nearest** to `close[1]` among valid clusters with approach ≤ `MaxApproach`.  
- At most one side armed per bar.  
- If both sides would confirm: **skip** (F_BOTH_SIDES).

### 5.2 Wick confirmation (E_WICK)

On bar `1` only.

**Short (needs sell cluster):**

| Code | Rule |
| --- | --- |
| E_W1 | `upperWick ≥ WickBodyMin × body` |
| E_W2 | `body ≤ MaxBody` |
| E_W3 | `closePos ≤ ClosePosMax` |
| E_W4 | Wick at level: `high` within cluster band — `|high - clusterHigh| ≤ tol` **or** `high ∈ [clusterHigh - tol, clusterExtremeHigh + tol]` |
| E_W5 | No acceptance: `close < clusterHigh` |

**Long:** mirror (`lowerWick`, `closePos ≥ ClosePosMin`, low at cluster, `close > clusterLow`).

### 5.3 Arm filters (E_ARM)

All must pass:

| Code | Rule |
| --- | --- |
| E_A1 | E_CLUSTER + E_WICK |
| E_A2 | `range20 ≥ MinRange` |
| E_A3 | `spread ≤ MaxSpread` |
| E_A4 | `abs(close[1] - clusterLevel) ≤ MaxApproach` |
| E_A5 | Session allowed |
| E_A6 | Outside news blackout |
| E_A7 | No position/pending with label |
| E_A8 | Computed `slDist ∈ [SlMin, SlMax]` |

### 5.4 Limit price (E_ENTRY)

```text
// Short
entry = clusterHigh + EntryOffsetK × tol
sl    = clusterExtremeHigh + SlBuffer
// Long
entry = clusterLow - EntryOffsetK × tol
sl    = clusterExtremeLow - SlBuffer

slDist = abs(entry - sl)
// if slDist < SlMin or slDist > SlMax → do not place (E_A8 fail)
// Normalize entry/sl to tick; respect broker stops level
```

Place **SellLimit** / **BuyLimit** with SL set; TP set from §6 using `R = slDist` (known pre-fill when entry & SL fixed).

---

## 6. Exit rules (LOCKED)

### 6.1 R definition

```text
R = abs(entry - sl)    // fixed at place/fill; do not recompute mid-trade
```

### 6.2 Single take profit (X_TP)

```text
// Long
tp = entry + TpRR × R
// Short
tp = entry - TpRR × R
```

- **One** TP on the full volume.  
- **No** partial close.  
- **No** second TP.

### 6.3 Break-even (X_BE)

When `profitR ≥ BeAtRR` and BE not yet applied:

```text
profitR = favorable_move / R
// favorable_move: long Bid-entry; short entry-Ask (pick one convention; document in code)
newSl = long ? entry + BeBuffer : entry - BeBuffer
// Only tighten SL (long: newSl > currentSl; short: newSl < currentSl)
```

### 6.4 Trailing (X_TRAIL)

When `UseTrail` and `profitR ≥ TrailStartRR`:

```text
// Long:  candidateSl = Bid - TrailDistRR × R
// Short: candidateSl = Ask + TrailDistRR × R
// Only tighten; never widen
```

Manage on tick (preferred) or each bar close minimum.

### 6.5 Exit outcomes

| Event | Action |
| --- | --- |
| Price hits TP | Full close |
| Price hits SL (initial / BE / trail) | Full close |
| Manual / kill-switch | Full close / flatten per risk module |

---

## 7. Pending lifecycle (X_PEND)

| Event | Action |
| --- | --- |
| Bars since arm ≥ `PendingTtlBars` | Cancel pending |
| Acceptance short: closed bar with `close > clusterExtremeHigh + BreakBuffer` | Cancel sell limit |
| Acceptance long: mirror | Cancel buy limit |
| Distance from cluster &gt; `MaxApproach × CancelApproachMult` before fill | Cancel |
| Optional: spread &gt; `MaxSpread` while pending | Cancel |
| After fill | Manage X_BE / X_TRAIL / X_TP only |
| Re-arm same level | Block until price leaves level by `> 2 × tol` (anti-spam) |

---

## 8. Risk

| Field | v1.0 default |
| --- | --- |
| Risk per trade | `RiskPercent` of equity, sized from `R` (SL distance) |
| Max positions | 1 |
| Max pending | 1 |
| Max filled trades / day | 6 |
| Daily loss stop | Recommended on (equity-based) |
| Lot | Risk-based; normalize volume step/min/max |

---

## 9. Reject / journal codes (minimum)

| Code | Meaning |
| --- | --- |
| F_SESSION | Outside session |
| F_NEWS | News blackout |
| F_SPREAD | Spread &gt; max |
| F_RANGE | range20 too small |
| F_APPROACH | Too far from cluster |
| F_TOUCHES | Cluster touches &lt; min |
| F_WICK | Wick rules fail |
| F_SL_BAND | slDist outside [SlMin,SlMax] |
| F_BOTH_SIDES | Both sides valid same bar |
| F_BUSY | Position or pending exists |
| F_TTL | Pending expired |
| F_ACCEPT | Acceptance break cancel |
| E_ARM | Armed limit |
| E_FILL | Filled |
| X_TP / X_SL / X_BE / X_TRAIL | Exit path markers |

---

## 10. Definition of done (implement)

- [ ] All thresholds numeric; no ambiguous language in code paths  
- [ ] Arm only on closed bar  
- [ ] Single TP by `TpRR`; no partial paths  
- [ ] BE/trail only in R multiples  
- [ ] SL structure + skip outside band  
- [ ] Pending TTL + acceptance + anti-spam  
- [ ] Journal: cluster level, touches, tol, entry, R, TpRR, arm/fill/cancel reason  
- [ ] Backtest path ready (every tick preferred)

---

## 11. Tester acceptance (initial — research)

Not go-live criteria; first gate after code:

| Metric | Tentative |
| --- | --- |
| Net expectancy after commission/spread | &gt; 0 on in-sample research window |
| OOS | Separate holdout; flat/negative → iterate params **or** abandon thesis, no endless curve-fit |
| Fill vs arm ratio | Log; low fill OK if expectancy holds |

---

## 12. Changelog

| Version | Date | Notes |
| --- | --- | --- |
| v1.0 | 2026-07-13 | Initial lock: cluster+wick+limit; exit 1×TP RR + BE/trail R; no partial |

---

## 13. Open implementation details (non-blocking for strategy lock)

These do **not** change strategy intent; resolve at architecture/code:

1. Exact clusterize algorithm (median seed vs histogram bins) — must satisfy touch/median/extreme definitions above.  
2. News source wiring.  
3. Whether TP is set on pending at place time vs on fill (prefer **at place** if broker allows absolute TP from entry).  
4. RiskManager / SessionFilter / NewsFilter reuse from `Common/` vs inline — engineering choice.

---

**HANDOFF status:** Strategy **LOCKED**. Ready for PLAN + architecture + cBot implementation when requested.
