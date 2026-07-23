# Product Requirements Document (PRD): SMC / ICT Modular Engine (`RedWave.Common.Smc`)

| Attribute | Value |
| :--- | :--- |
| **Status** | Approved |
| **Author** | `@algo-strategist` |
| **Version** | v1.0 |
| **Target Platform** | cTrader (C#) & MT5 (MQL5) |
| **Target Package** | `RedWave.Common.Smc` |

---

## 1. Executive Summary & Goals

Mục tiêu của dự án là xây dựng bộ công cụ **Modular SMC/ICT Core Engine** nằm trong thư viện chung `RedWave.Common.Smc`. 
Hệ thống này đóng vai trò là "Bộ não định lượng" giải mã hành vi dòng tiền thông minh (Smart Money / IPDA), tự động hóa các mô hình phân tích kỹ thuật SMC & ICT bao gồm: Market Structure (BOS/ChoCH/MSS), Fair Value Gap (FVG), Liquidity Sweep, Order Block (OB), và Premium/Discount Dealing Range.

### Key Success Metrics
* **Tính Độc Lập (Modularity):** Cho phép cBot chạy từng engine riêng lẻ (ví dụ: chỉ chạy FVG Engine) hoặc kết hợp đầy đủ.
* **Hiệu Năng High-Speed Backtest:** Tắt visual hình vẽ tự động trong lúc backtest giúp tốc độ test đạt tối thiểu 1,000 tick/giây.
* **Minh Bạch (Visual Clarity):** Render chính xác 100% các vùng FVG, OB, đường MSS lên chart khi ở chế độ Live/Demo.

---

## 2. Detailed Feature Specifications

### 2.1. Feature 1: Market Structure & MSS Engine
* **MSS (Market Structure Shift / ChoCH):** Tự động phát hiện khi giá đâm thủng Swing High/Low quan trọng.
* **Displacement Requirement:** Cú phá vỡ bắt buộc phải có thân nến nổ lớn ($>\text{ATR}(14) \times 1.5$) và để lại FVG.
* **Body vs Wick Break:** Cho phép cấu hình xác nhận bằng giá đóng cửa (Thân nến) hoặc râu nến.

### 2.2. Feature 2: Fair Value Gap (FVG) Engine
* **Mô hình 3 nến:** Nhận diện khoảng trống giữa $High(N-2)$ và $Low(N)$ đối với Bullish FVG, ngược lại với Bearish FVG.
* **Vòng đời FVG:** Quản lý 4 trạng thái: `Active`, `PartiallyFilled` (chạm mốc 50% CE), `Mitigated` (lấp 100%), `Invalidated` (đâm thủng).
* **Min Pip Filter:** Loại bỏ các FVG quá nhỏ không đủ bù chi phí Spread/Slippage.

### 2.3. Feature 3: Liquidity & Sweep Engine
* **Liquidity Pools:** Tự động đánh dấu BSL, SSL, EQH, EQL, Asian High/Low, PDH/PDL.
* **Judas Swing Detection:** Phát hiện râu nến quét qua vùng thanh khoản rồi đóng cửa chui ngược vào trong range.

### 2.4. Feature 4: Order Block (OB) & Breaker Block Engine
* **High-Probability OB:** Chỉ công nhận nến đảo chiều là Order Block chuẩn nếu theo sau nó có **Displacement + FVG**.
* **Breaker Block Conversion:** Tự động chuyển OB bị đâm thủng thành Breaker Block (Vùng kháng cự/hỗ trợ mới).

### 2.5. Feature 5: Dealing Range & Premium/Discount
* Tính toán Fibonacci 0% - 50% - 100% dựa trên Swing Range.
* **Rule:** Chỉ phát tín hiệu BUY ở vùng **Discount** ($<50\%$) và SELL ở vùng **Premium** ($>50\%$).

### 2.6. Feature 6: Visual Renderer & Toggle System
* **Tầng 1 (Logic Toggles):** `EnableFvgLogic`, `EnableStructureLogic`, `EnableObLogic`, `EnableLiquidityLogic`.
* **Tầng 2 (Visual Toggles):** `ShowFvgVisuals`, `ShowStructureVisuals`, `ShowObVisuals`, `ShowLiquidityVisuals`, `AutoCleanVisuals`.

---

## 3. Parameter Specifications

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `EnableFvgLogic` | bool | `true` | Bật/tắt tính toán FVG |
| `EnableStructureLogic` | bool | `true` | Bật/tắt tính toán BOS/MSS |
| `EnableObLogic` | bool | `false` | Bật/tắt tính toán Order Block |
| `MinFvgPips` | double | `1.5` | Chiều rộng tối thiểu của FVG (pips) |
| `DisplacementAtrMult` | double | `1.5` | Hệ số ATR để xác nhận nến nổ |
| `ShowFvgVisuals` | bool | `true` | Vẽ hộp FVG trên Chart |
| `ShowStructureVisuals` | bool | `true` | Vẽ đường BOS/MSS trên Chart |
| `AutoCleanVisuals` | bool | `true` | Xóa hình vẽ khi FVG/OB bị lấp |

---

## 4. Edge Cases & Risks

1. **High Volatility Spikes (Tin tức đỏ):** Giá đâm quá nhanh tạo ra nhiều FVG chồng chéo. *Giải pháp:* Giới hạn số lượng FVG active tối đa trên bộ nhớ (`MaxActiveFvgs = 10`).
2. **Memory Leak từ Chart Objects:** Nếu xóa nến/khung thời gian mà hình vẽ không xóa sẽ gây lag chart. *Giải pháp:* Gắn Key UUID chuẩn (`SMC_FVG_{Id}`) và dọn dẹp trong `OnStop()`.
