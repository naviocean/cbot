# PRD — Fib786Pullback v1.0

## 1. Identity

| Field | Value |
| --- | --- |
| Name | Fib786Pullback |
| Platform | cTrader cBot (C# / .NET 6) |
| Symbols | XAUUSD (primary) |
| Timeframe | M5 preferred |
| Account | personal (prop-ready filters) |
| Common lib | `Sources/Common` (Risk, Session, News, Trail) |

## 2. Regime

| Trade when | Stand down when |
| --- | --- |
| London / NY session (UTC) | Asia only (default off) |
| Impulse ≥ MinImpulse × ATR | Chop / tiny swings |
| Spread ≤ MaxSpread | Equity DD / daily loss trip |
| Cooldown elapsed after last trade | Busy position / max trades day |

## 3. Entry (testable)

Bars: **closed only**. Pivot strength `N` (default 5).

### Swing (no look-ahead)

- Pivot high at bar *i* confirmed when *N* bars left and *N* bars right all have lower highs.  
- Newest confirmable center = *N* closed bars ago.  
- Same for pivot lows (higher lows on sides).

### Structure

| Side | Structure event | Impulse leg |
| --- | --- | --- |
| Long | Confirmed pivot high **HH** (price > previous confirmed pivot high) | Prior swing low → new high |
| Short | Confirmed pivot low **LL** | Prior swing high → new low |

### Fib

- Long: `range = high − low`; level `L786 = high − 0.786 × range`  
- Short: `range = high − low`; level `L786 = low + 0.786 × range`  
- Zone half-width: `max(ZonePips floor, ATR × ZoneAtrMult)`  

### Confirm (signal bar = last closed)

**Long E1–E4 all true:**

1. Active long leg valid (not invalidated).  
2. Impulse size ≥ `MinImpulseAtr × ATR`.  
3. Bar low ≤ `L786 + zone` and bar high ≥ `L786 − zone` (touched zone).  
4. Close > Open **and** Close ≥ `L786` (reclaim).  

**Short:** mirror (Close < Open and Close ≤ L786; high/low touch zone).

### Entry type

Market at next tick after confirm (cTrader: `ExecuteMarketOrder` on bar signal).

### One shot per leg

After trade or reject-for-traded, same `(legHigh, legLow)` cannot re-enter until a **new** extreme forms.

### HTF bias (v1.2)

| Mode | Behavior |
| --- | --- |
| **Off** | No HTF gate |
| **Align** (v1.1) | Long only HTF up, short only HTF down; **flat = reject**. Often hurts deep-pullback longs |
| **BlockCounter** (default) | Reject only **clear opposite** (long vs HTF down / short vs HTF up); **flat OK** |

| Field | Default |
| --- | --- |
| HTF | H1 |
| Lookback N | 12 |
| Min move | 150 strategy pips (1.50 XAU) |
| Filter Long / Short | both true (can disable long filter if still weak) |

Reject codes: `F_HTF_FLAT` (Align only), `F_HTF_ALIGN`, `F_HTF_COUNTER`.

## 4. Exit

| Rule | Definition |
| --- | --- |
| SL | Beyond impulse origin ± `SL buffer` (pips floor + optional ATR); clamp to [SlMin, SlMax] effective |
| TP | `entry ± TpRR × R` (default 1.5R) |
| BE | Optional; start at BeAtRR; lock strategy pips |
| Trail | Optional; start TrailStartRR; dist strategy pips |
| Partial | **No** (v1) |

Invalidation of setup (no entry):

- Long: close &lt; leg low − buffer  
- Short: close &gt; leg high + buffer  
- Or leg age bars &gt; MaxLegAgeBars  

## 5. Risk

| Field | Default |
| --- | --- |
| Risk % | 0.5 |
| Max positions | 1 (bot label) |
| Max trades / day | 3 |
| Max equity DD % | 10 (block entries) |
| Max daily loss $ | 0 = off |
| Cooldown bars | 10 after position open |

## 6. Filters

- Session: London + NY on; Asia/Overlap off by default  
- Spread max (strategy pips)  
- News filter optional (schedule string)  
- Min impulse ATR  
- Cooldown + max trades/day  
- **HTF Mode (v1.2 default BlockCounter)**

## 7. Non-functional

- Label: `Fib786Pullback`  
- Log reject codes: `REJECT:F_*` / pass `E_LONG` / `E_SHORT`  
- Strategy pip: **100 pips = 1.0** price on XAU  
- Backtest: tick preferred for fill quality  

## 8. Definition of done

- [x] Rules unambiguous  
- [x] SL/TP/R defined  
- [x] Risk gates wired  
- [ ] Tick baseline BT on XAUUSD M5 (user)  
- [ ] PF/DD acceptance after first BT  

## Defaults summary

| Param | Default |
| --- | --- |
| PivotStrength | 5 |
| LookbackBars | 120 |
| MinImpulseAtr | 1.5 |
| FibLevel | 0.786 |
| ZonePips | 40 |
| ZoneAtrMult | 0.08 |
| SlBufferPips | 40 |
| SlMinPips | 200 |
| SlMaxPips | 800 |
| TpRR | 1.5 |
| CooldownBars | 10 |
| MaxLegAgeBars | 80 |
