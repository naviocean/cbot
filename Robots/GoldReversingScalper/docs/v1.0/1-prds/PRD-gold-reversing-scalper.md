# PRD: Gold Momentum Straddle Scalper (GRS-01 v2.0)

## 1. Document Overview
- **Document Type:** Product Requirements Document (PRD)
- **Target Bot:** GoldReversingScalper (GRS-01 v2.0)
- **Version:** v2.0
- **Author:** algo-strategist / cbot-expert
- **Platform:** cTrader Automate C# (.NET 6)

---

## 2. Strategy Specifications & Rules

### 2.1. Initial & Idle Momentum Tracking Rules
- Khi chưa có vị thế nào mở (trạng thái `Idle`):
  - **Anchor Price:** $Anchor = (Ask + Bid) / 2.0$.
  - Khởi tạo 2 lệnh Pending Order: `BUY STOP` tại `Anchor + DistancePips` và `SELL STOP` tại `Anchor - DistancePips`.
  - **Momentum Window Evaluation (ví dụ 200 ms):**
    - Theo dõi tốc độ di chuyển giá trong `MomentumWindowMs`.
    - Khi giá di chuyển chậm/sideway ($\Delta Price < MomentumMinMovePips$ trong $200 ms$): cBot liên tục **dời cả 2 lệnh Pending Stop bám theo Anchor hiện tại** để luôn duy trì khoảng cách `DistancePips` và **không bị dính lệnh nhầm trong sideway**.
    - Khi giá bứt phá cực nhanh ($\Delta Price \ge MomentumMinMovePips$ trong $200 ms$): Tốc độ biến động bão tố vượt qua tốc độ dời lệnh $\rightarrow$ Giá khớp 1 trong 2 lệnh Pending Stop.

### 2.2. Position & Risk Management Rules
- Khi 1 lệnh Pending Stop được kích hoạt (ví dụ Buy khớp):
  1. **Hủy ngay lập tức** lệnh Pending Stop còn lại (Sell Stop).
  2. Vị thế được cài đặt **Stop Loss (StopLossPips)** và **Take Profit (TakeProfitPips)** cố định.
  3. Quản lý Trailing Stop native cho vị thế qua `CTrailingManager` (`TrailStartPips` & `TrailStepPips`).

### 2.3. Position Exit & Reset Cycle
- Khi vị thế đóng (chạm TP, SL hoặc Trailing SL):
  - cBot tự động chuyển về trạng thái `Idle`.
  - Ngay lập tức mở lại 2 lệnh `BUY STOP` và `SELL STOP` mới xung quanh Anchor hiện tại và tiếp tục chu kỳ Momentum Tracking.

---

## 3. Parameter Specifications (XAUUSD Scale: 100 Pips = $1.00 Gold Price)

| Parameter | Group | Description | Default |
| --- | --- | --- | --- |
| `DistancePips` | Momentum Straddle | Khoảng cách đặt pending từ Anchor (100 pips = $1.00) | `100.0` |
| `MomentumWindowMs` | Momentum Straddle | Cửa sổ thời gian đo tốc độ giá (ms) | `200` |
| `MomentumMinMovePips` | Momentum Straddle | Biến động tối thiểu để dời pending (50 pips = $0.50) | `50.0` |
| `StopLossPips` | Position Management | Stop Loss cố định (200 pips = $2.00) | `200.0` |
| `TakeProfitPips` | Position Management | Take Profit cố định (400 pips = $4.00) | `400.0` |
| `UseTrailing` | Position Management | Bật/tắt Trailing Stop cho Position | `true` |
| `TrailStartPips` | Position Management | Mức lợi nhuận bắt đầu trail (100 pips = $1.00) | `100.0` |
| `TrailStepPips` | Position Management | Bước dời trailing (20 pips = $0.20) | `20.0` |
