# Task Tracker: SMC / ICT Core Engine (`RedWave.Common.Smc`)

| Attribute | Value |
| :--- | :--- |
| **Status** | v1.0 Deployed, v2.0 Roadmap Tasks Added |
| **Assignee** | `@cbot-expert` |
| **Target Directory** | `cAlgo/Sources/Common/Smc/` |

---

## 📋 Developer Task Checklist

### 1. Data Models & Base Interfaces (`Common/Smc/Models/`)
- [x] Create `Common/Smc/Models/SmcEnums.cs` (`StructureType`, `BreakType`, `FvgStatus`, `FvgMitigationMode`, `ObType`, `LiquidityType`, `MarketZone`).
- [x] Create `Common/Smc/Models/SmcDataModels.cs` (`PivotPoint`, `StructureEvent`, `FairValueGap`, `OrderBlock`, `LiquidityPool`).

### 2. Technical Engines (`Common/Smc/Engines/`)
- [x] Implement `MarketStructureEngine.cs` (Pivot calculation, BOS, ChoCH, MSS detection logic).
- [x] Implement `FvgEngine.cs` (3-candle gap scanner, 50% CE math, Displacement ATR filter, `FvgMitigationMode` `TouchEdge`/`HalfFillCE`/`FullFill`).
- [x] Implement `LiquidityEngine.cs` (BSL/SSL scanner, EQH/EQL grouping, Sweep râu nến detector).
- [x] Implement `OrderBlockEngine.cs` (Bullish/Bearish OB identification, FVG association check, Breaker Block conversion).
- [x] Implement `DealingRangeEngine.cs` (Equilibrium 50% math, Premium/Discount zone classification).

### 3. Visual Renderer & Chart Objects (`Common/Smc/Visuals/`)
- [x] Implement `SmcChartRenderer.cs` (`DrawFvg`, `DrawStructure`, `DrawOrderBlock`, `RemoveObject`).
- [x] Add high-contrast color scheme (Cyan/Pink FVG, Blue/Purple OB, Lime/Red BOS, Gold/Orange ChoCH, Yellow/Magenta MSS) and text labels.
- [x] Implement `MaxBarsToScan = 500` historical boundary limit to optimize CPU & Memory.

### 4. Facade Router & cBot Integration (`Common/Smc/`)
- [x] Implement `SmcConfluenceMatrix.cs` (Facade router aggregating all 5 core engines).
- [x] Package standalone test Indicator (`Indicators/SmcVisualTestIndicator.algo`).
- [x] Package standalone test cBot (`Robots/SmcVisualTestBot.algo`).

### 5. Unit Test Suite (`Tests/CommonTests/SmcEngineTests.cs`)
- [x] Implement `SmcEngineTests.cs` (Tests FVG 50% CE math, Mitigation Modes, BOS, ChoCH, MSS, Liquidity Pools, Order Blocks, Dealing Range Equilibrium, and SmcConfluenceMatrix).
- [x] Run dotnet test suite: **121 PASSED, 0 FAILED**.

### 6. Advanced ICT Upgrade Checklist (v2.0 Roadmap)
- [ ] Implement `NwogEngine.cs` (New Week Open Gap & New Day Open Gap tracking).
- [ ] Upgrade `FvgEngine.cs` to support **Inversion FVG (iFVG)** and **Balanced Price Range (BPR)**.
- [ ] Implement `IctUnicornDetector.cs` (Breaker Block + FVG Overlap setup scanner).
- [ ] Add `NwogEngine` and `IctUnicornDetector` test cases to `SmcEngineTests.cs`.
