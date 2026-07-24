using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for detecting Balanced Price Ranges (BPR) — overlaps between Bullish and Bearish Fair Value Gaps.
    /// ICT methodology considers BPR as one of the highest-probability entry zones.
    /// </summary>
    public class BprEngine
    {
        private readonly List<BalancedPriceRange> _bprs = new List<BalancedPriceRange>();
        private int _idCounter = 0;

        public double MinOverlapPips { get; set; } = 2.0;

        public IEnumerable<BalancedPriceRange> ActiveBprs => _bprs.Where(b => !b.IsMitigated);
        public IReadOnlyList<BalancedPriceRange> AllBprs => _bprs.AsReadOnly();

        public void Update(IEnumerable<FairValueGap> fvgs, double pipSize = 0.0001, double recentClose = 0)
        {
            if (fvgs == null)
                return;

            var activeBullish = fvgs.Where(f => f.Direction == TradeType.Buy && f.Status == FvgStatus.Active).ToList();
            var activeBearish = fvgs.Where(f => f.Direction == TradeType.Sell && f.Status == FvgStatus.Active).ToList();

            foreach (var bull in activeBullish)
            {
                foreach (var bear in activeBearish)
                {
                    double overlapTop = Math.Min(bull.TopPrice, bear.TopPrice);
                    double overlapBottom = Math.Max(bull.BottomPrice, bear.BottomPrice);
                    double overlapSize = overlapTop - overlapBottom;

                    if (overlapSize > 0 && (overlapSize / pipSize) >= MinOverlapPips)
                    {
                        if (!_bprs.Any(b => b.BullishFvg.Id == bull.Id && b.BearishFvg.Id == bear.Id))
                        {
                            // Direction rule:
                            // If Bullish FVG formed FIRST -> BPR acts as Support (Buy)
                            // If Bearish FVG formed FIRST -> BPR acts as Resistance (Sell)
                            TradeType bprDir = bull.CreatedBarIndex <= bear.CreatedBarIndex ? TradeType.Buy : TradeType.Sell;

                            _bprs.Add(new BalancedPriceRange
                            {
                                Id = ++_idCounter,
                                Direction = bprDir,
                                BullishFvg = bull,
                                BearishFvg = bear,
                                OverlapTopPrice = overlapTop,
                                OverlapBottomPrice = overlapBottom,
                                IsMitigated = false,
                                DetectedBarIndex = Math.Max(bull.CreatedBarIndex, bear.CreatedBarIndex)
                            });
                        }
                    }
                }
            }

            // Update mitigation status
            if (recentClose > 0)
            {
                foreach (var bpr in _bprs.Where(b => !b.IsMitigated))
                {
                    if (bpr.Direction == TradeType.Buy && recentClose < bpr.OverlapBottomPrice)
                    {
                        bpr.IsMitigated = true;
                    }
                    else if (bpr.Direction == TradeType.Sell && recentClose > bpr.OverlapTopPrice)
                    {
                        bpr.IsMitigated = true;
                    }
                }
            }
        }

        public BalancedPriceRange GetLatestBuyBpr() => ActiveBprs.LastOrDefault(b => b.Direction == TradeType.Buy);
        public BalancedPriceRange GetLatestSellBpr() => ActiveBprs.LastOrDefault(b => b.Direction == TradeType.Sell);

        public void Reset()
        {
            _bprs.Clear();
            _idCounter = 0;
        }
    }
}
