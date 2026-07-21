# PocAbsorption (PADR) — Project Root

| Field | Value |
| --- | --- |
| **Name** | PocAbsorption |
| **Strategy Code** | PADR — POC Absorption & Delta Rejection |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active Version** | **v1.0** — spec + docs |
| **Primary Symbol** | XAUUSD |
| **Signal TF** | M15 Session Profile + M1/M5 Rejection Confirmation |
| **Session** | London / NY (Asia off by default) |
| **Label** | `PocAbsorption` |
| **Status** | Spec & Docs Complete — Ready for Implementation |

## One-line Strategy

Trade **rejection at Session POC absorption zone** when high tick-volume cluster coincides with Cumulative Delta divergence on M15 profile, triggered on M1/M5 bar close with structural SL (Node Edge ± 5–7 pips buffer) and target at opposite Value Area (VAH/VAL) for Target R:R >= 2.0.

## Edge Hypothesis

POC (Point of Control) represents the fairest value where heavy trading occurs. When aggressive market orders (e.g. Sell) fail to push price through POC because passive limit orders absorb them, a Cumulative Delta divergence forms. Confirmation via M1/M5 rejection candle validates institutional absorption and provides tight structural R:R.

## Repo Map

| Path | Role |
| --- | --- |
| `Robots/PocAbsorption/PocAbsorption/PocAbsorption.cs` | Robot orchestrator & cTrader parameters |
| `Robots/PocAbsorption/PocAbsorption/SignalEngine.cs` | Pure entry/exit signal logic F/E codes |
| `Common/VolumeProfile.cs` | Session VP, POC node detection, VAH/VAL |
| `Common/ProfileData.cs` | Profile snapshot & price level binning |
| `Common/TickDeltaEngine.cs` | Tick up/down imbalance proxy (Lee-Ready test) |
| `Common/RiskManager.cs` | % risk sizing, account equity gates, max daily trades |
| `Common/SessionFilter.cs` | London / NY session window filtering |
| `Common/NewsFilter.cs` | Schedule-based high-impact news blackout |
| `Common/TrailingManager.cs` | BE & Trailing Stop management in R |
| `Common/Logger.cs` / `PriceUtils.cs` | Structured logging & pip/point math helpers |

## Documentation Index

| Doc | Path |
| --- | --- |
| README | [README.md](./README.md) |
| PRD | [v1.0/1-prds/PRD-padr.md](./v1.0/1-prds/PRD-padr.md) |
| Architecture | [v1.0/2-architecture/ARCH-poc-absorption.md](./v1.0/2-architecture/ARCH-poc-absorption.md) |
| Implementation Plan | [v1.0/3-plans/PLAN-implement-poc-absorption.md](./v1.0/3-plans/PLAN-implement-poc-absorption.md) |

## Version History

| Version | Date | Notes |
| --- | --- | --- |
| v1.0-spec | 2026-07-20 | Initial PRD, Architecture, and Implementation Plan |
