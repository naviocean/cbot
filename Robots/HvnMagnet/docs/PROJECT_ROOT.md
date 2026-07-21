# HvnMagnet (HMPD) — Project Root

| Field | Value |
| --- | --- |
| **Name** | HvnMagnet |
| **Strategy code** | HMPD — HVN Magnet Pullback + Delta Confirmation |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active version** | **v1.0** — code + spec |
| **Primary symbol** | XAUUSD |
| **Signal TF** | M15 (M5 research later) |
| **Bias TF** | H1 (parameter) |
| **Session** | London / NY / Overlap preferred; Asia off by default |
| **Label** | `HvnMagnet` |
| **Status** | Implemented (Release `.algo`); paper/demo before live scale |

## One-line strategy

Trade **pullback into strong HVN** on Adaptive Composite Volume Profile only when **HTF/POC bias**, **healthy profile shape**, and **tick-delta imbalance** agree; **risk-% size** (daily equity room cap); **single hard TP** (RR / Structure / Fixed$); BE/Trail in **R**; equity gates via `RiskManager.OnTick`.

## Edge hypothesis

HVN is a volume **magnet** (acceptance zone). In trend, price often revisits strong HVN and continues if aggressive flow returns. Edge = **structure (HVN) + direction (bias/shape) + flow (delta)** — not blind “buy support.”

Complementary to VacuumHunter:

| Bot | Node | Expectation |
| --- | --- | --- |
| VacuumHunter | LVN | Fill void / reject at thin volume |
| HvnMagnet | HVN | Hold / continue at thick volume |

## Repo map (target)

| Path | Role |
| --- | --- |
| `Robots/HvnMagnet/HvnMagnet/HvnMagnet.cs` | Robot orchestrator (to implement) |
| `Robots/HvnMagnet/HvnMagnet/SignalEngine.cs` | Pure entry rules F/E codes (to implement) |
| `Common/VolumeProfile.cs` | Composite VP, HVN/LVN, shape, viz |
| `Common/ProfileData.cs` | Profile snapshot + `Hvns` helpers |
| `Common/TickDeltaEngine.cs` | Tick up/down imbalance proxy |
| `Common/RiskManager.cs` | Volume + equity DD/daily $ + OnTick flatten |
| `Common/SessionFilter.cs` | Asia/London/NY/Overlap |
| `Common/NewsFilter.cs` | Manual schedule blackout |
| `Common/TrailingManager.cs` | BE + trailing from R |
| `Common/MarketCondition.cs` | Spread guard |
| `Common/Logger.cs` / `PriceUtils.cs` | Logging, price helpers |
| `Robots/VacuumHunter/**` | Reference skeleton (do not merge strategies) |

## Documentation index

| Doc | Path |
| --- | --- |
| README | [README.md](./README.md) |
| PRD | [v1.0/1-prds/PRD-hmpd.md](./v1.0/1-prds/PRD-hmpd.md) |
| Architecture | [v1.0/2-architecture/ARCH-hvn-magnet.md](./v1.0/2-architecture/ARCH-hvn-magnet.md) |
| ADR separate bot | [v1.0/2-architecture/ADR-001-separate-bot-from-vh.md](./v1.0/2-architecture/ADR-001-separate-bot-from-vh.md) |
| ADR delta required | [v1.0/2-architecture/ADR-002-delta-required-v1.md](./v1.0/2-architecture/ADR-002-delta-required-v1.md) |
| ADR no partial | [v1.0/2-architecture/ADR-003-no-partial-v1.md](./v1.0/2-architecture/ADR-003-no-partial-v1.md) |
| Implement plan | [v1.0/3-plans/PLAN-implement-hvn-magnet.md](./v1.0/3-plans/PLAN-implement-hvn-magnet.md) |
| Tasks | [v1.0/4-tasks/TASK-backlog.md](./v1.0/4-tasks/TASK-backlog.md) |
| Reports | [v1.0/5-reports/README.md](./v1.0/5-reports/README.md) |

## Version history

| Version | Date | Notes |
| --- | --- | --- |
| v1.0-spec | 2026-07-12 | Full PRD + ARCH + PLAN |
| v1.0 | 2026-07-12 | cBot + SignalEngine; Release build OK |
| v1.0.1 | 2026-07-12 | Review fixes: E7 Structure-only; BE/Trail + tradesToday recover; M1/M2; close-in-zone optional |
