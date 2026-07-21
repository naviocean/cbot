# Research notes — PmLh M5 iterate (2026-07-12)

Informal journal from cTrader logs (XAUUSD M5). Not a full Strategy Tester report.

## Iterations

| Pass | Observation | Change |
| --- | --- | --- |
| 1 | Mass `E_POC_NOISE` with \|M\| ≫ M_min | Streak counted POC plateaus as failure |
| 2 | Signals fire; knife long under bull POC | Added `E_PRICE_POC`, `E_LVN_SIDE` |
| 3 | Knife blocked; still mixed PnL day | Continue multi-day BT; watch chase longs |

## Code defaults after v1.0.1

- Use Streak Filter: **false**
- Min POC Move Bins: **1**
- Strong M Bypass: **1.0**
- Max Price-POC ×ATR: **1.5**
- Require LVN Side: **true**
- Exit: SL + RR×R + optional BE/trail only

## Open issues (not blocking more BT)

1. **POC lag / chase** — long after large impulse while M still small (price far *above* POC not blocked).  
2. **BE early** — many small BE wins vs full RR.  
3. **Tester Lot Size Mode** — confirm RiskPercent vs FixedLots on instance.  
4. **Single large loser** — verify SL path / R realized vs planned risk $.

## Next

- Multi-day BT same params; optional formal `REPORT-bt-*.md`.  
- Ablation later (LvnSource, Entry Mode, BE on/off).
