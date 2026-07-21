# PRD — POC Absorption & Delta Rejection (PADR v1.0)

| Field | Detail |
| --- | --- |
| **Document ID** | PRD-PADR-V1.0 |
| **Robot Name** | PocAbsorption |
| **Author** | Algo-Strategist / cBot Expert |
| **Status** | Approved Specification |
| **Target Platform** | cTrader Automate (C# / .NET 6) |
| **Target Instrument** | XAUUSD (Gold) |
| **Primary Goal** | Profit Factor (PF) >= 2.0 via Structural Edge & Flow Divergence |

---

## 1. Context & Executive Summary

Hiện nay, đa số các chiến lược Volume Profile thương mại trên mạng chỉ giao dịch nảy (bounce) đơn thuần tại VAH, VAL hoặc POC. Các thuật toán của Market Maker (MM) thường xuyên thực hiện quét Stop Hunt qua các vùng này trước khi đẩy giá thật.

**PADR (POC Absorption & Delta Rejection)** được thiết kế để giải quyết vấn đề này bằng cách kết hợp 3 yếu tố vi mô:
1. **Volume Profile Node Concentration:** Phát hiện vùng tích lũy thanh khoản đặc (Volume Spike tại POC >= 2.0x mức trung bình).
2. **Order Flow Absorption (Cumulative Delta Divergence):** Nhận biết lực hấp thụ lệnh thụ động (Passive Limit Orders) thông qua thuật toán Lee-Ready Tick Test trên cTrader.
3. **Micro-Bar Trigger Confirmation:** Loại bỏ tín hiệu giả (Fake Delta) bằng cách chờ nến M1/M5 đóng cửa thể hiện sự từ chối giá (Rejection) trước khi vào lệnh.

---

## 2. Quantitative & Trading Requirements

### 2.1 Asset & Session Constraints
* **Symbol:** XAUUSD.
* **Sessions:** London (14:00 - 21:00 VN) & New York (19:00 - 03:00 VN). 
* **Asia Session:** Mặc định TẮT (biến động thấp, mỏng thanh khoản).
* **Timeframes:**
  - **M15:** Tính toán Session Volume Profile & xác định POC/VA.
  - **M1 / M5:** Khung thời gian quét Trigger (Micro Bar Close).

### 2.2 Entry Rules (Thuật toán kích hoạt lệnh)

#### Buy Setup (Long Absorption at POC)
1. **Vị trí:** Giá tiệm cận vùng POC của Session hiện tại trong khoảng ± 5 pips ($0.50).
2. **Volume Spike:** Volume tích lũy tại POC Level >= `VolumeSpikeMultiplier` (mặc định `2.0x`) so with average of 5 neighboring levels.
3. **Delta Divergence:** Giá đang giảm về POC nhưng Cumulative Delta dồn trong 3-5 nến M1 gần nhất **tăng dương** (cho thấy lượng Sell Market bị nuốt chửng bởi Buy Limit).
4. **Rejection Trigger:** Nến M1 hoặc M5 đóng cửa quay trở lại **trên** mức POC (Rút chân tăng điểm).
5. **Action:** Mở lệnh BUY Market ngay tại giá Mở cửa nến kế tiếp.

#### Sell Setup (Short Absorption at POC)
1. **Vị trí:** Giá tiệm cận vùng POC của Session trong khoảng ± 5 pips.
2. **Volume Spike:** Volume tích lũy tại POC Level >= `VolumeSpikeMultiplier` (2.0x).
3. **Delta Divergence:** Giá đang tăng lên POC nhưng Cumulative Delta dồn **giảm âm** (Buy Market bị nuốt bởi Sell Limit).
4. **Rejection Trigger:** Nến M1 hoặc M5 đóng cửa quay xuống **dưới** mức POC (Rút chân giảm điểm).
5. **Action:** Mở lệnh SELL Market ngay tại giá Mở cửa nến kế tiếp.

### 2.3 Exit & Risk Rules (Quản lý Cắt lỗ / Chốt lời)

#### Stop Loss (SL) — Structural Node SL
* KHÔNG dùng SL cố định 5-7 pips.
* **Buy SL:** `Mép dưới của Node Volume Spike (Node Bottom Edge) - Buffer (mặc định 6 pips)`.
* **Sell SL:** `Mép trên của Node Volume Spike (Node Top Edge) + Buffer (mặc định 6 pips)`.
* **Minimum SL Guard:** Đảm bảo SL không nhỏ hơn `1.0x ATR(M15)` để tránh bị nhiễu spread.

#### Take Profit (TP)
* **Chế độ mặc định:** Target phía bên kia của Value Area (Lên VAH nếu Buy, xuống VAL nếu Sell).
* **R:R Gate:** Nếu khoảng cách từ Entry đến VAH/VAL cho tỉ lệ R:R < `MinRR` (mặc định `2.0`), **BỎ QUA KHÔNG VÀO LỆNH**.
* **Fixed R:R Backup:** Nếu giá vượt VAH/VAL, mục tiêu cố định tính theo `RR Multiple * RiskDistance` (default 2.5R).

---

## 3. Integration with Existing Common Modules

Hệ thống tận dụng tối đa 100% các Common Modules có sẵn trong codebase:

1. **`RedWave.Common.VolumeProfile` & `ProfileData`:**
   - Xây dựng Session Profile, phân chia Price Bins (Step 0.5 USD / 10 pips).
   - Xác định POC, VAH, VAL và tính toán mép trên/dưới của POC Node.
2. **`RedWave.Common.TickDeltaEngine`:**
   - Theo dõi từng Tick (`Ask`/`Bid` test) để tính Cumulative Delta dồn theo nến & theo đợt tiệm cận POC.
3. **`RedWave.Common.NewsFilter`:**
   - Tải lịch tin tức high-impact (UTC).
   - Ngưng mở lệnh mới 15 phút trước và 15 phút sau tin đỏ.
4. **`RedWave.Common.RiskManager`:**
   - Quản lý Lot size tự động theo `% Equity Risk`.
   - Giới hạn số lệnh tối đa trong ngày (`MaxTradesPerDay` = 3).
   - Kiểm soát Drawdown ngày & Max Equity Drawdown.
5. **`RedWave.Common.SessionFilter`:**
   - Lọc khung giờ phiên London & New York.
6. **`RedWave.Common.TrailingManager`:**
   - Hỗ trợ dời Break-Even (BE) khi lệnh đạt 1.0R và Trailing Stop theo R.

---

## 4. Input Parameters Specification

```csharp
// Session Settings
[Parameter("Trade London", Group = "Session", DefaultValue = true)]
public bool TradeLondon { get; set; }

[Parameter("Trade New York", Group = "Session", DefaultValue = true)]
public bool TradeNewYork { get; set; }

// Strategy Core Parameters
[Parameter("Volume Spike Multiplier", Group = "Strategy", DefaultValue = 2.0, MinValue = 1.2)]
public double VolumeSpikeMultiplier { get; set; }

[Parameter("POC Proximity Pips", Group = "Strategy", DefaultValue = 5.0)]
public double PocProximityPips { get; set; }

[Parameter("Confirmation TF", Group = "Strategy", DefaultValue = TimeFrame.Minute5)]
public TimeFrame ConfirmationTimeFrame { get; set; }

// Protection & R:R
[Parameter("Node SL Buffer (Pips)", Group = "Protection", DefaultValue = 6.0)]
public double NodeSlBufferPips { get; set; }

[Parameter("Min R:R Ratio", Group = "Protection", DefaultValue = 2.0)]
public double MinRrRatio { get; set; }

// Risk Management
[Parameter("Risk Percent", Group = "Risk", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 5.0)]
public double RiskPercent { get; set; }

[Parameter("Max Trades Per Day", Group = "Risk", DefaultValue = 3)]
public int MaxTradesPerDay { get; set; }

// News Filter
[Parameter("Enable News Filter", Group = "News", DefaultValue = true)]
public bool EnableNewsFilter { get; set; }

[Parameter("News Blackout Minutes", Group = "News", DefaultValue = 15)]
public int NewsBlackoutMinutes { get; set; }
```

---

## 5. Success Criteria & Verification

* **Compile:** Không lỗi/warning C# .NET 6 cTrader.
* **Backtest Verification:** Chạy backtest trên cTrader với dữ liệu **"Tick Data from Server"** trong 12 tháng gần nhất trên XAUUSD.
* **KPI Matrix Target:**
  - **Profit Factor (PF):** ≥ 2.0
  - **Max Equity Drawdown:** < 12%
  - **Win Rate:** ≥ 42%
  - **Average Win / Average Loss:** ≥ 2.5
