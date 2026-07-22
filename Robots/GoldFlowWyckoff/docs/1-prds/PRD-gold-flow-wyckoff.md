# PRD: Gold Flow Wyckoff Confluence v1.0 cBot

## 1. Identity

| Field | Description |
| :--- | :--- |
| **Name** | GoldFlowWyckoff |
| **Platform** | cTrader (C# .NET) |
| **Symbols** | XAUUSD |
| **Timeframes** | Execution: M5 / M15 \| Trend Bias: H1 / H4 |
| **Strategy Style** | Swing Bias + Intraday Scalping Confluence |
| **Account Type** | Personal & Funded / Prop Firm Compatible |
| **Common Modules** | `RedWave.Common` (`CVolumeProfileV2`, `CTickDeltaEngine`, `CRiskManager`, `CTrailingManager`, `CSessionFilter`, `CNewsFilter`, `CLogger`, `PriceUtils`) |

---

## 2. Market Regime & Edge

- **Edge:** Kết hợp 3 lớp Confluence:
  1. **Cấu trúc Wyckoff (Higher Low / Spring / Lower High / Upthrust)** từ Weis Wave.
  2. **Vùng thanh khoản giá trị (Volume Profile V2)**: Daily POC, VAH, VAL, HVN/LVN.
  3. **Lực hấp thụ & đẩy giá thực thời (Order Flow Tick Delta)** từ `CTickDeltaEngine`.
- **Trade khi:** Thị trường có xu hướng rõ ràng hoặc quay về kiểm tra lại biên Value Area (VAL/VAH) trong các phiên có thanh khoản cao.
- **Không trade khi:**
  - Nằm trong vùng biến động quá hẹp (Sideway hẹp quanh POC không có volume wave).
  - Trước/Sau tin tức kinh tế mạnh (High-Impact News Blackout).
  - Spread giãn quá mức quy định (`MaxSpreadPips`).

---

## 3. Entry System (3/3 Confluence Rules)

### A. Long Entry (Mua)

| Rule ID | Điều kiện | Chi tiết Định lượng |
| :--- | :--- | :--- |
| **E-L1** | **Wyckoff Higher Low / Spring** | Weis Wave trên H1/M15 tạo Higher Low **HOẶC** Giá quét qua Support (VAL/HVN) tạo Spring (Low đâm qua Support nhưng nến M5 đóng cửa quay lại nằm TRÊN Support). |
| **E-L2** | **Volume Profile Support** | Giá nằm quanh vùng VAL hoặc HVN của Daily Volume Profile V2 (trong khoảng `TouchBufferAtrMult`). |
| **E-L3** | **Order Flow Delta Confluence** | `CTickDeltaEngine` ghi nhận Buy Imbalance Ratio > `MinDeltaImbalance` (vd: > 1.25) hoặc Positive Delta Spike trong `DeltaWindowMs`. |

### B. Short Entry (Bán)

| Rule ID | Điều kiện | Chi tiết Định lượng |
| :--- | :--- | :--- |
| **E-S1** | **Wyckoff Lower High / Upthrust** | Weis Wave tạo Lower High **HOẶC** Price quét qua Resistance (VAH/HVN) tạo Upthrust (High đâm qua Resistance nhưng nến M5 đóng cửa DƯỚI Resistance). |
| **E-S2** | **Volume Profile Resistance** | Giá nằm quanh vùng VAH hoặc HVN của Daily Volume Profile V2 (trong khoảng `TouchBufferAtrMult`). |
| **E-S3** | **Order Flow Delta Confluence** | `CTickDeltaEngine` ghi nhận Sell Imbalance Ratio > `MinDeltaImbalance` (vd: > 1.25) hoặc Negative Delta Spike trong `DeltaWindowMs`. |

---

## 4. Exit & Trade Management

| Quản lý | Quy tắc | Chi tiết |
| :--- | :--- | :--- |
| **Stop Loss (SL)** | Structural + ATR Floor | Dưới Low gần nhất / LVN + Buffer (`SlAtrMult`). Không chặt hơn `MinSlAtrMult` × ATR. |
| **Take Profit (TP)** | Risk-Reward / Magnet | TP mặc định = `RrMultiple` × SL (Mặc định 1:2 cho Scalp, 1:3 cho Swing) hoặc Target tới vùng Magnet tiếp theo (POC/VAH/HVN). |
| **Partial Close** | 50% @ 1:2 RR | Đóng 50% khối lượng khi lợi nhuận chưa thực hiện đạt 2.0R (thông qua `CTrailingManager`). |
| **Move Breakeven** | BE @ 1:1 RR | Dời SL về Entry + Spread Offset khi lợi nhuận đạt 1.0R. |
| **Trailing Stop** | VWAP / Step Trailing | Tùy chọn kích hoạt Trailing theo VWAP hoặc Step Trailing khi profit > `TrailStartR`. |

---

## 5. Risk & Capital Protection (`CRiskManager`)

- **Risk / Trade:** 0.5% - 1.0% Equity.
- **Max Trades / Day:** Mặc định 2 lệnh/ngày.
- **Max Daily Loss ($ / %):** Khóa giao dịch khi thua quá % quy định trong ngày (mặc định 2.5%).
- **Max Equity Drawdown %:** Khóa giao dịch khi tổng DD đạt ngưỡng dừng (mặc định 8.0%).
- **Spread Filter:** Hủy bỏ vào lệnh nếu Spread > `MaxSpreadPips` (mặc định 8.0 pips).

---

## 6. Filters & Utilities

- **Session Filter (`CSessionFilter`):** Chỉ cho phép mở lệnh trong phiên London (07:00-16:00 UTC) và New York (13:30-22:00 UTC).
- **News Blackout (`CNewsFilter`):** Tự động dừng giao dịch trước/sau tin High Impact 30 phút.
- **Logger (`CLogger`):** Ghi log chi tiết tín hiệu Wyckoff, Delta Ratio, và trạng thái quản lý vị thế.

---

## 7. Definition of Done (DoD)

- [ ] Viết thành công `WyckoffWaveEngine.cs` xử lý sóng Weis & pivots.
- [ ] Tích hợp `CVolumeProfileV2`, `CTickDeltaEngine`, `CRiskManager`, `CTrailingManager`.
- [ ] Xây dựng cBot `GoldFlowWyckoff.cs` hoàn chỉnh với đầy đủ thuộc tính `[Parameter]`.
- [ ] Compile thành công không có lỗi hoặc warning nặng.
- [ ] Kiểm tra Backtest trên dữ liệu XAUUSD cTrader.
