# ADR-002: Soft TP1 partial (historical)

## Status

**Superseded by v1.1+** — VacuumHunter now uses a **single broker TP** at entry (RR / Structure / Fixed $). No partial, no soft TP1/TP2.

Kept for history only.

## Context (v1.0)

v1 opened with `TakeProfit = TP1`. When price hit TP1:

1. Broker closed **100%** of the position.  
2. Bot tried `ClosePosition(partial)` → **`EntityNotFound`**.  
3. Runner / BE / TP2 never ran.

## Decision (v1.0)

- Entry SL only; soft TP1 in bot dictionaries; partial then BE+TP2.

## Decision (v1.1+)

- Hard SL + hard TP full size at entry.  
- BE/Trail via `CTrailingManager` in **R** relative to that trade’s SL.  
- Partial stack removed (hurts average RR for this strategy).
