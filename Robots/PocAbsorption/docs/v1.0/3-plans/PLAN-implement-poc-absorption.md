# Implementation Plan — PocAbsorption (PADR v1.0)

| Field | Detail |
| --- | --- |
| **Document ID** | PLAN-PADR-V1.0 |
| **Target Bot** | PocAbsorption (`Robots/PocAbsorption/`) |
| **Target Release** | v1.0 Release Build (`PocAbsorption.algo`) |
| **Status** | Approved |

---

## 1. Overview & Objectives

Triển khai cBot **PocAbsorption (PADR)** trên cTrader C# / .NET 6 theo đúng quy chuẩn thiết kế tại `PRD-padr.md` và `ARCH-poc-absorption.md`. Tận dụng tối đa các thành phần infrastructure có sẵn tại `Common/`.

---

## 2. Work Breakdown Structure (Phân rã công việc)

### Phase 1: Infrastructure & Project Wiring
* **Task 1.1:** Tạo cấu trúc thư mục dự án `Robots/PocAbsorption/PocAbsorption/`.
* **Task 1.2:** Tạo `PocAbsorption.csproj` với khai báo Visual Link liên kết toàn bộ file `Common/*.cs`:
  ```xml
  <ItemGroup>
    <Compile Include="..\..\..\Common\*.cs" Link="Common\%(FileName)%(Extension)" />
  </ItemGroup>
  ```
* **Task 1.3:** Tạo solution file `PocAbsorption.sln`.

### Phase 2: Core Signal Engine (`SignalEngine.cs`)
* **Task 2.1:** Triển khai State Machine (`IDLE`, `PROFILE_ACTIVE`, `RETEST_POC`, `ARMED`, `TRIGGERED`).
* **Task 2.2:** Xây dựng phương thức `CalculateNodeEdge(pocPrice, out double topEdge, out double bottomEdge)` để xác định mép nốt Volume Spike.
* **Task 2.3:** Triển khai bộ lọc `IsVolumeSpike(pocPrice, multiplier)` so sánh volume POC với 5 nốt lân cận.
* **Task 2.4:** Tích hợp `TickDeltaEngine` để xác nhận `IsDeltaDivergence(tradeType)`.
* **Task 2.5:** Triển khai phương thức kiểm tra nến Rejection (`IsRejectionBar(bar, tradeType, pocPrice)`).

### Phase 3: Bot Orchestrator (`PocAbsorption.cs`)
* **Task 3.1:** Khai báo toàn bộ Input Parameters theo PRD (Session, Strategy, Protection, Risk, News).
* **Task 3.2:** Khởi tạo các Common Modules trong `OnStart()` (`RiskManager`, `SessionFilter`, `NewsFilter`, `TrailingManager`, `Logger`).
* **Task 3.3:** Xử lý sự kiện `OnTick()` cho Delta tracking và Trailing/Risk management.
* **Task 3.4:** Xử lý sự kiện `OnBar()` cho Signal Engine trigger, kiểm tra R:R gate, tính Lot size và thực thi lệnh `ExecuteMarketOrder()`.

### Phase 4: Build & Release Verification
* **Task 4.1:** Biên dịch dự án bằng `dotnet build -c Release`.
* **Task 4.2:** Đảm bảo 0 Lỗi (Errors), 0 Cảnh báo (Warnings).
* **Task 4.3:** Kiểm tra tạo file thành phẩm `PocAbsorption.algo`.

---

## 3. Verification Criteria & Checklists

| Step | Verification Command / Check | Expected Result |
| --- | --- | --- |
| Project Wiring | `dotnet build Robots/PocAbsorption/PocAbsorption/PocAbsorption.csproj` | Build Succeeded |
| Release Build | `dotnet build -c Release` | Generates `PocAbsorption.algo` without warnings |
| Code Quality | Static Audit & Review | Follows RedWave C# standards |
| Backtest | cTrader Backtester (Tick Data) | PF >= 2.0, Max DD < 12% |

---

## 4. Dependencies & Risks

* **Dependency:** Phải có tập tin lịch tin tức `news_events.csv` hoặc truyền chuỗi schedule cho `NewsFilter`.
* **Backtest Note:** Bắt buộc chạy backtest ở chế độ **"Tick Data from Server"** để cTrader nạp đúng Ask/Bid tick history cho `TickDeltaEngine`.
