using System;

namespace CommonTests
{
    public static class TestRunner
    {
        public static int PassedCount { get; private set; }
        public static int FailedCount { get; private set; }

        public static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.WriteLine($" [PASS] {testName}");
                PassedCount++;
            }
            else
            {
                Console.WriteLine($" [FAIL] {testName}");
                FailedCount++;
            }
        }

        public static void RunSuite(string suiteName, Action suiteAction)
        {
            Console.WriteLine($"\n--- [{suiteName}] ---");
            suiteAction?.Invoke();
        }

        public static void PrintSummary()
        {
            Console.WriteLine("\n=================================================");
            Console.WriteLine($" OVERALL RESULTS: {PassedCount} PASSED, {FailedCount} FAILED");
            Console.WriteLine("=================================================");

            if (FailedCount > 0)
            {
                Environment.Exit(1);
            }
        }
    }
}
