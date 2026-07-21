using System;

namespace cAlgo.Robots
{
    public enum SvbsEntryWindow
    {
        None = 0,
        /// <summary>Trade during Asia session hours using prior-day Asia VA.</summary>
        AsiaSession = 1,
        /// <summary>London hours: break frozen Asia VA (A→L expansion).</summary>
        AsiaToLondon = 2,
        /// <summary>NY / Overlap hours: break frozen London VA (L→NY expansion).</summary>
        LondonToNy = 3
    }

    public enum SvbsProfileKind
    {
        None = 0,
        Asia = 1,
        London = 2
    }

    /// <summary>
    /// Fixed UTC session geometry for SVBS-X (not user-tunable).
    /// User only enable/disable Asia / London / NY / Overlap via CSessionFilter.
    /// Hours match Common/CSessionFilter defaults.
    /// </summary>
    public sealed class SessionClock
    {
        // Profile build (frozen once complete)
        public const int AsiaStartHour = 0;
        public const int AsiaEndHour = 7;          // Asia VA [00:00, 07:00)
        public const int LondonProfileEndHour = 12; // London VA [07:00, 12:00)

        // Entry sub-windows (strategy)
        public const int AlEntryStartHour = 7;
        public const int AlEntryStartMinute = 30;  // buffer after London open
        public const int AlEntryEndHour = 12;      // until London profile freezes / mid-day

        public const int NyOverlapStartHour = 13;
        public const int NyOverlapStartMinute = 0;
        public const int OverlapEndHour = 16;
        public const int NyStartHour = 13;
        public const int NyStartMinute = 30;
        public const int NyEndHour = 23;

        public const int AsiaSessionEndHour = 9;   // CSessionFilter Asia end

        // Flat (end of host session)
        public const int AsiaFlatHour = 9;
        public const int LondonFlatHour = 16;
        public const int NyFlatHour = 23;

        public DateTime DayAt(DateTime utc, int h, int m = 0)
        {
            var d = utc.Date;
            return new DateTime(d.Year, d.Month, d.Day, h, m, 0, DateTimeKind.Utc);
        }

        public DateTime AsiaStart(DateTime utc) => DayAt(utc, AsiaStartHour);
        public DateTime AsiaEnd(DateTime utc) => DayAt(utc, AsiaEndHour);
        public DateTime LondonProfileEnd(DateTime utc) => DayAt(utc, LondonProfileEndHour);

        public bool CanFreezeAsia(DateTime utc) => utc >= AsiaEnd(utc);
        public bool CanFreezeLondon(DateTime utc) => utc >= LondonProfileEnd(utc);

        /// <summary>
        /// Resolve strategy window from fixed clocks ∩ enabled toggles.
        /// Priority: Overlap/NY (L→NY) &gt; London A→L &gt; Asia session.
        /// </summary>
        public SvbsEntryWindow ResolveWindow(
            DateTime utc,
            bool tradeAsia,
            bool tradeLondon,
            bool tradeNewYork,
            bool tradeOverlap)
        {
            int mins = utc.Hour * 60 + utc.Minute;

            // L→NY / Overlap first (afternoon)
            bool inOverlap = mins >= NyOverlapStartHour * 60 + NyOverlapStartMinute
                             && mins < OverlapEndHour * 60;
            bool inNy = mins >= NyStartHour * 60 + NyStartMinute
                        && mins < NyEndHour * 60;

            if (tradeOverlap && inOverlap)
                return SvbsEntryWindow.LondonToNy;
            if (tradeNewYork && inNy)
                return SvbsEntryWindow.LondonToNy;

            // A→L: London morning after open buffer
            bool inAl = mins >= AlEntryStartHour * 60 + AlEntryStartMinute
                        && mins < AlEntryEndHour * 60;
            if (tradeLondon && inAl)
                return SvbsEntryWindow.AsiaToLondon;

            // Asia session (use prior-day Asia VA)
            bool inAsia = mins >= AsiaStartHour * 60 && mins < AsiaSessionEndHour * 60;
            if (tradeAsia && inAsia)
                return SvbsEntryWindow.AsiaSession;

            return SvbsEntryWindow.None;
        }

        public SvbsProfileKind ProfileForWindow(SvbsEntryWindow window)
        {
            return window switch
            {
                SvbsEntryWindow.AsiaSession => SvbsProfileKind.Asia,
                SvbsEntryWindow.AsiaToLondon => SvbsProfileKind.Asia,
                SvbsEntryWindow.LondonToNy => SvbsProfileKind.London,
                _ => SvbsProfileKind.None
            };
        }

        /// <summary>Start of developing POC range for current window.</summary>
        public DateTime DevelopingWindowStart(DateTime utc, SvbsEntryWindow window)
        {
            return window switch
            {
                SvbsEntryWindow.AsiaSession => AsiaStart(utc),
                SvbsEntryWindow.AsiaToLondon => DayAt(utc, AlEntryStartHour, AlEntryStartMinute),
                SvbsEntryWindow.LondonToNy => DayAt(utc, NyOverlapStartHour, NyOverlapStartMinute),
                _ => utc.Date
            };
        }

        /// <summary>
        /// Session flat: past flat clock on entry day, or any later calendar day
        /// (weekend/gap — OnTick may not run at Friday 23:00).
        /// </summary>
        public bool ShouldFlat(DateTime utc, SvbsEntryWindow window, DateTime entryUtc)
        {
            // Held into next day / weekend → always flatten
            if (utc.Date > entryUtc.Date)
                return true;

            int flatHour = window switch
            {
                SvbsEntryWindow.AsiaSession => AsiaFlatHour,
                SvbsEntryWindow.AsiaToLondon => LondonFlatHour,
                SvbsEntryWindow.LondonToNy => NyFlatHour,
                _ => NyFlatHour
            };

            var flatAt = new DateTime(
                entryUtc.Year, entryUtc.Month, entryUtc.Day,
                flatHour, 0, 0, DateTimeKind.Utc);
            return utc >= flatAt;
        }

        public string DescribeFixed()
        {
            return "AsiaVA 00-07 | LonVA 07-12 | A→L entry 07:30-12 | L→NY 13-23 (overlap 13-16) | Asia sess 00-09 | flat Asia09/Lon16/NY23";
        }
    }
}
