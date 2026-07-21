---
name: strategy-tester-analysis
description: Analyze MT5 Strategy Tester reports (HTML/XML) and journals for robustness, overfitting, and prop fitness. Use when user provides backtest report, .htm/.html tester output, XML stats, equity curve discussion, PF/DD review, or /test report.
---

# Strategy Tester Analysis

Primary skill for **`ea-tester`** on backtest artifacts. Complements `ea-debugging-patterns` (runtime errors) — this skill is about **economic robustness**, not only retcodes.

## When to load

| Trigger | Action |
| ------- | ------ |
| `/test report`, HTML/XML Strategy Tester | Full analysis below |
| User quotes PF, DD, expectancy | Validate + contextualize |
| “Is this ready for live/prop?” | Combine with `prop-firm-constraints` if prop |

## Inputs to request (if missing)

1. Report file path(s) — **same settings** if comparing  
2. Symbol, TF, period (from–to), model (Every tick / 1-min OHLC / Open prices)  
3. Deposit, leverage, currency  
4. Spread mode (current / fixed), commission  
5. Forward / OOS period? Optimization?  

## Metrics — how to read (not vanity)

| Metric | Healthy signal (heuristic) | Red flags |
| ------ | -------------------------- | --------- |
| **Profit Factor** | Stable > ~1.3–1.5 with enough trades | PF huge + few trades; collapses OOS |
| **Max DD (equity)** | Consistent with risk model | DD ≈ profit (fragile); DD late spike |
| **Recovery Factor** | Profit / DD reasonable | Profit only from 1 streak |
| **Expected payoff** | Positive after costs | Tiny edge < spread/commission |
| **Trade count** | Enough for TF (e.g. hundreds on M15 multi-year) | < 30–50 total for “proof” |
| **Avg trade duration** | Matches strategy story | Instant open/close spam |
| **Sharpe / LR correlation** | If present, not extreme alone | Ignore if period cherry-picked |
| **Consecutive losses** | Within risk plan | Long streak that would hit daily prop limit |

**Never** rank strategies on PF alone.

## Backtest realism (anti-fantasy)

Flag and downgrade confidence when:

| Setting / pattern | Risk |
| ----------------- | ---- |
| Open prices only on M1 scalper | Unrealistic fills |
| “Every tick” but short history | Variance / luck |
| Fixed spread too low for symbol (XAU, weekend) | Inflated PF |
| Zero commission on raw account model | Overstated edge |
| Optimization on full sample, no OOS/forward | Overfit |
| Peak equity from 1–2 trades / 1 week | Fragile |
| Lots of trades in one volatile day | Prop daily loss risk |
| Modify-heavy / stop hunt sensitive without slip | Live degradation |

State clearly:

```text
CONFIDENCE: high | medium | low
REALISM: adequate | questionable | poor
```

## Journal + report together

If logs available:

- Error clusters (10016, 4756) near “best” periods → edge may be invalid stops luck  
- Open→close same bar floods → overtrading  
- Modify spam → broker risk / ban risk  

Use `ea-debugging-patterns` for code-level error meaning.

## Analysis output template

Save durable notes under `docs/{version}/5-reports/REPORT-{slug}.md` when user wants a lasting artifact.

```markdown
# REPORT: {slug}

## Setup
- Symbol / TF / period / model / deposit / spread / commission
- EA build / inputs (hash or list)

## Headline metrics
| Metric | Value |
| PF | |
| Net profit | |
| Max DD % / money | |
| Trades | |
| Expectancy | |

## Robustness
- CONFIDENCE / REALISM
- Overfit risks
- Cost sensitivity

## Prop lens (if applicable)
- Daily worst vs limit
- Total DD vs limit
- Consistency / trade rules

## Bugs / anomalies
- …

## Recommendations
1. … (inputs / filters / risk — not “increase lot”)
2. HANDOFF to dev if code fix needed
```

## Verdict language (Iron Law)

| Allowed | Forbidden |
| ------- | --------- |
| “Metrics under **these** settings look …” | “Guaranteed profitable” |
| “Not prop-safe under stated limits” | “Just increase risk to pass” |
| “Need OOS / every-tick / costs” | “Perfect EA” from one HTML |

## Handoff to developers

If code/logic change needed → `HANDOFF-*.md` with:

- Symptom in report/log  
- Suspected cause  
- Acceptance metrics after fix (e.g. “0× error 10016; DD < X”)  
