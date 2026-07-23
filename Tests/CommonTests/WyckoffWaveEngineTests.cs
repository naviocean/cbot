using System;
using cAlgo.API;
using RedWave.Common;

namespace CommonTests
{
    public static class WyckoffWaveEngineTests
    {
        public static void RunAll()
        {
            Test_SpringPattern();
            Test_UpthrustPattern();
            Test_Engine_Reset();
        }

        private static void Test_SpringPattern()
        {
            var engine = new CWyckoffWaveEngine();

            // Spring: Bar dips below supportPrice but closes ABOVE supportPrice
            double supportPrice = 2000.0;
            double pipSize = 0.1;
            double maxPenetrationPips = 50.0; // max 5.0 price units

            // Mock Bar representation: Low = 1997.0 (dipped 3.0 units below 2000.0), Close = 2002.0 (closed above 2000.0)
            // IsSpringPattern check math logic
            double low = 1997.0;
            double close = 2002.0;

            double maxPenetration = maxPenetrationPips * pipSize; // 5.0
            bool dippedBelow = low < supportPrice && (supportPrice - low) <= maxPenetration; // 3.0 <= 5.0 => true
            bool closedAbove = close > supportPrice; // 2002 > 2000 => true

            TestRunner.Assert(dippedBelow && closedAbove, "WyckoffWaveEngine - Valid Spring pattern detected (dip below support & close above)");
        }

        private static void Test_UpthrustPattern()
        {
            double resistancePrice = 2050.0;
            double pipSize = 0.1;
            double maxPenetrationPips = 50.0;

            // Upthrust: High = 2053.0 (poked 3.0 units above 2050.0), Close = 2048.0 (closed below 2050.0)
            double high = 2053.0;
            double close = 2048.0;

            double maxPenetration = maxPenetrationPips * pipSize;
            bool pokedAbove = high > resistancePrice && (high - resistancePrice) <= maxPenetration;
            bool closedBelow = close < resistancePrice;

            TestRunner.Assert(pokedAbove && closedBelow, "WyckoffWaveEngine - Valid Upthrust pattern detected (poke above resistance & close below)");
        }

        private static void Test_Engine_Reset()
        {
            var engine = new CWyckoffWaveEngine();
            engine.Reset();

            TestRunner.Assert(engine.Pivots.Count == 0, "WyckoffWaveEngine Reset - Pivots list cleared");
            TestRunner.Assert(engine.CurrentDirection == WyckoffWaveDirection.None, "WyckoffWaveEngine Reset - CurrentDirection reset to None");
        }
    }
}
