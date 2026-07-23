using System;
using cAlgo.API;
using RedWave.Common;

namespace CommonTests
{
    public static class MarketConditionTests
    {
        public static void RunAll()
        {
            Test_SpreadCheck_Disabled();
            Test_SpreadCheck_Logic();
            Test_NullSymbol_Safety();
        }

        private static void Test_SpreadCheck_Disabled()
        {
            var mc = new CMarketCondition();
            mc.SetSpreadCheck(enable: false, maxSpreadPips: 50.0);

            TestRunner.Assert(!mc.IsTradingOK(), "MarketCondition - Uninitialized symbol returns false");
        }

        private static void Test_SpreadCheck_Logic()
        {
            var mc = new CMarketCondition();
            mc.SetSpreadCheck(enable: true, maxSpreadPips: 2.0);

            double maxSpreadPips = 2.0;
            double normalSpreadPips = 1.5;
            double highSpreadPips = 2.5;

            TestRunner.Assert(normalSpreadPips <= maxSpreadPips, "MarketCondition - Normal spread (1.5 pips) within max (2.0 pips)");
            TestRunner.Assert(highSpreadPips > maxSpreadPips, "MarketCondition - High spread (2.5 pips) exceeds max (2.0 pips)");
        }

        private static void Test_NullSymbol_Safety()
        {
            var mc = new CMarketCondition();
            bool initialized = mc.Init(null);

            TestRunner.Assert(!initialized, "MarketCondition - Init with null symbol returns false");
            TestRunner.Assert(!mc.IsTradingOK(), "MarketCondition - IsTradingOK with null symbol returns false");
        }
    }
}
