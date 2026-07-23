using System;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for calculating Fibonacci Dealing Range (Equilibrium 50%, Premium, Discount, OTE).
    /// </summary>
    public class DealingRangeEngine
    {
        public double SwingHigh { get; private set; }
        public double SwingLow { get; private set; }
        public double Equilibrium => (SwingHigh + SwingLow) / 2.0;
        public double OteHigh => SwingLow + (SwingHigh - SwingLow) * 0.79;
        public double OteLow => SwingLow + (SwingHigh - SwingLow) * 0.618;

        public void Update(PivotPoint currentSwingHigh, PivotPoint currentSwingLow)
        {
            if (currentSwingHigh != null) SwingHigh = currentSwingHigh.Price;
            if (currentSwingLow != null) SwingLow = currentSwingLow.Price;
        }

        public MarketZone GetZone(double price)
        {
            if (SwingHigh <= 0 || SwingLow <= 0) return MarketZone.Equilibrium;
            if (price < Equilibrium) return MarketZone.Discount;
            if (price > Equilibrium) return MarketZone.Premium;
            return MarketZone.Equilibrium;
        }

        public bool IsInDiscount(double price)
        {
            return GetZone(price) == MarketZone.Discount;
        }

        public bool IsInPremium(double price)
        {
            return GetZone(price) == MarketZone.Premium;
        }
    }
}
