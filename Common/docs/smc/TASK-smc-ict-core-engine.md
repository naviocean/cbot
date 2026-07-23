# Task Tracker: SMC / ICT Core Engine (`RedWave.Common.Smc`)

| Attribute | Value |
| :--- | :--- |
| **Status** | In Progress |
| **Assignee** | `@cbot-expert` |
| **Target Directory** | `cAlgo/Sources/Common/Smc/` |

---

## 📋 Developer Task Checklist

### 1. Data Models & Base Interfaces (`Common/Smc/Models/`)
- [x] Create `Common/Smc/Models/SmcEnums.cs` (`StructureType`, `BreakType`, `FvgStatus`, `ObType`, `LiquidityType`, `MarketZone`).
- [x] Create `Common/Smc/Models/SmcDataModels.cs` (`PivotPoint`, `StructureEvent`, `FairValueGap`, `OrderBlock`, `LiquidityPool`).

### 2. Technical Engines (`Common/Smc/Engines/`)
- [x] Implement `MarketStructureEngine.cs` (Pivot calculation, BOS, ChoCH, MSS detection logic).
- [x] Implement `FvgEngine.cs` (3-candle gap scanner, 50% CE math, Displacement ATR filter, State update lifecycle).
- [x] Implement `LiquidityEngine.cs` (BSL/SSL scanner, EQH/EQL grouping, Sweep râu nến detector).
- [x] Implement `OrderBlockEngine.cs` (Bullish/Bearish OB identification, FVG association check, Breaker Block conversion).
- [x] Implement `DealingRangeEngine.cs` (Equilibrium 50% math, Premium/Discount zone classification).

### 3. Visual Renderer & Chart Objects (`Common/Smc/Visuals/`)
- [x] Implement `SmcChartRenderer.cs` (`DrawFvg`, `DrawStructure`, `DrawOrderBlock`, `RemoveObject`).
- [x] Add `AutoCleanVisuals` logic to purge `Mitigated` and `Invalidated` FVG/OB rectangles.
- [x] Implement Backtest Mode detection (Disable visual drawing calls during backtest optimization).

### 4. Facade Router & cBot Integration (`Common/Smc/`)
- [x] Implement `SmcConfluenceMatrix.cs` (Facade router aggregating all 5 core engines).
- [x] Create standalone FVG cBot example (`Robots/StandaloneFvgBot/`).
- [x] Create full SMC/ICT cBot example (`Robots/SmcHybridBot/`).

### 5. Unit Test Suite (`Tests/CommonTests/SmcEngineTests.cs`)
- [x] Implement `SmcEngineTests.cs` (Tests FVG 50% CE math, Mitigation Modes, BOS, ChoCH, MSS, Liquidity Pools, Order Blocks, Dealing Range Equilibrium, and SmcConfluenceMatrix).
- [x] Run dotnet test suite: **121 PASSED, 0 FAILED**.
