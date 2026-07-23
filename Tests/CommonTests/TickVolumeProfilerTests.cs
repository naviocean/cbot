using System;
using RedWave.Common;

namespace CommonTests
{
    public static class TickVolumeProfilerTests
    {
        public static void RunAll()
        {
            Test_TickIngestion_And_SlidingWindow();
            Test_AverageTicksPerWindow();
            Test_PriceDelta();
        }

        private static void Test_TickIngestion_And_SlidingWindow()
        {
            var profiler = new CTickVolumeProfiler();
            profiler.Init(symbol: "XAUUSD", windowMs: 5000); // 5s sliding window

            DateTime now = DateTime.UtcNow;

            // Tick 1 at 0s
            profiler.OnTick(2000.0, 2000.2, now.AddSeconds(-4));

            // Tick 2 at 2s
            profiler.OnTick(2001.0, 2001.2, now.AddSeconds(-2));

            // Tick 3 at 4s
            profiler.OnTick(2002.0, 2002.2, now);

            int ticks5s = profiler.GetTicksInWindow(5000);
            int ticks2s = profiler.GetTicksInWindow(2000);

            TestRunner.Assert(ticks5s == 3, "TickVolumeProfiler - 3 ticks inside 5s window");
            TestRunner.Assert(ticks2s >= 1, "TickVolumeProfiler - Ticks inside 2s window counted correctly");
        }

        private static void Test_AverageTicksPerWindow()
        {
            var profiler = new CTickVolumeProfiler();
            profiler.Init(symbol: "XAUUSD", windowMs: 10000);

            DateTime now = DateTime.UtcNow;
            profiler.OnTick(100.0, 100.0, now.AddSeconds(-10));

            for (int i = 1; i <= 10; i++)
            {
                profiler.OnTick(100.0 + i, 100.0 + i, now.AddSeconds(-10 + i));
            }

            double avgTicks10s = profiler.GetAverageTicksPerWindow(10000);
            TestRunner.Assert(avgTicks10s > 0, "TickVolumeProfiler - Calculated average ticks per window > 0");
        }

        private static void Test_PriceDelta()
        {
            var profiler = new CTickVolumeProfiler();
            profiler.Init(symbol: "EURUSD", windowMs: 10000);

            DateTime now = DateTime.UtcNow;
            profiler.OnTick(1.1000, 1.1002, now.AddSeconds(-5)); // Price mid = 1.1001
            profiler.OnTick(1.1050, 1.1052, now);               // Price mid = 1.1051

            double delta = profiler.GetPriceDelta(10000); // 1.1051 - 1.1001 = 0.0050
            TestRunner.Assert(Math.Abs(delta - 0.0050) < 1e-6, "TickVolumeProfiler - Price delta calculation over window");
        }
    }
}
