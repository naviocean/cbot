# ZigZagPocPullback — Project Root

| Field | Value |
| --- | --- |
| **Name** | ZigZagPocPullback |
| **Slug** | zzpoc |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active version** | **v1.0** (requirements locked) |
| **Primary symbol** | XAUUSD |
| **Signal TF** | Chart TF (M5 preferred) |
| **Label** | `ZigZagPocPullback` |
| **Status** | **v1.0 code** — build green; BT pending |

## One-line strategy

**Confirmed ZigZag pullback (Model A)** → zone = **rolling POC** *or* **Fib 38.2–61.8** (mode switch) → **market when price in zone** → SL beyond z2 ± ATR ratio → TP fixed RR.

## Edge hypothesis

After a confirmed swing pullback, price often revisits value (POC) or mid-leg Fib before continuation or range mean-reversion — trade quality setups only (one shot per z1, session filter, hard SL/TP).

## Repo map

| Path | Role |
| --- | --- |
| `ZigZagPocPullback/ZigZagPocPullback.cs` | Robot: arm OnBar, fill OnTick, risk/session |
| `ZigZagPocPullback/SignalEngine.cs` | ZZ + structure + POC/Fib zone |
| `Common/*` | Risk, Session, VolumeProfile, PriceUtils |
| `Robots/ZigZagPocPullback.algo` | Release build output |

## Documentation index

| Path | Role |
| --- | --- |
| `docs/v1.0/1-prds/PRD-zz-poc-pullback.md` | Spec (locked) |
| `docs/v1.0/3-plans/PLAN-implement-zz-poc-pullback.md` | Implement plan |

## Locked decisions (v1.0)

1. Model A pullback; no HTF bias.  
2. ZoneMode POC \| Fib exclusive; default POC rolling 3d @ H1.  
3. Market-in-zone (not pending limit).  
4. Max 5 same-direction; 1 entry per z1.  
5. SL = z2 ± SlAtrRatio×ATR; TP = fixed RR 2.0.  
6. Risk % 0.5 or fixed 0.01.  
7. Personal account; XAUUSD; cTrader only.
