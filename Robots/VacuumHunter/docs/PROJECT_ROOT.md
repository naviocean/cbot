# Vacuum Hunter — Project Root

| Field | Value |
| --- | --- |
| **Name** | VacuumHunter |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active version** | **v1.2** |
| **Primary symbol** | XAUUSD |
| **Signal TF** | M15 (also usable M30) |
| **Bias TF** | H1 (parameter) |
| **Session** | Asia / London / NY / Overlap toggles only (default NY) |
| **Label** | `VacuumHunter` |
| **Status** | Research / paper-ready; walk-forward before live scale |

## One-line strategy

Săn **LVN vacuum** trên Adaptive Composite Volume Profile; rejection + optional delta/shape/HTF; **risk-% size** (capped by daily equity room); **single TP** (RR / Structure / Fixed$); BE/Trail in **R**; account gates on **equity** via `RiskManager.OnTick`.

## Edge hypothesis

Giá xuyên nhanh hoặc reject tại vùng volume mỏng. Mục tiêu: **ít setup, quality cao**.

## Repo map

| Path | Role |
| --- | --- |
| `Robots/VacuumHunter/VacuumHunter/VacuumHunter.cs` | Robot orchestrator |
| `Robots/VacuumHunter/VacuumHunter/SignalEngine.cs` | Pure entry rules (E/F codes) |
| `Common/VolumeProfile.cs` | Composite VP, HVN/LVN, shape, viz |
| `Common/ProfileData.cs` | Profile snapshot + nodes |
| `Common/TickDeltaEngine.cs` | Tick up/down imbalance proxy |
| `Common/RiskManager.cs` | Volume sizing + equity DD/daily $ + OnTick flatten |
| `Common/SessionFilter.cs` | Asia/London/NY/Overlap (fixed UTC hours) |
| `Common/NewsFilter.cs` | Schedule blackout |
| `Common/TrailingManager.cs` | BE + trailing (pips; bot converts from R) |
| `Common/MarketCondition.cs` | Spread guard |
| `Common/Logger.cs` / `PriceUtils.cs` | Logging, price helpers |

## Documentation index

| Doc | Path |
| --- | --- |
| README | [README.md](./README.md) |
| PRD | [v1.0/1-prds/PRD-vacuum-hunter.md](./v1.0/1-prds/PRD-vacuum-hunter.md) |
| Architecture | [v1.0/2-architecture/ARCH-vacuum-hunter.md](./v1.0/2-architecture/ARCH-vacuum-hunter.md) |
| ADR XAU sizing | [v1.0/2-architecture/ADR-001-xau-volume-sizing.md](./v1.0/2-architecture/ADR-001-xau-volume-sizing.md) |
| ADR soft TP1 (historical) | [v1.0/2-architecture/ADR-002-soft-tp1-partial.md](./v1.0/2-architecture/ADR-002-soft-tp1-partial.md) — **superseded by v1.1 hard TP** |
| Optimize | [v1.0/3-plans/PLAN-optimize.md](./v1.0/3-plans/PLAN-optimize.md) |
| Tasks | [v1.0/4-tasks/TASK-backlog.md](./v1.0/4-tasks/TASK-backlog.md) |

## Version history

| Version | Date | Notes |
| --- | --- | --- |
| v1.0 | 2026-07 | Full stack; TP1 partial |
| v1.1 | 2026-07 | Single TP + BE/Trail R; no partial |
| v1.2 | 2026-07 | Sessions multi-toggle; daily loss/profit **$** + flatten; `RiskManager.OnTick` equity-only gates; size cap by daily room |
