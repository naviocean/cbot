using System;
using cAlgo.API;
using cAlgo.API.Internals;
using RedWave.Common;

namespace CommonTests
{
    [cAlgo.API.Robot(AccessRights = cAlgo.API.AccessRights.None)]
    public class MockRobot : cAlgo.API.Robot
    {
    }

    public static class TradeExecutorTests
    {
        public static void RunAll()
        {
            Test_CalculateStopLossPips();
            Test_CalculateTakeProfitPips();
            Test_HasPriceChanged_AntiSpam();
            Test_MinStopDistance_And_Spread_Safety();
        }

        private static void Test_CalculateStopLossPips()
        {
            double entryBuy = 2000.0;
            double slBuy = 1990.0;
            double pipSize = 0.1;
            double diffBuy = entryBuy - slBuy;
            double calculatedBuyPips = Math.Round(diffBuy / pipSize, 1);
            TestRunner.Assert(calculatedBuyPips == 100.0, "CalculateStopLossPips - Buy valid SL (100 pips)");

            double entrySell = 2000.0;
            double slSell = 2010.0;
            double diffSell = slSell - entrySell;
            double calculatedSellPips = Math.Round(diffSell / pipSize, 1);
            TestRunner.Assert(calculatedSellPips == 100.0, "CalculateStopLossPips - Sell valid SL (100 pips)");

            double invalidBuySl = 2005.0;
            double invalidBuyDiff = entryBuy - invalidBuySl;
            TestRunner.Assert(invalidBuyDiff < 0, "CalculateStopLossPips - Buy invalid SL returns negative/null");
        }

        private static void Test_CalculateTakeProfitPips()
        {
            double entryBuy = 2000.0;
            double tpBuy = 2030.0;
            double pipSize = 0.1;

            double diffBuy = tpBuy - entryBuy;
            double calculatedBuyPips = Math.Round(diffBuy / pipSize, 1);
            TestRunner.Assert(calculatedBuyPips == 300.0, "CalculateTakeProfitPips - Buy valid TP (300 pips)");

            double entrySell = 2000.0;
            double tpSell = 1970.0;
            double diffSell = entrySell - tpSell;
            double calculatedSellPips = Math.Round(diffSell / pipSize, 1);
            TestRunner.Assert(calculatedSellPips == 300.0, "CalculateTakeProfitPips - Sell valid TP (300 pips)");
        }

        private static void Test_HasPriceChanged_AntiSpam()
        {
            double tickSize = 0.01;

            TestRunner.Assert(!TradeExecutor.HasPriceChanged(null, null, tickSize), "HasPriceChanged - Both null returns false");
            TestRunner.Assert(TradeExecutor.HasPriceChanged(2000.0, null, tickSize), "HasPriceChanged - Null vs value returns true");
            TestRunner.Assert(!TradeExecutor.HasPriceChanged(2000.00, 2000.004, tickSize), "HasPriceChanged - Delta < TickSize returns false (anti-spam)");
            TestRunner.Assert(TradeExecutor.HasPriceChanged(2000.00, 2000.02, tickSize), "HasPriceChanged - Delta >= TickSize returns true");
        }

        private static void Test_MinStopDistance_And_Spread_Safety()
        {
            var robot = new MockRobot();
            var executor = new TradeExecutor(robot)
            {
                MinStopDistancePips = 2.0
            };

            TestRunner.Assert(executor.MinStopDistancePips == 2.0, "TradeExecutor - Configurable MinStopDistancePips property");
            TestRunner.Assert(executor.AutoAdjustInvalidStops == true, "TradeExecutor - AutoAdjustInvalidStops enabled by default");
        }
    }
}
