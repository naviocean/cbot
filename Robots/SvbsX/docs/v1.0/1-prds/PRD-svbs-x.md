# PRD — SVBS-X v1.1

**Status:** Implemented (`Robots/SvbsX/SvbsX/`) — **code of record**  
**Platform:** cTrader cBot (C# / .NET 6)  
**Date:** 2026-07-12  
**Owner:** RedWave / personal XAU  

---

## 1. Identity

| Field | Value |
| --- | --- |
| Name | SVBS-X (Session VA Expansion — XAU) |
| Platform | cTrader Automate |
| Symbols | **XAUUSD only** |
| Signal TF | **M5** (closed bar only) |
| Account | **Personal** |
| Position model | Single position, full size, label `SvbsX` |
| Common | VolumeProfile (`BuildRange`), RiskManager, TrailingManager, SessionFilter, NewsFilter, MarketCondition, Logger, PriceUtils |

---

## 2. Goals & non-goals

### Goals

- Session-VA **expansion** (not mean-reversion into VA).
- Acceptance delay (not first pierce only).
- Full-size exits: BE, trail, optional TP RR — **no partial**.
- Session enable toggles only (Asia / London / NY / Overlap).
- XAU-safe position sizing (broken TickValue).

### Non-goals

| Forbidden / removed |
| --- |
| Partial close / scale-out |
| Exit Mode enum / Trail-ATR hybrid / time-stop |
| User-editable session hours (fixed in `SessionClock`) |
| HVN/LVN targets, multi-symbol, grid, martingale |
| Prop challenge rules (optional daily **$** soft-stops only) |

---

## 3. Market regime

### Trade when

- Symbol XAU/GOLD  
- Enabled session window active (`CSessionFilter` + `SessionClock.ResolveWindow`)  
- Prior session profile valid  
- VA width in band; spread/news/risk OK  
- Max trades / day not hit; no open bot position  
- Acceptance signal on closed bar  

### Do not trade

- Outside windows / disabled sessions  
- `E_CHASE` (entry too far past VA)  
- `X1_SL_CAP` (structure SL wider than max ×ATR)  
- Invalid profile (`F3`), daily loss/profit $ hit, equity DD  

---

## 4. Sessions

### UI (enable only)

| Toggle | Default |
| --- | --- |
| Trade Asia | false |
| Trade London | true |
| Trade New York | true |
| Trade Overlap | true |

### Fixed UTC (code)

| Role | Window |
| --- | --- |
| Asia VA freeze | 00:00–07:00 |
| London VA freeze | 07:00–12:00 |
| A→L entry | 07:30–12:00 → profile **Asia** |
| L→NY entry | 13:00–23:00 (NY) / 13:00–16:00 (Overlap) → profile **London** |
| Asia entry | 00:00–09:00 → **prior-day** Asia VA |
| Flat | Asia 09 / Lon 16 / NY 23 **or next calendar day** (weekend gap) |

---

## 5. Entry (testable)

Closed bar only. State: `Idle → AcceptWait → Enter | Cancel`.

### Pre-filters

| ID | Rule |
| --- | --- |
| F1 | XAU symbol |
| F2 | Entry window + session filter |
| F3 | Prior profile valid |
| F4 | Min/Max VA width $ |
| F5 | Spread |
| F6 | Max trades / day |
| F7 | RiskManager (equity DD + daily $ loss/profit) |
| F8 | No open position |
| F9 | News (if enabled) |

### Break then accept

| Mode | Default | Rule |
| --- | --- | --- |
| **BreakConfirm** | **yes** | ≥1 bar after break, still close outside VA |
| RetestHold | no | Retest to edge **or** ≥2 bars still outside |
| Continuation | no | 2 closes outside + HL/LH |

| ID | Rule |
| --- | --- |
| E_BREAK | First close outside VAH/VAL + body ratio |
| Accept timeout | Default **24** bars → `C_TIMEOUT` |
| C_REACCEPT | Long close &lt; VAL / short close &gt; VAH (full fail) |
| Volume | Optional (`Use Volume Filter` default **false**) |
| POC | Optional (`Use POC Filter` default **false**) |

---

## 6. Stop / size / exit

### SL (structure)

- Anchor: VAH (long) / VAL (short); retest extreme only if **inside** [VAL, VAH].  
- Buffer: ATR × SL buffer mult.  
- Floor / cap: Min / Max SL **×ATR**.  
- **E_CHASE:** entry beyond VA by Max Entry Ext ×ATR → no trade.  
- **X1_SL_CAP:** structure distance &gt; Max SL ×ATR → no trade.  

### Size

| Mode | Rule |
| --- | --- |
| RiskPercent | Equity × Risk % vs SL (XAU: Oz / FixedRisk — [ADR-004](../2-architecture/ADR-004-xau-volume-sizing.md)) |
| FixedLots | Fixed lots → units |

### Exit (full size only)

| Tool | Control |
| --- | --- |
| SL | Initial structure |
| TP | **TP RR Multiple**; **0** = no hard TP |
| BE | Use BE, Start R, Lock R |
| Trail | Use Trailing, Start R, Step R only |
| Session flat | `ShouldFlat(utc, window, entryUtc)` |
| Partial | **Never** |

---

## 7. Parameters (code defaults)

### Trade & Risk

| Parameter | Default |
| --- | --- |
| Position Size Mode | RiskPercent |
| Risk % | 0.75 |
| Fixed Lots | 0.01 |
| Max Trades / Day | 2 |
| Max Daily Loss ($) | 0 (off) |
| Flatten On Daily Loss | false |
| Max Daily Profit ($) | 0 (off) |
| Flatten On Daily Profit | false |
| Max Equity DD % | 12 |
| Max Spread (pips) | 80 |

### Signal

| Parameter | Default |
| --- | --- |
| Accept Mode | BreakConfirm |
| Accept Timeout Bars | 24 |
| Use Volume Filter | false |
| Volume k | 1.2 |
| Use POC Filter | false |
| Min / Max VA Width $ | 4 / 50 |

### Stop / TP / BE / Trail

| Parameter | Default |
| --- | --- |
| SL buffer / Min / Max SL | 0.35 / 0.8 / 3.0 ×ATR |
| Max Entry Ext | 1.5 ×ATR |
| TP RR Multiple | 0 |
| BE Start / Lock | 1.0 / 0.05 R |
| Trail Start / Step | 1.5 / 0.5 R |

---

## 8. Reject / log codes (selected)

| Code | Meaning |
| --- | --- |
| E_BREAK_LONG/SHORT | Armed |
| PASS_LONG/SHORT | Accept → try execute |
| E_CHASE | Entry too far past VA |
| X1_SL_CAP | SL wider than max ×ATR |
| C_TIMEOUT / C_REACCEPT / C_WINDOW / C_FLIP | Setup cancel |
| F3_PROFILE | Invalid VA |
| F6_MAX_TRADES / F7_* | Risk gates |
| X4_SESSION_FLAT | Session or next-day flat |

---

## 9. Definition of Done

- [x] Rules match cBot parameters  
- [x] No partial close  
- [x] Session toggles only  
- [x] XAU sizing safeguards  
- [x] Docs v1.1 aligned with code  
- [ ] Walk-forward reports in `5-reports/`  

---

## 10. Handoff

| Item | Path |
| --- | --- |
| Code | `Robots/SvbsX/SvbsX/` |
| PRD | this file |
| README | `Robots/SvbsX/docs/README.md` |
