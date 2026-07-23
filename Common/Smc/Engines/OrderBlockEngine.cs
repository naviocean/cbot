using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for detecting High-Probability ICT Order Blocks, Breaker Blocks, and Mitigation Blocks.
    /// Handles automatic conversion of broken Order Blocks into Breaker Blocks.
    /// </summary>
    public class OrderBlockEngine
    {
        private readonly List<OrderBlock> _orderBlocks = new List<OrderBlock>();
        private int _idCounter = 0;

        /// <summary>
        /// Maximum number of active Order Blocks to keep per direction (prevents visual clutter).
        /// </summary>
        public int MaxActiveObPerDirection { get; set; } = 3;

        /// <summary>
        /// Enable automatic conversion of broken OBs into ICT Breaker Blocks.
        /// </summary>
        public bool EnableBreakerBlocks { get; set; } = true;

        /// <summary>
        /// Use candle body bounds (Open to Close) for clean ICT OB boxes.
        /// </summary>
        public bool UseBodyBounds { get; set; } = true;

        /// <summary>
        /// Active (unmitigated) Order Blocks, capped at MaxActiveObPerDirection for clean chart rendering.
        /// </summary>
        public IEnumerable<OrderBlock> ActiveOrderBlocks
        {
            get
            {
                var buyObs = _orderBlocks.Where(ob => !ob.IsMitigated && ob.Direction == TradeType.Buy)
                                         .TakeLast(MaxActiveObPerDirection);
                var sellObs = _orderBlocks.Where(ob => !ob.IsMitigated && ob.Direction == TradeType.Sell)
                                          .TakeLast(MaxActiveObPerDirection);
                return buyObs.Concat(sellObs);
            }
        }

        public IEnumerable<OrderBlock> BreakerBlocks =>
            _orderBlocks.Where(ob => ob.Type == ObType.BreakerBlock && !ob.IsMitigated);

        public IReadOnlyList<OrderBlock> AllOrderBlocks => _orderBlocks.AsReadOnly();

        public void Update(Bars bars, IEnumerable<FairValueGap> activeFvgs, IEnumerable<StructureEvent> structureEvents, int currBarIndex = -1)
        {
            if (bars == null || bars.Count < 4)
                return;

            if (currBarIndex < 0)
                currBarIndex = bars.Count - 1;

            if (currBarIndex < 3 || currBarIndex >= bars.Count)
                return;

            double high = bars.HighPrices[currBarIndex];
            double low = bars.LowPrices[currBarIndex];
            double close = bars.ClosePrices[currBarIndex];

            // 1. Check mitigation and Breaker Block conversion of existing active Order Blocks
            foreach (var ob in _orderBlocks.Where(ob => !ob.IsMitigated).ToList())
            {
                if (ob.Type == ObType.BreakerBlock)
                {
                    if (ob.Direction == TradeType.Buy && low <= ob.TopPrice) ob.IsMitigated = true;
                    else if (ob.Direction == TradeType.Sell && high >= ob.BottomPrice) ob.IsMitigated = true;
                    continue;
                }

                if (ob.Direction == TradeType.Buy)
                {
                    // Price closed below Bullish OB bottom -> Converts to Bearish Breaker Block
                    if (close < ob.BottomPrice)
                    {
                        if (EnableBreakerBlocks)
                        {
                            ob.Type = ObType.BreakerBlock;
                            ob.Direction = TradeType.Sell; // Flipped to Bearish Breaker Block (Resistance)
                            ob.IsMitigated = false;
                        }
                        else
                        {
                            ob.IsMitigated = true;
                        }
                    }
                    else if (low <= ob.TopPrice)
                    {
                        ob.IsMitigated = true;
                    }
                }
                else // Bearish OB
                {
                    // Price closed above Bearish OB top -> Converts to Bullish Breaker Block
                    if (close > ob.TopPrice)
                    {
                        if (EnableBreakerBlocks)
                        {
                            ob.Type = ObType.BreakerBlock;
                            ob.Direction = TradeType.Buy; // Flipped to Bullish Breaker Block (Support)
                            ob.IsMitigated = false;
                        }
                        else
                        {
                            ob.IsMitigated = true;
                        }
                    }
                    else if (high >= ob.BottomPrice)
                    {
                        ob.IsMitigated = true;
                    }
                }
            }

            // 2. Identify new High-Probability Order Block (Must be backed by FVG + Structure Break BOS/ChoCH/MSS)
            var recentFvg = activeFvgs?.LastOrDefault(f => f.CreatedBarIndex == currBarIndex - 1 && !f.IsInversion);
            if (recentFvg != null)
            {
                bool hasStructureBreak = structureEvents != null && 
                    structureEvents.Any(e => Math.Abs(e.TriggerBarIndex - recentFvg.CreatedBarIndex) <= 3 && e.Direction == recentFvg.Direction);

                if (hasStructureBreak)
                {
                    int obIndex = recentFvg.CreatedBarIndex - 1;
                    if (obIndex >= 0)
                    {
                        double obOpen = bars.OpenPrices[obIndex];
                        double obClose = bars.ClosePrices[obIndex];
                        double obHigh = bars.HighPrices[obIndex];
                        double obLow = bars.LowPrices[obIndex];

                        bool isValidBullishOb = recentFvg.Direction == TradeType.Buy && obClose <= obOpen;
                        bool isValidBearishOb = recentFvg.Direction == TradeType.Sell && obClose >= obOpen;

                        if (isValidBullishOb || isValidBearishOb)
                        {
                            if (!_orderBlocks.Any(ob => ob.BarIndex == obIndex))
                            {
                                double topPrice = UseBodyBounds ? Math.Max(obOpen, obClose) : obHigh;
                                double bottomPrice = UseBodyBounds ? Math.Min(obOpen, obClose) : obLow;

                                _orderBlocks.Add(new OrderBlock
                                {
                                    Id = ++_idCounter,
                                    Type = recentFvg.Direction == TradeType.Buy ? ObType.BullishOB : ObType.BearishOB,
                                    Direction = recentFvg.Direction,
                                    TopPrice = topPrice,
                                    BottomPrice = bottomPrice,
                                    BarIndex = obIndex,
                                    CreatedTime = bars.OpenTimes[obIndex],
                                    AssociatedFvgId = recentFvg.Id,
                                    IsMitigated = false
                                });
                            }
                        }
                    }
                }
            }

            if (_orderBlocks.Count > 100)
            {
                _orderBlocks.RemoveAll(ob => ob.IsMitigated);
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
