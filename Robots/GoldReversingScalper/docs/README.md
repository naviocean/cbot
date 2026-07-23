# Gold Momentum Straddle Scalper (GRS-01 v2.0)

cBot Scalping bám đuổi Momentum Bứt Phá Vàng (XAUUSD) trên cTrader Automate C# (.NET 6).

## Quick Start
- **Cặp giao dịch:** XAUUSD (Gold)
- **Khung thời gian:** Tick / M1
- **Nền tảng:** cTrader Automate C#

## Momentum Engine Modes (`MomentumMode`)
1. **`SimpleDelta`**: Đo chênh lệch giá đơn thuần trong cửa sổ 200ms ($\Delta P \ge 50$ pips).
2. **`TickFrequency` (Option 1)**: Đo chênh lệch giá $\ge 50$ pips KÈM THEO tần số tick dồn dập trong 200ms ($\ge N$ ticks).
3. **`OrderFlowDelta` (Option 2)**: Đo chênh lệch giá $\ge 50$ pips KÈM THEO lực Mua/Bán áp đảo từ `CTickDeltaEngine` (Imbalance ratio $\ge 1.5$).
4. **`Combined`**: Kết hợp cả 3 điều kiện: Price Delta + Tick Frequency + Order Flow Imbalance.

## Input Parameters (XAUUSD Scale: 100 Pips = $1.00 Gold Price)

| Parameter | Group | Description | Default |
| --- | --- | --- | --- |
| `Mode` | Momentum Straddle | Chế độ đo Momentum (`SimpleDelta`, `TickFrequency`, `OrderFlowDelta`, `Combined`) | `Combined` |
| `DistancePips` | Momentum Straddle | Khoảng cách từ Anchor (100 pips = $1.00) | `100.0` |
| `MomentumWindowMs` | Momentum Straddle | Cửa sổ đo tốc độ giá (ms) | `200` |
| `MomentumMinMovePips` | Momentum Straddle | Mức dịch chuyển giá để dời pending (50 pips = $0.50) | `50.0` |
| `MinTicksInWindow` | Momentum Straddle | Số lượng tick tối thiểu trong 200ms (Option 1) | `8` |
| `MinDeltaImbalance` | Momentum Straddle | Tỷ lệ lực Order Flow Imbalance Buy/Sell (Option 2) | `1.5` |
| `WaitNewBarForReEntry`| Momentum Straddle | Chờ mở nến mới rồi mới đặt lại bộ đôi pending straddle | `true` |
| `StopLossPips` | Position Management | Stop Loss cố định (200 pips = $2.00) | `200.0` |
| `TakeProfitPips` | Position Management | Take Profit cố định (400 pips = $4.00) | `400.0` |
| `UseTrailing` | Position Management | Bật/tắt Trailing Stop cho Position | `true` |
| `TrailStartPips` | Position Management | Mức lời bắt đầu trail (100 pips = $1.00) | `100.0` |
| `TrailStepPips` | Position Management | Bước dời trailing (20 pips = $0.20) | `20.0` |
