using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for detecting the ICT Unicorn Setup (High-probability confluence of Breaker Block / OB + FVG overlap).
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

            var activeObs = orderBlocks.Where(ob => !ob.IsMitigated).ToList();
            var activeFvgs = fvgs.Where(f => f.Status == FvgStatus.Active || f.Status == FvgStatus.PartiallyFilled || f.Status == FvgStatus.Inversion).ToList();

            foreach (var ob in activeObs)
            {
                foreach (var fvg in activeFvgs)
                {
                    if (ob.Direction != fvg.Direction)
                        continue;

                    // Check for price range overlap between Order/Breaker Block and FVG
                    double overlapTop = Math.Min(ob.TopPrice, fvg.TopPrice);
                    double overlapBottom = Math.Max(ob.BottomPrice, fvg.BottomPrice);

                    if (overlapTop > overlapBottom) // Valid geographic overlap
                    {
                        if (!_unicorns.Any(u => u.BreakerBlock.Id == ob.Id && u.Fvg.Id == fvg.Id))
                        {
                            _unicorns.Add(new UnicornSetup
                            {
                                Id = ++_idCounter,
                                Direction = ob.Direction,
                                BreakerBlock = ob,
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
