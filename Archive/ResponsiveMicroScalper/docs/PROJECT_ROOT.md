# Responsive Micro Scalper (RMS) — Project Root

| Field | Value |
| --- | --- |
| **Name** | Responsive Micro Scalper (RMS) |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active version** | v1.0 (spec only) |
| **Primary symbol** | XAUUSD |
| **Signal TF** | M1 (closed bar) |
| **Bias TF** | H1 (default) |
| **Account** | Personal |
| **Label** | `Rms` |
| **Status** | **ABANDONED** 2026-07-13 — do not develop further |

## One-line strategy

HTF bias + micro log-return **momentum/acceleration** on M1, with **vol-scaled** SL/TP (fixed **1.5R**) and **3-mode** regime scaling of thresholds, cooldown, and daily trade cap.

## Edge hypothesis

Short-horizon acceleration beyond a vol-normalized threshold, with HTF alignment, persists long enough for 1.5R after costs more often than noise and chop destroy expectancy.

## Decision: stop (2026-07-13)

| Item | Detail |
| --- | --- |
| **Decision** | **Stop** — no further feature/optimize/live on this edge |
| **Why** | Optimize ~6 months IS → OOS ~1 month recent **PnL flat** after costs; hypothesis not confirmed |
| **Keep** | Code + docs as research archive (reusable risk/regime patterns only) |
| **Do not** | Re-optimize thresholds, add filters to “save” accel edge, or merge into live stack |
| **Alternatives** | Other bots in repo (e.g. session/VA, pending-stop micro) if gold scalping continues elsewhere |

## Relationship to DynamicMicroScalper

| Bot | Edge |
| --- | --- |
| `DynamicMicroScalper` | Pending **stop** distance / refresh / OCO-style micro engine |
| **RMS** | Closed-bar **stats** momentum + adaptive regime (this project) |

**Do not** merge into `DynamicMicroScalper` without an explicit ADR.

## Non-goals (v1)

- Tick HFT, grid, martingale, pyramid  
- MA/RSI/ADX/VP/SMC signal stack  
- ML / HMM / Hurst regime  
- Partial exits  

## Repo map

| Path | Role |
| --- | --- |
| `Robots/ResponsiveMicroScalper/docs/` | Documentation |
| `Robots/ResponsiveMicroScalper/ResponsiveMicroScalper/ResponsiveMicroScalper.cs` | Robot orchestrator |
| `Robots/ResponsiveMicroScalper/ResponsiveMicroScalper/SignalEngine.cs` | Pure signal + regime |
| `Robots/ResponsiveMicroScalper/ResponsiveMicroScalper.sln` | Solution |
| `Robots/ResponsiveMicroScalper.algo` | Build output |
| `Common/*` | Risk, trail, session, news, logger, price utils |

## Documentation index

| Doc | Path |
| --- | --- |
| README | [README.md](./README.md) |
| PRD | [v1.0/1-prds/PRD-rms.md](./v1.0/1-prds/PRD-rms.md) |
| Tasks | [v1.0/4-tasks/TASK-backlog.md](./v1.0/4-tasks/TASK-backlog.md) |
| Reports | [v1.0/5-reports/](./v1.0/5-reports/) |

## Version history

| Version | Date | Notes |
| --- | --- | --- |
| v1.0.0 | 2026-07-13 | Implementable PRD from Adaptive Edition narrative; formulas, units, RR 1.5, closed-bar only, regime scale table locked |
| v1.0.0-impl | 2026-07-13 | Scaffold cBot compiled (SignalEngine + orchestrator + Common) |
| abandoned | 2026-07-13 | User stop: OOS flat after 6m optimize; no further development on this direction |
