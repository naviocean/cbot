# PRD — ZigZagPocPullback v1.0

**Date:** 2026-07-15  
**Author:** Dương Lê  
**Status:** Requirements locked (interview) — ready for PLAN / implement

## 1. Identity

| Field | Value |
| --- | --- |
| Name | ZigZagPocPullback |
| Platform | cTrader cBot (C# / .NET 6) |
| Symbols | **XAUUSD only** |
| Signal TF | **Chart timeframe** (bot bám chart; M5 preferred) |
| Account | **Personal** |
| Common lib | `Sources/Common` (Risk, Session, VolumeProfile / TickVolumeProfiler, PriceUtils) |
| Label | `ZigZagPocPullback` |

## 2. Edge / Regime

| Trade when | Stand down when |
| --- | --- |
| Confirmed ZigZag pullback (Model A) | z1 not fully confirmed (repaint risk) |
| Price enters zone (POC **or** Fib mode — one active) | Max same-direction positions reached |
| Session filter allows (if any session on) | Already traded this z1 (one-shot) |
| Mild trend / range pullback | Structure filter on and HH/HL not met |

**Model A (locked):** Pullback entry — **Buy** after confirmed **swing low (bottom)**; **Sell** after confirmed **swing high (peak)**.

## 3. Core components

### 3.1 ZigZag

- Custom or library ZigZag with **Depth / Deviation / Backstep** as inputs (optimize later).
- **z1 confirm (anti-repaint):** z1 is valid only when a **newer pivot** has formed after it (new leg started). No trade on unconfirmed z1.
- Indexing: use confirmed pivots only; signal evaluation on **closed bars** for structure; entry fill on tick when price in zone (see Entry).

### 3.2 Structure direction (preliminary)

When z1 is confirmed:

| Side | Preliminary |
| --- | --- |
| **Buy** | z1 is **bottom (low)** **and** z1 **&lt;** z3 (price of pivot) |
| **Sell** | z1 is **peak (high)** **and** z1 **&gt;** z3 |

### 3.3 Structure filter (optional)

| Field | Value |
| --- | --- |
| Input | `UseStructureFilter` on/off |
| Default | **Off** |
| When On (Buy) | At least 2 higher lows in recent confirmed pivots (last 4–5) |
| When On (Sell) | At least 2 lower highs in recent confirmed pivots |

### 3.4 HTF bias

| Field | Value |
| --- | --- |
| H1 EMA / structure bias | **Off (v1)** — trade both directions from chart ZZ only |

POC TF is **not** a bias filter; it only defines the zone when ZoneMode = POC.

## 4. Zone modes (mutually exclusive)

**Input `ZoneMode`:** `POC` | `Fib`  
**Default:** `POC`  
Exactly **one** mode active — no fallback between POC and Fib.

### 4.1 Mode POC

| Field | Value |
| --- | --- |
| Profile type | **Rolling lookback** (same family as PmLh) |
| POC TF | **Input** (default **H1**) |
| Lookback | **Input days** (default **3**) — build VP over lookback on POC TF bars |
| Zone | Price within `POC ± Buffer` |
| Buffer | `BufferAtrRatio × ATR(14)` on **signal (chart) TF** |

### 4.2 Mode Fib

| Field | Value |
| --- | --- |
| Leg | Confirmed z1 ↔ z2 (impulse into pullback) |
| Zone | Price inside **Fib 38.2% – 61.8%** of leg, expanded by buffer if needed |
| Buffer | Same `BufferAtrRatio × ATR(14)` (nới band) |

### 4.3 Buffer

| Field | Default | Notes |
| --- | --- | --- |
| `BufferAtrRatio` | **0.5** (assumption — tune in BT) | ATR-only buffer (no fixed-pips mode v1) |

## 5. Entry (testable)

| Rule | Definition |
| --- | --- |
| Type | **Market** when **current price is inside** zone |
| Not | Pending Limit placed at zone in advance |
| Timing | After setup valid (z1 confirmed + side + filters); on tick/bar check: bid/ask (or mid) in zone → `ExecuteMarketOrder` |
| One-shot | **Max 1 entry per confirmed z1** |
| Max positions | **Default 5 same direction**; Buy and Sell counted **separately**; input adjustable |
| Invalidation (no entry) | New opposite structure / z1 superseded without fill — setup drops; do not enter late after break of z2 if still implementing cancel rule (see Open) |

**Buy path (E1–E5 all true):**

1. z1 confirmed bottom; z1 &lt; z3.  
2. Structure filter pass or off.  
3. Session OK.  
4. This z1 not yet traded.  
5. Same-direction position count &lt; MaxPositions.  
6. Price in zone (POC or Fib per mode).  
7. → Market long.

**Sell:** mirror (z1 peak, z1 &gt; z3).

## 6. Exit

| Rule | Definition |
| --- | --- |
| **SL** | Buy: `Low(z2) − SlAtrRatio × ATR(14)`; Sell: `High(z2) + SlAtrRatio × ATR(14)` |
| `SlAtrRatio` | Input (default **1.0** assumption) |
| **TP** | Full close at **fixed RR**; `TpRR` default **2.0** |
| Partial | **No** (v1) |
| Trail / BE | **No** (v1) — phase 2 |

`R` = distance entry → SL (absolute price).  
TP = entry ± `TpRR × R`.

## 7. Risk

| Field | Default |
| --- | --- |
| Volume mode | **Risk %** or **Fixed lot** (user selects) |
| Risk % | **0.5** |
| Fixed lot | **0.01** |
| Max positions / direction | **5** (input) |
| Max daily loss / equity DD | Optional via Common RiskManager if wired (not blocking v1) |

## 8. Filters

### Session (like other RedWave cBots)

| Parameter | Default |
| --- | --- |
| Trade Asia | false |
| Trade London | true |
| Trade New York | true |
| Trade Overlap (Lon–NY) | false |

- Implementation: `CSessionFilter` (UTC fixed windows, OR logic).  
- If all disabled → treat as **no trade** or **full allow** — **decision at implement:** match Fib786/VacuumHunter behavior (document in PLAN).

### Other v1

| Filter | v1 |
| --- | --- |
| News | Optional / phase 2 (Common `NewsFilter` if easy) |
| Spread max | Recommended input; default TBD in PLAN |
| Volatility ATR cap | Phase 2 |

## 9. Parameters (optimize / inputs)

| Group | Parameters |
| --- | --- |
| ZigZag | Depth, Deviation, Backstep |
| Zone | ZoneMode, PocTimeframe, ProfileLookbackDays, BufferAtrRatio, Fib band fixed 38.2–61.8 |
| Risk | VolumeMode, RiskPercent, FixedLot, MaxPositionsPerSide |
| Exit | SlAtrRatio, TpRR |
| Structure | UseStructureFilter |
| Session | Asia / London / NY / Overlap booleans |

## 10. Non-functional

- Reuse `Common/VolumeProfile` or `TickVolumeProfiler` + pattern from **PmLh** rolling profile where possible.  
- Reject / pass logs: `REJECT:*`, `E_LONG`, `E_SHORT` style (consistent with sibling bots).  
- Strategy pip convention for XAU: align with existing bots (**100 pips = 1.0** price) if using pip-based caps later.  
- Backtest: tick preferred for market-in-zone fills.

## 11. Definition of done

- [x] Rules unambiguous for v1 (locked interview 2026-07-15)  
- [x] SL/TP/R and risk modes defined  
- [x] Zone modes mutually exclusive  
- [x] PLAN-implement written (`docs/v1.0/3-plans/PLAN-implement-zz-poc-pullback.md`)  
- [x] cBot + SignalEngine implemented (`dotnet build` Release green 2026-07-15)  
- [ ] Baseline BT XAUUSD (chart M5), ≥ 1y if data allows  
- [ ] Metrics review: WR, PF, Max DD  

## 12. Out of scope (v1)

- H1 EMA200 / HTF structure bias  
- POC + Fib simultaneous / fallback  
- Pending limit entry  
- Partial TP, BE, trailing  
- Multi-symbol  
- Anchored VP to ZZ leg  
- AI self-tuning  
- Martingale / grid  

## 13. Locked interview log

| # | Decision |
| --- | --- |
| Model | A — pullback (z1 bottom buy / peak sell) |
| Platform | cTrader |
| Symbol | XAUUSD only |
| Account | Personal |
| Risk | % default 0.5 **or** fixed lot default 0.01 |
| Zone | Mode switch POC \| Fib (default POC); not both |
| POC | Rolling lookback, TF input default H1, days default 3 |
| Fib zone | 38.2–61.8 band of z1–z2 |
| Buffer | ATR ratio only |
| Entry | Market when price in zone |
| Max pos | Default 5 **same direction** (sides separate) |
| Per z1 | 1 entry max |
| SL | z2 ± SlAtrRatio × ATR |
| TP | Fixed RR default 2.0 |
| HTF bias | Off |
| Session | Enable/disable Asia/London/NY/Overlap |
| Signal TF | Chart TF |
| z1 confirm | New pivot after z1 |
| Structure HH/HL | Input on/off, default off |

## Defaults summary (assumptions marked *)

| Param | Default |
| --- | --- |
| ZoneMode | POC |
| PocTimeframe | H1 |
| ProfileLookbackDays | 3 |
| BufferAtrRatio | 0.5* |
| SlAtrRatio | 1.0* |
| TpRR | 2.0 |
| RiskPercent | 0.5 |
| FixedLot | 0.01 |
| MaxPositionsPerSide | 5 |
| UseStructureFilter | false |
| Trade London / NY | true |
| Trade Asia / Overlap | false |
| ZigZag Depth/Dev/Backstep | TBD in PLAN (visual defaults)* |
