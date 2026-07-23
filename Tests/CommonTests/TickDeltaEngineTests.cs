using System;
using RedWave.Common;

namespace CommonTests
{
    public static class TickDeltaEngineTests
    {
        public static void RunAll()
        {
            Test_TickIngestion_And_Cvd();
            Test_Imbalance_Calculation();
            Test_ZeroTicks_Ignored();
            Test_CvdSlope_Calculation();
        }

        private static void Test_TickIngestion_And_Cvd()
        {
            var engine = new CTickDeltaEngine();
            engine.Init(maxSamples: 1000);

            DateTime now = DateTime.UtcNow;

            // Feed ticks: Initial mid = 2000.0
            engine.OnTick(1999.9, 2000.1, now);

            // Uptick (+1 Buy) -> mid = 2001.0
            engine.OnTick(2000.9, 2001.1, now.AddMilliseconds(100));

            // Uptick (+1 Buy) -> mid = 2002.0
            engine.OnTick(2001.9, 2002.1, now.AddMilliseconds(200));

            // Downtick (-1 Sell) -> mid = 2001.5
            engine.OnTick(2001.4, 2001.6, now.AddMilliseconds(300));

            // CVD = Buy (2) - Sell (1) = 1.0
            double cvd = engine.GetCvd(windowMs: 5000);
            int ticks = engine.GetTickCount(windowMs: 5000);

            TestRunner.Assert(cvd == 1.0, "TickDeltaEngine - Net CVD equals 2 buys - 1 sell = +1.0");
            TestRunner.Assert(ticks == 3, "TickDeltaEngine - Total valid ticks in window equals 3");
        }

        private static void Test_Imbalance_Calculation()
        {
            var engine = new CTickDeltaEngine();
            engine.Init(maxSamples: 1000);

            DateTime now = DateTime.UtcNow;
            engine.OnTick(100.0, 100.0, now);

            // 20 Upticks
            for (int i = 1; i <= 20; i++)
            {
                engine.OnTick(100.0 + i * 0.1, 100.0 + i * 0.1, now.AddMilliseconds(i * 10));
            }

            // 5 Downticks
            for (int i = 1; i <= 5; i++)
            {
                engine.OnTick(102.0 - i * 0.1, 102.0 - i * 0.1, now.AddMilliseconds(250 + i * 10));
            }

            // Buy = 20, Sell = 5 => Imbalance = 20 / 5 = 4.0
            double imbalance = engine.GetImbalance(windowMs: 5000, minTicks: 10);
            double sellImbalance = engine.GetSellImbalance(windowMs: 5000, minTicks: 10);

            TestRunner.Assert(imbalance == 4.0, "TickDeltaEngine - Buy/Sell imbalance equals 20/5 = 4.0");
            TestRunner.Assert(sellImbalance == 0.25, "TickDeltaEngine - Sell/Buy imbalance equals 5/20 = 0.25");
        }

        private static void Test_ZeroTicks_Ignored()
        {
            var engine = new CTickDeltaEngine();
            engine.Init(maxSamples: 1000);

            DateTime now = DateTime.UtcNow;
            engine.OnTick(100.0, 100.0, now);

            // Repeat exact same price 10 times -> zero delta
            for (int i = 1; i <= 10; i++)
            {
                engine.OnTick(100.0, 100.0, now.AddMilliseconds(i * 10));
            }

            TestRunner.Assert(engine.GetTickCount(5000) == 0, "TickDeltaEngine - Zero delta price ticks ignored");
        }

        private static void Test_CvdSlope_Calculation()
        {
            var engine = new CTickDeltaEngine();
            engine.Init(maxSamples: 1000);

            DateTime now = DateTime.UtcNow;
            engine.OnTick(100.0, 100.0, now.AddSeconds(-10));

            // Old half (10s to 5s ago): 2 buys
            engine.OnTick(101.0, 101.0, now.AddSeconds(-8));
            engine.OnTick(102.0, 102.0, now.AddSeconds(-6));

            // Recent half (5s to 0s ago): 5 buys
            engine.OnTick(103.0, 103.0, now.AddSeconds(-4));
            engine.OnTick(104.0, 104.0, now.AddSeconds(-3));
            engine.OnTick(105.0, 105.0, now.AddSeconds(-2));
            engine.OnTick(106.0, 106.0, now.AddSeconds(-1));
            engine.OnTick(107.0, 107.0, now);

            double slope = engine.GetCvdSlope(windowMs: 10000);
            TestRunner.Assert(slope > 0, "TickDeltaEngine - Accelerating buy volume produces positive CVD slope");
        }
    }
}
