using System;
using System.Collections.Generic;
using RedWave.Common;

namespace CommonTests
{
    public static class LoggerTests
    {
        public static void RunAll()
        {
            Test_Logger_Formatting_And_Filtering();
        }

        private static void Test_Logger_Formatting_And_Filtering()
        {
            var logger = new CLogger();
            var logs = new List<string>();

            // Init logger with LogLevel.Info (Debug logs should be filtered out)
            logger.Init("TestModule", LogLevel.Info, message => logs.Add(message));

            logger.Debug("This debug message should be skipped");
            logger.Info("This info message should be logged");
            logger.Warn("This warning message should be logged");
            logger.Error("This error message should be logged");

            TestRunner.Assert(logs.Count == 3, "CLogger - LogLevel.Info filters out Debug log (3 messages logged)");
            TestRunner.Assert(logs[0].Contains("[TestModule]") && logs[0].Contains("[INFO]"), "CLogger - Log formatting contains module name and INFO level");
            TestRunner.Assert(logs[1].Contains("[WARN]"), "CLogger - Log formatting contains WARN level");
            TestRunner.Assert(logs[2].Contains("[ERROR]"), "CLogger - Log formatting contains ERROR level");
        }
    }
}
