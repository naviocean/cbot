# ADR-003: Session enable toggles + fixed UTC clocks

## Context

User-facing session config should match other RedWave bots: **Trade Asia / London / NY / Overlap** only — not dozens of hour knobs.

## Decision

1. **UI:** four bools via `CSessionFilter` (same default hours as Common).
2. **Strategy clocks** fixed in `SessionClock` (UTC):

| Role | UTC |
| --- | --- |
| Asia VA build | 00:00–07:00 |
| London VA build | 07:00–12:00 |
| A→L entry (Asia VA) | 07:30–12:00 if Trade London |
| L→NY entry (London VA) | 13:00–23:00 NY / 13:00–16:00 Overlap |
| Asia session entry | 00:00–09:00 if Trade Asia (prior-day Asia VA) |
| Flat | Asia 09 / London 16 / NY 23; **or next calendar day** (weekend gap) |

3. Profile for window: Asia→London uses frozen Asia; London→NY uses frozen London; Asia session uses previous Asia.

## Consequences

| + | − |
| --- | --- |
| Simple UI | DST/broker offset needs code change if wrong |
| Weekend flat via date roll | Flat not at exact Friday 23:00 if no ticks — fixed by next-day rule |

## Status

Accepted — 2026-07-12; updated for toggles + next-day flat
