# Architecture: ICT Advanced Modules — Phase 2 (`RedWave.Common.Smc` v3.0)

| Attribute | Value |
| :--- | :--- |
| **Status** | Draft — Pending Implementation |
| **Author** | `@algo-strategist` |
| **Parent ARCH** | `ARCH-smc-ict-core-engine.md` |
| **Version** | v3.0 |

---

## 1. Module Structure

```
Common/Smc/
├── Models/
│   ├── SmcEnums.cs            ← THÊM: SessionType, KillZone, Po3Phase, BiasType
│   └── SmcDataModels.cs       ← THÊM: MtfBias, BalancedPriceRange
│
├── Engines/
│   ├── (Phase 1 — không thay đổi)
│   ├── MarketStructureEngine.cs
│   ├── FvgEngine.cs
│   ├── LiquidityEngine.cs     ← NÂNG CẤP: PDH/PDL/PWH/PWL + barTime param
│   ├── OrderBlockEngine.cs
│   ├── DealingRangeEngine.cs
│   ├── NwogEngine.cs
│   ├── IctUnicornDetector.cs
│   │
│   ├── (Phase 2 — MỚI)
│   ├── SessionEngine.cs       ← NEW: Kill Zones, Asian range
│   ├── PowerOfThreeEngine.cs  ← NEW: PO3 Accumulation/Manipulation/Distribution
│   ├── BprEngine.cs           ← NEW: Balanced Price Range detect
│   └── DailyBiasEngine.cs     ← NEW: Daily directional bias
│
├── Visuals/
│   └── SmcChartRenderer.cs    ← NÂNG CẤP: PDH/PDL lines, Asian box, BPR, KZ highlight
│
└── SmcConfluenceMatrix.cs     ← NÂNG CẤP: integrate 4 engines mới + signal gates mới
```

---

## 2. Data Flow Diagram

```
                    ┌─────────────────────────────────┐
                    │       cBot / Indicator           │
                    │  (provides Bars HTF + Bars LTF) │
                    └────────────┬────────────────────┘
                                 │
                    ┌────────────▼────────────────────┐
                    │     SmcConfluenceMatrix (LTF)    │
                    │                                  │
           HTFBias ─┤◄── htfMatrix.GetBias()          │
                    │                                  │
   ┌───────────────►│  OnBar(bars, i, barTime, pip)    │
   │                │                                  │
   │   ┌────────────┤  1. SessionEngine.Update()       │──► AsianHigh/Low
   │   │            │  2. LiquidityEngine.Update()     │──► PDH/PDL/PWH/PWL + Sweeps
   │   │            │  3. StructureEngine.Update()     │──► BOS/ChoCH/MSS
   │   │            │  4. FvgEngine.Update()           │──► FVGs + iFVG
   │   │            │  5. BprEngine.Update()           │──► BPR zones
   │   │            │  6. ObEngine.Update()            │──► OBs + BreakerBlocks
   │   │            │  7. RangeEngine.Update()         │──► Eq/Premium/Discount
   │   │            │  8. NwogEngine.Update()          │──► NWOG/NDOG
   │   │            │  9. UnicornDetector.Update()     │──► Unicorn setups
   │   │            │  10. Po3Engine.Update()          │──► PO3 phase
   │   │            │  11. BiasEngine.Update()         │──► Daily Bias
   │   │            └──────────────────────────────────┘
   │   │
   │   └───────────► SmcChartRenderer.Render()
   │
   │ [Kill Zone?] [Bias match?] [MTF match?] [PO3 valid?]
   │
   └───────────────► IsValidBuySetup() / IsValidSellSetup()
                                 │
                    ┌────────────▼────────────────────┐
                    │      Signal → cBot trade logic   │
                    └─────────────────────────────────┘
```

---

## 3. Dependency Map

```
SessionEngine           (standalone — chỉ cần DateTime, high, low)
    │
    ├─► LiquidityEngine.SetSessionLevels(asianHigh, asianLow)
    │
    └─► PowerOfThreeEngine.Update(session, liquidity, barTime)

LiquidityEngine         (nâng cấp — thêm DateTime để detect ngày/tuần mới)
    │
    └─► DailyBiasEngine.Update(... liquidity ...)

FvgEngine
    └─► BprEngine.Update(fvgs, pipSize)

StructureEngine
    ├─► DealingRangeEngine.Update(swingHigh, swingLow)
    └─► MtfBias = htfMatrix.GetBias()   ← built from StructureEngine + RangeEngine

SmcConfluenceMatrix     (orchestrator — gọi tất cả)
    ├── SessionEngine
    ├── LiquidityEngine
    ├── StructureEngine
    ├── FvgEngine
    ├── BprEngine (NEW)
    ├── ObEngine
    ├── RangeEngine
    ├── NwogEngine
    ├── UnicornDetector
    ├── PowerOfThreeEngine (NEW)
    ├── BiasEngine (NEW)
    └── HTFBias (caller-provided, MtfBias)
```

---

## 4. Sequence: `SessionEngine` Internal Logic

```
Update(barTime, high, low):
  1. Tính giờ UTC: utcHour = barTime.ToUniversalTime() + TimezoneOffsetHours (normalize về UTC)
  2. Xác định CurrentSession:
     - 20:00–23:59 → Asian
     - 00:00–01:59 → Asian (continued)
     - 02:00–04:59 → London
     - 05:00–06:59 → OffSession
     - 07:00–11:59 → NewYork
     - 12:00–13:29 → OffSession (Lunch)
     - 13:30–16:59 → NewYork (PM)
     - else        → OffSession
  3. Xác định ActiveKillZone:
     - 02:00–05:00 → LOKZ
     - 07:00–10:00 → NYAM
     - 13:30–16:00 → NYPM
     - 10:00–11:00 → SilverBullet1
     - 14:00–15:00 → SilverBullet2
     - 15:00–16:00 → SilverBullet3 (overlap với NYPM, ưu tiên SilverBullet)
     - else        → None
  4. Asian range update (chỉ khi CurrentSession == Asian && !AsianRangeLocked):
     AsianHigh = max(AsianHigh, high)
     AsianLow  = min(AsianLow, low)
  5. Lock Asian range khi đầu tiên sang London (detect transition Asian → London):
     AsianRangeLocked = true
  6. Reset Asian range khi đầu ngày mới (20:00 transition):
     AsianHigh = 0; AsianLow = double.MaxValue; AsianRangeLocked = false
```

---

## 5. Sequence: `LiquidityEngine` PDH/PDL Update

```
Update(bars, currBarIndex, barTime):
  1. Gọi existing sweep detection logic (giữ nguyên)
  2. Detect ngày mới: isNewDay = (barTime.Date != _lastBarDate)
     If isNewDay:
       PreviousDayHigh = _currentDayHigh
       PreviousDayLow  = _currentDayLow
       _currentDayHigh = bars.HighPrices[currBarIndex]
       _currentDayLow  = bars.LowPrices[currBarIndex]
       _lastBarDate = barTime.Date
     Else:
       _currentDayHigh = max(_currentDayHigh, bars.HighPrices[currBarIndex])
       _currentDayLow  = min(_currentDayLow,  bars.LowPrices[currBarIndex])
  3. Detect tuần mới tương tự cho PWH/PWL:
     isNewWeek = (GetWeekNumber(barTime) != GetWeekNumber(_lastBarDate))
```

---

## 6. Sequence: `PowerOfThreeEngine` Phase Detection

```
Update(session, liquidity, barTime):
  Switch CurrentPhase:
    None:
      If session.CurrentSession == Asian && session.AsianHigh - session.AsianLow >= MinAsianRangePips:
        → CurrentPhase = Accumulation

    Accumulation:
      If session.CurrentSession == London || session.CurrentSession == NewYork:
        Check: liquidity.HasRecentSweep(AsianHigh, ClosedBackInside=true, withinBars=5)
          → ManipulationSweepPrice = AsianHigh; DistributionDirection = Sell
          → CurrentPhase = Manipulation
        Check: liquidity.HasRecentSweep(AsianLow, ClosedBackInside=true, withinBars=5)
          → ManipulationSweepPrice = AsianLow; DistributionDirection = Buy
          → CurrentPhase = Manipulation

    Manipulation:
      If bars trong Distribution direction đang tạo momentum:
        (StructureEngine có BOS mới theo DistributionDirection)
        → CurrentPhase = Distribution; IsSetupValid = true

    Distribution:
      Auto-reset khi sang ngày mới (Reset() được gọi từ SmcConfluenceMatrix)
```

---

## 7. Sequence: `BprEngine` Overlap Detection

```
Update(fvgs, pipSize):
  1. Filter: activeBullishFvgs = fvgs.Where(f => f.Direction==Buy && f.Status==Active)
             activeBearishFvgs = fvgs.Where(f => f.Direction==Sell && f.Status==Active)
  2. For each pair (bullFvg, bearFvg):
     overlapTop    = min(bullFvg.TopPrice, bearFvg.TopPrice)
     overlapBottom = max(bullFvg.BottomPrice, bearFvg.BottomPrice)
     overlapPips   = (overlapTop - overlapBottom) / pipSize
     If overlapPips >= MinOverlapPips && !alreadyDetected(bullFvg.Id, bearFvg.Id):
       direction = bullFvg.CreatedBarIndex < bearFvg.CreatedBarIndex ? Buy : Sell
       _bprs.Add(new BalancedPriceRange { ... })
  3. Update mitigation:
     For each bpr in _bprs:
       If bpr.Direction==Buy && recentClose < bpr.OverlapBottom: bpr.IsMitigated = true
       If bpr.Direction==Sell && recentClose > bpr.OverlapTop:   bpr.IsMitigated = true
```

---

## 8. Reset Contract

Tất cả Phase 2 engines phải implement `Reset()` theo cùng contract:

```csharp
public void Reset()
{
    // Clear all state về initial values
    // KHÔNG reset config params (MinGapPips, TimezoneOffsetHours, v.v.)
}
```

`SmcConfluenceMatrix.Reset()` phải gọi Reset() cho cả 4 engines mới:
```csharp
SessionEngine.Reset();
Po3Engine.Reset();
BprEngine.Reset();
BiasEngine.Reset();
```

---

## 9. Breaking Changes từ v2.0 → v3.0

| File | Change | Impact |
| :--- | :--- | :--- |
| `LiquidityEngine.cs` | `Update()` thêm param `DateTime barTime` | Compile error nếu chưa update caller |
| `SmcConfluenceMatrix.cs` | `OnBar()` signature có thể thay đổi | Cần update cBot consumers |
| `SmcEnums.cs` | Thêm `SessionType`, `KillZone`, `Po3Phase`, `BiasType` | Non-breaking (additive) |
| `SmcDataModels.cs` | Thêm `MtfBias`, `BalancedPriceRange` | Non-breaking (additive) |
