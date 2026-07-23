using System;
using RedWave.Common;

namespace CommonTests
{
    public static class NewsFilterTests
    {
        public static void RunAll()
        {
            Test_LoadFromString_Parsing();
            Test_BlackoutWindow_Filtering();
            Test_GetNearestEvent();
        }

        private static void Test_LoadFromString_Parsing()
        {
            var filter = new CNewsFilter();
            filter.Init(enabled: true, blackoutMinutes: 30);

            // Load 3 news events in schedule string
            string schedule = "2026-07-22 13:30|NFP; 2026-07-22 18:00|FOMC; 2026-07-23 12:30";
            int loaded = filter.LoadFromString(schedule);

            TestRunner.Assert(loaded == 3, "NewsFilter LoadFromString - Successfully parsed 3 news events");
            TestRunner.Assert(filter.EventCount == 3, "NewsFilter EventCount equals 3");
        }

        private static void Test_BlackoutWindow_Filtering()
        {
            var filter = new CNewsFilter();
            filter.Init(enabled: true, blackoutMinutes: 30);

            DateTime eventTime = new DateTime(2026, 7, 22, 13, 30, 0, DateTimeKind.Utc);
            filter.AddEvent(eventTime);

            // 1. Inside blackout: 13:15 UTC (15 mins before event) -> Blocked
            DateTime insideTime = new DateTime(2026, 7, 22, 13, 15, 0, DateTimeKind.Utc);
            TestRunner.Assert(!filter.IsTradingAllowed(insideTime), "NewsFilter Blackout - 13:15 UTC (15m before 13:30 event) blocked");

            // 2. Outside blackout: 14:15 UTC (45 mins after event) -> Allowed
            DateTime outsideTime = new DateTime(2026, 7, 22, 14, 15, 0, DateTimeKind.Utc);
            TestRunner.Assert(filter.IsTradingAllowed(outsideTime), "NewsFilter Blackout - 14:15 UTC (45m after 13:30 event) allowed");
        }

        private static void Test_GetNearestEvent()
        {
            var filter = new CNewsFilter();
            DateTime ev1 = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
            DateTime ev2 = new DateTime(2026, 7, 22, 18, 0, 0, DateTimeKind.Utc);

            filter.AddEvent(ev1);
            filter.AddEvent(ev2);

            DateTime checkTime = new DateTime(2026, 7, 22, 13, 0, 0, DateTimeKind.Utc);
            DateTime? nearest = filter.GetNearestEvent(checkTime);

            TestRunner.Assert(nearest.HasValue && nearest.Value == ev1, "NewsFilter GetNearestEvent - 13:00 UTC nearest to 12:00 UTC event");
        }
    }
}
