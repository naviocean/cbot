---
name: trading-requirements
description: Capture and lock trading strategy requirements for EA/cBot PRDs and plans. Prefer this over generic product-requirements/requirements-analysis for RedWave EA work. Use when defining entry/exit, risk, filters, platform, or writing PRD/PLAN for algo-strategist.
---

# Trading Requirements

**Purpose:** Turn vague trading ideas into **implementable, testable** specs for `mql5-expert` / `cbot-expert`.

> Replaces day-to-day use of generic `product-requirements`, `requirements-analysis`, and `requirements-clarity` for **EA/cBot** work. Those skills remain in the kit for non-trading text tasks only — do **not** load all three for a normal strategy PRD.

## When to load

| Class / task | Use |
| ------------ | --- |
| `strategy`, `feature`, `/plan`, `/brainstorm` | Yes — after/with `brainstorming` as needed |
| `trivial`, `bugfix`, `analyze` | No |
| Prop mentioned | Also load `prop-firm-constraints` |

## Core questions (Why / Simpler)

1. **Why this edge?** What market behavior is exploited?  
2. **Simpler?** Can fewer filters/indicators keep the edge?  
3. **When it fails?** Regime where strategy must stand down.

## Spec skeleton (minimum for handoff)

Fill before coding. Store as `docs/{version}/1-prds/PRD-{slug}.md` or embed in `PLAN-*.md`.

### 1. Identity

| Field | Example |
| ----- | ------- |
| Name | SessionBreakout |
| Platform | MT5 \| cTrader \| dual |
| Symbols | XAUUSD |
| Timeframe(s) | Signal M15, trend H1 |
| Account type | personal \| prop (→ prop skill) |
| rwcommon | required \| optional \| unknown |

### 2. Market regime

- Trade when: trend / range / session (London, NY, Asia)  
- Do **not** trade when: news, low liquidity, spread > X  

### 3. Entry (exact, testable)

- Long if: … (bar index: closed bar only? shift=1?)  
- Short if: …  
- One position vs pyramid: …  
- Signal source: indicator / price action / external  

### 4. Exit

- SL: structure / ATR / fixed pips — **points vs pips explicit**  
- TP: R-multiple / opposite signal / partial  
- BE / trailing: when, mode  
- Time exit / session flat:  

### 5. Risk

| Field | Value |
| ----- | ----- |
| Risk % per trade | |
| Max positions | |
| Max daily trades / daily loss | |
| Max total DD stop | |
| Lot method | risk-based \| fixed |

### 6. Filters

- Session / DOW  
- Spread / volatility  
- News  
- Higher-TF bias  

### 7. Non-functional

- Magic number policy  
- Logging needs  
- Backtest model expectation (every tick / …)  
- Prop constraints summary (or N/A)  

### 8. Definition of done

- [ ] Rules unambiguous (no two interpretations)  
- [ ] Pip/point and SL/TP defined  
- [ ] Risk kill rules defined if prop/personal limits exist  
- [ ] Verify profile: `mt5-code` / `cbot-code`  
- [ ] Tester acceptance: min trades, max DD, PF floor (optional)  

## Quality bar (self-check before user approval)

| Check | Fail if |
| ----- | ------- |
| Ambiguity | “Buy when strong” without definition |
| Lookahead | Uses future bar / same-bar open as closed signal without stating |
| Risk | No SL or unbounded grid without explicit accept |
| Platform | MT5-only API assumed for cBot (or reverse) |
| Pip/point | “50 pips” on 3/5 digit without convention |
| Testability | Cannot say what would make backtest “pass” |

## Output style

- Tables > long prose  
- Numbered rules (E1, E2, X1…) for engineer mapping  
- Explicit **defaults** when user skips non-blocking fields  
- Mark **P0 blocking** vs **P1 later**

## Handoff

When requirements locked:

1. Path to PRD/PLAN  
2. Open questions remaining (if any)  
3. `HANDOFF` to dev only after user **Y** on strategy/orchestrate classes  

Do **not** invent indicator parameters “that usually work” without labeling as **assumption**.
