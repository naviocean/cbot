# PRD — HvnMagnet / HMPD v1.0

**Status:** Implemented (v1.0 code; research soak pending)  
**Platform:** cTrader cBot  
**Spec version:** v1.0  
**Date:** 2026-07-12  
**Strategy code:** HMPD (HVN Magnet Pullback + Delta Confirmation)

---

## 1. Identity

| Field | Value |
| --- | --- |
| Name | HvnMagnet |
| Platform | cTrader Automate (C#) |
| Symbols | XAUUSD (primary) |
| Signal TF | **M15** closed bar (M5 = research later only) |
| Bias TF | H1 (`HTF Timeframe`) |
| Account | Personal research first; prop optional with equity gates |
| Position model | Netting / single bot label; **max 1 open** |
| rwcommon | Reuse `RedWave.Common` (VolumeProfile, Risk, Delta, Session, …) |

---

## 2. Goals & non-goals

### Goals

- Selective **HVN pullback continuation** with **mandatory** bias + delta + shape (v1 research defaults).
- Testable reject codes for ablation and journal forensics.
- Risk % sizing correct on XAU (FixedRisk + daily room), same contract as VacuumHunter.
- **Min R gate** so sticky HVN / near-POC setups are skipped.
- Modular bot: pure `SignalEngine` + thin orchestrator.

### Non-goals (v1.0)

- True exchange footprint / bid-ask volume.
- Auto economic calendar download.
- Partial close / multi-TP runner stack.
- Merge into VacuumHunter as dual LVN+HVN mode.
- Multi-symbol portfolio / grid / martingale.
- ML integration.
- Marketing frequency of 4–8 trades/day.

---

## 3. Market regime

| Trade when | Do not trade when |
| --- | --- |
| Inside any **enabled** session (OR): London / NY / Overlap (Asia optional) | Outside all enabled windows |
| Spread ≤ Max Spread (pips) | Spread too wide |
| Peak equity DD &lt; Max Equity DD % | Peak equity kill (block ± optional flatten) |
| Daily equity PnL &gt; −Max Daily Loss $ | Daily loss gate |
| Daily equity PnL &lt; +Max Daily Profit $ | Daily profit gate |
| Trades today &lt; Max Trades / Day | Cap hit |
| No open position with bot label | Already in market |
| Optional: outside news blackout | ±N min around scheduled news |
| HTF bias aligned | Counter-trend vs POC |
| Profile shape not exhaustion vs side | Opposing shape |

**Account risk note:** Daily loss/profit and equity DD use **Account.Equity** only (day start = first equity sample of UTC day). Gates act on **equity level**, not guaranteed per-order max loss under gaps.

---

## 4. Volume Profile (structure)

### Data

- Source: bar `TickVolumes`, distributed across OHLC into price bins.
- Not true futures volume.

### Adaptive composite

| Param | Default | Meaning |
| --- | --- | --- |
| Lookback Days | **3** | Trading days in composite (2–3 research range) |
| Bin Size | 0.5 | Price bin width ($) on XAU |
| Value Area % | 70 | VA expand from POC |
| Weight Decay | 0.8 | Day weight = decay^age (newest = 1) |
| HVN Threshold | **1.25** | Bin ≥ mean×threshold → HVN candidate |
| LVN Threshold | 0.65 | Kept for profile completeness / viz (not entry node) |
| Max HVN Width ($) | **15** | Reject oversized sticky bands |
| Min HVN Strength | **1.25** | Min `avgVol/meanVol` (same units as HVN strength) |
| Top N HVN | **3** | Only consider strongest N nodes by Strength |
| Visualize Profile | false | Chart overlay optional |

### Outputs (`ProfileData`)

- Histogram, POC, VAH, VAL  
- HVN node list (`Low`, `High`, `Mid`, `Strength`)  
- Shape: `Bullish` | `Bearish` | `Neutral` | `DShape`  

### Rebuild

- On each **closed** signal bar (`OnBar`), last closed index = `Count - 2`.

### Helpers to reuse

- `ProfileData.Hvns`
- `FindNearestHvnBelow` / `FindNearestHvnAbove`
- Extend if needed: `FindEligibleHvns(price, side, topN, minStrength, maxWidth)` in SignalEngine (pure logic OK without Common change first).

---

## 5. Entry rules (testable)

Signal evaluated **on closed bar only** (`SignalEngine`).

### Filters (all must pass)

| ID | Rule |
| --- | --- |
| F1 | Enabled sessions (`SessionFilter` OR) |
| F2 | News OK if enabled (`NewsFilter` schedule) |
| F3 | Spread OK (`MarketCondition`) |
| F3_EQUITY | `RiskManager.IsTradingAllowed` |
| F4 | Trades today &lt; Max Trades / Day |
| F5 | No open position with `Bot Label` on symbol |

### Structure / confirm

| ID | Rule |
| --- | --- |
| E1 | Closed bar **touches** an eligible HVN (± `Touch Buffer ATR Mult` × ATR) |
| E2 | HVN **eligible**: Strength ≥ Min; Width$ ≤ Max; rank in Top N by Strength |
| E2pos | **Long:** touched HVN is **below or containing** bar low side (support); prefer HVN with `High ≤ POC` or mid ≤ POC when `Require Hvn Below Poc` on |
| E2pos | **Short:** touched HVN is **above or containing** bar high side (resistance); prefer mid ≥ POC when filter on |
| E3 | Rejection candle at HVN (see below) |
| E4 | **Delta required (v1 default on):** imbalance ≥ Min Delta Strength + min ticks in window |
| E5 | **Shape required (v1 default on):** not opposing (see below) |
| E6 | **HTF required (v1 default on):** long if HTF close &gt; POC; short if HTF close &lt; POC |
| E7 | **Min first-target R (Structure TP only):** when `TP Mode = Structure`, projected magnet distance / est. SL ≥ `Min First Target R`; RiskReward/FixedPrice skip hard E7 |

### Touch (E1)

- **Long:** `BarLow ≤ HVN.High + buffer` AND `BarLow ≥ HVN.Low − buffer` (intersects expanded band), **or** low pierces into band.  
- Practical: treat band as `[HVN.Low − buffer, HVN.High + buffer]`; require bar range intersects band and close evaluation uses rejection.  
- Same mirror for short with highs.

### Rejection (E3) — aligned with VacuumHunter

**Long (bullish rejection at support HVN)**

- Close in upper portion of bar range (`closePos ≥ 0.45`).  
- Lower wick ≥ body × `Rejection Wick/Body` **or** wick ≥ 25% range.  
- Bullish close or strong closePos ≥ 0.55.

**Short:** mirror (upper wick, closePos ≤ 0.55).

### Delta (E4) — required when filter on

- `CTickDeltaEngine`: mid uptick = buy, downtick = sell; zero ticks ignored.  
- Long strength = buy/sell ratio; short = sell/buy.  
- Insufficient ticks in window → **fail** (no neutral pass when filter required).  
- **Not** true exchange delta.

### Shape (E5)

| Side | Allow | Block |
| --- | --- | --- |
| Long | Bullish, DShape | Bearish |
| Short | Bearish, DShape | Bullish |
| Either | — | Neutral **blocked** when `Block Neutral Shape=true` (default **true** for live-leaning research) |

### Side selection

- Evaluate long and short independently if both HVNs touch (rare).  
- If both pass: pick **higher** delta imbalance.  
- If both fail: combined reject reason string.

### Failed acceptance (post-setup / optional v1.0 management)

Not an entry filter; exit/management:

| ID | Rule |
| --- | --- |
| M1 | If long and closed bar **close &lt; HVN.Low − buffer** before BE: optional **invalidate** (close market) when `Use Failed Acceptance Exit=true` (default **false** v1; enable after soak) |
| M2 | Optional time stop: if in trade ≥ `Max Bars In Trade` and profit &lt; 0.3R → flatten (default **off**) |

---

## 6. Exit rules (single TP, no partial)

| ID | Rule |
| --- | --- |
| X1 | **SL** long: wider of (`HVN.Low − ATR×HVN buffer`, `entry − ATR×Min SL distance`); short mirrored on `HVN.High` |
| X2 | **TP** single level, **full position**, broker TP at entry |
| X2a | TP Mode **RiskReward** (default): TP = entry ± SL_dist × RR Multiple (default 2.0) |
| X2b | TP Mode **Structure**: first magnet in direction — hierarchy: next HVN mid → POC → opposite VA (VAH long / VAL short); **abort entry** if missing or R &lt; Min First Target R (E7) |
| X2c | TP Mode **FixedPrice**: TP = entry ± Fixed TP ($) |
| X3 | **BE** optional: after profit ≥ BE Start (R) × SL_dist; lock entry ± BE Lock (R) |
| X4 | **Trailing** optional: start after Trail Start (R); step Trail Step (R) |
| X5 | No TP1/TP2 stack, no partial close (v1.0) |

### Structure target hierarchy (X2b detail)

**Long (from entry, targets above):**

1. Nearest **HVN mid** strictly above entry (not the entry HVN).  
2. Else **POC** if POC &gt; entry + min distance.  
3. Else **VAH**.  
4. Fail → `E7_RR` / no structure.

**Short:** nearest HVN mid below → POC → VAL.

### Position sizing

| Mode | Behavior |
| --- | --- |
| **RiskPercent** (default) | risk $ = Balance × Risk% / 100; volume via FixedRisk + conservative cap |
| **FixedLots** | volume = FixedLots × LotSize; still scale down if estimated risk &gt; daily room |

- If Max Daily Loss $ &gt; 0: remaining room caps risk $.  
- Never force min volume when it would oversize risk.

### Account risk runtime (`CRiskManager`)

| Hook | Behavior |
| --- | --- |
| Bot `OnTick` → `RiskManager.OnTick()` | Equity gates; flatten if configured |
| Bot `OnBar` | No flatten; only `IsTradingAllowed` for new signals |

---

## 7. Parameters (code defaults)

### Trade & Risk

| Parameter | Default |
| --- | --- |
| Enable Trading | true |
| Bot Label | HvnMagnet |
| Lot Size Mode | RiskPercent |
| Risk % | **0.50** |
| Fixed Lots | 0.01 |
| Max Trades / Day | **3** |
| Max Spread (pips) | 80 |
| Max Equity DD % | 10 |
| Flatten On Equity DD | false |
| Max Daily Loss ($) | 0 (set for prop sim) |
| Flatten On Daily Loss | false |
| Max Daily Profit ($) | 0 |
| Flatten On Daily Profit | false |
| Debug Logging | false |

### Stop Loss

| Parameter (UI) | Default | Meaning |
| --- | --- | --- |
| ATR Period | 14 | ATR for distances |
| **HVN buffer (×ATR)** | 0.5 | Long SL = HVN.Low − ATR×k |
| **Min SL distance (×ATR)** | 0.8 | Floor entry→SL |

### Take Profit

| Parameter | Default |
| --- | --- |
| TP Mode | RiskReward |
| RR Multiple | 2.0 |
| Fixed TP ($) | 20 |
| Min First Target R | **1.0** |
| Allow POC/VA Targets | true |

### Break Even / Trailing

| Parameter | Default |
| --- | --- |
| Use Break Even | true |
| BE Start (R) | 1.0 |
| BE Lock (R) | 0.05 |
| BE Add Spread | true |
| Use Trailing | **false** |
| Trail Start (R) | 1.5 |
| Trail Step (R) | 0.5 |

### Volume Profile

| Parameter | Default |
| --- | --- |
| Lookback Days | 3 |
| Bin Size | 0.5 |
| Value Area % | 70 |
| HVN Threshold | 1.25 |
| Weight Decay | 0.8 |
| Min HVN Strength | 1.25 |
| Max HVN Width ($) | 15 |
| Top N HVN | 3 |
| Visualize Profile | false |

### Signal Filters

| Parameter | Default |
| --- | --- |
| Min Delta Strength | 1.2 |
| Min Delta Ticks | 15 |
| Delta Window (ms) | 300000 |
| Rejection Wick/Body | 0.35 |
| Require Delta Filter | **true** |
| Require Shape Filter | **true** |
| Require HTF Filter | **true** |
| Block Neutral Shape | **true** |
| Require Hvn Below/Above Poc | **false** (optional siết) |
| Touch Buffer ATR Mult | 0.15 |
| HTF Timeframe | Hour |
| Use Failed Acceptance Exit | false |
| Max Bars In Trade | 0 (0=off) |

### Session (UTC fixed windows, OR — enable/disable only)

| Parameter | Default | Fixed window (UTC) |
| --- | --- |
| Trade Asia | **false** | 00:00–09:00 |
| Trade London | **true** | 07:00–16:00 |
| Trade New York | **true** | 13:30–23:00 |
| Trade Overlap (Lon-NY) | false | 13:00–16:00 |

### News

| Parameter | Default |
| --- | --- |
| Enable News Filter | false |
| News Blackout (min) | 30 |
| News Schedule UTC | empty list |

---

## 8. Reject / pass codes (logging)

| Code | Meaning |
| --- | --- |
| F1_SESSION | Outside enabled sessions |
| F2_NEWS | News blackout |
| F3_SPREAD | Spread |
| F3_EQUITY | Equity / daily gate |
| F4_MAX_TRADES | Daily cap |
| F5_OPEN_POS | Already in trade |
| PROFILE_INVALID | VP not valid |
| E1_NO_TOUCH | No HVN touch (`near=` debug) |
| E2_WEAK_HVN | Strength below min |
| E2_HVN_TOO_WIDE | Width above max |
| E2_NOT_TOP_N | Not in top strength set |
| E2_POC_SIDE | Optional POC-side filter fail |
| E3_NO_*_REJECT | Candle rejection fail |
| E4_DELTA* | Delta filter / insufficient ticks |
| E5_SHAPE_* | Shape filter |
| E6_HTF_* | HTF bias fail |
| E7_RR | First target R &lt; min |
| PASS:LONG / PASS:SHORT | Entry allowed |

Info journal: PASS, OPEN, CLOSE, day summary, risk warnings.  
Debug: per-bar rejects.

---

## 9. Definition of done

### Spec / product v1.0-spec

- [x] Rules unambiguous (this PRD)  
- [x] SL/TP/risk defined; XAU sizing pattern  
- [x] Single hard TP (no partial)  
- [x] Docs under `Robots/HvnMagnet/docs/`  
- [ ] Code implement + compile Release `.algo`  
- [ ] Ablation reports in `5-reports/`  
- [ ] Demo/micro forward soak  
- [ ] Live small-risk only after soak  

### Implement acceptance

- [ ] `SignalEngine` pure + reject codes match table  
- [ ] OPEN log includes hvn mid/strength, delta, RR, risk$  
- [ ] Structure TP respects Min First Target R  
- [ ] RiskManager.OnTick only path for flatten  

---

## 10. Risks & assumptions

1. Tick volume and mid-delta proxies differ by broker feed.  
2. HVN stickiness → chop; Min R + session filter are primary mitigations.  
3. Low sample size if filters strict — do not overfit many knobs.  
4. Shorter lookback (2–3d) more reactive than VH 4d — monitor false HVNs.  
5. Co-running VacuumHunter + HvnMagnet can open opposing ideas same day; use separate labels and lower combined risk.  
6. News schedule is **manual**; empty = no blackout.

---

## 11. Relationship to VacuumHunter

| Aspect | VacuumHunter | HvnMagnet |
| --- | --- | --- |
| Node | LVN | HVN |
| Story | Vacuum fill | Magnet hold |
| Delta default | off | **on** |
| Shape default | off | **on** |
| Lookback default | 4d | **3d** |
| Max trades/day | 2 | **3** |
| Risk % default | 0.75 | **0.50** |

**ADR-001:** separate bot, shared Common only.
