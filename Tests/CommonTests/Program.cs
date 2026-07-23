using System;

namespace CommonTests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  RUNNING ALL 15 UNIT TEST SUITES FOR REDWAVE COMMON");
            Console.WriteLine("=================================================");

            TestRunner.RunSuite("VolumeProfileV2 Suite", VolumeProfileV2Tests.RunAll);
            TestRunner.RunSuite("VolumeProfile (V1) Suite", VolumeProfileTests.RunAll);
            TestRunner.RunSuite("TradeExecutor Suite", TradeExecutorTests.RunAll);
            TestRunner.RunSuite("TrailingManager Suite", TrailingManagerTests.RunAll);
            TestRunner.RunSuite("MarketCondition Suite", MarketConditionTests.RunAll);
            TestRunner.RunSuite("SessionFilter Suite", SessionFilterTests.RunAll);
            TestRunner.RunSuite("RiskManager Suite", RiskManagerTests.RunAll);
            TestRunner.RunSuite("PriceUtils Suite", PriceUtilsTests.RunAll);
            TestRunner.RunSuite("TimeFilter Suite", TimeFilterTests.RunAll);
            TestRunner.RunSuite("TickDeltaEngine Suite", TickDeltaEngineTests.RunAll);
            TestRunner.RunSuite("TickVolumeProfiler Suite", TickVolumeProfilerTests.RunAll);
            TestRunner.RunSuite("ProfileData Suite", ProfileDataTests.RunAll);
            TestRunner.RunSuite("NewsFilter Suite", NewsFilterTests.RunAll);
            TestRunner.RunSuite("WyckoffWaveEngine Suite", WyckoffWaveEngineTests.RunAll);
            TestRunner.RunSuite("SmcEngine Suite", SmcEngineTests.RunAll);
            TestRunner.RunSuite("Logger Suite", LoggerTests.RunAll);

            TestRunner.PrintSummary();
        }
    }
}
