using System;
using RedWave.Common;

namespace CommonTests
{
    public static class TimeFilterTests
    {
        public static void RunAll()
        {
            Test_Default_MonToFri_Allowed();
            Test_CustomDayHours_Filtering();
            Test_Weekend_Blocked();
        }

        private static void Test_Default_MonToFri_Allowed()
        {
            var filter = new CTimeFilter();

            // Monday 10:00 UTC
            DateTime monTime = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
            TestRunner.Assert(filter.IsTradingAllowed(monTime), "TimeFilter - Monday 10:00 UTC allowed by default");

            // Friday 15:00 UTC
            DateTime friTime = new DateTime(2026, 7, 24, 15, 0, 0, DateTimeKind.Utc);
            TestRunner.Assert(filter.IsTradingAllowed(friTime), "TimeFilter - Friday 15:00 UTC allowed by default");
        }

        private static void Test_CustomDayHours_Filtering()
        {
            var filter = new CTimeFilter();

            // Enable Monday (Day 1) only for hours 13,14,15 ("13,14,15")
            filter.SetDayHours(dayOfWeek: 1, enable: true, hourList: "13, 14, 15");

            DateTime monAllowed = new DateTime(2026, 7, 20, 14, 30, 0, DateTimeKind.Utc); // 14:30
            DateTime monBlocked = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);  // 10:00

            TestRunner.Assert(filter.IsTradingAllowed(monAllowed), "TimeFilter - Monday 14:30 UTC inside allowed hour list (13,14,15)");
            TestRunner.Assert(!filter.IsTradingAllowed(monBlocked), "TimeFilter - Monday 10:00 UTC outside allowed hour list blocked");
        }

        private static void Test_Weekend_Blocked()
        {
            var filter = new CTimeFilter();
            filter.SetDayHours(dayOfWeek: 6, enable: false, hourList: ""); // Saturday

            DateTime satTime = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
            TestRunner.Assert(!filter.IsTradingAllowed(satTime), "TimeFilter - Saturday trading disabled/blocked");
        }
    }
}
