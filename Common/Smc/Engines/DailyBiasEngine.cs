using System;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for calculating Daily Directional Bias based on a 4-condition scoring system.
    /// Conditions check HTF alignment, Dealing Range zone (Premium/Discount), PDH/PDL intactness, and Asian Midpoint relation.
    /// </summary>
    public class DailyBiasEngine
    {
        public BiasType TodayBias { get; private set; } = BiasType.Neutral;
        public double BiasScore { get; private set; } = 0.0;
        public double MinBiasScoreThreshold { get; set; } = 0.5;

        public void Update(MtfBias htfBias, DealingRangeEngine range, LiquidityEngine liquidity, SessionEngine session, double currentPrice, DateTime? barTime = null)
        {
            double buyScore = 0.0;
            double sellScore = 0.0;

            // 1. HTF Alignment (25%)
            if (htfBias != null && htfBias.IsValid)
            {
                if (htfBias.Direction == TradeType.Buy) buyScore += 0.25;
                else if (htfBias.Direction == TradeType.Sell) sellScore += 0.25;
            }

            // 2. Premium / Discount Zone (25%)
            if (range != null && currentPrice > 0)
            {
                if (range.IsInDiscount(currentPrice)) buyScore += 0.25;
                else if (range.IsInPremium(currentPrice)) sellScore += 0.25;
            }

            // 3. PDH / PDL Intactness (25%)
            if (liquidity != null)
            {
                // If PDL hasn't been swept today -> favors Buy
                if (!liquidity.HasRecentSweep(LiquidityType.PDL, withinBars: 50)) buyScore += 0.25;

                // If PDH hasn't been swept today -> favors Sell
                if (!liquidity.HasRecentSweep(LiquidityType.PDH, withinBars: 50)) sellScore += 0.25;
            }

            // 4. Asian Midpoint Relation (25%)
            if (session != null && session.AsianMidpoint > 0 && currentPrice > 0)
            {
                if (currentPrice > session.AsianMidpoint) buyScore += 0.25;
                else if (currentPrice < session.AsianMidpoint) sellScore += 0.25;
            }

            // Determine final bias
            if (buyScore >= MinBiasScoreThreshold && buyScore >= sellScore)
            {
                TodayBias = BiasType.BuyBias;
                BiasScore = buyScore;
            }
            else if (sellScore >= MinBiasScoreThreshold && sellScore > buyScore)
            {
                TodayBias = BiasType.SellBias;
                BiasScore = sellScore;
            }
            else
            {
                TodayBias = BiasType.Neutral;
                BiasScore = Math.Max(buyScore, sellScore);
            }
        }

        public void Reset()
        {
            TodayBias = BiasType.Neutral;
            BiasScore = 0.0;
        }
    }
}
