# PRD — Responsive Micro Scalper (RMS) v1.0

**Status:** **ABANDONED** 2026-07-13 — scaffold kept as archive only; **no further development**  
**Platform:** cTrader cBot (C# / .NET 6)  
**Spec version:** v1.0.0  
**Date:** 2026-07-13  
**Owner:** RedWave / personal XAU  
**Source:** Adaptive Edition narrative (2026-07-12) → locked implementable rules  

> **Code name:** `Rms` / label `Rms`  
> **Not the same strategy** as `DynamicMicroScalper` (pending-stop distance engine). This is a **new** bot.

---

## 0. Decision log (locked for v1)

| Decision | Locked value | Why |
| --- | --- | --- |
| Signal timing | **Closed M1 bar only** | Avoid OnTick spam, BT ≠ live drift |
| Manage timing | OnTick + OnBar for exit/BE/trail/time-stop | Responsiveness without re-entry noise |
| RR | **TP = 1.5 × SL distance** | Single expectancy model; drop dual “WR 55% + RR 1:2” claim |
| Sizing | **Risk % of equity vs SL** (XAU-safe) | Fixed 0.01 is research-only override |
| Regime modes | **3 modes, scale table only** | No free-form score weights in v1 |
| Session | London / NY / Overlap **on**; Asia **off** | Cut thin-liquidity noise |
| Daily risk | **Hard stop trading** after daily loss gate | Required at 8–15 trades/day intent |
| News | Optional filter (default **off**) | Non-goal calendar auto; param ready |
| Autocorrelation / HMM / ML | **Out of v1** | Anti-overfit |

**Assumption labels:** Defaults below are **first-compile / first-BT** starting points, not claimed optimal live params.

---

## 1. Identity

| Field | Value |
| --- | --- |
| Name | Responsive Micro Scalper (RMS) |
| Platform | cTrader Automate (C# / .NET 6) |
| Symbols | **XAUUSD** primary (architecture symbol-agnostic where possible) |
| Signal TF | **M1** closed bar |
| Bias TF | **H1** default (param: H1 / H4) |
| Account | Personal first |
| Position model | **Max 1** open position; bot label `Rms`; no pyramid / grid / martingale |
| Libraries | Prefer `RedWave.Common`: RiskManager, TrailingManager, SessionFilter, NewsFilter (opt), Logger, PriceUtils |

---

## 2. Goals & non-goals

### Goals

- Micro **momentum + acceleration** continuation aligned with HTF bias.  
- **Volatility-scaled** SL/TP and adaptive threshold/cooldown via regime modes.  
- Controllable frequency: cooldown + daily cap + single position.  
- Positive **expectancy after costs** as primary success metric (not vanity WR).  
- Testable reject codes for ablation.  
- Low-maintenance VPS: no discretionary levels / SMC drawings.

### Non-goals (v1)

| Out of scope |
| --- |
| Tick/HFT scalping, multi-position, martingale/grid |
| MA / RSI / ADX / candle patterns / VP / SMC zones as signal |
| Autocorrelation, Hurst, HMM, ML regime |
| Dynamic correlation (DXY, US30) |
| Partial scale-out |
| Telegram / multi-symbol production |
| Claiming uniqueness as edge proof |

### Edge hypothesis (falsifiable)

> On XAU M1, when HTF bias is defined and **short-horizon log-return velocity is accelerating** beyond a **vol-normalized** threshold, the move **persists** long enough (≤ max hold) for a **1.5R** target more often than costs + false accelerations destroy expectancy.

**Fail modes:** mean-reverting micro noise; post-spike exhaustion; spread/slippage eating R; HTF bias trapping counter-pullbacks.

---

## 3. Units & conventions (mandatory)

| Concept | Definition |
| --- | --- |
| **Price** | Symbol quote (XAU typically 2 decimals) |
| **Pip** | `Symbol.PipSize` (cTrader). Distances logged in pips = `priceDistance / PipSize` |
| **Point** | `Symbol.TickSize` when needed for normalize |
| **Spread (pips)** | `(Ask - Bid) / PipSize` at evaluation time |
| **Log return** | `r_t = ln(C_t / C_{t-1})` on **closed** bar closes; if `C ≤ 0` → invalid bar |
| **ATR** | Wilder ATR(**14**) on signal TF (M1), **price units** |
| **HTF bar** | Closed bar on bias TF only (no forming bar) |
| **Index** | Signal bar = last **closed** M1: `Bars.Count - 2` (standard cTrader); never use forming bar for entry |

**Warm-up:** No signal until buffers ready:

- M1 closes: ≥ `max(MomPeriod + 2, VarWindow + 2, AtrPeriod + AtrAvgPeriod + 5)`  
- HTF closes: ≥ `HtfLookback + 2`  
- ATR series: ≥ `AtrPeriod + AtrAvgPeriod`

---

## 4. Market regime (when to trade)

### Trade when (all true)

| ID | Condition |
| --- | --- |
| F1 | Symbol tradeable; spreads finite; not weekend flat if broker closed |
| F2 | Session filter allows (see §8) |
| F3 | Spread ≤ `MaxSpreadPips` |
| F4 | RiskManager allows (equity DD + daily loss/profit gates) |
| F5 | Trades today &lt; `MaxTradesDay` **after regime scale** (integer floor ≥ 1) |
| F6 | No open position with label `Rms` on symbol |
| F7 | Cooldown elapsed (closed bars since last **exit** ≥ scaled cooldown) |
| F8 | Buffers warm; ATR &gt; 0; vol mult finite |
| F9 | News OK if `UseNewsFilter` |
| F10 | Micro not chop (`E_VAR`) |
| F11 | HTF bias not flat (`E_BIAS`) — v1 bias is binary; flat only if equal closes (rare) or optional min strength fail |
| F12 | Regime mode ≠ **StandDown** (v1: only A/N/C; StandDown reserved = never unless future flag) |

### Do not trade

- Any filter fail above  
- Opposite-side signal while in position (ignored; no reverse-flip v1)  
- After daily loss flatten / kill for remainder of UTC day  

---

## 5. Core formulas (single meaning)

All series use **closed** bars. Let `C[i]` = close of closed bar `i` (i increases with time; `t` = signal bar).

### 5.1 One-bar log return

```text
r[t] = ln(C[t] / C[t-1])
```

### 5.2 Micro momentum (velocity)

```text
K = MomPeriod                    // default 3
M[t] = ln(C[t] / C[t-K])        // ≡ sum of K one-bar log returns
```

### 5.3 Acceleration

```text
A[t] = M[t] - M[t-1]
     = ln(C[t] / C[t-K]) - ln(C[t-1] / C[t-1-K])
```

### 5.4 Micro variance (chop detector)

```text
W = VarWindow                    // default 10
μ = (1/W) * Σ_{j=0..W-1} r[t-j]
V[t] = (1/W) * Σ_{j=0..W-1} (r[t-j] - μ)^2     // population variance
```

Pass chop filter if:

```text
V[t] ≥ VarMinBase * RegimeVarScale(mode)
```

### 5.5 Volatility for SL/TP and threshold scale

```text
ATR[t]     = WilderATR(C, H, L; period = AtrPeriod)     // price
ATR_avg[t] = SMA(ATR, AtrAvgPeriod)                     // default 100
volMult[t] = clamp(ATR[t] / ATR_avg[t], VolMultMin, VolMultMax)
             // defaults clamp [0.50, 2.50]; if ATR_avg ≤ 0 → invalid
```

### 5.6 Dynamic acceleration threshold

```text
thresh[t] = BaseAccelThresh * volMult[t] * RegimeAccelScale(mode)
```

### 5.7 HTF bias

On bias TF, last **closed** HTF bar `H`:

```text
N = HtfLookback                  // default 20
Δ = Close_H[H] - Close_H[H-N]

Bias:
  Bull if Δ > 0
  Bear if Δ < 0
  Flat if Δ == 0 → reject E_BIAS_FLAT

Optional strength (default ON):
  ATR_htf = WilderATR on HTF, period AtrPeriod
  S = abs(Δ) / ATR_htf
  If UseHtfStrength and S < HtfMinStrength → reject E_BIAS_WEAK
```

### 5.8 Regime classification (re-eval every `RegimeEvalBars` M1 closes)

```text
volRatio = ATR[t] / ATR_avg[t]          // same as pre-clamp ratio
S_htf    = abs(Δ) / ATR_htf             // from §5.7; if ATR_htf≤0 → Normal fallback

mode =
  Aggressive  if volRatio ≥ VolHigh and S_htf ≥ StrengthHigh
  Conservative if volRatio ≤ VolLow or S_htf < StrengthLow
  Normal      otherwise

// If both Aggressive and Conservative conditions true (should be rare):
// prefer Conservative (safety).
```

| Mode | AccelScale | CooldownScale | MaxTradesScale | VarMinScale | SlDistScale |
| --- | --- | --- | --- | --- | --- |
| Aggressive | 0.85 | 0.70 | 1.25 | 0.90 | 1.10 |
| Normal | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 |
| Conservative | 1.25 | 1.40 | 0.70 | 1.15 | 0.90 |

```text
cooldownBars = max(1, round(CooldownBase * CooldownScale))
maxTradesDay = max(1, round(MaxTradesDayBase * MaxTradesScale))
```

Regime is **sticky** between re-evals (no tick flip). On re-eval bar, recompute and log `REGIME_*`.

### 5.9 SL / TP distances (price)

```text
slDist = ATR[t] * SlMult * SlDistScale(mode)
tpDist = slDist * TpRr                        // TpRr default 1.5
```

- Long: `SL = entry - slDist`, `TP = entry + tpDist`  
- Short: `SL = entry + slDist`, `TP = entry - tpDist`  
- Normalize to tick; if `slDist < MinSlPips * PipSize` → reject `E_SL_TINY`  
- If `slDist > MaxSlPips * PipSize` → reject `E_SL_WIDE`

### 5.10 Position size

| Mode | Rule |
| --- | --- |
| `RiskPercent` (default) | Volume from equity × `RiskPercent` / risk per unit for `slDist` (XAU-safe path same family as SvbsX/VH: Oz / FixedRisk via Common if available) |
| `FixedLots` | `FixedLots` → volume units |

Volume normalized to `VolumeInUnitsMin/Step/Max`. If volume rounds to 0 → reject `E_LOT`.

---

## 6. Entry rules (testable)

Evaluated **once per new closed M1 bar** when flat and filters F1–F9 pass.

### 6.1 Long (Buy) — all must pass

| ID | Rule |
| --- | --- |
| E_BIAS_L | Bias = Bull (and strength OK if enabled) |
| E_VAR | `V[t] ≥ VarMinBase * VarMinScale(mode)` |
| E_MOM_L | `M[t] > 0` |
| E_ACC_L | `A[t] > thresh[t]` |
| E_SL | slDist within [MinSlPips, MaxSlPips] in pips |
| E_LOT | Sized volume valid |
| E_GATE | F4–F7, F9 |

→ `ExecuteMarketOrder(Buy)` with SL/TP absolute; label `Rms`; magic/comment per bot standard.

### 6.2 Short (Sell) — symmetric

| ID | Rule |
| --- | --- |
| E_BIAS_S | Bias = Bear (+ strength) |
| E_VAR | same |
| E_MOM_S | `M[t] < 0` |
| E_ACC_S | `A[t] < -thresh[t]` |
| E_SL / E_LOT / E_GATE | same |

### 6.3 Mutual exclusion

- If both long and short conditions true (pathological): **no trade**, log `E_BOTH`.  
- One evaluation per bar max; at most one entry per bar.

### 6.4 Entry execution details

| Item | Rule |
| --- | --- |
| Order type | Market |
| Slippage | Broker/cTrader defaults; log fill vs signal mid |
| Signal price ref | Mid or Bid/Ask consistent: long uses Ask entry assumption for SL math; short Bid — or use fill price to set SL/TP if API modifies post-fill (prefer set on order) |
| Same-bar reentry | Forbidden while position open; after exit, cooldown applies |

---

## 7. Exit rules (priority)

While position open, evaluate continuously (OnTick) and on bar:

| Priority | ID | Rule |
| --- | --- | --- |
| 1 | X_DAILY | RiskManager daily loss flatten → close market, stop new entries rest of day |
| 2 | X_SL | Hard SL hit (broker or protect) |
| 3 | X_TP | Hard TP hit |
| 4 | X_TIME | Hold time ≥ `MaxHoldMinutes` (wall clock from position open time) → market close |
| 5 | X_BE | If `UseBreakeven`: when unrealized ≥ `BeTriggerR * slDist` in price, move SL to entry ± `BeLockPips` (and optional + spread) |
| 6 | X_TRAIL | If `UseTrailing`: after `TrailStartR * slDist` profit, trail by `TrailStepR * slDist` (full size only) |

**v1:** no partial; no opposite-signal exit (optional later `ExitOnFlip` default **false**).

On any exit → start cooldown counter from that closed-bar index / timestamp; increment `TradesToday` on **entry** (not exit). Daily trade counter resets on **UTC date** change (align with RiskManager day if shared).

---

## 8. Session & calendar

### Session toggles (reuse `CSessionFilter` pattern)

| Toggle | Default |
| --- | --- |
| Trade Asian | **false** |
| Trade London | **true** |
| Trade New York | **true** |
| Trade EU-US Overlap | **true** |

Hours: existing Common session windows (UTC) — same as other RedWave cBots; do not invent a second clock in v1.

### Optional news

| Param | Default |
| --- | --- |
| Use News Filter | false |
| Blackout minutes before/after | 15 / 15 |

---

## 9. Risk & safety

| Control | Default | Behavior |
| --- | --- | --- |
| Risk % / trade | **0.50** | vs SL distance |
| Max Equity DD % | **12** | stop new trades (and optional flatten if Common supports) |
| Max Daily Loss $ | **0** (off) **or** set in BT | when &gt; 0: no new trades; `FlattenOnDailyLoss` default **true** |
| Max Daily Profit $ | 0 (off) | optional |
| Max Trades / Day base | **12** | scaled by regime |
| Cooldown base (bars) | **5** | scaled by regime |
| Kill switch | User stop cBot | always |

**No** martingale, grid, recover mode, or average-down.

---

## 10. Parameters (code defaults)

### 10.1 General

| Parameter | Default | Range / notes |
| --- | --- | --- |
| Bot Label | `Rms` | string |
| Magic Number | `20260713001` | double/long per convention |
| Position Size Mode | RiskPercent | RiskPercent / FixedLots |
| Risk % | 0.50 | 0.1–2.0 |
| Fixed Lots | 0.01 | override mode |
| Max Trades / Day (Base) | 12 | 3–25 |
| Cooldown Bars (Base) | 5 | 1–30 |
| Max Hold Minutes | 12 | 5–60 |
| Max Spread (pips) | **50** | measure live; **not** 0.5 — was unrealistic on XAU |
| Max Equity DD % | 12 | 0 = off if supported |
| Max Daily Loss ($) | 0 | 0 = off |
| Flatten On Daily Loss | true | |
| Max Daily Profit ($) | 0 | 0 = off |

### 10.2 Signal

| Parameter | Default | Notes |
| --- | --- | --- |
| HTF Timeframe | H1 | H1 / H4 |
| HTF Lookback | 20 | bars |
| Use HTF Strength | true | |
| HTF Min Strength | 0.50 | ×ATR_htf |
| Mom Period (K) | 3 | 2–8 |
| Base Accel Threshold | **8e-5** | log-return units; **must re-calibrate** on first BT histogram of A[t] |
| Var Window (W) | 10 | 5–20 |
| Var Min (Base) | **5e-8** | variance of log returns; re-calibrate with A |
| ATR Period | 14 | |
| ATR Avg Period | 100 | for volMult |
| Vol Mult Min / Max | 0.50 / 2.50 | clamp |
| SL Mult | 1.20 | ×ATR |
| TP RR | **1.50** | TP = SL × RR |
| Min SL (pips) | 20 | floor |
| Max SL (pips) | 400 | cap (XAU M1) |
| Regime Eval Bars | 90 | M1 closes |
| Vol High / Low | 1.30 / 0.70 | volRatio |
| Strength High / Low | 1.00 / 0.35 | S_htf |

> **Calibration note:** `BaseAccelThreshold` and `VarMinBase` from narrative were not unit-verified on live XAU M1. First BT task: log distributions of `A[t]`, `V[t]`, `thresh[t]` and set base so **target ~5–15 signals/day before cooldown**, not after overfitting exits.

### 10.3 Exit extras

| Parameter | Default |
| --- | --- |
| Use Breakeven | false |
| BE Trigger (R) | 0.80 |
| BE Lock (pips) | 2 |
| BE Add Spread | true |
| Use Trailing | false |
| Trail Start (R) | 1.00 |
| Trail Step (R) | 0.40 |

### 10.4 Session / news / log

| Parameter | Default |
| --- | --- |
| Session toggles | §8 |
| Use News Filter | false |
| Log Level | Info |
| Log Regime Each Eval | true |
| Log Reject Codes | true |

---

## 11. Reject / event codes

| Code | Meaning |
| --- | --- |
| `F_SPREAD` | Spread &gt; max |
| `F_SESSION` | Outside session |
| `F_RISK` | Equity/daily gate |
| `F_MAXTRADES` | Day cap |
| `F_POSITION` | Already in position |
| `F_COOLDOWN` | Cooldown active |
| `F_WARMUP` | Buffers not ready |
| `F_NEWS` | News blackout |
| `E_BIAS_FLAT` | ΔHTF = 0 |
| `E_BIAS_WEAK` | Strength below min |
| `E_VAR` | Micro variance too low (chop) |
| `E_MOM` | Momentum wrong sign / zero |
| `E_ACC` | Acceleration below threshold |
| `E_SL_TINY` / `E_SL_WIDE` | SL distance bounds |
| `E_LOT` | Volume invalid |
| `E_BOTH` | Long+short same bar |
| `REGIME_AGG` / `REGIME_NRM` / `REGIME_CON` | Mode set |
| `ENT_L` / `ENT_S` | Entry sent |
| `X_SL` / `X_TP` / `X_TIME` / `X_BE` / `X_TRAIL` / `X_DAILY` | Exit reason |

---

## 12. Architecture (implementation constraints)

| Component | Responsibility |
| --- | --- |
| `Rms.cs` (Robot) | Params, OnStart/OnBar/OnTick, order I/O, session/risk wiring |
| `SignalEngine.cs` | Pure calc: M, A, V, bias, regime, thresh, pass/fail + reject code |
| `TradeManager` path | Size, market entry, SL/TP, BE/trail via Common TrailingManager |
| State | `TradesToday`, `LastExitBarIndex`, `CurrentRegime`, `DayStampUtc` |

**Rules:**

- SignalEngine **must not** place orders (unit-testable).  
- No heavy alloc per tick; ring buffers for closes/returns.  
- Prefer ATR from cAlgo indicator API or self-contained Wilder (document which).  
- **Do not** rewrite `DynamicMicroScalper` in place — separate project `Robots/ResponsiveMicroScalper/`.

### Event model

```text
OnBar (M1):
  if new closed bar:
    maybe re-eval regime
    if flat: EvaluateEntry()
    update logs

OnTick:
  if position: ManageExits (time, BE, trail)
  RiskManager checks
```

---

## 13. Backtest & acceptance (DoD)

### 13.1 Test setup

| Item | Requirement |
| --- | --- |
| Symbol | XAUUSD |
| Data | Tick or best available; **commission + realistic spread** model |
| Period | ≥ 12 months spanning vol expansion and range (e.g. 2025–2026) |
| Forward | Demo 2–4 weeks after IS pass |

### 13.2 Metrics (primary)

| Metric | Acceptance (v1 research gate) |
| --- | --- |
| Net expectancy / trade **after costs** | &gt; 0 |
| Profit factor | ≥ **1.15** first gate (1.30 aspirational) |
| Max equity DD | ≤ **20%** on risk 0.5% |
| Trades / day (avg) | **4–15** (adaptive; not forced to 8–12) |
| Win rate | **Not a hard gate** (informational) |
| Consistency | No single month owns &gt; 60% of total profit without review |

### 13.3 Mandatory checks

- [ ] All formulas match §5 (code review + sample hand calc on 20 bars)  
- [ ] No entry on forming bar  
- [ ] Cooldown and daily cap enforced  
- [ ] Regime scales applied to thresh/cooldown/max trades/SL  
- [ ] Daily loss gate stops entries  
- [ ] Time exit works  
- [ ] Reject codes appear in log  
- [ ] XAU volume sizing does not use broken TickValue blindly  
- [ ] Separate bot from DynamicMicroScalper  

### 13.4 Optimization discipline

1. Calibrate `BaseAccelThreshold` / `VarMinBase` from distributions (not grid-max PF).  
2. Freeze formulas; optimize **≤ 5** knobs (e.g. BaseAccel, SL Mult, Cooldown, MaxTrades, HtfMinStrength).  
3. Walk-forward or holdout last 20–30% of sample.  
4. Regime thresholds: change only if modes never fire or always Aggressive.

---

## 14. Roadmap (not v1)

| Phase | Item |
| --- | --- |
| v1.1 | Exit on bias flip; session flat end-of-day |
| v1.2 | Performance feedback (pause after N losses) |
| v1.3 | Correlation filter DXY |
| v2 | Partial scale-out; multi-symbol; richer regime |

---

## 15. Open questions (non-blocking; defaults apply if silent)

| # | Question | Default if unanswered |
| --- | --- | --- |
| Q1 | Prefer H1 or H4 bias for first BT? | **H1** |
| Q2 | Max Daily Loss $ amount for personal account size? | **0 (off)** until user sets |
| Q3 | Enable BE/Trail for first BT? | **both off** (isolate entry edge) |
| Q4 | Project folder name | `Robots/ResponsiveMicroScalper/` |

---

## 16. Definition of done (spec)

- [x] Rules unambiguous (E/F/X IDs)  
- [x] Pip/price/log-return units defined  
- [x] SL/TP/RR single model  
- [x] Risk kill rules defined  
- [x] Regime scale table fixed  
- [x] BT acceptance gates defined  
- [x] User **Y** on this PRD  
- [x] PLAN written  
- [x] Implement scaffold + compile (Release 0 err)  
- [x] BT/optimize feedback: OOS flat → **project stopped** (no calibrate/live path)  

---

*Not financial advice. User owns all trading risk.*
