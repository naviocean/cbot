# Product Requirements Document (PRD): SMC / ICT Modular Engine (`RedWave.Common.Smc`)

| Attribute | Value |
| :--- | :--- |
| **Status** | Approved (v1.0 Deployed, v2.0 ICT Advanced Planned) |
| **Author** | `@algo-strategist` |
| **Version** | v2.0 Roadmap |
| **Target Platform** | cTrader (C#) & MT5 (MQL5) |
| **Target Package** | `RedWave.Common.Smc` |

---

## 1. Executive Summary & Goals

Mục tiêu của dự án là xây dựng bộ công cụ **Modular SMC/ICT Core Engine** nằm trong thư viện chung `RedWave.Common.Smc`. 
Hệ thống này đóng vai trò là "Bộ não định lượng" giải mã hành vi dòng tiền thông minh (Smart Money / IPDA), tự động hóa các mô hình phân tích kỹ thuật SMC & ICT bao gồm: Market Structure (BOS/ChoCH/MSS), Fair Value Gap (FVG), Liquidity Sweep, Order Block (OB), Premium/Discount Dealing Range, và **các mô hình ICT Master nâng cao (NWOG/NDOG, Inversion FVG, BPR, Unicorn Setup)**.

### Key Success Metrics
* **Tính Độc Lập (Modularity):** Cho phép cBot chạy từng engine riêng lẻ (ví dụ: chỉ chạy FVG Engine) hoặc kết hợp đầy đủ.
* **Hiệu Năng High-Speed Backtest:** Tắt visual hình vẽ tự động trong lúc backtest giúp tốc độ test đạt tối thiểu 1,000 tick/giây.
* **Minh Bạch (Visual Clarity):** Render chính xác 100% các vùng FVG, OB, đường MSS lên chart khi ở chế độ Live/Demo.

---

## 2. Detailed Feature Specifications

### 2.1. Feature 1: Market Structure & MSS Engine
* **MSS (Market Structure Shift / ChoCH):** Tự động phát hiện khi giá đâm thủng Swing High/Low quan trọng.
* **Displacement Requirement:** Cú phá vỡ bắt buộc phải có thân nến nổ lớn và đi kèm FVG.
* **Body vs Wick Break:** Cho phép cấu hình xác nhận bằng giá đóng cửa (Thân nến) hoặc râu nến.

### 2.2. Feature 2: Fair Value Gap (FVG) Engine
* **Mô hình 3 nến:** Nhận diện khoảng trống giữa $High(N-2)$ và $Low(N)$ đối với Bullish FVG, ngược lại với Bearish FVG.
* **Vòng đời FVG:** Quản lý các trạng thái: `Active`, `PartiallyFilled` (chạm mốc 50% CE), `Mitigated` (chạm mép lấp theo `FvgMitigationMode`), `Invalidated` (đâm thủng).
* **Mitigation Modes:** Cung cấp 3 chế độ `TouchEdge` (mặc định), `HalfFillCE`, và `FullFill`.

### 2.3. Feature 3: Liquidity & Sweep Engine
* **Liquidity Pools:** Tự động đánh dấu BSL, SSL, EQH, EQL, Asian High/Low, PDH/PDL.
* **Judas Swing Detection:** Phát hiện râu nến quét qua vùng thanh khoản rồi đóng cửa chui ngược vào trong range.

### 2.4. Feature 4: Order Block (OB) & Breaker Block Engine
* **High-Probability OB:** Chỉ công nhận nến đảo chiều là Order Block chuẩn nếu theo sau nó có **Displacement + FVG**.
* **Breaker Block Conversion:** Tự động chuyển OB bị đâm thủng thành Breaker Block (Vùng kháng cự/hỗ trợ mới).

### 2.5. Feature 5: Dealing Range & Premium/Discount
* Tính toán Fibonacci 0% - 50% - 100% dựa trên Swing Range.
* **Rule:** Chỉ phát tín hiệu BUY ở vùng **Discount** ($<50\%$) và SELL ở vùng **Premium** ($>50\%$).

---

## 3. Advanced ICT Specifications (v2.0 Upgrade Roadmap)

### 3.1. Feature 7: NWOG & NDOG Engine (`NwogEngine.cs`)
* **NWOG (New Week Open Gap):** Khoảng trống giá giữa nến đóng cửa thứ 6 ($23:59$) và mở cửa sáng thứ 2 ($00:00$).
* **NDOG (New Day Open Gap):** Khoảng trống giá giữa nến đóng cửa ngày hôm trước ($23:59$) và mở cửa ngày hôm sau ($00:00$).
* **Quy tắc:** Đóng vai trò mốc cản/nam châm hút giá (Price Magnet) cố định trong tuần/ngày.

### 3.2. Feature 8: Inversion FVG (iFVG) & Balanced Price Range (BPR)
* **Inversion FVG (iFVG):** Khi một Bullish FVG bị giá đóng cửa đâm thủng xuống $\rightarrow$ Tự động chuyển đổi vai trò thành **Inversion Bearish FVG** (vùng Kháng cự để SELL).
* **Balanced Price Range (BPR):** Phát hiện vùng chồng lấp giữa 1 Bullish FVG và 1 Bearish FVG hình thành nối tiếp nhau.

### 3.3. Feature 9: ICT Unicorn Setup Engine (`IctUnicornDetector.cs`)
* **Quy tắc Unicorn:** Phát tín hiệu High-Winrate Setup khi một **Breaker Block** và một **Fair Value Gap (FVG)** cùng xuất hiện chồng lấp ở cùng một vùng giá.

---

## 4. Parameter Specifications

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `EnableFvgLogic` | bool | `true` | Bật/tắt tính toán FVG |
| `EnableStructureLogic` | bool | `true` | Bật/tắt tính toán BOS/MSS |
| `EnableObLogic` | bool | `true` | Bật/tắt tính toán Order Block |
| `MaxBarsToScan` | int | `500` | Số lượng nến lịch sử quét tối đa (tối ưu hiệu năng) |
| `MitigationMode` | enum | `TouchEdge` | Chế độ xác nhận lấp FVG (`TouchEdge`, `HalfFillCE`, `FullFill`) |
| `MinFvgPips` | double | `1.0` | Chiều rộng tối thiểu của FVG (pips) |
| `ShowFvgVisuals` | bool | `true` | Vẽ hộp FVG trên Chart (Cyan / HotPink) |
| `ShowStructureVisuals` | bool | `true` | Vẽ đường BOS/MSS trên Chart (LimeGreen/Crimson/Yellow/Magenta) |

---

## 5. Edge Cases & Risks

1. **High Volatility Spikes (Tin tức đỏ):** Giá đâm quá nhanh tạo ra nhiều FVG chồng chéo. *Giải pháp:* Giới hạn số lượng FVG active tối đa trên bộ nhớ (`MaxActiveMemory = 200`).
2. **Memory Leak từ Chart Objects:** Nếu xóa nến/khung thời gian mà hình vẽ không xóa sẽ gây lag chart. *Giải pháp:* Gắn Key UUID chuẩn (`SMC_FVG_{Id}`) và dọn dẹp khi hết hiệu lực.
