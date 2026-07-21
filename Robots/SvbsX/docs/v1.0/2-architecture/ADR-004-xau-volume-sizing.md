# ADR-004: XAUUSD volume sizing (broken TickValue)

## Context

Many cTrader XAU feeds report:

```text
TickSize=0.01  TickValue=0.01  LotSize=100
```

Naive `PriceToAmount` / tick formula understates risk ~**100×** → volumes like 448–956 units and equity wipe. Same class of bug as VacuumHunter ADR-001.

## Decision

1. If gold and tick meta inconsistent (`TickValue` not ≈ `LotSize × TickSize`): size with **Oz heuristic** only:  
   `volume ≈ riskMoney / slDist` (1 unit ≈ $1 per $1 move).
2. Otherwise take **minimum** of VolumeForFixedRisk, Oz, Tick.
3. Cap by Oz; SAFETY ABORT if `volume × slDist > 3 × riskMoney`.
4. Log `via=…`, `riskEst`, `riskAtSl`, tick/lot meta on OPEN.

## Consequences

| + | − |
| --- | --- |
| Avoids 100× oversize on bad meta | Oz assumes USD-ish $1/oz unit |
| Aligns with observed FixedRisk ~ few units / $75 | Still validate per broker with one OPEN log |

## Status

Accepted — 2026-07-12 (post wipe incident in backtest)
