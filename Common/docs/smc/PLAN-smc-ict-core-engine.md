# Implementation Plan: SMC / ICT Core Engine (`RedWave.Common.Smc`)

| Attribute | Value |
| :--- | :--- |
| **Status** | In Progress |
| **Author** | `@algo-strategist` |
| **Target Directory** | `cAlgo/Sources/Common/Smc/` |

---

## 1. High-Level Phases & Roadmap

### Phase 1: Core Foundation & Data Structures (Sprint 1)
* Khởi tạo cấu trúc thư mục `Common/Smc/Models/` và `Common/Smc/Engines/`.
* Định nghĩa đầy đủ `SmcEnums.cs` và `SmcDataModels.cs` (PivotPoint, FVG, OrderBlock, LiquidityPool).

### Phase 2: Core Engines Implementation (Sprint 2 - 3)
* Viết `MarketStructureEngine.cs` (Pivot Point, BOS, ChoCH, MSS).
* Viết `FvgEngine.cs` (Quét FVG 3 nến, 50% CE, trạng thái Vòng đời).
* Viết `LiquidityEngine.cs` (Quét BSL/SSL, EQH/EQL, Sweep Event).
* Viết `OrderBlockEngine.cs` & `DealingRangeEngine.cs`.

### Phase 3: Visual Renderer & Toggle Controls (Sprint 4)
* Viết `SmcChartRenderer.cs` (Vẽ Rectangle FVG, TrendLine BOS/MSS, Text Labels trên cTrader Canvas).
* Tích hợp cơ chế Toggle tự động xóa đối tượng `Mitigated` và tắt Visual khi Backtest.

### Phase 4: Facade Router & Integration (Sprint 5)
* Xây dựng `SmcConfluenceMatrix.cs` kết nối 5 Engine thành bộ lọc duy nhất.
* Viết cBot mẫu test tính độc lập của `FvgEngine` và test full set `SmcConfluenceMatrix`.

---

## 2. Verification Plan & Test Strategy

| Phase | Verification Method | Pass Criteria |
| :--- | :--- | :--- |
| **Phase 1** | Build Solution C# | Compilation success 0 error |
| **Phase 2** | Unit Test với Mock Bar Data | Quét chuẩn 100% FVG và MSS trên nến mẫu |
| **Phase 3** | cTrader Visual Chart Testing | Render đúng vị trí FVG, tự xóa khi bị lấp |
| **Phase 4** | Strategy Tester Backtest Speed | Tốc độ Backtest $> 1,000$ tick/s khi tắt visual |
