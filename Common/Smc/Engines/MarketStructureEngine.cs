using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for detecting Market Structure Pivots, BOS (Break of Structure),
    /// ChoCH (Change of Character), and MSS (Market Structure Shift).
    /// </summary>
    public class MarketStructureEngine
    {
        private readonly List<PivotPoint> _pivots = new List<PivotPoint>();
        private readonly List<StructureEvent> _events = new List<StructureEvent>();

        public int PivotPeriod { get; set; } = 2;
        public bool RequireBodyClose { get; set; } = true;

        private TradeType? _lastDirection;

        public PivotPoint CurrentSwingHigh { get; private set; }
        public PivotPoint CurrentSwingLow { get; private set; }
        public BreakType LastBreakType { get; private set; }
        public TradeType LastDirection => _lastDirection ?? TradeType.Buy;
        public StructureEvent LatestEvent => _events.LastOrDefault();

        public IReadOnlyList<PivotPoint> Pivots => _pivots.AsReadOnly();
        public IReadOnlyList<StructureEvent> Events => _events.AsReadOnly();

        public void Update(Bars bars, int currBarIndex = -1, IEnumerable<FairValueGap> activeFvgs = null)
        {
            if (bars == null || bars.Count < (PivotPeriod * 2 + 1))
                return;

            if (currBarIndex < 0)
                currBarIndex = bars.Count - 1;

            if (currBarIndex < PivotPeriod * 2 || currBarIndex >= bars.Count)
                return;

            int pivotCandidateIndex = currBarIndex - PivotPeriod;
            DetectPivotAt(bars, pivotCandidateIndex);
            CheckStructureBreak(bars, currBarIndex, activeFvgs);
        }

        private void DetectPivotAt(Bars bars, int index)
        {
            if (index < PivotPeriod || index >= bars.Count - PivotPeriod)
                return;

            double candHigh = bars.HighPrices[index];
            double candLow = bars.LowPrices[index];

            bool isHigh = true;
            bool isLow = true;

            for (int i = 1; i <= PivotPeriod; i++)
            {
                if (bars.HighPrices[index - i] >= candHigh || bars.HighPrices[index + i] >= candHigh)
                    isHigh = false;
                if (bars.LowPrices[index - i] <= candLow || bars.LowPrices[index + i] <= candLow)
                    isLow = false;
            }

            if (isHigh)
            {
                var pivot = new PivotPoint
                {
                    Index = index,
                    Time = bars.OpenTimes[index],
                    Price = candHigh,
                    Type = StructureType.SwingHigh,
                    IsMajor = true
                };
                _pivots.Add(pivot);
                CurrentSwingHigh = pivot;
            }

            if (isLow)
            {
                var pivot = new PivotPoint
                {
                    Index = index,
                    Time = bars.OpenTimes[index],
                    Price = candLow,
                    Type = StructureType.SwingLow,
                    IsMajor = true
                };
                _pivots.Add(pivot);
                CurrentSwingLow = pivot;
            }
        }

        private void CheckStructureBreak(Bars bars, int barIndex, IEnumerable<FairValueGap> activeFvgs)
        {
            if (CurrentSwingHigh == null || CurrentSwingLow == null)
                return;

            double testPrice = RequireBodyClose ? bars.ClosePrices[barIndex] : bars.HighPrices[barIndex];
            double testLowPrice = RequireBodyClose ? bars.ClosePrices[barIndex] : bars.LowPrices[barIndex];

            if (testPrice > CurrentSwingHigh.Price)
            {
                BreakType bType = BreakType.BOS;

                if (_lastDirection.HasValue && _lastDirection.Value == TradeType.Sell)
                {
                    // Check if reversal is accompanied by FVG (ICT MSS Rule)
                    bool hasFvg = activeFvgs != null && activeFvgs.Any(f => Math.Abs(f.CreatedBarIndex - barIndex) <= 2);
                    bType = hasFvg ? BreakType.MSS : BreakType.ChoCH;
                }
                
                var evt = new StructureEvent
                {
                    Type = bType,
                    Direction = TradeType.Buy,
                    BrokenPivot = CurrentSwingHigh,
                    TriggerBarIndex = barIndex,
                    TriggerTime = bars.OpenTimes[barIndex],
                    IsBodyBreak = RequireBodyClose
                };

                _events.Add(evt);
                LastBreakType = bType;
                _lastDirection = TradeType.Buy;
                CurrentSwingHigh = null; // Reset to prevent continuous duplicate event emission
            }
            else if (testLowPrice < CurrentSwingLow.Price)
            {
                BreakType bType = BreakType.BOS;

                if (_lastDirection.HasValue && _lastDirection.Value == TradeType.Buy)
                {
                    // Check if reversal is accompanied by FVG (ICT MSS Rule)
                    bool hasFvg = activeFvgs != null && activeFvgs.Any(f => Math.Abs(f.CreatedBarIndex - barIndex) <= 2);
                    bType = hasFvg ? BreakType.MSS : BreakType.ChoCH;
                }

                var evt = new StructureEvent
                {
                    Type = bType,
                    Direction = TradeType.Sell,
                    BrokenPivot = CurrentSwingLow,
                    TriggerBarIndex = barIndex,
                    TriggerTime = bars.OpenTimes[barIndex],
                    IsBodyBreak = RequireBodyClose
                };

                _events.Add(evt);
                LastBreakType = bType;
                _lastDirection = TradeType.Sell;
                CurrentSwingLow = null; // Reset to prevent continuous duplicate event emission
            }
        }

        public void Reset()
        {
            _pivots.Clear();
            _events.Clear();
            CurrentSwingHigh = null;
            CurrentSwingLow = null;
            _lastDirection = null;
        }
    }
}
