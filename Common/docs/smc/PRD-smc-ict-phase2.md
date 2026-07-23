# Product Requirements Document (PRD): ICT Advanced Modules — Phase 2 (`RedWave.Common.Smc`)

| Attribute | Value |
| :--- | :--- |
| **Status** | Approved — Ready for Implementation |
| **Author** | `@algo-strategist` |
| **Version** | v3.0 |
| **Parent PRD** | `PRD-smc-ict-core-engine.md` (v1.0/v2.0 foundation) |
| **Target Platform** | cTrader (C#) |
| **Target Package** | `RedWave.Common.Smc` |
| **Target Directory** | `Common/Smc/Engines/` |

---

## 1. Executive Summary

Phase 1 (v1.0/v2.0) đã hoàn thành bộ **core ICT structure detection** (BOS/ChoCH/MSS, FVG, OB, BreakerBlock, Liquidity, DealingRange, NWOG, Unicorn). Tuy nhiên, bộ engine hiện tại **thiếu context thời gian và bias đa khung thời gian** — hai yếu tố mà ICT methodology coi là bắt buộc để lọc noise và chỉ trade trong "high probability windows".

Phase 2 (v3.0) bổ sung **6 module mới** theo 3 nhóm ưu tiên:

| Priority | Module | Mô tả |
| :---: | :--- | :--- |
| 🔴 Critical | `SessionEngine` | Kill Zones, Asian range, session boundaries |
| 🔴 Critical | `LiquidityEngine` (nâng cấp) | PDH/PDL/PWH/PWL, AsianHigh/Low detection |
| 🔴 Critical | `MultiTimeframeContext` | HTF bias → LTF entry alignment |
| 🟡 High | `PowerOfThreeEngine` | Accumulation → Manipulation → Distribution |
| 🟡 High | `BprEngine` | Balanced Price Range (BPR) |
| 🟠 Medium | `DailyBiasEngine` | Daily Bias (Buy Day / Sell Day) |

---

## 2. Feature Specifications

---

### 2.1. Feature 1: `SessionEngine` *(Priority: Critical)*

**Mục tiêu:** Xác định các Kill Zones ICT theo giờ UTC. Chỉ cho phép engine emit signal trong window thời gian có institutional activity.

#### 2.1.1. Kill Zones

| Zone | UTC Window | Tên gọi ICT |
| :--- | :--- | :--- |
| Asian KZ | 20:00 – 00:00 | Asian Session / NWOG Formation |
| London KZ | 02:00 – 05:00 | London Open Kill Zone (LOKZ) |
| NY AM KZ | 07:00 – 10:00 | New York AM Kill Zone (NYAM) |
| NY Lunch | 12:00 – 13:00 | NY Lunch / Low Probability |
| NY PM KZ | 13:30 – 16:00 | NY PM Kill Zone |
| Silver Bullet 1 | 10:00 – 11:00 | SB1 (NY time = UTC-4/UTC-5) |
| Silver Bullet 2 | 14:00 – 15:00 | SB2 |
| Silver Bullet 3 | 15:00 – 16:00 | SB3 |

> **Lưu ý:** Tất cả giờ có thể config theo timezone offset. Default = UTC+0.

#### 2.1.2. Asian Range Tracking

```
Asian Range = [AsianLow, AsianHigh] trong session 20:00–00:00 UTC
→ Cập nhật rolling theo từng bar trong session
→ Khi sang London (00:00 UTC): range bị "locked" (không cập nhật nữa)
→ AsianMidpoint = (AsianHigh + AsianLow) / 2
→ Dùng như reference cho PO3 Engine (Manipulation detection)
```

#### 2.1.3. API Public

```csharp
public class SessionEngine
{
    // Config
    public int TimezoneOffsetHours { get; set; } = 0; // UTC offset (VN = +7)

    // Current state
    public SessionType CurrentSession { get; }        // Asian/London/NewYork/OffSession
    public KillZone ActiveKillZone { get; }           // None/LOKZ/NYAM/NYPM/SilverBullet1/2/3
    public bool IsInKillZone { get; }
    public bool IsInSilverBullet { get; }

    // Asian range (locked sau 00:00 UTC)
    public double AsianHigh { get; }
    public double AsianLow { get; }
    public double AsianMidpoint { get; }
    public bool AsianRangeLocked { get; }

    // Methods
    public void Update(DateTime barTime, double high, double low);
    public void Reset();
}
```

#### 2.1.4. Enums cần thêm vào `SmcEnums.cs`

```csharp
public enum SessionType { Asian, London, NewYork, OffSession }
public enum KillZone { None, LOKZ, NYAM, NYPM, SilverBullet1, SilverBullet2, SilverBullet3 }
```

---

### 2.2. Feature 2: `LiquidityEngine` Upgrade — PDH/PDL/PWH/PWL *(Priority: Critical)*

**Mục tiêu:** Detect và track các previous period highs/lows như ICT liquidity targets.

#### 2.2.1. Các level cần detect

| Level | Mô tả | ICT role |
| :--- | :--- | :--- |
| PDH | Previous Day High | Primary BSL target |
| PDL | Previous Day Low | Primary SSL target |
| PWH | Previous Week High | Major BSL target |
| PWL | Previous Week Low | Major SSL target |
| AsianHigh | Ngày hiện tại — Asian session high | Intraday BSL |
| AsianLow | Ngày hiện tại — Asian session low | Intraday SSL |

#### 2.2.2. Logic detect

```
PDH/PDL: Khi bar đầu tiên ngày mới mở (00:00 server time)
         → ghi lại High/Low của ngày hôm qua (rolling)

PWH/PWL: Khi bar đầu tiên tuần mới mở (Monday 00:00)
         → ghi lại High/Low của tuần hôm trước

AsianHigh/Low: Lấy từ SessionEngine (đã lock sau 00:00 UTC)
              → gọi LiquidityEngine.SetSessionLevels(session.AsianHigh, session.AsianLow)
```

#### 2.2.3. API thêm vào `LiquidityEngine`

```csharp
// Thêm properties:
public double PreviousDayHigh { get; }
public double PreviousDayLow { get; }
public double PreviousWeekHigh { get; }
public double PreviousWeekLow { get; }
public double AsianSessionHigh { get; }
public double AsianSessionLow { get; }

// Thêm method (gọi từ SmcConfluenceMatrix.OnBar):
public void SetSessionLevels(double asianHigh, double asianLow);

// Update cần nhận barTime để detect ngày/tuần mới:
// Signature cũ:  public void Update(Bars bars, int currBarIndex)
// Signature mới: public void Update(Bars bars, int currBarIndex, DateTime barTime)
```

> **Breaking change nhỏ:** `LiquidityEngine.Update()` signature thay đổi — cần update `SmcConfluenceMatrix.OnBar()`.

---

### 2.3. Feature 3: `MultiTimeframeContext` *(Priority: Critical)*

**Mục tiêu:** Cho phép `SmcConfluenceMatrix` nhận HTF bias từ bên ngoài để filter LTF signals.

#### 2.3.1. Design

```
Approach: Caller-provided (không tự fetch HTF bars)
→ Dev trong cBot khởi tạo SmcConfluenceMatrix cho 2 TF: HTF + LTF
→ OnBar HTF: htfMatrix.OnBar(htfBars, i) → cập nhật HTF structure
→ Mỗi LTF bar: ltfMatrix.HTFBias = htfMatrix.GetBias()
→ LTF matrix signal gates kiểm tra HTFBias trước khi emit
```

#### 2.3.2. `MtfBias` class (thêm vào `SmcDataModels.cs`)

```csharp
public class MtfBias
{
    public bool IsValid { get; set; }              // false = chưa đủ history
    public TradeType Direction { get; set; }       // Buy = bullish HTF bias
    public BreakType LastHTFBreak { get; set; }    // BOS/ChoCH/MSS
    public MarketZone HTFZone { get; set; }        // Premium/Discount/Equilibrium
    public DateTime UpdatedAt { get; set; }
}
```

#### 2.3.3. Thêm vào `SmcConfluenceMatrix`

```csharp
// Property mới:
public MtfBias HTFBias { get; set; }

// Method mới:
public MtfBias GetBias()
{
    return new MtfBias
    {
        IsValid = StructureEngine.LastDirection.HasValue,
        Direction = StructureEngine.LastDirection ?? TradeType.Buy,
        LastHTFBreak = StructureEngine.LatestEvent?.Type ?? BreakType.BOS,
        HTFZone = RangeEngine.GetZone(/* last close */),
        UpdatedAt = DateTime.UtcNow
    };
}
```

---

### 2.4. Feature 4: `PowerOfThreeEngine` *(Priority: High)*

**Mục tiêu:** Detect 3 phase của ICT Power of 3 trong mỗi session/ngày.

#### 2.4.1. 3 Phases

```
Phase 1 — Accumulation:
  Giá di chuyển sideways trong hoặc gần Asian range.
  Điều kiện: SessionEngine.CurrentSession == Asian && giá không break AsianHigh/AsianLow

Phase 2 — Manipulation (Judas Swing):
  London/NYAM mở, giá phá vỡ một phía Asian range để sweep liquidity rồi đóng lại.
  Điều kiện: LiquidityEngine.HasRecentSweep(AsianHigh/AsianLow, ClosedBackInside=true)

Phase 3 — Distribution:
  True directional move ngược chiều Manipulation.
  If swept AsianHigh (BSL) → Distribution = Sell
  If swept AsianLow  (SSL) → Distribution = Buy
```

#### 2.4.2. API

```csharp
public class PowerOfThreeEngine
{
    public Po3Phase CurrentPhase { get; }             // None/Accumulation/Manipulation/Distribution
    public TradeType? DistributionDirection { get; }  // null nếu chưa có Manipulation
    public double ManipulationSweepPrice { get; }     // Giá cực trị tại Judas Swing
    public bool IsSetupValid { get; }                 // true = A→M→D đã complete

    public void Update(SessionEngine session, LiquidityEngine liquidity, DateTime barTime);
    public void Reset();
}

public enum Po3Phase { None, Accumulation, Manipulation, Distribution }
```

---

### 2.5. Feature 5: `BprEngine` — Balanced Price Range *(Priority: High)*

**Mục tiêu:** Detect vùng chồng lấp giữa Bearish FVG và Bullish FVG — ICT gọi là highest-probability entry.

#### 2.5.1. Logic

```
BPR hợp lệ khi:
  BullishFvg = FVG có Direction=Buy,  [BullBottom, BullTop]
  BearishFvg = FVG có Direction=Sell, [BearBottom, BearTop]

  Overlap = max(BullBottom, BearBottom) → min(BullTop, BearTop)
  OverlapSize = OverlapTop - OverlapBottom > MinBprOverlapPips

  BPR Direction:
    Nếu Bullish FVG hình thành TRƯỚC Bearish FVG → BPR = Support (Direction=Buy)
    Nếu Bearish FVG hình thành TRƯỚC Bullish FVG → BPR = Resistance (Direction=Sell)
```

#### 2.5.2. API

```csharp
public class BprEngine
{
    public double MinOverlapPips { get; set; } = 2.0;
    public IReadOnlyList<BalancedPriceRange> ActiveBprs { get; }

    public void Update(IEnumerable<FairValueGap> fvgs, double pipSize);
    public BalancedPriceRange GetLatestBuyBpr();    // Direction=Buy (Support)
    public BalancedPriceRange GetLatestSellBpr();   // Direction=Sell (Resistance)
    public void Reset();
}

public class BalancedPriceRange
{
    public int Id { get; set; }
    public TradeType Direction { get; set; }
    public FairValueGap BullishFvg { get; set; }
    public FairValueGap BearishFvg { get; set; }
    public double OverlapTopPrice { get; set; }
    public double OverlapBottomPrice { get; set; }
    public double MidPrice => (OverlapTopPrice + OverlapBottomPrice) / 2.0;
    public bool IsMitigated { get; set; }
    public int DetectedBarIndex { get; set; }
}
```

> **Mitigation:** BPR bị mitigated khi giá đóng cửa hoàn toàn bên kia zone (qua OverlapTop với BPR Buy, hoặc qua OverlapBottom với BPR Sell).

---

### 2.6. Feature 6: `DailyBiasEngine` *(Priority: Medium)*

**Mục tiêu:** Xác định hướng bias cho ngày giao dịch dựa trên HTF context.

#### 2.6.1. Logic

```
Buy Bias (cần thỏa ≥ 3/4):
  1. HTFBias.Direction == Buy
  2. CurrentPrice < RangeEngine.Equilibrium (Discount zone)
  3. PDL còn intact (chưa bị LiquidityEngine sweep)
  4. SessionEngine.AsianClose > SessionEngine.AsianMidpoint (bullish Asian close)

Sell Bias (cần thỏa ≥ 3/4):
  1. HTFBias.Direction == Sell
  2. CurrentPrice > RangeEngine.Equilibrium (Premium zone)
  3. PDH còn intact
  4. SessionEngine.AsianClose < SessionEngine.AsianMidpoint

BiasScore = conditions_met / 4.0 (0.0 → 1.0)
Neutral:   BiasScore < 0.5
```

#### 2.6.2. API

```csharp
public class DailyBiasEngine
{
    public BiasType TodayBias { get; }
    public double BiasScore { get; }  // 0.0–1.0

    public void Update(MtfBias htfBias, DealingRangeEngine range,
                       LiquidityEngine liquidity, SessionEngine session,
                       double currentPrice, DateTime barTime);
    public void Reset();
}

public enum BiasType { BuyBias, SellBias, Neutral }
```

---

## 3. Updated `SmcConfluenceMatrix` — Full Integration

### 3.1. New Engines

```csharp
public class SmcConfluenceMatrix
{
    // Phase 1 engines (existing, không thay đổi)
    public MarketStructureEngine StructureEngine { get; }
    public FvgEngine FvgEngine { get; }
    public LiquidityEngine LiquidityEngine { get; }
    public OrderBlockEngine ObEngine { get; }
    public DealingRangeEngine RangeEngine { get; }
    public NwogEngine NwogEngine { get; }
    public IctUnicornDetector UnicornDetector { get; }

    // Phase 2 engines (new)
    public SessionEngine SessionEngine { get; }
    public PowerOfThreeEngine Po3Engine { get; }
    public BprEngine BprEngine { get; }
    public DailyBiasEngine BiasEngine { get; }
    public MtfBias HTFBias { get; set; }  // Caller sets this
}
```

### 3.2. Updated `OnBar()` Sequence

```
1. SessionEngine.Update(barTime, high, low)
2. LiquidityEngine.Update(bars, i, barTime)           ← thêm barTime
3. LiquidityEngine.SetSessionLevels(session.AsianHigh, session.AsianLow)
4. StructureEngine.Update(bars, i)
5. FvgEngine.Update(bars, i, pipSize)
6. BprEngine.Update(FvgEngine.AllFvgs, pipSize)       ← NEW
7. ObEngine.Update(bars, FvgEngine.ActiveFvgs, StructureEngine.Events, i)
8. RangeEngine.Update(StructureEngine.LastSwingHigh, StructureEngine.LastSwingLow)
9. NwogEngine.Update(bars, i, barTime, pipSize)
10. UnicornDetector.Update(ObEngine.ActiveOrderBlocks, FvgEngine.ActiveFvgs, barTime)
11. Po3Engine.Update(SessionEngine, LiquidityEngine, barTime)   ← NEW
12. BiasEngine.Update(HTFBias, RangeEngine, LiquidityEngine, SessionEngine, close, barTime) ← NEW
```

### 3.3. Updated Signal Gates

```csharp
public bool IsValidBuySetup(double currentPrice)
{
    if (EnableKillZoneFilter && !SessionEngine.IsInKillZone) return false;
    if (EnableBiasFilter && BiasEngine.TodayBias == BiasType.SellBias) return false;
    if (EnableMtfFilter && HTFBias?.IsValid == true && HTFBias.Direction != TradeType.Buy) return false;
    if (!RangeEngine.IsInDiscount(currentPrice)) return false;
    if (StructureEngine.LastDirection != TradeType.Buy) return false;
    if (EnablePo3Filter && Po3Engine.IsSetupValid && Po3Engine.DistributionDirection != TradeType.Buy) return false;
    return FvgEngine.GetLatestBuyFvg() != null || BprEngine.GetLatestBuyBpr() != null
           || ObEngine.GetPrimaryBuyOb() != null;
}
```

---

## 4. Visual Rendering — `SmcChartRenderer` Updates

| Element | Color | Style | Notes |
| :--- | :--- | :--- | :--- |
| Asian Range box | `ARGB(30, 128, 128, 128)` | Filled rectangle | Reset mỗi ngày |
| Kill Zone highlight | `ARGB(20, 255, 215, 0)` | Background per KZ period | Xóa khi hết KZ |
| PDH line | `Color.Gold`, dashed | Horizontal, extends right | Label "PDH" |
| PDL line | `Color.Silver`, dashed | Horizontal, extends right | Label "PDL" |
| PWH line | `Color.Gold`, LinesDots | Horizontal, thicker | Label "PWH" |
| PWL line | `Color.Silver`, LinesDots | Horizontal, thicker | Label "PWL" |
| BPR zone | `ARGB(80, 255, 140, 0)` (Orange) | Filled rectangle | Label "BPR (Buy)" / "BPR (Sell)" |
| PO3 Manipulation marker | `ARGB(120, 220, 20, 60)` | Arrow/triangle tại Judas bar | Label "Manipulation" |
| PO3 Distribution arrow | `ARGB(150, 0, 200, 100)` | Directional label | Label "Distribution ↑" / "↓" |

---

## 5. Parameter Table

| Parameter | Engine | Type | Default | Mô tả |
| :--- | :--- | :--- | :--- | :--- |
| `TimezoneOffsetHours` | `SessionEngine` | int | 0 | UTC offset (VN = +7) |
| `EnableKillZoneFilter` | Matrix | bool | true | Chỉ signal trong Kill Zone |
| `EnableMtfFilter` | Matrix | bool | true | Yêu cầu HTF direction match |
| `EnableBiasFilter` | Matrix | bool | true | Yêu cầu Daily Bias match |
| `EnablePo3Filter` | Matrix | bool | true | Chỉ signal sau PO3 Manipulation |
| `MinAsianRangePips` | `PowerOfThreeEngine` | double | 5.0 | Range tối thiểu để PO3 valid |
| `MinBprOverlapPips` | `BprEngine` | double | 2.0 | Overlap tối thiểu để BPR valid |
| `BiasMinScore` | `DailyBiasEngine` | double | 0.5 | Score tối thiểu để xem là bias |

---

## 6. Success Criteria

| Criterion | Measure |
| :--- | :--- |
| Kill Zone filter giảm noise | Signal count giảm ≥ 60%; không signal trong NY Lunch |
| PDH/PDL hiển thị đúng | Khớp chart thực trên XAUUSD H1 back 30 ngày |
| MTF filter ngăn counter-trend | 0 Buy signal khi HTF.Direction=Sell trong backtest |
| PO3 detect Judas Swing | Detect ≥ 80% London manipulation trên XAUUSD M15 |
| BPR overlap math | Tính đúng với tolerance ±0.1 pip |
| Unit tests | ≥ 10 test cases mới, 100% PASS, no regression |
