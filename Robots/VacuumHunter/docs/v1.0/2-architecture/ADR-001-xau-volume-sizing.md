# ADR-001: XAUUSD volume sizing via VolumeForFixedRisk

## Context

On some cTrader XAUUSD symbols the feed reports:

```text
tickSize=0.01  tickValue=0.01  lotSize=100  pipSize=0.01  pipValue=0.01
```

Naive formula:

```text
lossPerUnit = (slDist / tickSize) * tickValue / lotSize
```

understates risk by ~**100×** (e.g. vol=625 for $100 risk instead of ~6). Live PnL (~$1 per unit per $1 move) contradicted TickValue math.

Pip-only `PriceToAmount` similarly reported `risk=$1 (0.01%)` while true risk was ~1%.

## Decision

1. **Primary sizing:** `Symbol.VolumeForFixedRisk(riskMoney, slPips, RoundingMode.Down)`.  
2. Tick-based volume is diagnostic only; if tick vol &gt; 3× FixedRisk vol → **discard tick**.  
3. Hard cap volume at FixedRisk(1.5 × riskMoney).  
4. Report expected risk via **inverse** of FixedRisk (scale target risk by vol ratio), not raw TickValue.  
5. Never force `VolumeInUnitsMin` when that would exceed risk target.

## Consequences

- ✅ Matches observed correct sizes (~6 units / $100 / ~$16 SL on 10k).  
- ✅ Safer across odd CFD metadata.  
- ❌ Still depends on broker’s FixedRisk implementation quality.  
- ❌ Cross-broker portability should re-validate with one OPEN log line.

## Related bugs / follow-ups

| Symptom | Cause | Fix |
| --- | --- | --- |
| vol=625, equity chaos | Tick sizing | FixedRisk primary |
| risk=$1 log | Pip/Tick amount | Inverse FixedRisk / price-unit check |
| FixedRisk vol ≫ risk$/slDist | Broker metadata | Conservative cap `vol ≤ risk$/slDist` |
| EntityNotFound partial | Soft TP1 race | **v1.1+ hard single TP** (no partial) |
| Trail kills runner | pips too small on XAU | Trail in **R**; default off |

## Daily loss (clarification)

Account **daily loss $** is equity vs day-start equity — **not** max loss per order. Per-order size uses Risk % and remaining daily **equity room**.
