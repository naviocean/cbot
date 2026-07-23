using System;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Facade class aggregating all core SMC/ICT engines into a unified signal matrix.
    /// </summary>
    public class SmcConfluenceMatrix
    {
        public MarketStructureEngine StructureEngine { get; }
        public FvgEngine FvgEngine { get; }
        public LiquidityEngine LiquidityEngine { get; }
        public OrderBlockEngine ObEngine { get; }
        public DealingRangeEngine RangeEngine { get; }
        public NwogEngine NwogEngine { get; }
        public IctUnicornDetector UnicornDetector { get; }
        public SessionEngine SessionEngine { get; }
        public BprEngine BprEngine { get; }
        public PowerOfThreeEngine Po3Engine { get; }
        public DailyBiasEngine BiasEngine { get; }

        public MtfBias HTFBias { get; set; }
        public bool EnableMtfFilter { get; set; } = true;
        public bool EnablePo3Filter { get; set; } = false;
        public bool EnableBiasFilter { get; set; } = false;
        public bool EnableKillZoneFilter { get; set; } = false;

        public SmcConfluenceMatrix()
        {
            StructureEngine = new MarketStructureEngine();
            FvgEngine = new FvgEngine();
            LiquidityEngine = new LiquidityEngine();
            ObEngine = new OrderBlockEngine();
            RangeEngine = new DealingRangeEngine();
            NwogEngine = new NwogEngine();
            UnicornDetector = new IctUnicornDetector();
            SessionEngine = new SessionEngine();
            BprEngine = new BprEngine();
            Po3Engine = new PowerOfThreeEngine();
            BiasEngine = new DailyBiasEngine();
        }

        /// <summary>
        /// Update all child engines on a bar event.
        /// Pass optional barIndex for historical back-filling.
        /// </summary>
        public void OnBar(Bars bars, int barIndex = -1, double pipSize = 0.0001)
        {
            DateTime? barTime = null;
            double recentClose = 0;
            if (bars != null && bars.Count > 0)
            {
                int idx = (barIndex >= 0 && barIndex < bars.Count) ? barIndex : bars.Count - 1;
                barTime = bars.OpenTimes[idx];
                double barHigh = bars.HighPrices[idx];
                double barLow = bars.LowPrices[idx];
                recentClose = bars.ClosePrices[idx];
                SessionEngine.Update(barTime.Value, barHigh, barLow);
            }

            LiquidityEngine.Update(bars, barIndex, pipSize, barTime);
            LiquidityEngine.SetSessionLevels(SessionEngine.AsianHigh, SessionEngine.AsianLow);
            FvgEngine.Update(bars, barIndex, pipSize);
            BprEngine.Update(FvgEngine.AllFvgs, pipSize, recentClose);
            StructureEngine.Update(bars, barIndex, FvgEngine.AllFvgs);
            ObEngine.Update(bars, FvgEngine.ActiveFvgs, StructureEngine.Events, barIndex);
            RangeEngine.Update(StructureEngine.CurrentSwingHigh, StructureEngine.CurrentSwingLow);
            NwogEngine.Update(bars, barIndex, pipSize);
            UnicornDetector.Update(ObEngine.ActiveOrderBlocks, FvgEngine.ActiveFvgs, barTime);
            Po3Engine.Update(SessionEngine, LiquidityEngine, pipSize, barTime);
            BiasEngine.Update(HTFBias, RangeEngine, LiquidityEngine, SessionEngine, recentClose, barTime);
        }

        public MtfBias GetBias(double currentPrice = 0)
        {
            return new MtfBias
            {
                IsValid = StructureEngine.HasDirection,
                Direction = StructureEngine.LastDirection,
                LastHTFBreak = StructureEngine.LatestEvent?.Type ?? BreakType.BOS,
                HTFZone = RangeEngine.GetZone(currentPrice),
                UpdatedAt = DateTime.UtcNow
            };
        }

        public bool IsValidBuySetup(double currentPrice, out FairValueGap targetFvg, out OrderBlock targetOb)
        {
            targetFvg = null;
            targetOb = null;

            if (EnableKillZoneFilter && !SessionEngine.IsInKillZone)
                return false;

            if (EnableBiasFilter && BiasEngine.TodayBias == BiasType.SellBias)
                return false;

            if (EnableMtfFilter && HTFBias != null && HTFBias.IsValid && HTFBias.Direction != TradeType.Buy)
                return false;

            if (EnablePo3Filter && Po3Engine.IsSetupValid && Po3Engine.DistributionDirection != TradeType.Buy)
                return false;

            if (!RangeEngine.IsInDiscount(currentPrice))
                return false;

            if (StructureEngine.LastDirection != TradeType.Buy)
                return false;

            targetFvg = FvgEngine.GetLatestBuyFvg();
            targetOb = ObEngine.GetPrimaryBuyOb();

            return targetFvg != null || targetOb != null || BprEngine.GetLatestBuyBpr() != null;
        }

        public bool IsValidSellSetup(double currentPrice, out FairValueGap targetFvg, out OrderBlock targetOb)
        {
            targetFvg = null;
            targetOb = null;

            if (EnableKillZoneFilter && !SessionEngine.IsInKillZone)
                return false;

            if (EnableBiasFilter && BiasEngine.TodayBias == BiasType.BuyBias)
                return false;

            if (EnableMtfFilter && HTFBias != null && HTFBias.IsValid && HTFBias.Direction != TradeType.Sell)
                return false;

            if (EnablePo3Filter && Po3Engine.IsSetupValid && Po3Engine.DistributionDirection != TradeType.Sell)
                return false;

            if (!RangeEngine.IsInPremium(currentPrice))
                return false;

            if (StructureEngine.LastDirection != TradeType.Sell)
                return false;

            targetFvg = FvgEngine.GetLatestSellFvg();
            targetOb = ObEngine.GetPrimarySellOb();

            return targetFvg != null || targetOb != null || BprEngine.GetLatestSellBpr() != null;
        }

        public void Reset()
        {
            StructureEngine.Reset();
            FvgEngine.Reset();
            LiquidityEngine.Reset();
            ObEngine.Reset();
            RangeEngine.Reset();
            NwogEngine.Reset();
            UnicornDetector.Reset();
            SessionEngine.Reset();
            BprEngine.Reset();
            Po3Engine.Reset();
            BiasEngine.Reset();
        }
    }
}
