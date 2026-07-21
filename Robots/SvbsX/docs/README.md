# SVBS-X

cBot cTrader: **Session VA Expansion** trên XAUUSD. Break Value Area phiên trước → **acceptance** → full-size entry; **BE / Trail / optional TP RR**. **Không partial close.**

## Quick Start

| Item | Value |
| --- | --- |
| Symbol | **XAUUSD** only |
| Chart TF | **M5** (primary) |
| Session | Enable only: **Asia / London / NY / Overlap** (hours fixed UTC in code) |
| Account | Personal |
| Size | **RiskPercent** (0.75%) or **FixedLots** |
| Max trades / day | 2 |
| Daily risk | **Max Daily Loss ($)** / **Max Daily Profit ($)** — `0` = off |
| Build | `Robots/SvbsX/SvbsX.sln` → Release → `SvbsX.algo` |
| Common | `../../../Common/*.cs` (`BuildRange`, RiskManager, TrailingManager, …) |

### Run

1. Attach **SvbsX** on XAUUSD **M5** (Robot TimeZone = UTC).
2. Enable sessions (default: London + NY + Overlap).
3. Paper first: `Debug Logging = true`.
4. Journal: `PROFILE Asia/London` → `E_BREAK_*` → `PASS_*` → `OPEN` (check `vol` small, `riskAtSl` ≈ risk %).
5. Optional: `Visualize Profile = true`.

### Logging

| `Debug Logging` | Journal |
| --- | --- |
| **false** | Start, E_BREAK, PASS, OPEN/CLOSE, BE, day summary, hard rejects |
| **true** | + soft rejects, size model debug |

## Core Logic (short)

1. Freeze **Asia VA** (00:00–07:00 UTC) and **London VA** (07:00–12:00) after session end.
2. Entry windows (if enabled): A→L **07:30–12** (Asia VA), L→NY **13–23** / overlap **13–16** (London VA), Asia session **00–09** (prior-day Asia VA).
3. **Break** first close outside VAH/VAL → wait **acceptance** (default **BreakConfirm**).
4. Filters optional: volume, POC (default **off**).
5. SL: VA edge ± ATR buffer; min/max SL in **×ATR**; **E_CHASE** if entry too far past VA.
6. Size: XAU-safe (Oz / FixedRisk; never trust broken TickValue alone).
7. Exit: SL, optional **TP RR** (`0` = none), **BE**, **Trail** (R only), **session flat** (incl. next calendar day / weekend).

## Exit (hard rules)

| Rule | Value |
| --- | --- |
| Partial close | **FORBIDDEN** |
| TP RR Multiple | **0** = no hard TP; e.g. **2** = TP at 2R |
| Break Even | Use BE, Start **1.0R**, Lock **0.05R** |
| Trailing | Use Trailing, Start **1.5R**, Step **0.5R** (no ATR hybrid) |
| Session flat | End of host session **or** next calendar day |
| Time stop | **Removed** |

## Research / live baseline params

| Parameter | Default (code) |
| --- | --- |
| Position Size Mode | RiskPercent |
| Risk % | 0.75 |
| Max Daily Loss ($) | 0 (off) |
| Max Daily Profit ($) | 0 (off) |
| Max Trades / Day | 2 |
| Accept Mode | **BreakConfirm** |
| Accept Timeout Bars | **24** |
| Use Volume Filter | **false** |
| Use POC Filter | **false** |
| Max VA Width $ | **50** |
| SL buffer / Min / Max SL | **0.35 / 0.8 / 3.0 ×ATR** |
| Max Entry Ext | **1.5 ×ATR** |
| TP RR Multiple | **0** |
| BE / Trail | on, 1.0R / 1.5R+0.5R |

## Docs

- [PROJECT_ROOT.md](./PROJECT_ROOT.md) — index & version  
- [PRD](./v1.0/1-prds/PRD-svbs-x.md) — full rules  
- [Architecture](./v1.0/2-architecture/ARCH-svbs-x.md)  
- [Implementation plan](./v1.0/3-plans/PLAN-implement-svbs-x.md)  
- [Task backlog](./v1.0/4-tasks/TASK-backlog.md)  

## Limitations

- CFD **tick volume** ≠ COMEX; volume filter is relative only.
- Session clocks are **UTC fixed** in `SessionClock` — map broker if needed.
- XAU news: spread/slippage can exceed theoretical SL risk (check deal close vs SL).
- Symbol meta `tickVal=0.01 lot=100` is common; sizing uses Oz/FixedRisk safeguards (see ADR-004).
