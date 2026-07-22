# Implementation Plan: Gold Flow Wyckoff Confluence cBot

Plan triển khai cBot **Gold Flow Wyckoff Confluence v1.0** trên cTrader C#.

---

## Task Breakdown

### Phase 1: Setup Cấu trúc Dự án
- [ ] Create `Robots/GoldFlowWyckoff/GoldFlowWyckoff/GoldFlowWyckoff.csproj` bao gồm references tới `../../Common/*.cs`.
- [ ] Create `Robots/GoldFlowWyckoff/GoldFlowWyckoff.sln`.

### Phase 2: Engine Cấu trúc Wyckoff (`WyckoffWaveEngine.cs`)
- [ ] Xây dựng `WyckoffWaveEngine.cs` xử lý tính toán sóng Weis Wave (ZigZag ATR/%), xác định điểm xoay Pivot High/Low.
- [ ] Viết hàm phát hiện mẫu hình **Higher Low**, **Lower High**, **Spring** (quét Support bật lên) và **Upthrust** (quét Resistance rụt đầu xuống).

### Phase 3: Lắp ráp Main Bot (`GoldFlowWyckoff.cs`)
- [ ] Khai báo thuộc tính `[Parameter]` phân nhóm rõ ràng (Trade & Risk, Wyckoff, Volume Profile V2, Order Flow Delta, Stop Loss, Take Profit, Break Even, Trailing, Session, News).
- [ ] Khởi tạo các module Common trong `OnStart()`: `CLogger`, `CRiskManager`, `CVolumeProfileV2`, `CTickDeltaEngine`, `CSessionFilter`, `CNewsFilter`, `CTrailingManager`.
- [ ] Lập trình logic kiểm tra Confluence 3/3 tại `OnBar()` / `OnTick()`:
  1. Wyckoff Structure Pivot / Spring / Upthrust.
  2. Giá nằm trong vùng Daily VAH / VAL / HVN từ `CVolumeProfileV2`.
  3. Tick Delta Imbalance / Spike từ `CTickDeltaEngine`.
- [ ] Thực thi vào lệnh với Lot Size tự động từ `CRiskManager`.
- [ ] Cấu hình quản lý vị thế trong `OnTick()` qua `CTrailingManager` (Partial Close 50% tại 1:2, BE tại 1:1, Trailing SL).

### Phase 4: Verify & Build
- [ ] Build dự án bằng `dotnet build` hoặc kiểm tra cú pháp C#.
- [ ] Đảm bảo không có warning nghiêm trọng hoặc lỗi thiếu reference.

---

## Verification Plan

### Manual Verification
- Kiểm tra tính đầy đủ của file solution và project structure.
- Đảm bảo các parameter trùng khớp với PRD.
- Verify việc sử dụng `CVolumeProfileV2` và các module trong `RedWave.Common`.
