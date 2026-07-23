# Implementation Plan: SMC / ICT Core Engine (`RedWave.Common.Smc`)

| Attribute | Value |
| :--- | :--- |
| **Status** | v1.0 Completed & Pushed, v2.0 Roadmap Planned |
| **Author** | `@algo-strategist` |
| **Target Directory** | `cAlgo/Sources/Common/Smc/` |

---

## 1. High-Level Phases & Roadmap

### Phase 1: Core Foundation & Data Structures (Sprint 1) - [DONE]
* Khởi tạo cấu trúc thư mục `Common/Smc/Models/` và `Common/Smc/Engines/`.
* Định nghĩa đầy đủ `SmcEnums.cs` và `SmcDataModels.cs`.

### Phase 2: Core Engines Implementation (Sprint 2 - 3) - [DONE]
* Viết `MarketStructureEngine.cs` (Pivot Point, BOS, ChoCH, MSS).
* Viết `FvgEngine.cs` (Quét FVG 3 nến, 50% CE, FvgMitigationMode `TouchEdge`/`HalfFill`/`FullFill`).
* Viết `LiquidityEngine.cs` (Quét BSL/SSL, EQH/EQL, Sweep Event).
* Viết `OrderBlockEngine.cs` & `DealingRangeEngine.cs`.

### Phase 3: Visual Renderer & Toggle Controls (Sprint 4) - [DONE]
* Viết `SmcChartRenderer.cs` (Vẽ Rectangle FVG Cyan/Pink, OB RoyalBlue/Purple, BOS LimeGreen/Crimson, ChoCH Gold/Orange, MSS Yellow/Magenta).
* Tự động xóa đối tượng `Mitigated` và giới hạn `MaxBarsToScan = 500`.

### Phase 4: Facade Router & Integration (Sprint 5) - [DONE]
* Xây dựng `SmcConfluenceMatrix.cs` kết nối 5 Engine thành bộ lọc duy nhất.
* Đóng gói `SmcVisualTestIndicator.algo` và `SmcVisualTestBot.algo` (0 Error).
* Viết bộ unit test `SmcEngineTests.cs` (121 Passed, 0 Failed).

### Phase 5: ICT Advanced Engines (v2.0 Upgrade Roadmap - Planned)
* Viết `NwogEngine.cs` (Quét khoảng trống NWOG & NDOG).
* Nâng cấp `FvgEngine` hỗ trợ **Inversion FVG (iFVG)** và **Balanced Price Range (BPR)**.
* Viết `IctUnicornDetector.cs` (Nhận diện setup Breaker Block + FVG Overlap).

---

## 2. Verification Plan & Test Strategy

| Phase | Verification Method | Pass Criteria |
| :--- | :--- | :--- |
| **Phase 1-4** | dotnet build & test suite | Build succeeded, 121 Passed 0 Failed |
| **Phase 5** | Unit test `NwogEngine` & `IctUnicornDetector` | 100% test pass on mock bar data |
