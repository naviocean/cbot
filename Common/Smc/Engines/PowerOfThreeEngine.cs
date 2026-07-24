using System;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for tracking the ICT Power of Three (PO3) market structure:
    /// Accumulation (Asian) -> Manipulation (Judas Swing sweep of Asian Range) -> Distribution (Expansion).
    /// </summary>
    public class PowerOfThreeEngine
    {
        public double MinAsianRangePips { get; set; } = 5.0;

        public Po3Phase CurrentPhase { get; private set; } = Po3Phase.None;
        public TradeType? DistributionDirection { get; private set; }
        public double ManipulationSweepPrice { get; private set; }
        public bool IsSetupValid => CurrentPhase == Po3Phase.Distribution;

        public void Update(SessionEngine session, LiquidityEngine liquidity, MarketStructureEngine structure = null, double pipSize = 0.0001, DateTime? barTime = null)
        {
            if (session == null || liquidity == null)
                return;

            double asianRangePips = (session.AsianHigh > 0 && session.AsianLow < double.MaxValue)
                ? (session.AsianHigh - session.AsianLow) / pipSize
                : 0;

            switch (CurrentPhase)
            {
                case Po3Phase.None:
                    if (session.CurrentSession == SessionType.Asian && asianRangePips >= MinAsianRangePips)
                    {
                        CurrentPhase = Po3Phase.Accumulation;
                    }
                    break;

                case Po3Phase.Accumulation:
                    if (session.CurrentSession == SessionType.London || session.CurrentSession == SessionType.NewYork)
                    {
                        if (liquidity.HasRecentSweep(LiquidityType.AsianHigh, withinBars: 10))
                        {
                            ManipulationSweepPrice = session.AsianHigh;
                            DistributionDirection = TradeType.Sell;
                            CurrentPhase = Po3Phase.Manipulation;
                        }
                        else if (liquidity.HasRecentSweep(LiquidityType.AsianLow, withinBars: 10))
                        {
                            ManipulationSweepPrice = session.AsianLow;
                            DistributionDirection = TradeType.Buy;
                            CurrentPhase = Po3Phase.Manipulation;
                        }
                    }
                    break;

                case Po3Phase.Manipulation:
                    if (DistributionDirection.HasValue)
                    {
                        if (structure == null || (structure.HasDirection && structure.LastDirection == DistributionDirection.Value))
                        {
                            CurrentPhase = Po3Phase.Distribution;
                        }
                    }
                    break;

                case Po3Phase.Distribution:
                    // Auto-reset when new Asian session starts
                    if (session.CurrentSession == SessionType.Asian && !session.AsianRangeLocked)
                    {
                        Reset();
                    }
                    break;
            }
        }

        public void Reset()
        {
            CurrentPhase = Po3Phase.None;
            DistributionDirection = null;
            ManipulationSweepPrice = 0;
        }
    }
}
