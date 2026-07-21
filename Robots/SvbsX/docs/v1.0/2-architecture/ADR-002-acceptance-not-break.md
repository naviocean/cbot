# ADR-002: Enter on acceptance, not on first VA break

## Context

Original SVBS idea entered on VAH/VAL break + volume surge. On XAU, London/NY opens frequently produce **volume spikes + liquidity sweeps** through Asia highs/VA that reverse quickly. Entering on first close outside VA overfits open noise.

## Decision

Two-phase entry:

1. **Break** — arm setup only (first close outside prior session VA + body quality).
2. **Accept** — enter only after retest-hold (default) or continuation confirmation, with volume surge measured on the **accept** bar and POC migration filter.

Timeout: 12 M5 bars; invalidate if price re-accepts mid-VA.

## Consequences

| + | − |
| --- | --- |
| Fewer stop-hunts at session open | Miss some vertical trend days (no retest) |
| Volume filter more meaningful | Lower trade frequency (~0.5–2/day) |
| Clear state machine for logs | More implementation complexity |

## Alternatives considered

| Alt | Why rejected for v1 |
| --- | --- |
| Market on first break | High fakeout rate on XAU |
| Pending stop beyond break high only | Still hit by spikes; no acceptance |
| Mean-revert into VA | Opposite edge; out of product scope |

## Status

Accepted — 2026-07-12
