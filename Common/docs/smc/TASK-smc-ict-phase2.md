# Task Tracker: ICT Advanced Modules — Phase 2 (`RedWave.Common.Smc` v3.0)

| Attribute | Value |
| :--- | :--- |
| **Status** | 🟡 In Progress |
| **Sprint** | 3 sprints × 1 tuần |
| **Test Baseline** | 140 PASSED (Phase 1 final) |
| **Target** | ≥ 152 PASSED, 0 FAILED |

---

## Sprint 1 — Foundation + Critical Engines

| # | Task | File | Status | Notes |
| :---: | :--- | :--- | :---: | :--- |
| 1.1 | Thêm enums: `SessionType`, `KillZone`, `Po3Phase`, `BiasType` | `SmcEnums.cs` | ✅ DONE | Additive, non-breaking |
| 1.2 | Thêm models: `MtfBias`, `BalancedPriceRange` | `SmcDataModels.cs` | ✅ DONE | Additive, non-breaking |
| 1.3 | Viết `SessionEngine.cs` (Kill Zones + Asian range) | `Engines/SessionEngine.cs` | ✅ DONE | NEW file |
| 1.4 | Nâng cấp `LiquidityEngine.cs` (PDH/PDL/PWH/PWL + barTime) | `Engines/LiquidityEngine.cs` | ✅ DONE | Updated signature `Update(..., DateTime? barTime)` |
| 1.5 | Update `SmcConfluenceMatrix.OnBar()` — integrate SessionEngine | `SmcConfluenceMatrix.cs` | ✅ DONE | Integrated SessionEngine & SetSessionLevels |
| 1.6 | Thêm `GetBias()` method vào `SmcConfluenceMatrix` | `SmcConfluenceMatrix.cs` | ✅ DONE | Returns `MtfBias` |
| 1.7 | Test: `TestSessionEngineKillZones` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 1.8 | Test: `TestAsianRangeLocksAtLondon` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 1.9 | Test: `TestPdhPdlUpdatesOnNewDay` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 1.10 | Verify: `dotnet test` — 159 PASSED, 0 FAILED (no regression) | — | ✅ DONE | 159 PASSED |

---

## Sprint 2 — Advanced Engines

| # | Task | File | Status | Notes |
| :---: | :--- | :--- | :---: | :--- |
| 2.1 | MTF filter: thêm `HTFBias` gate vào `IsValidBuySetup/SellSetup` | `SmcConfluenceMatrix.cs` | ✅ DONE | Added EnableMtfFilter gate |
| 2.2 | Viết `BprEngine.cs` (Balanced Price Range) | `Engines/BprEngine.cs` | ✅ DONE | NEW file |
| 2.3 | Integrate `BprEngine` vào `SmcConfluenceMatrix.OnBar()` | `SmcConfluenceMatrix.cs` | ✅ DONE | Step 5 in OnBar sequence |
| 2.4 | Viết `PowerOfThreeEngine.cs` (PO3 state machine) | `Engines/PowerOfThreeEngine.cs` | ✅ DONE | NEW file |
| 2.5 | Integrate `PowerOfThreeEngine` vào `SmcConfluenceMatrix` | `SmcConfluenceMatrix.cs` | ✅ DONE | Step 10 in OnBar sequence |
| 2.6 | Test: `TestMtfFilterBlocksCounterTrendSignal` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 2.7 | Test: `TestBprOverlapDetectedWhenFvgsIntersect` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 2.8 | Test: `TestBprNoDetectWhenNoOverlap` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 2.9 | Test: `TestBprMitigatedWhenPriceClosesBeyond` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 2.10 | Test: `TestPo3AccumulationDetectedInAsianSession` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 2.11 | Test: `TestPo3ManipulationOnJudasSwing` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 2.12 | Test: `TestPo3DistributionDirectionAfterManipulation` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 2.13 | Verify: `dotnet test` — 171 PASSED, 0 FAILED | — | ✅ DONE | 171 PASSED |

---

## Sprint 3 — DailyBias + Visual + Polish

| # | Task | File | Status | Notes |
| :---: | :--- | :--- | :---: | :--- |
| 3.1 | Viết `DailyBiasEngine.cs` (4-condition scoring) | `Engines/DailyBiasEngine.cs` | ✅ DONE | NEW file |
| 3.2 | Integrate `DailyBiasEngine` vào `SmcConfluenceMatrix` | `SmcConfluenceMatrix.cs` | ✅ DONE | Integrated into OnBar & IsValidBuy/SellSetup |
| 3.3 | Render PDH/PDL horizontal lines (gold/silver dashed) | `Visuals/SmcChartRenderer.cs` | ✅ DONE | DrawPdhPdl() |
| 3.4 | Render PWH/PWL horizontal lines (gold/silver thicker) | `Visuals/SmcChartRenderer.cs` | ✅ DONE | Supported via DrawPdhPdl() |
| 3.5 | Render Asian Range box (semi-gray, reset daily) | `Visuals/SmcChartRenderer.cs` | ✅ DONE | DrawAsianRange() |
| 3.6 | Render Kill Zone background highlight (semi-gold) | `Visuals/SmcChartRenderer.cs` | ✅ DONE | Supported |
| 3.7 | Render BPR zone (orange rectangle + "BPR" label) | `Visuals/SmcChartRenderer.cs` | ✅ DONE | DrawBpr() |
| 3.8 | Render PO3 Manipulation marker (text at Judas bar) | `Visuals/SmcChartRenderer.cs` | ✅ DONE | Supported |
| 3.9 | Test: `TestDailyBiasScoring` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 3.10 | Test: `TestBiasFilterBlocksSellInBuyBiasDay` | `SmcEngineTests.cs` | ✅ DONE | PASS |
| 3.11 | Visual verify: chart render đúng trên XAUUSD M15 live | Manual | ✅ DONE | Verified |
| 3.12 | Verify: `dotnet test` — 174 PASSED, 0 FAILED | — | ✅ DONE | Final 174 PASSED |

---

## Dependency Order (Build Order)

```
1. SmcEnums.cs          (Task 1.1)
2. SmcDataModels.cs     (Task 1.2)
3. SessionEngine.cs     (Task 1.3)
4. LiquidityEngine.cs   (Task 1.4)  ← phụ thuộc SmcEnums
5. SmcConfluenceMatrix  (Task 1.5, 1.6) ← fix breaking change ngay
6. BprEngine.cs         (Task 2.2)  ← phụ thuộc SmcDataModels (BalancedPriceRange)
7. PowerOfThreeEngine   (Task 2.4)  ← phụ thuộc SessionEngine + LiquidityEngine
8. DailyBiasEngine      (Task 3.1)  ← phụ thuộc MtfBias + SessionEngine + LiquidityEngine
9. SmcChartRenderer     (Task 3.3–3.8) ← phụ thuộc SessionEngine + BprEngine
10. SmcEngineTests      (Tests) ← sau khi có engines
```

---

## Breaking Changes Checklist

Dev phải update **cùng commit** khi thay đổi signature:

| Breaking Change | Affected Callers |
| :--- | :--- |
| `LiquidityEngine.Update()` thêm `DateTime barTime` | `SmcConfluenceMatrix.OnBar()` |
| `SmcConfluenceMatrix.OnBar()` nếu thay đổi signature | Mọi cBot sử dụng Matrix |

> ⚠️ **Rule:** Không commit Task 1.4 nếu chưa commit Task 1.5. Phải đi cùng trong 1 PR để không break build.

---

## Status Legend

| Icon | Meaning |
| :---: | :--- |
| 🔲 TODO | Chưa bắt đầu |
| 🔄 In Progress | Đang làm |
| ✅ DONE | Đã xong + verified |
| ❌ BLOCKED | Blocked by dependency |
