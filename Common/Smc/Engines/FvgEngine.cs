using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for detecting and managing Fair Value Gaps (3-candle price imbalances) and Inversion FVGs (iFVG).
    /// Supports standalone usage or integration into SmcConfluenceMatrix.
    /// </summary>
    public class FvgEngine
    {
        private readonly List<FairValueGap> _fvgs = new List<FairValueGap>();
        private int _idCounter = 0;

        /// <summary>
        /// Minimum gap width in pips required to register an FVG.
        /// </summary>
        public double MinGapPips { get; set; } = 1.0;

        /// <summary>
        /// Enable automatic conversion of invalidated FVGs into Inversion FVGs (iFVG).
        /// </summary>
        public bool EnableInversionFvg { get; set; } = true;

        /// <summary>
        /// Condition threshold to consider an FVG as Mitigated (Filled).
        /// Default = TouchEdge (price touching entry edge fills/mitigates the FVG).
        /// </summary>
        public FvgMitigationMode MitigationMode { get; set; } = FvgMitigationMode.TouchEdge;

        /// <summary>
        /// Maximum active FVGs kept in memory to prevent performance leaks.
        /// </summary>
        public int MaxActiveMemory { get; set; } = 200;

        /// <summary>
        /// Read-only collection of all tracked FVGs.
        /// </summary>
        public IReadOnlyList<FairValueGap> AllFvgs => _fvgs.AsReadOnly();

        /// <summary>
        /// Active (unfilled) FVGs including active Inversion FVGs.
        /// </summary>
        public IEnumerable<FairValueGap> ActiveFvgs => 
            _fvgs.Where(f => f.Status == FvgStatus.Active || f.Status == FvgStatus.PartiallyFilled || f.Status == FvgStatus.Inversion);

        /// <summary>
        /// Inversion FVGs (iFVG) whose roles have flipped.
        /// </summary>
        public IEnumerable<FairValueGap> InversionFvgs =>
            _fvgs.Where(f => f.IsInversion && f.Status == FvgStatus.Inversion);

        /// <summary>
        /// Scan bar at index and update status of existing FVGs.
        /// </summary>
        public void Update(Bars bars, int currBarIndex = -1, double pipSize = 0.0001)
        {
            if (bars == null || bars.Count < 3)
                return;

            if (currBarIndex < 0)
                currBarIndex = bars.Count - 1;

            if (currBarIndex < 2 || currBarIndex >= bars.Count)
                return;

            // 1. Update lifecycle status of existing active FVGs at current bar
            UpdateExistingFvgStatus(bars, currBarIndex);

            // 2. Detect new FVG created at bar (currBarIndex - 1) confirmed by bar currBarIndex
            int middleBarIndex = currBarIndex - 1;
            int firstBarIndex = currBarIndex - 2;
            int thirdBarIndex = currBarIndex;

            double firstHigh = bars.HighPrices[firstBarIndex];
            double firstLow = bars.LowPrices[firstBarIndex];
            double thirdHigh = bars.HighPrices[thirdBarIndex];
            double thirdLow = bars.LowPrices[thirdBarIndex];

            // Bullish FVG: Low of candle 3 > High of candle 1
            if (thirdLow > firstHigh)
            {
                double gapPips = (thirdLow - firstHigh) / pipSize;
                if (gapPips >= MinGapPips)
                {
                    _fvgs.Add(new FairValueGap
                    {
                        Id = ++_idCounter,
                        Direction = TradeType.Buy,
                        TopPrice = thirdLow,
                        BottomPrice = firstHigh,
                        Status = FvgStatus.Active,
                        CreatedBarIndex = middleBarIndex,
                        CreatedTime = bars.OpenTimes[middleBarIndex],
                        GapPips = gapPips,
                        IsInversion = false
                    });
                }
            }
            // Bearish FVG: High of candle 3 < Low of candle 1
            else if (thirdHigh < firstLow)
            {
                double gapPips = (firstLow - thirdHigh) / pipSize;
                if (gapPips >= MinGapPips)
                {
                    _fvgs.Add(new FairValueGap
                    {
                        Id = ++_idCounter,
                        Direction = TradeType.Sell,
                        TopPrice = firstLow,
                        BottomPrice = thirdHigh,
                        Status = FvgStatus.Active,
                        CreatedBarIndex = middleBarIndex,
                        CreatedTime = bars.OpenTimes[middleBarIndex],
                        GapPips = gapPips,
                        IsInversion = false
                    });
                }
            }

            // Trim memory buffer if capacity exceeded
            if (_fvgs.Count > MaxActiveMemory * 2)
            {
                _fvgs.RemoveAll(f => f.Status == FvgStatus.Mitigated || f.Status == FvgStatus.Invalidated);
            }
        }

        private void UpdateExistingFvgStatus(Bars bars, int currentBarIndex)
        {
            double high = bars.HighPrices[currentBarIndex];
            double low = bars.LowPrices[currentBarIndex];
            double close = bars.ClosePrices[currentBarIndex];

            foreach (var fvg in ActiveFvgs.ToList())
            {
                // Handle Inversion FVG (iFVG) status checks
                if (fvg.IsInversion)
                {
                    if (fvg.Direction == TradeType.Buy) // Bullish iFVG (Support for BUY)
                    {
                        if (close < fvg.BottomPrice) fvg.Status = FvgStatus.Invalidated;
                        else if (low <= fvg.TopPrice) fvg.Status = FvgStatus.Mitigated;
                    }
                    else // Bearish iFVG (Resistance for SELL)
                    {
                        if (close > fvg.TopPrice) fvg.Status = FvgStatus.Invalidated;
                        else if (high >= fvg.BottomPrice) fvg.Status = FvgStatus.Mitigated;
                    }
                    continue;
                }

                // Standard FVG status checks
                if (fvg.Direction == TradeType.Buy)
                {
                    // Price closed below FVG bottom -> Invert role or Invalidate
                    if (close < fvg.BottomPrice)
                    {
                        if (EnableInversionFvg)
                        {
                            fvg.Status = FvgStatus.Inversion;
                            fvg.IsInversion = true;
                            fvg.Direction = TradeType.Sell; // Flipped to Bearish iFVG (Resistance)
                        }
                        else
                        {
                            fvg.Status = FvgStatus.Invalidated;
                        }
                    }
                    else
                    {
                        bool isMitigated = false;
                        switch (MitigationMode)
                        {
                            case FvgMitigationMode.TouchEdge:
                                isMitigated = low <= fvg.TopPrice;
                                break;
                            case FvgMitigationMode.HalfFillCE:
                                isMitigated = low <= fvg.ConsequentEncroachment;
                                break;
                            case FvgMitigationMode.FullFill:
                                isMitigated = low <= fvg.BottomPrice;
                                break;
                        }

                        if (isMitigated)
                        {
                            fvg.Status = FvgStatus.Mitigated;
                        }
                        else if (low <= fvg.ConsequentEncroachment)
                        {
                            fvg.Status = FvgStatus.PartiallyFilled;
                        }
                    }
                }
                else // Bearish FVG
                {
                    // Price closed above FVG top -> Invert role or Invalidate
                    if (close > fvg.TopPrice)
                    {
                        if (EnableInversionFvg)
                        {
                            fvg.Status = FvgStatus.Inversion;
                            fvg.IsInversion = true;
                            fvg.Direction = TradeType.Buy; // Flipped to Bullish iFVG (Support)
                        }
                        else
                        {
                            fvg.Status = FvgStatus.Invalidated;
                        }
                    }
                    else
                    {
                        bool isMitigated = false;
                        switch (MitigationMode)
                        {
                            case FvgMitigationMode.TouchEdge:
                                isMitigated = high >= fvg.BottomPrice;
                                break;
                            case FvgMitigationMode.HalfFillCE:
                                isMitigated = high >= fvg.ConsequentEncroachment;
                                break;
                            case FvgMitigationMode.FullFill:
                                isMitigated = high >= fvg.TopPrice;
                                break;
                        }

                        if (isMitigated)
                        {
                            fvg.Status = FvgStatus.Mitigated;
                        }
                        else if (high >= fvg.ConsequentEncroachment)
                        {
                            fvg.Status = FvgStatus.PartiallyFilled;
                        }
                    }
                }
            }
        }

        public FairValueGap GetLatestBuyFvg()
        {
            return ActiveFvgs.LastOrDefault(f => f.Direction == TradeType.Buy && !f.IsInversion);
        }

        public FairValueGap GetLatestSellFvg()
        {
            return ActiveFvgs.LastOrDefault(f => f.Direction == TradeType.Sell && !f.IsInversion);
        }

        public FairValueGap GetLatestBuyInversionFvg()
        {
            return InversionFvgs.LastOrDefault(f => f.Direction == TradeType.Buy);
        }

        public FairValueGap GetLatestSellInversionFvg()
        {
            return InversionFvgs.LastOrDefault(f => f.Direction == TradeType.Sell);
        }

        public void Reset()
        {
            _fvgs.Clear();
            _idCounter = 0;
        }
    }
}
