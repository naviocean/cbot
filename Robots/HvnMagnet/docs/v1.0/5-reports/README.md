# Reports — HvnMagnet

Store Strategy Tester / cTrader backtest exports and research notes here.

## Naming

```text
REPORT-bt-YYYYMMDD-XAUUSD-M15-<tag>.md
```

Optional: attach HTML/CSV from tester alongside.

## Minimum fields per report

| Field | Example |
| --- | --- |
| Period | 2024-01-01 → 2025-12-31 |
| Symbol / TF | XAUUSD M15 |
| Preset | Research baseline (PRD §7) |
| Ablation ID | A / B / C / D |
| Trades | N |
| Net / PF / MaxDD% | … |
| Expectancy R | … |
| Notes | Failures, session bias, param freezes |

## Ablation log (fill after runs)

| ID | HTF | Shape | Delta | MinR | Trades | PF | MaxDD% | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A | on | off | off | on | | | | |
| B | on | on | off | on | | | | |
| C | on | on | on | on | | | | product default |
| D | on | on | on | on + PocSide | | | | |

## Live / demo soak

Journal path or brief: start date, risk%, max trades/day, incidents (spread, flatten, bug).
