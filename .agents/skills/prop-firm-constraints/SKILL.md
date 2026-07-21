---
name: prop-firm-constraints
description: Prop-firm and funded-account trading constraints for EA/cBot design and review. Use when user mentions prop firm, FTMO, funded, challenge, payout rules, daily loss, max drawdown, consistency, news blackout, or account scaling plans.
---

# Prop-Firm Constraints

Use for **strategy design** (`algo-strategist`) and **backtest/report review** (`ea-tester`). Encode firm rules as **hard filters** the EA must respect — not as afterthought inputs.

> Rules differ by firm and change over time. Always confirm the user's **current challenge/funded rule sheet**. Defaults below are **conservative templates**, not legal advice.

## When to load

| Trigger | Action |
| ------- | ------ |
| prop, FTMO, funded, challenge, payout | Load this skill before locking entry/risk |
| Max DD / daily loss in PRD | Map to EA inputs + kill-switch behavior |
| Reviewing backtest for prop suitability | Score against checklist below |

Personal accounts: skip full skill; still ask risk % if vague.

## Blocking questions (ask if unknown)

1. **Firm / program** (name + challenge vs funded)?  
2. **Max total drawdown** (static vs trailing; equity or balance)?  
3. **Max daily loss** (how is “day” defined — server midnight)?  
4. **Min trading days / min trades** for payout?  
5. **Consistency** rule (e.g. best day ≤ X% of total profit)?  
6. **News** blackout (minutes before/after high-impact)?  
7. **Weekend / swap / holding** limits?  
8. **EA / HFT / martingale / grid** allowed?  
9. **Lot / risk** caps relative to balance?  
10. **Profit target** for phase (challenge)?

## Constraint → EA design mapping

| Constraint | Design implication |
| ---------- | ------------------ |
| Max daily loss | Equity kill-switch; block new entries for rest of day; count floating + closed |
| Max total DD | Soft stop → flatten or trade disable; never “hope recover” with size-up |
| Trailing DD | Track high-water equity; risk budget shrinks as peak rises |
| Consistency | Cap risk per trade/day so one day cannot dominate profit |
| News filter | Time filter around calendar (or hard session cut) |
| No martingale / grid | Single (or fixed max) positions; no lot multiply on loss |
| Min trade duration | Avoid scalp that firm flags as tick-scalping if banned |
| Weekend flat | Force close or block Friday after time T |

## Risk defaults (template only — override with firm sheet)

| Parameter | Conservative starter |
| --------- | -------------------- |
| Risk per trade | ≤ 0.25–0.5% (challenge often tighter than “1% retail”) |
| Daily loss stop | ≤ 60–80% of firm daily limit (buffer for spread/slippage) |
| Total DD stop | ≤ 60–80% of firm max DD |
| Max trades / day | Explicit cap (avoid revenge loops) |
| Correlated symbols | Treat basket as one risk unit if multi-symbol |

## Patterns to **reject** or flag for prop

- Martingale / recovery multipliers  
- Unbounded grid  
- No daily equity guard  
- Risk % that can breach daily limit in **1–2** full SL hits  
- “Pass challenge ASAP” with oversized lots  
- Curve-fit single week optimized without OOS  

## PRD / HANDOFF fields (required when prop=true)

```markdown
## Prop constraints
- Firm / phase:
- Max daily loss: … (definition: …)
- Max total DD: … (static/trailing, equity/balance)
- Consistency:
- News: …
- Forbidden styles: …
- EA kill rules: daily / total / news
- Risk per trade: …
```

## Tester checklist (prop lens)

- [ ] Worst historical day loss < firm daily limit (with buffer)  
- [ ] Max equity DD < firm max DD (with buffer)  
- [ ] No single day >> consistency threshold (if any)  
- [ ] Trade count / duration vs firm min rules  
- [ ] Spread/slippage stress still inside daily limit  

If fail: **do not** recommend “just raise lot”. Prefer lower risk, fewer sessions, or strategy change.

## Handoff to `mql5-expert` / `cbot-expert`

Specify implementables:

- Inputs: `InpMaxDailyLossPct`, `InpMaxTotalDdPct`, `InpRiskPercent`, news window, session flat  
- Prefer RWCommon `RiskManager` equity protection **when** `rwcommon=required`  
- Log when kill-switch trips (journal-readable for `/test`)
