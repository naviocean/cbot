using System;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for tracking ICT Sessions (Asian, London, NY) and Kill Zones (LOKZ, NYAM, NYPM, Silver Bullets),
    /// as well as Asian Range high, low, and midpoint.
    /// </summary>
    public class SessionEngine
    {
        public int TimezoneOffsetHours { get; set; } = 0; // UTC offset

        public SessionType CurrentSession { get; private set; } = SessionType.OffSession;
        public KillZone ActiveKillZone { get; private set; } = KillZone.None;
        public bool IsInKillZone => ActiveKillZone != KillZone.None;
        public bool IsInSilverBullet => ActiveKillZone == KillZone.SilverBullet1 ||
                                       ActiveKillZone == KillZone.SilverBullet2 ||
                                       ActiveKillZone == KillZone.SilverBullet3;

        public double AsianHigh { get; private set; } = 0;
        public double AsianLow { get; private set; } = double.MaxValue;
        public double AsianMidpoint => (AsianHigh > 0 && AsianLow < double.MaxValue) ? (AsianHigh + AsianLow) / 2.0 : 0;
        public bool AsianRangeLocked { get; private set; } = false;

        private SessionType _prevSession = SessionType.OffSession;

        public void Update(DateTime barTime, double high, double low)
        {
            DateTime utcTime = barTime.ToUniversalTime().AddHours(TimezoneOffsetHours);
            TimeSpan timeOfDay = utcTime.TimeOfDay;

            // 1. Session Detection (UTC)
            // 20:00 - 01:59 -> Asian
            // 02:00 - 04:59 -> London
            // 05:00 - 06:59 -> OffSession
            // 07:00 - 11:59 -> NewYork AM
            // 12:00 - 13:29 -> OffSession (NY Lunch)
            // 13:30 - 16:59 -> NewYork PM
            SessionType newSession;
            if (timeOfDay >= new TimeSpan(20, 0, 0) || timeOfDay < new TimeSpan(2, 0, 0))
            {
                newSession = SessionType.Asian;
            }
            else if (timeOfDay >= new TimeSpan(2, 0, 0) && timeOfDay < new TimeSpan(5, 0, 0))
            {
                newSession = SessionType.London;
            }
            else if (timeOfDay >= new TimeSpan(7, 0, 0) && timeOfDay < new TimeSpan(12, 0, 0))
            {
                newSession = SessionType.NewYork;
            }
            else if (timeOfDay >= new TimeSpan(13, 30, 0) && timeOfDay < new TimeSpan(17, 0, 0))
            {
                newSession = SessionType.NewYork;
            }
            else
            {
                newSession = SessionType.OffSession;
            }

            // Detect session transitions
            if (_prevSession == SessionType.Asian && newSession == SessionType.London)
            {
                AsianRangeLocked = true; // Lock Asian Range at London Open
            }
            else if (newSession == SessionType.Asian && _prevSession != SessionType.Asian)
            {
                // New Asian Session start -> Reset Asian Range
                AsianHigh = high;
                AsianLow = low;
                AsianRangeLocked = false;
            }

            CurrentSession = newSession;

            // Update Asian Range if in Asian Session and not locked
            if (CurrentSession == SessionType.Asian && !AsianRangeLocked)
            {
                if (high > AsianHigh) AsianHigh = high;
                if (low < AsianLow) AsianLow = low;
            }

            _prevSession = newSession;

            // 2. Kill Zone Detection (UTC)
            // LOKZ: 02:00 - 05:00
            // NYAM: 07:00 - 10:00
            // NYPM: 13:30 - 16:00
            // SilverBullet1: 10:00 - 11:00
            // SilverBullet2: 14:00 - 15:00
            // SilverBullet3: 15:00 - 16:00
            if (timeOfDay >= new TimeSpan(10, 0, 0) && timeOfDay < new TimeSpan(11, 0, 0))
            {
                ActiveKillZone = KillZone.SilverBullet1;
            }
            else if (timeOfDay >= new TimeSpan(14, 0, 0) && timeOfDay < new TimeSpan(15, 0, 0))
            {
                ActiveKillZone = KillZone.SilverBullet2;
            }
            else if (timeOfDay >= new TimeSpan(15, 0, 0) && timeOfDay < new TimeSpan(16, 0, 0))
            {
                ActiveKillZone = KillZone.SilverBullet3;
            }
            else if (timeOfDay >= new TimeSpan(2, 0, 0) && timeOfDay < new TimeSpan(5, 0, 0))
            {
                ActiveKillZone = KillZone.LOKZ;
            }
            else if (timeOfDay >= new TimeSpan(7, 0, 0) && timeOfDay < new TimeSpan(10, 0, 0))
            {
                ActiveKillZone = KillZone.NYAM;
            }
            else if (timeOfDay >= new TimeSpan(13, 30, 0) && timeOfDay < new TimeSpan(16, 0, 0))
            {
                ActiveKillZone = KillZone.NYPM;
            }
            else
            {
                ActiveKillZone = KillZone.None;
            }
        }

        public void Reset()
        {
            CurrentSession = SessionType.OffSession;
            ActiveKillZone = KillZone.None;
            AsianHigh = 0;
            AsianLow = double.MaxValue;
            AsianRangeLocked = false;
            _prevSession = SessionType.OffSession;
        }
    }
}
