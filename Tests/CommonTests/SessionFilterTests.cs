using System;
using RedWave.Common;

namespace CommonTests
{
    public static class SessionFilterTests
    {
        public static void RunAll()
        {
            Test_AllSessions_Enabled();
            Test_NyOnly_Mode();
            Test_OvernightSession_Window();
            Test_NoSessionsEnabled_BlocksTrading();
            Test_DescribeEnabled_String();
        }

        private static void Test_AllSessions_Enabled()
        {
            var filter = new CSessionFilter();
            filter.Init(enableAsian: true, enableEuropean: true, enableUS: true, enableOverlap: true);

            // Tokyo morning 02:00 UTC -> Asian
            DateTime asianTime = new DateTime(2026, 7, 22, 2, 30, 0, DateTimeKind.Utc);
            TestRunner.Assert(filter.IsTradingAllowed(asianTime), "SessionFilter - Asian session 02:30 UTC allowed");

            // London morning 09:00 UTC -> European
            DateTime euTime = new DateTime(2026, 7, 22, 9, 0, 0, DateTimeKind.Utc);
            TestRunner.Assert(filter.IsTradingAllowed(euTime), "SessionFilter - European session 09:00 UTC allowed");

            // London-NY overlap 14:00 UTC -> Overlap & US
            DateTime overlapTime = new DateTime(2026, 7, 22, 14, 0, 0, DateTimeKind.Utc);
            TestRunner.Assert(filter.IsTradingAllowed(overlapTime), "SessionFilter - Overlap session 14:00 UTC allowed");
        }

        private static void Test_NyOnly_Mode()
        {
            var filter = new CSessionFilter();
            filter.InitNyOnly();

            // 05:00 UTC -> Asian time -> Should be BLOCKED in NY-only mode
            DateTime asianTime = new DateTime(2026, 7, 22, 5, 0, 0, DateTimeKind.Utc);
            TestRunner.Assert(!filter.IsTradingAllowed(asianTime), "SessionFilter NY-only - Asian 05:00 UTC blocked");

            // 15:00 UTC -> NY session -> Should be ALLOWED
            DateTime nyTime = new DateTime(2026, 7, 22, 15, 0, 0, DateTimeKind.Utc);
            TestRunner.Assert(filter.IsTradingAllowed(nyTime), "SessionFilter NY-only - NY 15:00 UTC allowed");
        }

        private static void Test_OvernightSession_Window()
        {
            var filter = new CSessionFilter();
            filter.Init(enableAsian: true, enableEuropean: false, enableUS: false, enableOverlap: false);
            // Custom overnight Asian session: 22:00 UTC to 06:00 UTC
            filter.SetAsianSession(startHour: 22, startMinute: 0, endHour: 6, endMinute: 0);

            DateTime lateNight = new DateTime(2026, 7, 22, 23, 30, 0, DateTimeKind.Utc); // 23:30 UTC
            DateTime earlyMorning = new DateTime(2026, 7, 22, 3, 15, 0, DateTimeKind.Utc); // 03:15 UTC
            DateTime afternoon = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc); // 12:00 UTC

            TestRunner.Assert(filter.IsTradingAllowed(lateNight), "SessionFilter Overnight - 23:30 UTC inside 22:00-06:00 window allowed");
            TestRunner.Assert(filter.IsTradingAllowed(earlyMorning), "SessionFilter Overnight - 03:15 UTC inside 22:00-06:00 window allowed");
            TestRunner.Assert(!filter.IsTradingAllowed(afternoon), "SessionFilter Overnight - 12:00 UTC outside 22:00-06:00 window blocked");
        }

        private static void Test_NoSessionsEnabled_BlocksTrading()
        {
            var filter = new CSessionFilter();
            filter.Init(enableAsian: false, enableEuropean: false, enableUS: false, enableOverlap: false);

            DateTime time = new DateTime(2026, 7, 22, 14, 0, 0, DateTimeKind.Utc);
            TestRunner.Assert(!filter.IsTradingAllowed(time), "SessionFilter - All sessions disabled blocks all trading");
        }

        private static void Test_DescribeEnabled_String()
        {
            var filter = new CSessionFilter();
            filter.Init(enableAsian: false, enableEuropean: true, enableUS: true, enableOverlap: false);

            string desc = filter.DescribeEnabled();
            TestRunner.Assert(desc == "London+NY", "SessionFilter - DescribeEnabled returns 'London+NY'");
        }
    }
}
