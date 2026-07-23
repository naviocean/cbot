using System;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Represents a Swing Pivot High or Low point in price data.
    /// </summary>
    public class PivotPoint
    {
        public int Index { get; set; }
        public DateTime Time { get; set; }
        public double Price { get; set; }
        public StructureType Type { get; set; }
        public bool IsMajor { get; set; } // Major (HTF) vs Minor (LTF)
    }

    /// <summary>
    /// Event data when price breaks a structure level (BOS, ChoCH, or MSS).
    /// </summary>
    public class StructureEvent
    {
        public BreakType Type { get; set; }
        public TradeType Direction { get; set; }
        public PivotPoint BrokenPivot { get; set; }
        public int TriggerBarIndex { get; set; }
        public DateTime TriggerTime { get; set; }
        public bool IsBodyBreak { get; set; } // True = Body Close, False = Wick Sweep
    }

    /// <summary>
    /// Represents a Fair Value Gap (3-candle imbalance).
    /// </summary>
    public class FairValueGap
    {
        public int Id { get; set; }
        public TradeType Direction { get; set; } // Buy (Bullish FVG) or Sell (Bearish FVG)
        public double TopPrice { get; set; }
        public double BottomPrice { get; set; }
        public double ConsequentEncroachment => (TopPrice + BottomPrice) / 2.0; // 50% CE level
        public FvgStatus Status { get; set; }
        public int CreatedBarIndex { get; set; }
        public DateTime CreatedTime { get; set; }
        public double GapPips { get; set; }
    }

    /// <summary>
    /// Represents an Order Block or Breaker Block zone.
    /// </summary>
    public class OrderBlock
    {
        public int Id { get; set; }
        public ObType Type { get; set; }
        public TradeType Direction { get; set; }
        public double TopPrice { get; set; }
        public double BottomPrice { get; set; }
        public int BarIndex { get; set; }
        public DateTime CreatedTime { get; set; }
        public int AssociatedFvgId { get; set; } // FVG ID associated with displacement
        public bool IsMitigated { get; set; }
    }

    /// <summary>
    /// Represents a Liquidity Pool level (BSL, SSL, EQH, EQL, etc.).
    /// </summary>
    public class LiquidityPool
    {
        public int Id { get; set; }
        public LiquidityType Type { get; set; }
        public double PriceLevel { get; set; }
        public int CreatedBarIndex { get; set; }
        public DateTime CreatedTime { get; set; }
        public bool IsSwept { get; set; }
    }

    /// <summary>
    /// Event data when price sweeps liquidity at a Liquidity Pool.
    /// </summary>
    public class SweepEvent
    {
        public LiquidityPool Pool { get; set; }
        public double SweptExtremumPrice { get; set; }
        public int SweepBarIndex { get; set; }
        public DateTime SweepTime { get; set; }
        public bool ClosedBackInside { get; set; } // True = Rejection wick sweep
    }
}
