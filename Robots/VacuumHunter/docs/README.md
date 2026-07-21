# VacuumHunter

cBot cTrader săn **LVN vacuum** trên Adaptive Composite Volume Profile (XAUUSD): rejection + optional delta/shape/HTF; **risk %** size; **single TP**; BE/Trail theo **R**; risk gates theo **account equity**.

## Quick Start

| Item | Value |
| --- | --- |
| Symbol | XAUUSD |
| Chart TF | **M15** (preferred) or M30 |
| Session | Enable only: **Asia / London / NY / Overlap** (hours fixed in `CSessionFilter`; default NY) |
| Balance | ≥ $1,000 recommended for 0.5–1% risk |
| Build | `Robots/VacuumHunter/VacuumHunter/` → Release → `VacuumHunter.algo` |
| Common | Linked via csproj `../../../Common/*.cs` |

### Run

1. Build Release (`dotnet build -c Release` or cTrader).
2. Attach **VacuumHunter** on XAUUSD M15.
3. Defaults: `Enable Trading=true`, `Debug Logging=false`, `Use Trailing=false`, `Trade New York=true`.
4. OPEN log: `risk=$…` matches Risk % (and daily room if Daily Loss set); `RR=…`.
5. Optional: `Visualize Profile=true`.

### Logging

| `Debug Logging` | Journal |
| --- | --- |
| **false** | Start, PASS, OPEN/CLOSE, day summary, risk warnings, hard errors |
| **true** | + reject bars, risk/spread debug |

## Core Logic (short)

1. **OnBar:** rebuild composite VP; evaluate signal on **closed** bar.
2. Filters: sessions (OR), news, spread, **equity risk gates**, max trades/day, 1 position.
3. Touch LVN → rejection → HTF vs POC (optional delta/shape).
4. **SL** = LVN ± ATR buffer, floored by min SL distance (×ATR).
5. **TP** = one full-size target: RiskReward (default) / Structure / Fixed $.
6. **OnTick only:** `RiskManager.OnTick` (equity gates + optional flatten); BE/Trail if enabled.

## Exit parameters

| Group | Params |
| --- | --- |
| Stop Loss | **LVN buffer (×ATR)**; **Min SL distance (×ATR)** |
| Take Profit | TP Mode **RiskReward** (default), RR Multiple=2, Fixed TP ($) |
| Break Even | Use BE, **BE Start (R)**, **BE Lock (R)**, Add Spread |
| Trailing | Use Trailing (**false**), **Trail Start (R)**, **Trail Step (R)** |

**No** TP1/TP2 partial.

## Account risk gates (equity only)

Daily/equity limits watch **Account.Equity**, not per-trade TP/SL.

| Parameter | Default | Meaning |
| --- | --- | --- |
| Max Equity DD % | 10 | Peak high-water mark; blocks new entries (0=off) |
| Flatten On Equity DD | false | Also market-close bot positions |
| Max Daily Loss ($) | 0=off | `equity − dayStartEquity ≤ −$X` → block |
| Flatten On Daily Loss | false | Also flatten |
| Max Daily Profit ($) | 0=off | Day PnL ≥ +$X → block |
| Flatten On Daily Profit | false | Also flatten |

`Risk %` sizes each new order; if Daily Loss is set, risk $ is also **capped by remaining daily room**.

## Research baseline

| Parameter | Default / research |
| --- | --- |
| Lot Size Mode | **RiskPercent** (default) or **FixedLots** |
| Risk % | 0.75–1.0 (if RiskPercent) |
| Fixed Lots | e.g. 0.01 (if FixedLots) |
| TP Mode / RR | RiskReward / **2.0** |
| BE | on, 1R / 0.05R |
| Trailing | **off** |
| Delta / Shape | **off** |
| HTF | **on** |
| Sessions | NY only |
| Daily loss/profit $ | 0 until you set prop-style caps |

## Docs

- [PROJECT_ROOT.md](./PROJECT_ROOT.md)
- [PRD](./v1.0/1-prds/PRD-vacuum-hunter.md)
- [Architecture](./v1.0/2-architecture/ARCH-vacuum-hunter.md)
- [Optimize](./v1.0/3-plans/PLAN-optimize.md)

## Limitations

- CFD tick volume ≠ exchange volume.
- Delta = mid up/down proxy.
- Low trade count → walk-forward, don’t over-optimize.
- Gap/slip can still push equity past daily $ before flatten fill.
