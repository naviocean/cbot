using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for detecting the ICT Unicorn Setup (High-probability confluence of Breaker Block + FVG overlap).
    /// </summary>
    public class IctUnicornDetector
    {
        private readonly List<UnicornSetup> _unicorns = new List<UnicornSetup>();
        private int _idCounter = 0;

        public IReadOnlyList<UnicornSetup> DetectedUnicorns => _unicorns.AsReadOnly();

        public void Update(IEnumerable<OrderBlock> orderBlocks, IEnumerable<FairValueGap> fvgs)
        {
            if (orderBlocks == null || fvgs == null)
                return;

            var breakers = orderBlocks.Where(ob => ob.Type == ObType.BreakerBlock && !ob.IsMitigated).ToList();
            var activeFvgs = fvgs.Where(f => f.Status == FvgStatus.Active || f.Status == FvgStatus.PartiallyFilled).ToList();

            foreach (var breaker in breakers)
            {
                foreach (var fvg in activeFvgs)
                {
                    if (breaker.Direction != fvg.Direction)
                        continue;

                    // Check for price range overlap between Breaker Block and FVG
                    double overlapTop = Math.Min(breaker.TopPrice, fvg.TopPrice);
                    double overlapBottom = Math.Max(breaker.BottomPrice, fvg.BottomPrice);

                    if (overlapTop > overlapBottom) // Valid geographic overlap
                    {
                        string existingKey = $"{breaker.Id}_{fvg.Id}";
                        if (!_unicorns.Any(u => u.BreakerBlock.Id == breaker.Id && u.Fvg.Id == fvg.Id))
                        {
                            _unicorns.Add(new UnicornSetup
                            {
                                Id = ++_idCounter,
                                Direction = breaker.Direction,
                                BreakerBlock = breaker,
                                Fvg = fvg,
                                OverlapTopPrice = overlapTop,
                                OverlapBottomPrice = overlapBottom,
                                DetectedTime = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
        }

        public UnicornSetup GetLatestBuyUnicorn()
        {
            return _unicorns.LastOrDefault(u => u.Direction == TradeType.Buy && !u.BreakerBlock.IsMitigated && u.Fvg.Status != FvgStatus.Mitigated);
        }

        public UnicornSetup GetLatestSellUnicorn()
        {
            return _unicorns.LastOrDefault(u => u.Direction == TradeType.Sell && !u.BreakerBlock.IsMitigated && u.Fvg.Status != FvgStatus.Mitigated);
        }

        public void Reset()
        {
            _unicorns.Clear();
            _idCounter = 0;
        }
    }
}
