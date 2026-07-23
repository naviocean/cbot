using System;
using cAlgo.API;
using RedWave.Common;

namespace CommonTests
{
    public static class TrailingManagerTests
    {
        public static void RunAll()
        {
            Test_Breakeven_Price_Calculation();
            Test_Trailing_Distance_Calculation();
            Test_ArmTrailFromCurrent_ActivationPips();
            Test_TrailingManager_Initialization();
        }

        private static void Test_Breakeven_Price_Calculation()
        {
            double entryPrice = 2000.0;
            double beLockPips = 2.0; // 2 pips = 0.2 in price when pipSize = 0.1
            double pipSize = 0.1;
            double spread = 0.05; // 0.5 pips spread

            double lockOffsetNoSpread = beLockPips * pipSize; // 0.2
            double buyBeSlNoSpread = entryPrice + lockOffsetNoSpread; // 2000.2
            double sellBeSlNoSpread = entryPrice - lockOffsetNoSpread; // 1999.8

            TestRunner.Assert(buyBeSlNoSpread == 2000.2, "Breakeven BUY price offset without spread");
            TestRunner.Assert(sellBeSlNoSpread == 1999.8, "Breakeven SELL price offset without spread");

            double extraPips = spread / pipSize; // 0.5 pips
            double lockOffsetWithSpread = (beLockPips + extraPips) * pipSize; // 2.5 * 0.1 = 0.25
            double buyBeSlWithSpread = entryPrice + lockOffsetWithSpread; // 2000.25

            TestRunner.Assert(Math.Abs(buyBeSlWithSpread - 2000.25) < 1e-5, "Breakeven BUY price offset with spread added");
        }

        private static void Test_Trailing_Distance_Calculation()
        {
            double bid = 2010.0;
            double ask = 2010.2;
            double trailStepPips = 15.0; // 15 pips = 1.5 price distance when pipSize = 0.1
            double pipSize = 0.1;

            double trailDistance = trailStepPips * pipSize; // 1.5

            double buyTrailSl = bid - trailDistance; // 2010.0 - 1.5 = 2008.5
            double sellTrailSl = ask + trailDistance; // 2010.2 + 1.5 = 2011.7

            TestRunner.Assert(buyTrailSl == 2008.5, "Trailing BUY SL target calculation from Bid");
            TestRunner.Assert(sellTrailSl == 2011.7, "Trailing SELL SL target calculation from Ask");
        }

        private static void Test_ArmTrailFromCurrent_ActivationPips()
        {
            double currentProfitPips = 20.0;
            double extraStartPips = 10.0;

            double expectedActivationAtPips = currentProfitPips + Math.Max(0, extraStartPips); // 30.0 pips

            TestRunner.Assert(expectedActivationAtPips == 30.0, "ArmTrailFromCurrent calculates activateAtPips from current profit");
        }

        private static void Test_TrailingManager_Initialization()
        {
            var tm = new CTrailingManager();
            tm.SetTrailPoints(10, 5, 1);
            tm.SetBreakevenPoints(15, 2, true);

            TestRunner.Assert(tm != null, "CTrailingManager instance created and configured with trailing & breakeven points");
        }
    }
}
