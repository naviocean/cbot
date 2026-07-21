# Fib786Pullback — Project Root

| Field | Value |
| --- | --- |
| **Name** | Fib786Pullback |
| **Slug** | f786 |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active version** | **v1.2** (HTF BlockCounter) |
| **Primary symbol** | XAUUSD |
| **Signal TF** | **M5 preferred** (M15 OK) |
| **Label** | `Fib786Pullback` |
| **Status** | v1.2 — hard Align HTF hurt long PF; default BlockCounter |

## One-line strategy

**New HH/LL via N-bar swing pivots** → Fib from new extreme → prior opposite swing → **entry at 78.6% deep pullback + closed-bar candle confirm** → single TP by RR; min impulse + session + cooldown + **HTF bias**.

## Edge hypothesis

After a real structure break (new HH/LL) of sufficient impulse size, a deep retrace to ~78.6% often is the last retest before continuation — trade only with candle reclaim and filters (session, cooldown, max trades/day).

## Repo map

| Path | Role |
| --- | --- |
| `Fib786Pullback/Fib786Pullback.cs` | Robot: risk, session, market entry, BE/trail |
| `Fib786Pullback/SignalEngine.cs` | Pure swing + Fib + confirm |
| `Common/*` | Risk, Session, News, Trail, PriceUtils |
| `Fib786Pullback.algo` | Build output (repo root `Robots/`) |

## Documentation index

| Path | Role |
| --- | --- |
| `docs/v1.0/1-prds/PRD-fib786-pullback.md` | Spec |
| `docs/v1.0/3-plans/PLAN-implement-fib786.md` | Implement plan |

## Locked decisions (v1.0 → v1.1)

1. **No Zigzag library** — N-bar fractal pivots only (confirmed, no look-ahead).  
2. **Closed bar only** — signal on last closed bar.  
3. **Market entry** after confirm (not limit-at-78.6 blind).  
4. **Single TP** (`TpRR`); optional BE + trail.  
5. **One position** max; no grid/pyramid.  
6. **XAU strategy pips:** 100 pips = 1.0 price (1 pip = 0.01).  
7. **v1.1 Align HTF** (hard with-trend) — empirically hurt long PF on 78.6 pullback.  
8. **v1.2 HTF Mode:** `Off` | `Align` | **`BlockCounter` (default)** — only block clear opposite bias; flat allowed. Lookback 12, min move 150 pips. Per-side toggles `HTF Filter Long/Short`.
