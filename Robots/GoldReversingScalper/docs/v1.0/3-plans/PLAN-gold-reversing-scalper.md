# PLAN: Gold Momentum Straddle Scalper (GRS-01 v2.0) Plan

## 1. Executive Summary
Thiết kế lại cBot **GoldReversingScalper** v2.0 chuyển sang mô hình **Momentum Floating Straddle**.
Tự động dời bộ đôi Pending Order bám theo giá Anchor $(Ask+Bid)/2$ khi thị trường sideway/di chuyển chậm trong cửa sổ 200ms, và bắt trọn cú bứt phá Momentum khi giá nổ mạnh vượt ngưỡng.

---

## 2. Các Bước Triển Khai Kỹ Thuật

1. **Khởi tạo Data Structure:** Lớp `TickSample` (Time, AnchorPrice) và danh sách lưu mẫu tick trong `MomentumWindowMs`.
2. **Cập nhật `OnTick` Engine:**
   - Quản lý trạng thái `Idle` và `PositionActive`.
   - Tính toán biến động giá Anchor trong cửa sổ 200ms.
   - Dời 2 lệnh Pending Stop theo Anchor khi giá di chuyển dưới ngưỡng.
3. **Position & Trailing Management:**
   - Hủy lệnh pending còn lại khi 1 lệnh khớp.
   - Tích hợp `CTrailingManager` cho Trailing SL/TP native.
   - Tự động tạo lại 2 lệnh pending straddle mới khi position đóng.

---

## 3. Verification Criteria
1. **Idle Floating Test:** Xác nhận 2 lệnh pending dời trượt theo Anchor khi giá biến động nhỏ.
2. **Momentum Fill Test:** Khi nổ momentum, 1 lệnh pending khớp $\rightarrow$ lệnh pending kia bị hủy ngay lập tức.
3. **Trailing & Exit Test:** Lệnh có SL/TP và dời Trailing SL đúng pips.
4. **Compile Test:** Build sạch 0 warning/error.
