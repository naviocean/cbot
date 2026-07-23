# Implementation Plan: ICT Advanced Modules — Phase 2 (`RedWave.Common.Smc` v3.0)

| Attribute | Value |
| :--- | :--- |
| **Status** | Ready for Development |
| **Author** | `@algo-strategist` |
| **Parent PLAN** | `PLAN-smc-ict-core-engine.md` |
| **Version** | v3.0 |
| **Target Directory** | `Common/Smc/` |
| **Estimated Sprints** | 3 sprints (6 engines + upgrades + tests) |

---

## Phase 2 — Sprint Breakdown

### Sprint 1: Foundation + Critical Engines (Tuần 1)

**Mục tiêu:** Unblock signal quality ngay — Kill Zone + PDH/PDL là high ROI nhất.

#### Task 1.1: Enum & Model additions
- File: `Common/Smc/Models/SmcEnums.cs`
- Thêm: `SessionType`, `KillZone`, `Po3Phase`, `BiasType`
- **Verify:** `dotnet build` thành công, 0 compile error

#### Task 1.2: `MtfBias` + `BalancedPriceRange` models
- File: `Common/Smc/Models/SmcDataModels.cs`
- Thêm: class `MtfBias`, class `BalancedPriceRange`
- **Verify:** Build thành công

#### Task 1.3: `SessionEngine.cs` — NEW
- File: `Common/Smc/Engines/SessionEngine.cs`
- Logic: Kill Zone detection theo UTC + Asian range tracking
- Ref: `ARCH-smc-ict-phase2.md` Section 4 (Sequence)
- **Verify:**
  - Unit test: `TestSessionEngineKillZones()` — assert IsInKillZone đúng với mock DateTime
  - Unit test: `TestAsianRangeLocksAtLondon()` — assert lock behavior

#### Task 1.4: `LiquidityEngine.cs` — NÂNG CẤP
- File: `Common/Smc/Engines/LiquidityEngine.cs`
- Thêm: PDH/PDL/PWH/PWL detection (xem ARCH Section 5)
- Thêm param `DateTime barTime` vào `Update()`
- Thêm `SetSessionLevels(double asianHigh, double asianLow)`
- **Verify:**
  - Build thành công (cần cập nhật `SmcConfluenceMatrix.OnBar()` cùng lúc)
  - Unit test: `TestPdhPdlUpdatesOnNewDay()` — giả lập 2 ngày data, assert PDH = High ngày hôm trước

#### Task 1.5: `SmcConfluenceMatrix.cs` — Update OnBar + Reset
- File: `Common/Smc/SmcConfluenceMatrix.cs`
- Thêm: `SessionEngine`, `MtfBias HTFBias`
- Update `OnBar()`: gọi SessionEngine trước, truyền barTime vào LiquidityEngine
- Update `Reset()`: gọi `SessionEngine.Reset()`
- Thêm method `GetBias()` → return `MtfBias`
- **Verify:** Existing 140 tests vẫn PASS (no regression)

---

### Sprint 2: Advanced Engines (Tuần 2)

**Mục tiêu:** PO3 + BPR + MTF filter — các engine nâng signal quality lên mức production.

#### Task 2.1: `MultiTimeframeContext` integration
- File: `Common/Smc/SmcConfluenceMatrix.cs` (cập nhật tiếp)
- Thêm `HTFBias` property + gate trong `IsValidBuySetup/IsValidSellSetup`
- **Verify:** Unit test `TestMtfFilterBlocksCounterTrendSignal()`

#### Task 2.2: `BprEngine.cs` — NEW
- File: `Common/Smc/Engines/BprEngine.cs`
- Logic: Overlap detection giữa Bullish FVG và Bearish FVG (xem ARCH Section 7)
- **Verify:**
  - Unit test: `TestBprOverlapDetectedWhenFvgsIntersect()`
  - Unit test: `TestBprNoDetectWhenNoOverlap()`
  - Unit test: `TestBprMitigatedWhenPriceClosesBeyond()`

#### Task 2.3: `PowerOfThreeEngine.cs` — NEW
- File: `Common/Smc/Engines/PowerOfThreeEngine.cs`
- Logic: PO3 phase state machine (xem ARCH Section 6)
- Depends on: `SessionEngine` (Asian range) + `LiquidityEngine` (sweep detection)
- **Verify:**
  - Unit test: `TestPo3AccumulationDetectedInAsianSession()`
  - Unit test: `TestPo3ManipulationOnJudasSwing()`
  - Unit test: `TestPo3DistributionDirectionAfterManipulation()`

#### Task 2.4: Integrate PO3 + BPR vào `SmcConfluenceMatrix`
- Update `OnBar()` sequence (bước 5 + 10, xem ARCH Section 2)
- Update `Reset()`: gọi `Po3Engine.Reset()` + `BprEngine.Reset()`
- **Verify:** Full test suite PASS

---

### Sprint 3: Bias Engine + Visual + Test Coverage (Tuần 3)

**Mục tiêu:** Hoàn thiện DailyBias + chart rendering + unit test coverage ≥ 15 new tests.

#### Task 3.1: `DailyBiasEngine.cs` — NEW
- File: `Common/Smc/Engines/DailyBiasEngine.cs`
- Logic: 4-condition scoring system (xem PRD Section 2.6)
- **Verify:** Unit test `TestDailyBiasScoring()`

#### Task 3.2: `SmcChartRenderer.cs` — NÂNG CẤP
- Thêm render: PDH/PDL horizontal lines (dashed, gold/silver)
- Thêm render: PWH/PWL horizontal lines (thicker)
- Thêm render: Asian Range box (semi-transparent gray rectangle)
- Thêm render: Kill Zone background highlight
- Thêm render: BPR zone (orange rectangle + label)
- Thêm render: PO3 Manipulation marker (text hoặc arrow)
- Ref: `PRD-smc-ict-phase2.md` Section 4 (Visual table)
- **Verify:** Visual test indicator trên XAUUSD M15, kiểm tra bằng mắt

#### Task 3.3: Integrate `DailyBiasEngine` vào `SmcConfluenceMatrix`
- Thêm `BiasEngine`, gọi trong `OnBar()` và `Reset()`
- **Verify:** Test `TestBiasFilterBlocksSellInBuyBiasDay()`

#### Task 3.4: Unit Test Coverage Sprint
- File: `Tests/CommonTests/SmcEngineTests.cs`
- Thêm ≥ 10 test cases mới (xem Task List bên dưới)
- Target: ≥ 150 PASSED, 0 FAILED

---

## Task List Đầy Đủ (Tracking)

### Models & Enums
- [ ] `SmcEnums.cs` — thêm `SessionType`, `KillZone`, `Po3Phase`, `BiasType`
- [ ] `SmcDataModels.cs` — thêm `MtfBias`, `BalancedPriceRange`

### New Engines
- [ ] `SessionEngine.cs` — Kill Zones + Asian range
- [ ] `BprEngine.cs` — Balanced Price Range detect
- [ ] `PowerOfThreeEngine.cs` — PO3 state machine
- [ ] `DailyBiasEngine.cs` — Daily directional bias scoring

### Upgrades
- [ ] `LiquidityEngine.cs` — thêm PDH/PDL/PWH/PWL + `DateTime barTime` param
- [ ] `SmcConfluenceMatrix.cs` — integrate 4 engines mới + signal gates + `GetBias()`
- [ ] `SmcChartRenderer.cs` — render PDH/PDL/BPR/AsianRange/KillZone/PO3

### Unit Tests
- [ ] `TestSessionEngineKillZones` — IsInKillZone với mock DateTime
- [ ] `TestAsianRangeLocksAtLondon` — lock behavior khi transition
- [ ] `TestPdhPdlUpdatesOnNewDay` — PDH/PDL rolling detect
- [ ] `TestMtfFilterBlocksCounterTrendSignal` — HTF=Sell → no Buy signal
- [ ] `TestBprOverlapDetectedWhenFvgsIntersect` — overlap math đúng
- [ ] `TestBprNoDetectWhenNoOverlap` — no BPR khi không có overlap
- [ ] `TestBprMitigatedWhenPriceClosesBeyond` — mitigation logic
- [ ] `TestPo3AccumulationDetectedInAsianSession` — phase 1
- [ ] `TestPo3ManipulationOnJudasSwing` — phase 2
- [ ] `TestPo3DistributionDirectionAfterManipulation` — phase 3
- [ ] `TestDailyBiasScoring` — score calculation
- [ ] `TestBiasFilterBlocksSellInBuyBiasDay` — gate logic

---

## Verify Profile (v3.0)

| Checkpoint | Criteria | Evidence |
| :--- | :--- | :--- |
| Sau Sprint 1 | Build thành công + 140 tests PASS | `dotnet test` output |
| Sau Sprint 2 | ≥ 147 tests PASS + BPR/PO3 manual verify | Test log + chart screenshot |
| Sau Sprint 3 | ≥ 152 tests PASS + visual render đúng | Test log + chart recording |
| Final | No regression vs Phase 1 tests | All 140 original tests still PASS |
