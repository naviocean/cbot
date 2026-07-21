# PocAbsorption (PADR)

cBot cTrader: **POC Absorption & Delta Rejection** trên Session Volume Profile (XAUUSD). Tự động phát hiện điểm hấp thụ thanh khoản (Absorption) tại Session POC, kết hợp phân kỳ Cumulative Delta (Tick-based) và xác nhận qua nến đóng M1/M5. 

## Quick Start

| Parameter | Value |
| --- | --- |
| Symbol | XAUUSD (Gold) |
| Signal TF | M15 (Profile) / M1 or M5 (Entry Confirmation) |
| Session | London + NY (Asia disabled by default) |
| Target PF | >= 2.0 |
| Minimum Balance | ≥ $1,000 (Recommended for 0.25% - 0.5% risk per trade) |
| Common Modules | Linked via `../../../Common/*.cs` |

## Core Logic

1. **Session Volume Profile:** Khởi tạo và cập nhật Profile theo phiên (London 14:00 VN, NY 19:00 VN).
2. **Absorption & Delta Filter:** 
   - Kiểm tra Volume tại nốt POC >= 2.0x Volume trung bình của 5 nốt lân cận.
   - Phân tích Cumulative Delta (Bid/Ask tick test): Delta đi ngược chiều với giá tiệm cận POC.
3. **Trigger Entry:** Khi đủ điều kiện "Arm", chờ đúng 1 nến M1 hoặc M5 đóng cửa thể hiện sự từ chối giá (Rejection Candle).
4. **Structural SL:** Đặt phía ngoài mép Volume Node POC + 5–7 pips buffer (XAUUSD).
5. **Take Profit:** Mục tiêu đối diện Value Area (VAH/VAL) hoặc tối thiểu R:R 1:2.0.
6. **Risk & Equity Gates:** Quản lý % risk theo `RiskManager`, giới hạn số lệnh/ngày, né tin tức qua `NewsFilter`.

## Parameter Summary

| Group | Parameter | Default | Meaning |
| --- | --- | --- | --- |
| Session | `Trade London` | true | Bật giao dịch phiên London |
| Session | `Trade New York` | true | Bật giao dịch phiên Mỹ |
| Profile | `Volume Spike Multiplier` | 2.0 | Tỉ lệ volume POC so với lân cận |
| Entry | `Micro Confirmation TF` | M5 | Khung nến đóng để Trigger Entry |
| Risk | `Risk Percent` | 0.5% | Phần trăm rủi ro trên mỗi lệnh |
| Risk | `Max Trades Per Day` | 3 | Giới hạn số lệnh tối đa 1 ngày |
| Protection | `Node SL Buffer (Pips)` | 6.0 | Khoảng cách đệm ngoài mép Node |
| Protection | `Min RR Ratio` | 2.0 | R:R tối thiểu để cho phép vào lệnh |
| News | `Enable News Filter` | true | Né tin tức cao điểm (±15 phút) |

## Documentation

- [PROJECT_ROOT.md](./PROJECT_ROOT.md)
- [PRD](./v1.0/1-prds/PRD-padr.md)
- [Architecture](./v1.0/2-architecture/ARCH-poc-absorption.md)
- [Implementation Plan](./v1.0/3-plans/PLAN-implement-poc-absorption.md)
