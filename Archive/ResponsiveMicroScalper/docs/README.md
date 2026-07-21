# Responsive Micro Scalper (RMS)

Statistics-driven **M1 micro-scalper** for XAUUSD: HTF bias, micro momentum + acceleration, volatility-scaled risk, and simple Aggressive / Normal / Conservative regime scaling.

## Quick start

| Item | Value |
| --- | --- |
| Platform | cTrader cBot |
| Symbol | XAUUSD |
| Signal TF | M1 (closed bar) |
| Bias TF | H1 (default) |
| Risk default | 0.5% / trade |
| RR | 1.5 × SL (vol/ATR based) |
| Status | **Abandoned** (2026-07-13) — OOS flat after optimize; archive only |

## Core logic (summary)

1. **HTF bias** — close vs close N bars ago on H1/H4.  
2. **Chop filter** — micro variance of log returns must clear floor.  
3. **Trigger** — momentum sign + acceleration beyond vol-scaled threshold.  
4. **Regime** — scales threshold, cooldown, max trades, SL distance.  
5. **Exit** — SL / TP / optional BE+trail / time stop / daily risk kill.

## Docs

| Doc | Purpose |
| --- | --- |
| [PROJECT_ROOT.md](./PROJECT_ROOT.md) | Index + status |
| [PRD-rms.md](./v1.0/1-prds/PRD-rms.md) | Full rules, formulas, params, DoD |
| [TASK-backlog.md](./v1.0/4-tasks/TASK-backlog.md) | Implementation tasks after approval |

## Not this bot

`Robots/DynamicMicroScalper` is a different strategy (pending-stop distance engine).

## Stopped

Do **not** continue developing the M1 accel-continuation edge. Research closed after IS optimize / OOS ~breakeven. Code remains for reference only.

## Disclaimer

Personal research tooling. Not financial advice. You own all capital risk.
