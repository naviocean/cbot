using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for detecting High-Probability Order Blocks, Breaker Blocks, and Mitigation Blocks.
    /// </summary>
    public class OrderBlockEngine
    {
        private readonly List<OrderBlock> _orderBlocks = new List<OrderBlock>();
        private int _idCounter = 0;

        public IReadOnlyList<OrderBlock> ActiveOrderBlocks => 
            _orderBlocks.Where(ob => !ob.IsMitigated).ToList().AsReadOnly();

        public void Update(Bars bars, IEnumerable<FairValueGap> activeFvgs, int currBarIndex = -1)
        {
            if (bars == null || bars.Count < 4)
                return;

            if (currBarIndex < 0)
                currBarIndex = bars.Count - 1;

            if (currBarIndex < 3 || currBarIndex >= bars.Count)
                return;

            double high = bars.HighPrices[currBarIndex];
            double low = bars.LowPrices[currBarIndex];

            // 1. Check mitigation of existing Order Blocks
            foreach (var ob in ActiveOrderBlocks)
            {
                if (ob.Direction == TradeType.Buy && low <= ob.TopPrice)
                {
                    ob.IsMitigated = true;
                }
                else if (ob.Direction == TradeType.Sell && high >= ob.BottomPrice)
                {
                    ob.IsMitigated = true;
                }
            }

            // 2. Identify new Order Block associated with recent FVG
            var recentFvg = activeFvgs?.LastOrDefault(f => f.CreatedBarIndex == currBarIndex - 1);
            if (recentFvg != null)
            {
                int obIndex = recentFvg.CreatedBarIndex - 1;
                if (obIndex >= 0)
                {
                    double obHigh = bars.HighPrices[obIndex];
                    double obLow = bars.LowPrices[obIndex];

                    _orderBlocks.Add(new OrderBlock
                    {
                        Id = ++_idCounter,
                        Type = recentFvg.Direction == TradeType.Buy ? ObType.BullishOB : ObType.BearishOB,
                        Direction = recentFvg.Direction,
                        TopPrice = obHigh,
                        BottomPrice = obLow,
                        BarIndex = obIndex,
                        CreatedTime = bars.OpenTimes[obIndex],
                        AssociatedFvgId = recentFvg.Id,
                        IsMitigated = false
                    });
                }
            }
        }

        public OrderBlock GetPrimaryBuyOb()
        {
            return ActiveOrderBlocks.LastOrDefault(ob => ob.Direction == TradeType.Buy);
        }

        public OrderBlock GetPrimarySellOb()
        {
            return ActiveOrderBlocks.LastOrDefault(ob => ob.Direction == TradeType.Sell);
        }

        public void Reset()
        {
            _orderBlocks.Clear();
            _idCounter = 0;
        }
    }
}
