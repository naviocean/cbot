# SVBS-X — Project Root

| Field | Value |
| --- | --- |
| **Name** | SVBS-X (Session VA Expansion — XAU) |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active version** | v1.1 (code of record) |
| **Primary symbol** | XAUUSD |
| **Signal TF** | M5 (primary) |
| **Account** | Personal |
| **Label** | `SvbsX` |
| **Status** | Implemented — paper/backtest before live scale |

## One-line strategy

Trade **session expansion** on gold: break prior session VA → **acceptance** (default BreakConfirm) → full-size entry; manage with **BE + trail** and optional **TP RR**; **no partial**.

## Edge hypothesis

Most retail fades VAH/VAL. SVBS-X trades **acceptance outside** prior session balance. On XAU, open spikes are often hunts — entry is delayed (acceptance), with chase/SL caps and XAU-safe sizing.

## Non-goals

- Partial close / scale-out  
- HVN/LVN take-profit targets  
- Multi-symbol / grid / martingale  
- Prop hard rules (optional daily $ soft-stops only)  
- True exchange volume / footprint  
- Exit Mode enum / Trail-ATR hybrid / time-stop (removed)

## Repo map

| Path | Role |
| --- | --- |
| `Robots/SvbsX/SvbsX/SvbsX.cs` | Robot orchestrator |
| `Robots/SvbsX/SvbsX/SignalEngine.cs` | Entry state machine |
| `Robots/SvbsX/SvbsX/SessionClock.cs` | Fixed UTC clocks + flat (incl. next day) |
| `Robots/SvbsX/SvbsX.sln` | Solution |
| `Robots/SvbsX.algo` | Build output |
| `Robots/SvbsX/docs/` | Documentation |
| `Common/VolumeProfile.cs` | `BuildRange` session VP |
| `Common/RiskManager.cs` | Equity DD + daily $ limits |
| `Common/TrailingManager.cs` | BE + trail full size |
| `Common/SessionFilter.cs` | Asia/London/NY/Overlap toggles |
| `Common/*` | News, MarketCondition, Logger, PriceUtils |

## Documentation index

| Doc | Path |
| --- | --- |
| README | [README.md](./README.md) |
| PRD | [v1.0/1-prds/PRD-svbs-x.md](./v1.0/1-prds/PRD-svbs-x.md) |
| Architecture | [v1.0/2-architecture/ARCH-svbs-x.md](./v1.0/2-architecture/ARCH-svbs-x.md) |
| ADR-001 No partial | [ADR-001-no-partial-exit.md](./v1.0/2-architecture/ADR-001-no-partial-exit.md) |
| ADR-002 Acceptance | [ADR-002-acceptance-not-break.md](./v1.0/2-architecture/ADR-002-acceptance-not-break.md) |
| ADR-003 Sessions | [ADR-003-session-windows-xau.md](./v1.0/2-architecture/ADR-003-session-windows-xau.md) |
| ADR-004 XAU sizing | [ADR-004-xau-volume-sizing.md](./v1.0/2-architecture/ADR-004-xau-volume-sizing.md) |
| Plan | [PLAN-implement-svbs-x.md](./v1.0/3-plans/PLAN-implement-svbs-x.md) |
| Tasks | [TASK-backlog.md](./v1.0/4-tasks/TASK-backlog.md) |
| Reports | [5-reports/](./v1.0/5-reports/) |

## Version history

| Version | Date | Notes |
| --- | --- | --- |
| v1.0 | 2026-07-12 | Spec + initial cBot |
| v1.1 | 2026-07-12 | Session toggles only; BreakConfirm default; SL ×ATR + chase; XAU size Oz/FixedRisk; daily loss/profit $; no ExitMode / Trail-ATR / time-stop; TP RR only; weekend session flat |
