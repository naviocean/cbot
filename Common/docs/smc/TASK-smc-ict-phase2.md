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
| 1.1 | Thêm enums: `SessionType`, `KillZone`, `Po3Phase`, `BiasType` | `SmcEnums.cs` | 🔲 TODO | Additive, non-breaking |
| 1.2 | Thêm models: `MtfBias`, `BalancedPriceRange` | `SmcDataModels.cs` | 🔲 TODO | Additive, non-breaking |
| 1.3 | Viết `SessionEngine.cs` (Kill Zones + Asian range) | `Engines/SessionEngine.cs` | 🔲 TODO | NEW file |
| 1.4 | Nâng cấp `LiquidityEngine.cs` (PDH/PDL/PWH/PWL + barTime) | `Engines/LiquidityEngine.cs` | 🔲 TODO | ⚠️ Breaking: Update signature `Update(..., DateTime barTime)` |
| 1.5 | Update `SmcConfluenceMatrix.OnBar()` — integrate SessionEngine | `SmcConfluenceMatrix.cs` | 🔲 TODO | Fix breaking change từ 1.4 |
| 1.6 | Thêm `GetBias()` method vào `SmcConfluenceMatrix` | `SmcConfluenceMatrix.cs` | 🔲 TODO | Return `MtfBias` |
| 1.7 | Test: `TestSessionEngineKillZones` | `SmcEngineTests.cs` | 🔲 TODO | |
| 1.8 | Test: `TestAsianRangeLocksAtLondon` | `SmcEngineTests.cs` | 🔲 TODO | |
| 1.9 | Test: `TestPdhPdlUpdatesOnNewDay` | `SmcEngineTests.cs` | 🔲 TODO | |
| 1.10 | Verify: `dotnet test` — 140 PASSED, 0 FAILED (no regression) | — | 🔲 TODO | |

---

## Sprint 2 — Advanced Engines

| # | Task | File | Status | Notes |
| :---: | :--- | :--- | :---: | :--- |
| 2.1 | MTF filter: thêm `HTFBias` gate vào `IsValidBuySetup/SellSetup` | `SmcConfluenceMatrix.cs` | 🔲 TODO | Depends on `MtfBias` model (1.2) |
| 2.2 | Viết `BprEngine.cs` (Balanced Price Range) | `Engines/BprEngine.cs` | 🔲 TODO | NEW file |
| 2.3 | Integrate `BprEngine` vào `SmcConfluenceMatrix.OnBar()` | `SmcConfluenceMatrix.cs` | 🔲 TODO | Bước 5 trong OnBar sequence |
| 2.4 | Viết `PowerOfThreeEngine.cs` (PO3 state machine) | `Engines/PowerOfThreeEngine.cs` | 🔲 TODO | NEW file — depends on SessionEngine + LiquidityEngine |
| 2.5 | Integrate `PowerOfThreeEngine` vào `SmcConfluenceMatrix` | `SmcConfluenceMatrix.cs` | 🔲 TODO | Bước 10 trong OnBar sequence |
| 2.6 | Test: `TestMtfFilterBlocksCounterTrendSignal` | `SmcEngineTests.cs` | 🔲 TODO | HTF=Sell → no Buy signal |
| 2.7 | Test: `TestBprOverlapDetectedWhenFvgsIntersect` | `SmcEngineTests.cs` | 🔲 TODO | |
| 2.8 | Test: `TestBprNoDetectWhenNoOverlap` | `SmcEngineTests.cs` | 🔲 TODO | |
| 2.9 | Test: `TestBprMitigatedWhenPriceClosesBeyond` | `SmcEngineTests.cs` | 🔲 TODO | |
| 2.10 | Test: `TestPo3AccumulationDetectedInAsianSession` | `SmcEngineTests.cs` | 🔲 TODO | |
| 2.11 | Test: `TestPo3ManipulationOnJudasSwing` | `SmcEngineTests.cs` | 🔲 TODO | |
| 2.12 | Test: `TestPo3DistributionDirectionAfterManipulation` | `SmcEngineTests.cs` | 🔲 TODO | |
| 2.13 | Verify: `dotnet test` — ≥ 147 PASSED, 0 FAILED | — | 🔲 TODO | |

---

## Sprint 3 — DailyBias + Visual + Polish

| # | Task | File | Status | Notes |
| :---: | :--- | :--- | :---: | :--- |
| 3.1 | Viết `DailyBiasEngine.cs` (4-condition scoring) | `Engines/DailyBiasEngine.cs` | 🔲 TODO | NEW file |
| 3.2 | Integrate `DailyBiasEngine` vào `SmcConfluenceMatrix` | `SmcConfluenceMatrix.cs` | 🔲 TODO | Bước 11 trong OnBar sequence |
| 3.3 | Render PDH/PDL horizontal lines (gold/silver dashed) | `Visuals/SmcChartRenderer.cs` | 🔲 TODO | Extends right |
| 3.4 | Render PWH/PWL horizontal lines (gold/silver thicker) | `Visuals/SmcChartRenderer.cs` | 🔲 TODO | |
| 3.5 | Render Asian Range box (semi-gray, reset daily) | `Visuals/SmcChartRenderer.cs` | 🔲 TODO | DrawRectangle |
| 3.6 | Render Kill Zone background highlight (semi-gold) | `Visuals/SmcChartRenderer.cs` | 🔲 TODO | Overlay per KZ period |
| 3.7 | Render BPR zone (orange rectangle + "BPR" label) | `Visuals/SmcChartRenderer.cs` | 🔲 TODO | |
| 3.8 | Render PO3 Manipulation marker (text at Judas bar) | `Visuals/SmcChartRenderer.cs` | 🔲 TODO | |
| 3.9 | Test: `TestDailyBiasScoring` | `SmcEngineTests.cs` | 🔲 TODO | |
| 3.10 | Test: `TestBiasFilterBlocksSellInBuyBiasDay` | `SmcEngineTests.cs` | 🔲 TODO | |
| 3.11 | Visual verify: chart render đúng trên XAUUSD M15 live | Manual | 🔲 TODO | Screenshot required |
| 3.12 | Verify: `dotnet test` — ≥ 152 PASSED, 0 FAILED | — | 🔲 TODO | Final green |

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
