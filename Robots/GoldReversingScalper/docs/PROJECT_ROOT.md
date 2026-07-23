# Gold Momentum Straddle Scalper (GRS-01 v2.0) — Project Root

| Field | Value |
| --- | --- |
| **Name** | GoldReversingScalper (GRS-01 v2.0) |
| **Platform** | cTrader Automate (cBot, C# / .NET 6) |
| **Active version** | **v2.0** |
| **Primary symbol** | XAUUSD |
| **Signal TF** | Tick / M1 |
| **Session** | London / New York (parameterized) |
| **Label** | `GRS-01` |
| **Status** | Momentum Floating Straddle Model |

## Strategy Overview
cBot Momentum Floating Straddle bám theo giá Anchor $(Ask+Bid)/2$:
- Khi sideway/chạy chậm trong 200ms: Dời cả 2 lệnh `BUY STOP` & `SELL STOP` bám theo Anchor để tránh dính lệnh nhầm.
- Khi nổ Momentum: Giá lao nhanh khớp 1 lệnh Stop $\rightarrow$ Hủy lệnh còn lại $\rightarrow$ Vị thế chạy với SL/TP & Trailing SL.

## Version history
| Version | Date | Notes |
| --- | --- | --- |
| v1.0 | 2026-07 | Reversing Pending Order (SAR) |
| v2.0 | 2026-07 | Upgrade to Momentum Floating Straddle Engine with SL/TP & Trailing |
