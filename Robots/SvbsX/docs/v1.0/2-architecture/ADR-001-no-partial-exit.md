# ADR-001: No partial close

## Context

Breakout strategies often use scale-out (e.g. 40% @ 1.5R, runner) to smooth equity. Product owner for SVBS-X **explicitly rejects partial closes**. BE and trailing full size are acceptable.

## Decision

- **Single full-size position** until complete exit.
- Exit tools allowed: initial SL, break-even, trailing stop, fixed RR (research), session/time flat.
- **No** TP1/TP2 volume splits, no `ClosePosition(position, partialVolume)` paths.

## Consequences

| + | − |
| --- | --- |
| Simpler code & fewer bugs | Larger giveback from peak open PnL |
| Cleaner R-distribution stats | Winrate may look lower than partial systems |
| Matches owner preference | Trail distance must be calibrated (not too tight) |

## Status

Accepted — 2026-07-12
