# PmLh (PM-LH) — Project Root

| Field | Value |
| --- | --- |
| **Name** | PmLh |
| **Strategy code** | PM-LH — POC Migration + LVN Highway |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active version** | **v1.0.1** — code + spec; research BT ongoing |
| **Primary symbol** | XAUUSD (research; not exclusive) |
| **Signal TF** | Parameterized (research: M5 / M15 / M30) |
| **Bias TF** | H1 (parameter, optional filter) |
| **Session** | Asia / London / NY / Overlap toggles (all research-open) |
| **Label** | `PmLh` |
| **Status** | Implemented (Release `.algo`); M5 journal soaks in progress — do not freeze live defaults yet |

## One-line strategy

Trade **in the direction of rolling POC migration** when price interacts with an **LVN** treated as a **highway** (fast travel through thin volume); **risk-% size**; exit **only** via **hard SL**, **fixed TP = RR×R**, or **trailing** (optional BE).

## Edge hypothesis

- **POC migration** ≈ acceptance / value shifting in direction D.  
- **LVN** ≈ low acceptance; price often moves quickly through the void (**highway**), not a magnet.  
- Edge candidate = **direction (migration) + thin structure (LVN) + optional flow/regime filters**.  
- **Not proven** until implement + backtest + ablation; scope (TF/session/frequency) stays **open** until data.

## Complementary bots (do not merge)

| Bot | Node / idea | Expectation |
| --- | --- | --- |
| VacuumHunter | LVN | Vacuum: reject / fill void |
| HvnMagnet | HVN | Magnet: pullback hold |
| **PmLh** | LVN + **POC migrate** | Highway: **run with** migration through LVN |

Same LVN can mean opposite intents (VH vs PmLh). Co-run policy is a **research/ops** decision after correlation data — not a code merge.

## Repo map (target)

| Path | Role |
| --- | --- |
| `Robots/PmLh/PmLh/PmLh.cs` | Robot orchestrator |
| `Robots/PmLh/PmLh/SignalEngine.cs` | Pure entry rules (F/E codes) |
| `Robots/PmLh/PmLh/PocMigrationTracker.cs` | Rolling POC series + migration score |
| `Robots/PmLh/PmLh.algo` | Release build output (repo root under PmLh/) |
| `Common/VolumeProfile.cs` | Composite / rolling VP, HVN/LVN, shape |
| `Common/ProfileData.cs` | Profile snapshot + node helpers |
| `Common/TickDeltaEngine.cs` | Tick imbalance proxy (optional E filter) |
| `Common/RiskManager.cs` | Volume + equity DD/daily $ + OnTick flatten |
| `Common/SessionFilter.cs` | Asia/London/NY/Overlap |
| `Common/NewsFilter.cs` | Manual schedule blackout |
| `Common/TrailingManager.cs` | BE + trailing from R |
| `Common/MarketCondition.cs` | Spread guard |
| `Robots/VacuumHunter/**` | Orchestrator template (copy-adapt; do not rewrite mid-flight) |
| `Robots/HvnMagnet/**` | Parallel product pattern (filters, RR exit) |

## Documentation index

| Doc | Path |
| --- | --- |
| README | [README.md](./README.md) |
| PRD | [v1.0/1-prds/PRD-pmlh.md](./v1.0/1-prds/PRD-pmlh.md) |
| Architecture | [v1.0/2-architecture/ARCH-pm-lh.md](./v1.0/2-architecture/ARCH-pm-lh.md) |
| ADR separate bot | [v1.0/2-architecture/ADR-001-separate-bot.md](./v1.0/2-architecture/ADR-001-separate-bot.md) |
| ADR exit RR only | [v1.0/2-architecture/ADR-002-exit-rr-sl-trail.md](./v1.0/2-architecture/ADR-002-exit-rr-sl-trail.md) |
| ADR profile sources | [v1.0/2-architecture/ADR-003-profile-sources.md](./v1.0/2-architecture/ADR-003-profile-sources.md) |
| Implement plan | [v1.0/3-plans/PLAN-implement-pm-lh.md](./v1.0/3-plans/PLAN-implement-pm-lh.md) |
| Tasks | [v1.0/4-tasks/TASK-backlog.md](./v1.0/4-tasks/TASK-backlog.md) |
| Reports | [v1.0/5-reports/README.md](./v1.0/5-reports/README.md) |

## Version history

| Version | Date | Notes |
| --- | --- | --- |
| v1.0-spec | 2026-07-12 | Full PRD + ARCH + ADRs + PLAN; research-wide scope; exit = SL/TP(R)/trail only |
| v1.0 | 2026-07-12 | cBot + SignalEngine + PocMigrationTracker; Release build OK |
| v1.0.1 | 2026-07-12 | BT-driven: streak fix + Strong M bypass; E_PRICE_POC; E_LVN_SIDE; docs sync |
