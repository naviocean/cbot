using System;
using RedWave.Common;

namespace CommonTests
{
    public static class RiskManagerTests
    {
        public static void RunAll()
        {
            Test_EquityProtection_DrawdownBreach();
            Test_DailyLoss_LimitBreach();
            Test_DailyProfit_LimitBreach();
            Test_DayRolling_Reset();
        }

        private static void Test_EquityProtection_DrawdownBreach()
        {
            var rm = new CRiskManager();
            rm.SetEquityProtection(maxDrawdownPct: 10.0, flattenOnBreach: true);

            DateTime time = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);

            // 1. Initial equity = 10,000 (Peak established = 10,000)
            var status1 = rm.Evaluate(10000.0, time);
            TestRunner.Assert(status1.AllowNewEntries == true, "RiskManager PeakDD - 10,000 equity initial peak established, entries allowed");

            // 2. Equity rises to 12,000 (New Peak established = 12,000)
            var status2 = rm.Evaluate(12000.0, time);
            TestRunner.Assert(status2.AllowNewEntries == true, "RiskManager PeakDD - 12,000 new peak equity, entries allowed");

            // 3. Equity drops to 11,000 (Drawdown = (12000 - 11000)/12000 = 8.33% < 10% limit)
            var status3 = rm.Evaluate(11000.0, time);
            TestRunner.Assert(status3.AllowNewEntries == true, "RiskManager PeakDD - 8.33% DD < 10% limit, entries allowed");

            // 4. Equity drops to 10,700 (Drawdown = (12000 - 10700)/12000 = 10.83% >= 10% limit) -> BREACH
            var status4 = rm.Evaluate(10700.0, time);
            TestRunner.Assert(status4.AllowNewEntries == false, "RiskManager PeakDD - 10.83% DD >= 10% limit BLOCKS new entries");
            TestRunner.Assert(status4.RequestFlatten == true, "RiskManager PeakDD - 10.83% DD triggers RequestFlatten = true");
        }

        private static void Test_DailyLoss_LimitBreach()
        {
            var rm = new CRiskManager();
            rm.SetDailyLimits(maxDailyLossAmount: 500.0, maxDailyProfitAmount: 0, flattenOnDailyLoss: true);

            DateTime dayStart = new DateTime(2026, 7, 22, 0, 5, 0, DateTimeKind.Utc);

            // 1. Day start equity = 10,000
            var status1 = rm.Evaluate(10000.0, dayStart);
            TestRunner.Assert(status1.AllowNewEntries == true, "RiskManager DailyLoss - Day start equity 10,000, entries allowed");

            // 2. Equity drops to 9,600 (Daily loss = $400 < $500 max loss)
            DateTime midDay = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
            var status2 = rm.Evaluate(9600.0, midDay);
            TestRunner.Assert(status2.AllowNewEntries == true, "RiskManager DailyLoss - $400 loss < $500 budget, entries allowed");

            // 3. Equity drops to 9,450 (Daily loss = $550 >= $500 max loss) -> BREACH
            var status3 = rm.Evaluate(9450.0, midDay);
            TestRunner.Assert(status3.AllowNewEntries == false, "RiskManager DailyLoss - $550 loss >= $500 limit BLOCKS new entries");
            TestRunner.Assert(status3.RequestFlatten == true, "RiskManager DailyLoss - Exceeding daily loss budget triggers RequestFlatten");
        }

        private static void Test_DailyProfit_LimitBreach()
        {
            var rm = new CRiskManager();
            rm.SetDailyLimits(maxDailyLossAmount: 0, maxDailyProfitAmount: 1000.0, flattenOnDailyProfit: true);

            DateTime dayStart = new DateTime(2026, 7, 22, 0, 5, 0, DateTimeKind.Utc);

            // 1. Day start equity = 10,000
            rm.Evaluate(10000.0, dayStart);

            // 2. Equity rises to 11,200 (Daily profit = $1,200 >= $1,000 target) -> BREACH
            DateTime midDay = new DateTime(2026, 7, 22, 14, 0, 0, DateTimeKind.Utc);
            var status = rm.Evaluate(11200.0, midDay);

            TestRunner.Assert(status.AllowNewEntries == false, "RiskManager DailyProfit - $1,200 profit >= $1,000 target BLOCKS new entries");
            TestRunner.Assert(status.RequestFlatten == true, "RiskManager DailyProfit - Reaching daily profit target triggers RequestFlatten");
        }

        private static void Test_DayRolling_Reset()
        {
            var rm = new CRiskManager();
            rm.SetDailyLimits(maxDailyLossAmount: 500.0, maxDailyProfitAmount: 0);

            DateTime day1 = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
            rm.Evaluate(10000.0, day1); // Day 1 start equity = 10,000

            // Day 1 loss breach
            var statusDay1Breach = rm.Evaluate(9400.0, day1);
            TestRunner.Assert(statusDay1Breach.AllowNewEntries == false, "RiskManager DayRoll - Day 1 loss breach blocks entries");

            // Day 2 rolls in (2026-07-23 01:00 UTC) -> New day start equity = 9,400
            DateTime day2 = new DateTime(2026, 7, 23, 1, 0, 0, DateTimeKind.Utc);
            var statusDay2Start = rm.Evaluate(9400.0, day2);

            TestRunner.Assert(statusDay2Start.AllowNewEntries == true, "RiskManager DayRoll - Day 2 start resets daily block, entries allowed again");
        }
    }
}
