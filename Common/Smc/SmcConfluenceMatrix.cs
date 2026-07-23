using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Facade class aggregating all 5 core SMC/ICT engines into a unified signal matrix.
    /// </summary>
    public class SmcConfluenceMatrix
    {
        public MarketStructureEngine StructureEngine { get; }
        public FvgEngine FvgEngine { get; }
        public LiquidityEngine LiquidityEngine { get; }
        public OrderBlockEngine ObEngine { get; }
        public DealingRangeEngine RangeEngine { get; }

        public SmcConfluenceMatrix()
        {
            StructureEngine = new MarketStructureEngine();
            FvgEngine = new FvgEngine();
            LiquidityEngine = new LiquidityEngine();
            ObEngine = new OrderBlockEngine();
            RangeEngine = new DealingRangeEngine();
        }

        /// <summary>
        /// Update all child engines on a bar event.
        /// Pass optional barIndex for historical back-filling.
        /// </summary>
        public void OnBar(Bars bars, int barIndex = -1, double pipSize = 0.0001)
        {
            FvgEngine.Update(bars, barIndex, pipSize);
            StructureEngine.Update(bars, barIndex, FvgEngine.AllFvgs);
            LiquidityEngine.Update(bars, barIndex, pipSize);
            ObEngine.Update(bars, FvgEngine.ActiveFvgs, barIndex);
            RangeEngine.Update(StructureEngine.CurrentSwingHigh, StructureEngine.CurrentSwingLow);
        }

        public bool IsValidBuySetup(double currentPrice, out FairValueGap targetFvg, out OrderBlock targetOb)
        {
            targetFvg = null;
            targetOb = null;

            if (!RangeEngine.IsInDiscount(currentPrice))
                return false;

            if (StructureEngine.LastDirection != TradeType.Buy)
                return false;

            targetFvg = FvgEngine.GetLatestBuyFvg();
            targetOb = ObEngine.GetPrimaryBuyOb();

            return targetFvg != null || targetOb != null;
        }

        public bool IsValidSellSetup(double currentPrice, out FairValueGap targetFvg, out OrderBlock targetOb)
        {
            targetFvg = null;
            targetOb = null;

            if (!RangeEngine.IsInPremium(currentPrice))
                return false;

            if (StructureEngine.LastDirection != TradeType.Sell)
                return false;

            targetFvg = FvgEngine.GetLatestSellFvg();
            targetOb = ObEngine.GetPrimarySellOb();

            return targetFvg != null || targetOb != null;
        }

        public void Reset()
        {
            StructureEngine.Reset();
            FvgEngine.Reset();
            LiquidityEngine.Reset();
            ObEngine.Reset();
        }
    }
}
