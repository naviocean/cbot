# PLAN — Optimize VacuumHunter correctly

## Principle

Optimize **edge (structure)**, not risk%. Use few axes, walk-forward, ablation. Low frequency by design.

## Do not optimize

- Risk %, Max Trades / Day  
- Session toggles as “edge” (test sessions separately if needed)  
- Max Daily Loss/Profit $, Flatten flags (ops / prop rules)  
- Trail (keep **off** for baseline)

## Pipeline

### T0 — Technical sanity

- Confirm OPEN: `risk$` ≈ Risk% and ≤ `dayRoom` if daily loss set  
- Single TP fill path (SL/TP hard); BE if enabled  
- Risk flatten only on ticks if Flatten* on  
- **Pass/fail only**

### T1 — Structure baseline

| Param | Value |
| --- | --- |
| Require Delta | false |
| Require Shape | false |
| Require HTF | true |
| Allow POC/VA | true |
| Use Trailing | false |
| TP Mode | RiskReward, RR=2 |
| Sessions | NY only (or Overlap-only as alt test) |

Coarse grid example:

| Axis | Values |
| --- | --- |
| Bin Size | 0.5, 0.75, 1.0 |
| Lookback Days | 3, 4, 5 |
| Min LVN Strength | 0.15, 0.20, 0.30 |
| Min SL distance (×ATR) | 0.6, 0.8, 1.0 |

### T2 — Ablation

1. HTF on/off  
2. Delta on + MinDeltaStrength  
3. Shape on  
4. RR ∈ {1.5, 2.0, 2.5}  
5. Session: NY vs London+NY vs Overlap only  

### T3 — Walk-forward

Optimize coarse IS → freeze → single OOS run per fold. Reject if OOS collapses.

## Metrics

Use: #trades, PF, MaxDD %, expectancy R.  
Avoid sole objective: max net profit, winrate alone.

## Presets

### Research

```text
Risk % = 0.75
TP Mode = RiskReward, RR = 2
Require Delta/Shape = false, HTF = true
Trailing = false
Trade New York = true (others false)
Max Daily Loss/Profit $ = 0 (or set for prop sim)
```

### Conservative

```text
Risk % = 0.5
Require Delta = true, Min Delta Strength = 1.2
Require Shape = true, HTF = true
Min LVN Strength = 0.28
Max Trades / Day = 1
Max Daily Loss $ = (account-specific), Flatten On Daily Loss = true if prop
```
