# HvnMagnet (HMPD)

cBot cTrader: **HVN Magnet Pullback + Delta Confirmation** trên Adaptive Composite Volume Profile (XAUUSD). Pullback về HVN mạnh + bias HTF + shape healthy + delta confirm; **risk %** size; **single TP**; BE/Trail theo **R**; risk gates theo **account equity**.

## Quick Start

| Item | Value |
| --- | --- |
| Symbol | XAUUSD |
| Chart TF | **M15** (preferred) |
| Session | **London + NY** (Asia off by default) |
| Balance | ≥ $1,000 recommended for 0.25–0.5% risk |
| Build | `Robots/HvnMagnet/HvnMagnet/` → Release → `HvnMagnet.algo` |
| Common | Linked via csproj `../../../Common/*.cs` |
| Status | **v1.0 code** — build Release; research/BT still pending |

### Run (after build)

1. Build Release (`dotnet build -c Release` or cTrader).
2. Attach **HvnMagnet** on XAUUSD M15.
3. Defaults research: `Enable Trading=true`, `Require Delta=true`, `Require Shape=true`, `Require HTF=true`.
4. OPEN log: `risk=$…`, `RR=…`, `hvn=…`, `delta=…`.
5. Optional: `Visualize Profile=true`.

### Logging

| `Debug Logging` | Journal |
| --- | --- |
| **false** | Start, PASS, OPEN/CLOSE, day summary, risk warnings, hard errors |
| **true** | + reject bars, near-HVN debug, RR skip reasons |

## Core Logic (short)

1. **OnBar:** rebuild composite VP (2–3 days default); evaluate on **closed** bar.
2. Filters: sessions (OR), news, spread, **equity risk gates**, max trades/day, 1 position.
3. Select eligible **strong HVN** → price **touch** → **rejection** candle → **HTF bias** + **shape** + **delta**.
4. **RR gate:** skip if first structure target R &lt; Min R.
5. **SL** = HVN edge ± ATR buffer, floored by min SL distance (×ATR).
6. **TP** = one full-size target: RiskReward / Structure / Fixed $.
7. **OnTick only:** `RiskManager.OnTick`; BE/Trail if enabled.

## Exit parameters

| Group | Params |
| --- | --- |
| Stop Loss | **HVN buffer (×ATR)**; **Min SL distance (×ATR)** |
| Take Profit | TP Mode **RiskReward** (default), RR Multiple=2, Structure magnet, Fixed $ |
| Break Even | Use BE, **BE Start (R)**, **BE Lock (R)**, Add Spread |
| Trailing | Use Trailing (**false** baseline), **Trail Start (R)**, **Trail Step (R)** |

**No** TP1/TP2 partial in v1.0 (see ADR-003).

## Account risk gates (equity only)

| Parameter | Default | Meaning |
| --- | --- | --- |
| Max Equity DD % | 10 | Peak HWM; blocks new entries (0=off) |
| Flatten On Equity DD | false | Also market-close bot positions |
| Max Daily Loss ($) | 0=off | Block when day equity PnL ≤ −$X |
| Flatten On Daily Loss | false | Also flatten |
| Max Daily Profit ($) | 0=off | Block after +$X day |
| Flatten On Daily Profit | false | Also flatten |
| Max Trades / Day | **3** | Hard cap (quality over frequency) |

## Research baseline

```text
Risk % = 0.5
TP Mode = RiskReward, RR = 2.0
Require HTF = true
Require Delta = true
Require Shape = true
Min HVN Strength = 1.25
Min First Target R = 1.0
Lookback Days = 3
Trade London = true, Trade New York = true, Asia = false
Trailing = false
```

## Docs

- [PROJECT_ROOT.md](./PROJECT_ROOT.md)
- [PRD](./v1.0/1-prds/PRD-hmpd.md)
- [Architecture](./v1.0/2-architecture/ARCH-hvn-magnet.md)
- [Implement plan](./v1.0/3-plans/PLAN-implement-hvn-magnet.md)

## Limitations

- CFD tick volume ≠ exchange volume.
- Delta = mid up/down **proxy**, not true footprint.
- HVN is sticky — chop risk; min R + time invalidation required.
- Target frequency **not** 4–8/day; expect low-to-moderate selective trades.
- Do not co-run conflicting VH + HvnMagnet same label without coordination.
