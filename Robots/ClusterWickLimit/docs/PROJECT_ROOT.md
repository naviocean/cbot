# ClusterWickLimit — Project Root

| Field | Value |
| --- | --- |
| **Name** | ClusterWickLimit |
| **Slug** | cwl |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active version** | **v1.1** (structure scale + ATR floors) |
| **Primary symbol** | XAUUSD |
| **Signal TF** | **M5 preferred**; M1 OK if ATR scaling on |
| **Label** | `ClusterWickLimit` |
| **Status** | Implemented v1.1 — **tick baseline only** (no micro optimize) |

## One-line strategy

**Structure-scale** liquidity cluster + wick rejection (closed bar) → limit retest outside level → **1 TP by RR**; BE start in R; BE lock + trail dist in **strategy pips**. Geometry floors = max(pips, ATR×mult).

## Edge hypothesis

Equal high/low cluster raid + strong reject; retest limit. **v1.1** sizes R/TP to XAU amplitude (~2–6$+ SL band), not micro $1 scalping (v1.0 failed on tick).

## Repo map

| Path | Role |
| --- | --- |
| `ClusterWickLimit/ClusterWickLimit.cs` | Robot + ATR effective geometry |
| `ClusterWickLimit/SignalEngine.cs` | Pure cluster + wick + arm |
| `Common/*` | Risk, Session, News, Trail, … |
| `ClusterWickLimit.algo` | Build output |

## Documentation index

| Path | Role |
| --- | --- |
| `docs/v1.0/1-prds/PRD-cluster-wick-limit.md` | Spec (+ v1.1 scale note) |
| `docs/v1.0/3-plans/PLAN-implement-cwl.md` | Implement plan |
| `docs/v1.0/5-reports/` | Review / BT notes |

## Locked decisions

1. Closed bar only — no forming-bar arm.  
2. Single TP (`TpRR`); no partial / TP2.  
3. BE start + trail start in **R**; BE lock + trail dist in **strategy pips** (100 pips = 1.0).  
4. SL structure-first; skip if R outside **effective** [SlMin, SlMax] (pips + ATR).  
5. Independent project.  
6. **v1.1:** not micro; validate on **tick data**, not M1 opening prices.
