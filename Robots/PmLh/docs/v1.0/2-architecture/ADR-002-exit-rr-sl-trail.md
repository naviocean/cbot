# ADR-002: Exit = SL + fixed TP(R) + optional trail only

## Context

Early PM-LH narrative included “take profit when POC goes flat or reverses.” That couples exit to a lagging migration metric, complicates backtests, and mixes thesis exit with risk geometry.

Team decision: **v1 exit must be simple** for implementability and clean research comparison across TF/session/params.

## Decision

**v1 exits only:**

1. **Hard SL** (structure + ATR floor)  
2. **Hard TP** = `RR × R` where `R = |entry − SL|`  
3. **Optional BE** (moves SL)  
4. **Optional trailing** (moves SL)

**Explicitly out of v1:**

- POC flat / reverse exit  
- Time stop  
- Structure TP (next HVN / VA)  
- Partial closes  

Account-level **RiskManager flatten** may still close positions (equity/daily) — logged as `X_FLATTEN_EQUITY`, not strategy thesis.

## Consequences

- ✅ One path for expectancy vs RR, trail on/off  
- ✅ Reuse TrailingManager contract from VH/HMPD  
- ✅ Simpler ExecuteSignal  
- ❌ May leave money on table vs structure magnets — re-evaluate only after baseline RR results  
- ❌ POC remains **entry/bias only**

## Status

Accepted (v1.0-spec).
