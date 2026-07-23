# PRD — VacuumHunter v1.0

**Status:** Implemented (code of record: `Robots/VacuumHunter`, `Common/*`)  
**Platform:** cTrader cBot  
**Spec version:** v1.6 (aligned with code: VP2, RiskAmount, SlTimeFrame ATR buffer)  
**Date:** 2026-07  

---

## 1. Identity

| Field | Value |
| --- | --- |
| Name | VacuumHunter |
| Platform | cTrader Automate (C#) |
| Symbols | XAUUSD (primary) |
| Signal TF | M15 (M30 acceptable) |
| Bias TF | H1 (`HTF Timeframe`) |
| Account | Personal research; prop optional later |
| Position model | Netting / single label position (V1: max 1 open) |

---

## 2. Goals & non-goals

### Goals

- High-selectivity LVN vacuum setups on composite volume profile.
- Modular Common engines reusable by other bots.
- Risk % sizing that is correct on XAU (broker FixedRisk).
- Testable reject codes for ablation and journal forensics.

### Non-goals (v1.0)

- True exchange footprint / bid-ask volume.
- Auto economic calendar download.
- Multi-symbol portfolio / grid / martingale.
- ML (VpPaTransformer) integration.

---

## 3. Market regime

| Trade when | Do not trade when |
| --- | --- |
| Inside any **enabled** session (OR): Asia / London / NY / Overlap | Outside all enabled windows |
| Spread ≤ Max Spread (pips) | Spread too wide |
| Peak equity DD &lt; Max Equity DD % | Peak equity kill (block ± optional flatten) |
| Daily equity PnL &gt; −Max Daily Loss $ | Daily loss gate (block ± optional flatten) |
| Daily equity PnL &lt; +Max Daily Profit $ | Daily profit gate (block ± optional flatten) |
| Trades today &lt; Max Trades / Day | Cap hit |
| No open position with bot label | Already in market |
| Optional: outside news blackout | ±N min around scheduled news |

**Account risk note:** Daily loss/profit and equity DD use **Account.Equity** only (day start = first equity sample of UTC day). They are **not** per-order max loss. A single trade’s NetProfit can still exceed Daily Loss $ if Risk % sizes a large 1R or price gaps; gates act on **equity level**, then block/flatten.

---

## 4. Volume Profile (structure)

### Data

- Source: **bar `TickVolumes`**, distributed across OHLC into price bins.
- Not true futures volume.

### Adaptive composite

| Param | Default | Meaning |
| --- | --- | --- |
| Lookback Days | 4 | Trading days in composite |
| Bin Size | 0.5 | Price bin width ($) on XAU |
| Value Area % | 70 | VA expand from POC |
| Weight Decay | 0.8 | Day weight = decay^age (newest = 1) |
| LVN Threshold | 0.65 | Bin ≤ mean×threshold → low-vol candidate |
| HVN Threshold | 1.25 | Bin ≥ mean×threshold → high-vol region |
| Max LVN Width ($) | 25 | Split/reject oversized voids |
| Min LVN Strength | 0.20 | Min strength to trade |

### Outputs (`ProfileData`)

- Histogram, POC, VAH, VAL  
- HVN / LVN node list (`Low`, `High`, `Mid`, `Strength`)  
- Shape: `Bullish` | `Bearish` | `Neutral` | `DShape`  

### LVN acceptance

- Strength = max(shoulder-relative, mean-relative).  
- Prefer LVN between HVNs; allow **one-sided** HVN if strength ≥ 0.25.  
- Width ≤ Max LVN Width.

### Rebuild

- On each **closed** signal bar (`OnBar`), last closed index = `Count - 2`.

---

## 5. Entry rules (testable)

Signal evaluated **on closed bar only** (`SignalEngine`).

### Filters (all must pass)

| ID | Rule |
| --- | --- |
| F1 | Enabled sessions (`SessionFilter` OR) |
| F2 | News OK if enabled (`NewsFilter` schedule) |
| F3 | Spread OK (`MarketCondition`) |
| F3_EQUITY | `RiskManager.IsTradingAllowed` (peak DD + daily $ on equity) |
| F4 | Trades today &lt; Max Trades / Day |
| F5 | No open position with `Bot Label` on symbol |

### Structure / confirm

| ID | Rule |
| --- | --- |
| E1 | Closed bar high/low touches LVN (± `Touch Buffer ATR Mult` × ATR) |
| E2 | LVN strength ≥ Min LVN Strength; width ≤ Max LVN Width |
| E2 support | **Long:** support below LVN = nearest HVN or VAL/POC if `Allow POC/VA Targets` |
| E2 resist | **Short:** resistance above = nearest HVN or POC/VAH |
| E3 | Rejection candle (see below) |
| E4 | Optional: tick imbalance ≥ Min Delta Strength + min ticks |
| E5 | Optional: shape not opposing (block Bearish long / Bullish short) |
| E6 | Optional HTF: long if H1 close &gt; POC; short if H1 close &lt; POC |
| (TP) | Take-profit price chosen by bot exit mode (not an entry reject for RR/Fixed) |

### Rejection (E3)

**Long (bullish rejection)**

- Close in upper portion of bar range (closePos ≥ 0.45).  
- Lower wick ≥ body × `Rejection Wick/Body` **or** wick ≥ 25% range.  
- Bullish close or strong closePos ≥ 0.55.

**Short:** mirror (upper wick, closePos ≤ 0.55).

### Delta (E4) — optional

- `TickDeltaEngine`: mid uptick = buy, downtick = sell; zero ticks ignored.  
- Long strength = buy/sell; short = sell/buy.  
- Insufficient ticks in window → neutral (fails if filter on).  
- **Not** true exchange delta.

### Side selection

- Evaluate long and short independently on the same touched LVN.  
- If both pass: pick higher imbalance.  
- If both fail: combined reject reason string.

---

## 6. Exit rules (single TP, no partial)

| ID | Rule |
| --- | --- |
| X1 | **SL** long: wider of (LVN.Low − ATR×**LVN buffer**, entry − ATR×**Min SL distance**); short mirrored |
| X2 | **TP** single level, **full position**, broker TP at entry |
| X2a | TP Mode **RiskReward** (default): TP = entry ± SL_dist × RR Multiple (default 2.0) |
| X2b | TP Mode **Structure**: one magnet (HVN / POC / VA mid); abort if missing or RR &lt; 0.5 |
| X2c | TP Mode **FixedPrice**: TP = entry ± Fixed TP ($) |
| X3 | **BE** optional: after profit ≥ **BE Start (R)** × SL_dist; lock entry ± **BE Lock (R)** × SL_dist (+ spread if on) |
| X4 | **Trailing** optional: start after profit ≥ **Trail Start (R)** × SL_dist; trail distance = **Trail Step (R)** × SL_dist |
| X5 | No TP1/TP2 stack, no partial close |

### Position sizing

| Mode | Behavior |
| --- | --- |
| **RiskPercent** (default) | risk $ = Balance × Risk% / 100; volume via FixedRisk + conservative `vol ≤ risk$/slDist` |
| **RiskAmount** | risk $ = RiskAmount ($); volume via FixedRisk + conservative `vol ≤ risk$/slDist` |
| **FixedLots** | volume = FixedLots × LotSize (normalized); still scaled down if estimated risk > daily room |

- If Max Daily Loss $ > 0: remaining room = MaxDailyLoss + (Equity − dayStartEquity); all modes respect room (abort or scale).  
- Never force min volume when it would oversize risk.

### Account risk runtime (`CRiskManager`)

| Hook | Behavior |
| --- | --- |
| Bot `OnTick` → `RiskManager.OnTick()` | Recompute equity gates; **flatten if configured** |
| Bot `OnBar` | **No** flatten; only `IsTradingAllowed` for new signals |
| Daily metrics | **Equity only** — not per-trade NetProfit |

---

## 7. Parameters (code defaults)

### Trade & Risk

| Parameter | Default |
| --- | --- |
| Enable Trading | true |
| Bot Label | VacuumHunter |
| Lot Size Mode | RiskPercent (default) \| RiskAmount \| FixedLots |
| Risk % | 0.75 (when RiskPercent) |
| Risk Amount ($) | 50.0 (when RiskAmount) |
| Fixed Lots | 0.01 (when FixedLots) |
| Max Trades / Day | 2 |
| Max Spread (pips) | 80 |
| Max Equity DD % | 10 (0=off; peak HWM; blocks new entries) |
| Flatten On Equity DD | false (true = also market-close all bot positions) |
| Max Daily Loss ($) | 0 (0=off; account currency vs day-start equity) |
| Flatten On Daily Loss | false |
| Max Daily Profit ($) | 0 (0=off; blocks new entries after target $) |
| Flatten On Daily Profit | false |
| Debug Logging | false |

Risk defaults: **block new only**. Enable Flatten**Spec version:** v1.4 (aligned with code: VP2, RiskAmount, MaxSlDistance & MaxLvnWidth filters)  

### Stop Loss

| Parameter (UI) | Property | Default | Meaning |
| --- | --- | --- | --- |
| **SL TimeFrame** | SlTimeFrame | Hour (H1) | Timeframe để lấy ATR buffer cho SL |
| ATR Period | AtrPeriod | 14 | ATR Period cho SL buffer |
| **LVN buffer (×ATR)** | SlAtrMult | 0.5 | Đệm **ngoài mép LVN**: Long SL = LVN.Low − ATR(HTF)×k; Short = LVN.High + ATR(HTF)×k |

### Take Profit

| Parameter | Default |
| --- | --- |
| TP Mode | RiskReward |
| RR Multiple | 2.0 |
| Fixed TP ($) | 20 |

### Break Even / Trailing (R = multiple of **this trade’s** SL price distance)

| Parameter (UI) | Default | Meaning |
| --- | --- | --- |
| Use Break Even | true | Enable BE |
| BE Start (R) | 1.0 | Trigger when profit ≥ 1 × SL distance |
| BE Lock (R) | 0.05 | Lock this far beyond entry |
| BE Add Spread | true | Add spread into lock |
| Use Trailing | **false** | Enable trail |
| Trail Start (R) | 1.5 | Start trail after 1.5R profit |
| Trail Step (R) | 0.5 | SL stays 0.5R behind price |

Why not raw $ or “pips”? SL width changes every trade; **R scales with SL**. On XAU, cTrader pip often = $0.01 so “20 pips” ≠ 20 gold points.

### Volume Profile (V2 Engine)

| Parameter | Default | Meaning |
| --- | --- | --- |
| VP Mode | Daily | Daily composite vs RollingHours intraday window |
| VP Lookback (Hours) | 8.0 | Lookback window when VP Mode = RollingHours |
| Lookback Days | 4 | Trading days in composite |
| Bin Size | 0.5 | Price bin width ($) on XAU |
| Value Area % | 70 | VA expand from POC |
| LVN Threshold | 0.65 | Bin ≤ mean×threshold → low-vol candidate |
| HVN Threshold | 1.25 | Bin ≥ mean×threshold → high-vol region |
| Weight Decay | 0.8 | Day weight = decay^age (newest = 1) |
| Min LVN Strength | 0.20 | Min strength to trade |
| Max LVN Width ($) | 25 | Split/reject oversized voids |
| Use M1 Source Bars | true | Compute bin delta using M1 intraday bars |
| Use Gaussian Smooth | true | 1D Gaussian kernel smoothing for noise reduction |
| Visualize Profile | true | Render VP on chart |

### Signal Filters

| Parameter | Default |
| --- | --- |
| Min Delta Strength | 1.2 |
| Min Delta Ticks | 15 |
| Delta Window (ms) | 300000 |
| Rejection Wick/Body | 0.35 |
| Require Delta Filter | **false** |
| Require Shape Filter | **false** |
| Require HTF Filter | **true** |
| Allow POC/VA Targets | **true** |
| Touch Buffer ATR Mult | 0.15 |
| HTF Timeframe | Hour |

### Session (UTC fixed windows, OR logic — enable/disable only)

| Parameter | Default | Fixed window (UTC) |
| --- | --- | --- |
| Trade Asia | false | 00:00–09:00 |
| Trade London | false | 07:00–16:00 |
| Trade New York | **true** | 13:30–23:00 |
| Trade Overlap (Lon-NY) | false | 13:00–16:00 |

No UI for hours (kept simple). Change windows only in `CSessionFilter` code if needed.

### News

| Parameter | Default |
| --- | --- |
| Enable News Filter | false |
| News Blackout (min) | 30 |
| News Schedule UTC | (empty; `yyyy-MM-dd HH:mm` list) |
---


## 8. Reject / pass codes (logging)

| Code | Meaning |
| --- | --- |
| F1_SESSION | Outside NY |
| F2_NEWS | News blackout |
| F3_SPREAD | Spread |
| F3_EQUITY | Equity DD kill |
| F4_MAX_TRADES | Daily cap |
| F5_OPEN_POS | Already in trade |
| NO_LVN / PROFILE_INVALID | Structure missing |
| E1_NO_TOUCH | Price not near LVN (`near=` in debug) |
| E2_WEAK_LVN / E2_LVN_TOO_WIDE | LVN quality |
| E2_NO_SUPPORT / E2_NO_RESIST | No magnet below/above |
| E3_NO_*_REJECT | Candle rejection fail |
| E4_DELTA* | Delta filter |
| E5_SHAPE_* | Shape filter |
| E6_HTF_* | HTF bias fail |
| PASS:LONG / PASS:SHORT | Entry allowed |

Info journal (default): PASS, OPEN, CLOSE, day summary, risk warnings.  
Debug: per-bar rejects.

---

## 9. Definition of done (v1.0 product)

- [x] Rules implementable without ambiguity for engineer  
- [x] SL/TP/risk defined; XAU sizing via FixedRisk + daily room  
- [x] Single hard TP (no partial race)  
- [x] RiskManager.OnTick equity gates + optional flatten  
- [x] Modular Common + bot  
- [x] Spec docs under `docs/` (kept aligned v1.2)  
- [ ] Walk-forward OOS freeze (research ops)  
- [ ] Live small-risk soak  

---

## 10. Risks & assumptions

1. Tick volume proxy may differ by broker.  
2. Low sample size → easy overfit if optimizing many knobs.  
3. Some XAU symbols report `TickValue=0.01` with `LotSize=100` inconsistently — code prefers FixedRisk.  
4. News schedule is **manual** string; empty = no blackout even if filter enabled with 0 events.  
