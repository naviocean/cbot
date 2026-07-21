# ZigZagPocPullback

ZigZag pullback + Volume Profile POC (or Fib mid-zone) cBot for **XAUUSD** on cTrader.

## Quick Start

- **Symbol:** XAUUSD  
- **TF:** Chart TF (M5 preferred)  
- **Account:** Personal  
- **Status:** v1.0 code build green — run BT in cTrader  
- **Algo:** `Robots/ZigZagPocPullback.algo`  

## Core Logic

1. **cTrader Guru ZigZag** (Depth/Deviation/BackStep, HighLow) — same algo as indicator.  
2. Wait for **z1 confirmed** (tip pivot forms after z1; tip can repaint).  
3. Side: buy on confirmed bottom (z1 &lt; z3); sell on confirmed peak (z1 &gt; z3).  
4. Zone: **POC** rolling lookback **or** **Fib 38.2–61.8** (user mode).  
5. **Market** when price is inside zone.  
6. SL beyond z2 ± ATR ratio; TP at fixed RR (default 2).  

**ZZ defaults:** Depth=12, Deviation=5 (points×TickSize), BackStep=3 — match Guru indicator.  
**Chart check:** attach `Indicators/ZigZag/ZigZag.cs` with same params; bot visuals should align.  

## Chart visuals (default ON)

| Object | Meaning |
| ------ | ------- |
| Yellow ZZ lines | Confirmed ZigZag legs |
| Dotted grey tip | Forming tip (not tradeable as z1) |
| Labels `z1` `z2` `z3` `tip` | Pivot roles |
| Blue zone box | Entry zone (armed) |
| Orange POC line | Rolling POC (mode POC) |
| Red dotted SL | Structural stop from z2 |
| Status text | ARMED / last reject code |

Toggle group **Visual**: Show Visuals / ZigZag / Zone / Labels.

## Docs

| Doc | Path |
| --- | --- |
| PRD | `docs/v1.0/1-prds/PRD-zz-poc-pullback.md` |
| PLAN | `docs/v1.0/3-plans/PLAN-implement-zz-poc-pullback.md` |
| Project root | `docs/PROJECT_ROOT.md` |

## Input Parameters (summary)

See PRD defaults table. Key groups: ZigZag, ZoneMode, Risk, SL/TP, Session.
