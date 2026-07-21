# PRD — PmLh / PM-LH v1.0

**Status:** Implemented (v1.0.1 code; multi-day research BT ongoing)  
**Platform:** cTrader cBot  
**Spec version:** v1.0.1  
**Date:** 2026-07-12  
**Strategy code:** PM-LH (POC Migration + LVN Highway)

> **v1.0.1** aligns docs with post-BT fixes: streak defaults, Strong M bypass, price–POC align, LVN side gate.

---

## 1. Identity

| Field | Value |
| --- | --- |
| Name | PmLh |
| Platform | cTrader Automate (C# / .NET 6) |
| Symbols | XAUUSD primary; architecture must not hard-code only one symbol |
| Signal TF | **Chart TF** — research matrix includes M5, M15, M30 (no production freeze until data) |
| Bias TF | H1 (`HTF Timeframe` param); filter optional |
| Account | Personal research first; prop optional via equity gates |
| Position model | Netting / single bot label; **max 1 open** |
| Libraries | Reuse `RedWave.Common` (VolumeProfile, Risk, Delta, Session, Trail, …) |

---

## 2. Goals & non-goals

### Goals

- Implement **testable** POC-migration + LVN-highway entry with reject codes for ablation.  
- **Research-wide** parameter surface (TF via chart, sessions, migration windows, entry modes, optional filters) — **do not pre-cut scope** before backtest.  
- **Simple exit v1:** hard SL, fixed TP by R, optional BE + trailing only.  
- Risk % sizing correct on XAU (same contract as VacuumHunter / HvnMagnet).  
- Modular: pure `SignalEngine` + thin orchestrator; copy-adapt VH skeleton.  
- Logging sufficient for ablation tables and MFE/MAE post-analysis.

### Non-goals (v1.0)

- True exchange footprint / bid-ask volume.  
- Auto economic calendar download.  
- Partial close / multi-TP / runner stack.  
- **POC reverse / flat as exit** (entry bias only).  
- Time-stop exit (optional later; not v1).  
- Structure TP (next HVN) as exit mode (not v1).  
- Merge into VacuumHunter or HvnMagnet.  
- ML integration.  
- Declaring “live defaults” for TF/session/frequency without backtest evidence.

---

## 3. Market regime (filters — all parameterized)

| Trade when | Do not trade when |
| --- | --- |
| Inside any **enabled** session (OR) | Outside all enabled windows |
| Spread ≤ Max Spread (pips) | Spread too wide |
| Peak equity DD &lt; Max Equity DD % (if on) | Peak equity kill |
| Daily equity PnL &gt; −Max Daily Loss $ (if on) | Daily loss gate |
| Daily equity PnL &lt; +Max Daily Profit $ (if on) | Daily profit gate |
| Trades today &lt; Max Trades / Day | Cap hit |
| No open position with bot label | Already in market |
| Optional: outside news blackout | ±N min around scheduled news |
| Optional filters (shape / HTF / delta / expand) pass when enabled | Filter reject |

**Research note:** Session toggles, filter toggles, and max trades are **knobs**. Defaults below are **starting points for first compile**, not claims of optimality.

**Account risk note:** Daily loss/profit and equity DD use **Account.Equity** (day start = first equity sample of UTC day). Gates act on equity level, not guaranteed per-order max loss under gaps.

---

## 4. Volume profile & POC migration

### 4.1 Data

- Source: bar `TickVolumes`, distributed across OHLC into price bins (existing `CVolumeProfile`).  
- Not true futures volume.

### 4.2 Profile roles (configurable — see ADR-003)

| Role | Purpose | Default research path |
| --- | --- | --- |
| **Structure profile** `P_struct` | LVN/HVN nodes, shape, VAH/VAL | Adaptive **composite** lookback days (like VH/HMPD) |
| **Migration profile** series | POC over time | **Rolling** last `N` closed bars each bar |

**LvnSource** param:

| Value | LVN taken from |
| --- | --- |
| `Composite` (default) | `P_struct` composite |
| `Rolling` | same rolling window as POC |
| `DualPreferComposite` | composite LVN if any eligible; else rolling |

All three are in scope for research; implement behind param.

### 4.3 Composite params (structure)

| Param | Default | Meaning |
| --- | --- | --- |
| Lookback Days | 3 | Trading days in composite |
| Bin Size | 0.5 | Price bin width ($) on XAU |
| Value Area % | 70 | VA from POC |
| Weight Decay | 0.8 | Day weight = decay^age |
| HVN Threshold | 1.25 | High-vol candidate |
| LVN Threshold | 0.65 | Low-vol candidate |
| Max LVN Width ($) | 25 | Reject oversized voids |
| Min LVN Strength | 0.20 | Min strength to trade |
| Top N LVN | 3 | Consider strongest / nearest N |
| Visualize Profile | false | Chart overlay optional |

### 4.4 Rolling POC / migration

On each **closed** signal bar (`index = Count - 2`):

1. Build rolling profile over last **`Poc Window Bars` = N** closed bars (or incremental equivalent).  
2. Record `poc[t] = rolling.POC`.  
3. Keep ring buffer of length ≥ `K + streak window`.

**Migration score (signed):**

```text
Δ   = poc[t] − poc[t − K]
ATR = ATR(AtrPeriod) on signal TF
M   = Δ / ATR          // if ATR ≤ 0 → invalid

Direction D:
  Bull  if M ≥ +M_min
  Bear  if M ≤ −M_min
  Flat  otherwise → no trade (E_POC_FLAT)
```

| Param | Default | Meaning |
| --- | --- | --- |
| Poc Window Bars (N) | 24 | Rolling profile length |
| Migrate Lookback (K) | 6 | Bars for ΔPOC |
| Min Migration (M_min) | 0.40 | Min \|M\| in ATR units |
| Min Poc Move Bins | **1** | \|Δ\| ≥ BinSize × this (E_POC_TINY); 0 = off |
| Use Streak Filter | **false** | Research default off; net M is primary |
| Streak Need / Of | **2** / 6 | When streak on: non-zero 1-bar steps only (plateaus ignored) |
| Strong M Bypass | **1.0** | If \|M\| ≥ this, skip streak (0 = never) |
| Max Price-POC (×ATR) | **1.5** | Reject long if close &lt; POC − k·ATR; short if close &gt; POC + k·ATR; 0 = off |
| Require LVN Side | **true** | Long: close not fully under LVN; short: not fully above LVN |

**Streak semantics (when enabled):** count only 1-bar \|ΔPOC\| ≥ 0.25×BinSize. Pass if enough same-sign steps, or same &gt; opp (≤1 reverse), or zero non-zero steps after net Δ already passed M_min. Rolling VP POC often plateaus then jumps — counting zeros as failures caused mass `E_POC_NOISE` in early M5 BT.

**Reject codes (migration + price align):**

| Code | Condition |
| --- | --- |
| `E_POC_FLAT` | \|M\| &lt; M_min |
| `E_POC_TINY` | \|Δ\| &lt; BinSize × Min Poc Move Bins |
| `E_POC_NOISE` | streak filter fails (and not strong-M bypass) |
| `E_POC_INVALID` | buffer not warm / ATR invalid / profile invalid |
| `E_PRICE_POC` | price too far on wrong side of rolling POC (stale migration) |
| `E_LVN_SIDE` | close fully under LVN (long) / over LVN (short) |

---

## 5. Entry rules (testable)

Signal evaluated **on closed bar only** (`SignalEngine`).

### 5.1 Filters (all must pass)

| ID | Rule |
| --- | --- |
| F1 | Enabled sessions (`SessionFilter` OR) |
| F2 | News OK if enabled |
| F3 | Spread OK |
| F3_EQUITY | `RiskManager.IsTradingAllowed` |
| F4 | Trades today &lt; Max Trades / Day |
| F5 | No open position with `Bot Label` |

### 5.2 Core edge filters

| ID | Rule |
| --- | --- |
| E0 | Profile(s) valid; migration buffer warm |
| E_MIG | Direction D from migration score (see §4.4) |
| E_PRICE_POC | Price not too far on wrong side of `PocNow` (Max Price-POC ×ATR; **default on** @ 1.5) |
| E1 | Price **interacts** with eligible LVN per **Entry Mode** (below) |
| E2 | LVN eligible: Strength ≥ Min; Width$ ≤ Max; in Top N consideration set |
| E_LVN_SIDE | Optional (default **on**): long close ≥ LVN.Low−buf; short close ≤ LVN.High+buf |
| E3 | **Acceptance** candle in direction D (see §5.4) |
| E4 | Optional: tick delta imbalance ≥ Min Delta Strength + min ticks (**default off** research) |
| E5 | Optional: shape not opposing D (**default off** research) |
| E6 | Optional HTF: long if HTF close &gt; structure/composite POC proxy; short if &lt; (**default off** research) |
| E7 | Optional range expand: bar range ≥ ExpandAtrMult×ATR **or** ATR(fast)/ATR(slow) ≥ ExpandRatio (**default off**) |
| E8 | Optional max SL: estimated SL distance ≤ MaxSlAtrMult×ATR; else reject (**default on** safety) |
| E9 | Reserved / unused (no structure TP in v1) |

**Impl note E6:** Code uses HTF close vs **signal structure composite POC** as proxy (`HtfPoc`), not a separate HTF profile build.

**Impl note E_PRICE_POC:** Blocks e.g. long when rolling POC still bull after a dump (price many ATR below POC). Does **not** block long far *above* POC (chase after impulse remains an open research issue).

### 5.3 LVN eligibility (E2)

- Strength ≥ `Min LVN Strength`.  
- Width (High−Low) ≤ `Max LVN Width`.  
- Prefer nodes with clear shoulders (existing strength metric).  
- Side context:  
  - **Long:** LVN is at/near price and suitable for **up** highway (price at or below LVN mid / retesting after break up — mode-specific).  
  - **Short:** mirror.

### 5.4 Entry modes (E1) — parameterized

#### Mode `ShallowRetest` (default)

```text
Long (D = bull):
  PriorBreak: within PriorBreakBars, a closed bar closed ≥ LVN.Mid
              (or closed above LVN.High — param PreferFullClear)
  Now: bar range intersects LVN band [Low−buf, High+buf]
  Dwell: consecutive bars with midpoint inside LVN ≤ MaxDwellBars
         (count ending at signal bar); if exceed → E_LVN_DWELL
  Acceptance E3: see below
```

#### Mode `Pierce`

```text
Long:
  Signal bar: Low ≤ LVN.Mid ≤ High (or traverses full LVN)
  Close ≥ LVN.High − small buffer (clearing thin zone upward)
  Body ≥ BodyAtrMult × ATR (optional strength)
```

#### Mode `TouchOnly` (ablation / weak)

```text
  Bar intersects LVN band + E_MIG + E3 only (no prior break / dwell)
```

| Param | Default |
| --- | --- |
| Entry Mode | ShallowRetest |
| Touch Buffer ATR Mult | 0.15 |
| Prior Break Bars | 8 |
| Max Dwell Bars | 3 |
| Prefer Full Clear | false |
| Body ATR Mult (Pierce) | 0.5 |

### 5.5 Acceptance candle (E3)

**Long**

- `closePos = (Close − Low) / (High − Low)` (range=0 → fail)  
- closePos ≥ 0.45  
- Close ≥ Open **or** closePos ≥ 0.55  
- Optional: close ≥ LVN.Mid when mode requires

**Short:** mirror (closePos ≤ 0.55, bearish).

### 5.6 Side selection

- Only evaluate side **D** from migration (do not take counter-migration trades).  
- If multiple LVNs eligible: pick **nearest** to price in direction of travel, break ties by **higher Strength**.  
- Counter-D always rejected (`E_SIDE_VS_MIG`).

---

## 6. Exit rules (v1 — locked)

See [ADR-002](../2-architecture/ADR-002-exit-rr-sl-trail.md).

| ID | Rule |
| --- | --- |
| X1 | **SL** long: wider of (`LVN.Low − ATR×LvnBuffer`, `entry − ATR×MinSlAtrMult`); short mirrored on `LVN.High` |
| X2 | **TP** single level, **full position**, broker TP: `entry ± RR × SL_dist` only |
| X3 | **BE** optional: after profit ≥ BE Start (R)×SL_dist; lock entry ± BE Lock (R) (+ spread if on) |
| X4 | **Trailing** optional: start after Trail Start (R); distance Trail Step (R)×SL_dist |
| X5 | **Exit reasons only:** `SL` \| `TP` \| `TRAIL` (BE is SL modification, not separate thesis) |
| X6 | No partial; no structure TP; no POC-based exit; no time stop in v1 |

### Position sizing

| Mode | Behavior |
| --- | --- |
| **RiskPercent** (default) | risk $ = Balance × Risk% / 100; volume via FixedRisk + conservative cap |
| **FixedLots** | volume = FixedLots; still scale if daily room exceeded |

- Max Daily Loss $ &gt; 0 → remaining room caps risk $.  
- Never force min volume when it would oversize risk.

### Account risk runtime

| Hook | Behavior |
| --- | --- |
| `OnTick` → `RiskManager.OnTick()` | Equity gates; flatten if configured |
| `OnBar` | No flatten; only `IsTradingAllowed` for new entries |

---

## 7. Parameters (code defaults — research starters)

### Trade & Risk

| Parameter | Default |
| --- | --- |
| Enable Trading | true |
| Bot Label | PmLh |
| Lot Size Mode | RiskPercent |
| Risk % | 0.50 |
| Fixed Lots | 0.01 |
| Max Trades / Day | 10 (loose for research; tighten later from data) |
| Max Spread (pips) | 80 |
| Max Equity DD % | 10 (0=off) |
| Flatten On Equity DD | false |
| Max Daily Loss ($) | 0 |
| Flatten On Daily Loss | false |
| Max Daily Profit ($) | 0 |
| Flatten On Daily Profit | false |
| Debug Logging | false |

### Stop / Take profit

| Parameter | Default |
| --- | --- |
| ATR Period | 14 |
| LVN buffer (×ATR) | 0.5 |
| Min SL distance (×ATR) | 0.8 |
| Max SL distance (×ATR) | 2.5 (E8; 0=off) |
| RR Multiple | 2.0 |

### Break even / trailing

| Parameter | Default |
| --- | --- |
| Use Break Even | true |
| BE Start (R) | 1.0 |
| BE Lock (R) | 0.05 |
| BE Add Spread | true |
| Use Trailing | false |
| Trail Start (R) | 1.5 |
| Trail Step (R) | 0.5 |

### Volume profile (structure)

| Parameter | Default |
| --- | --- |
| Lvn Source | Composite |
| Lookback Days | 3 |
| Bin Size | 0.5 |
| Value Area % | 70 |
| Weight Decay | 0.8 |
| LVN Threshold | 0.65 |
| HVN Threshold | 1.25 |
| Min LVN Strength | 0.20 |
| Max LVN Width ($) | 25 |
| Top N LVN | 3 |
| Visualize Profile | false |

### POC migration

| Parameter | Default |
| --- | --- |
| Poc Window Bars | 24 |
| Migrate Lookback Bars | 6 |
| Min Migration M | 0.40 |
| Min Poc Move Bins | **1** |
| Use Streak Filter | **false** |
| Streak Need | **2** |
| Streak Of | 6 |
| Strong M Bypass | **1.0** |
| Max Price-POC (×ATR) | **1.5** |
| Require LVN Side | **true** |

### Signal / entry

| Parameter | Default |
| --- | --- |
| Entry Mode | ShallowRetest |
| Touch Buffer ATR Mult | 0.15 |
| Prior Break Bars | 8 |
| Max Dwell Bars | 3 |
| Prefer Full Clear | false |
| Body ATR Mult | 0.5 |
| Require Delta Filter | **false** |
| Require Shape Filter | **false** |
| Require HTF Filter | **false** |
| Require Expand Filter | **false** |
| Block Neutral Shape | false |
| Block DShape | false |
| Min Delta Strength | 1.2 |
| Min Delta Ticks | 15 |
| Delta Window (ms) | 300000 |
| HTF Timeframe | Hour |
| Expand ATR Mult | 0.7 |
| Expand ATR Fast/Slow | 5 / 20 |
| Expand Ratio | 1.1 |

### Session (UTC fixed windows — enable/disable only)

| Parameter | Default | Window (UTC) |
| --- | --- | --- |
| Trade Asia | **true** (research-open) | 00:00–09:00 |
| Trade London | true | 07:00–16:00 |
| Trade New York | true | 13:30–23:00 |
| Trade Overlap | true | 13:00–16:00 |

### News

| Parameter | Default |
| --- | --- |
| Enable News Filter | false |
| News Blackout (min) | 30 |
| News Schedule UTC | empty list |

---

## 8. Reject / pass codes (journal)

### Filters

| Code | Meaning |
| --- | --- |
| `F1_SESSION` | Outside sessions |
| `F2_NEWS` | News blackout |
| `F3_SPREAD` | Spread |
| `F3_EQUITY` | Equity / daily gate |
| `F4_MAX_TRADES` | Daily cap |
| `F5_OPEN` | Position already open (`REJECT:F5_OPEN`) |

### Edge

| Code | Meaning |
| --- | --- |
| `E_PROFILE` | Invalid profile |
| `E_POC_FLAT` / `TINY` / `NOISE` / `INVALID` | Migration |
| `E_PRICE_POC` | Price vs POC wrong-side distance |
| `E1_NO_INTERACT` | No LVN interaction for mode |
| `E2_WEAK_LVN` / `E2_WIDE_LVN` / `E2_NO_LVN` | Node quality |
| `E_LVN_SIDE` | Close under LVN (long) / over (short) |
| `E_LVN_DWELL` | Too long in LVN (ShallowRetest) |
| `E_NO_PRIOR_BREAK` | Missing prior break |
| `E3_NO_ACCEPT` | Acceptance candle fail |
| `E4_DELTA` | Delta filter |
| `E5_SHAPE` | Shape filter |
| `E6_HTF` | HTF filter |
| `E7_EXPAND` | Expand filter |
| `E8_SL_WIDE` | SL too wide vs ATR |
| `E_SIDE_VS_MIG` | Side not aligned (should not evaluate) |
| `PASS:LONG` / `PASS:SHORT` | Entry |

### Exit log reasons

| Code | Meaning |
| --- | --- |
| `X_SL` | Stop hit |
| `X_TP` | Take profit hit |
| `X_TRAIL` | Trailing stop hit |
| `X_FLATTEN_EQUITY` | RiskManager flatten (account gate; not strategy thesis) |

Debug: per-bar reject string when `Debug Logging`.

---

## 9. Observability (required for research)

Each `PASS` / executed OPEN log should include:

- `M`, `Δ`, `poc[t]`, `N`, `K`  
- LVN low/high/mid/strength, LvnSource  
- Entry mode, dwell count  
- SL, TP, R-distance, RR  
- Optional: shape, delta, HTF flag  

Optional CSV / print line for offline ablation (orchestrator debug).

---

## 10. Acceptance criteria (product)

| ID | Criterion |
| --- | --- |
| A1 | Compiles Release `.algo`; attaches chart without throw |
| A2 | Closed-bar only signals; F/E codes match PRD |
| A3 | OPEN always has broker SL + TP (RR); no structure TP path in v1 |
| A4 | Trail/BE only modify SL; no POC exit path |
| A5 | Risk% volume ≈ RiskManager contract on XAU (spot-check log) |
| A6 | Ablation possible: disable E4–E7 via params |
| A7 | At least one backtest report path under `5-reports/` after first run |
| A8 | Docs updated if codes diverge from PRD |

**Not** acceptance for v1: profitable OOS, live-ready TF freeze, co-run mutex with VH.

---

## 11. Risks & open research questions

| Risk / Q | Mitigation |
| --- | --- |
| Migration lag / noise | Params N,K,M_min,streak; ablation |
| Streak mass-reject (M5 early BT) | Streak default off; non-zero steps only; Strong M bypass |
| Stale bull POC + price dump | Max Price-POC ×ATR (E_PRICE_POC) |
| Chase after impulse (long far above POC) | Open research; not blocked by E_PRICE_POC |
| BE early vs full RR | Optional; research toggle |
| FixedLots vs Risk% in tester | Confirm Lot Size Mode on instance |
| LVN highway vs vacuum conflict with VH | Separate label; correlation study later |
| Rolling VP cost on M5 | Optimize rebuild; measure |
| Tick volume ≠ real volume | Accept platform limit; document |
| Overfit multi-param | Freeze protocol after first grid; WF later |
| Which LvnSource wins? | ADR-003 + A/B runs |

---

## 12. Relationship to VacuumHunter / HvnMagnet

| Aspect | VacuumHunter | HvnMagnet | PmLh |
| --- | --- | --- | --- |
| Node | LVN | HVN | LVN |
| Intent | Reject / vacuum | Pullback magnet | Highway with migration |
| Direction driver | Rejection + optional HTF | HTF + shape + delta | **POC migration** primary |
| Exit v1 | RR / Structure / Fixed | RR / Structure / Fixed | **RR only** + trail |

---

## 13. References

- [ARCH-pm-lh.md](../2-architecture/ARCH-pm-lh.md)  
- [PLAN-implement-pm-lh.md](../3-plans/PLAN-implement-pm-lh.md)  
- `Robots/VacuumHunter` orchestrator template  
- `Common/VolumeProfile.cs`, `ProfileData.cs`
