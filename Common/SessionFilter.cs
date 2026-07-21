using System;
using cAlgo.API.Internals;

namespace RedWave.Common
{
    public class CSessionFilter
    {
        private CLogger _logger;
        private bool _enableAsian;
        private bool _enableEuropean;
        private bool _enableUS;
        private bool _enableOverlap;

        // Session times (GMT)
        private struct SessionTime
        {
            public int StartHour;
            public int StartMinute;
            public int EndHour;
            public int EndMinute;
        }

        private SessionTime _asianSession;
        private SessionTime _europeanSession;
        private SessionTime _usSession;
        private SessionTime _overlapSession;

        public CSessionFilter()
        {
            _enableAsian = false;
            _enableEuropean = true;
            _enableUS = true;
            _enableOverlap = true;

            // Default times in UTC (broker Server.TimeInUtc)
            // Asia: Tokyo/Sydney-ish cash hours (approx)
            _asianSession = new SessionTime { StartHour = 0, StartMinute = 0, EndHour = 9, EndMinute = 0 };
            // London
            _europeanSession = new SessionTime { StartHour = 7, StartMinute = 0, EndHour = 16, EndMinute = 0 };
            // New York (cash open ~13:30 UTC when US on standard/DST varies — adjustable)
            _usSession = new SessionTime { StartHour = 13, StartMinute = 30, EndHour = 23, EndMinute = 0 };
            // London–NY overlap
            _overlapSession = new SessionTime { StartHour = 13, StartMinute = 0, EndHour = 16, EndMinute = 0 };
        }

        public void Init(bool enableAsian, bool enableEuropean, bool enableUS, bool enableOverlap, CLogger logger = null)
        {
            _logger = logger;
            _enableAsian = enableAsian;
            _enableEuropean = enableEuropean;
            _enableUS = enableUS;
            _enableOverlap = enableOverlap;

            _logger?.Debug($"SessionFilter Asian={_enableAsian} EU={_enableEuropean} NY={_enableUS} Overlap={_enableOverlap}");
        }

        /// <summary>NY-only mode used by Vacuum Hunter (13:30–23:00 UTC by default).</summary>
        public void InitNyOnly(CLogger logger = null)
        {
            Init(false, false, true, false, logger);
        }

        public void SetUsSession(int startHour, int startMinute, int endHour, int endMinute)
        {
            _usSession = new SessionTime
            {
                StartHour = startHour,
                StartMinute = startMinute,
                EndHour = endHour,
                EndMinute = endMinute
            };
            _logger?.Debug($"NY window {startHour:D2}:{startMinute:D2}-{endHour:D2}:{endMinute:D2} GMT");
        }

        public void SetAsianSession(int startHour, int startMinute, int endHour, int endMinute)
        {
            _asianSession = new SessionTime
            {
                StartHour = startHour,
                StartMinute = startMinute,
                EndHour = endHour,
                EndMinute = endMinute
            };
        }

        public void SetEuropeanSession(int startHour, int startMinute, int endHour, int endMinute)
        {
            _europeanSession = new SessionTime
            {
                StartHour = startHour,
                StartMinute = startMinute,
                EndHour = endHour,
                EndMinute = endMinute
            };
        }

        public void SetOverlapSession(int startHour, int startMinute, int endHour, int endMinute)
        {
            _overlapSession = new SessionTime
            {
                StartHour = startHour,
                StartMinute = startMinute,
                EndHour = endHour,
                EndMinute = endMinute
            };
        }

        /// <summary>Human-readable enabled sessions for logs.</summary>
        public string DescribeEnabled()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (_enableAsian) parts.Add("Asia");
            if (_enableEuropean) parts.Add("London");
            if (_enableUS) parts.Add("NY");
            if (_enableOverlap) parts.Add("Overlap");
            return parts.Count > 0 ? string.Join("+", parts) : "none";
        }

        private bool IsInSession(SessionTime session, int hour, int minute)
        {
            int currentMinutes = hour * 60 + minute;
            int startMinutes = session.StartHour * 60 + session.StartMinute;
            int endMinutes = session.EndHour * 60 + session.EndMinute;

            if (startMinutes > endMinutes) // Overnight session
            {
                return (currentMinutes >= startMinutes || currentMinutes < endMinutes);
            }
            return (currentMinutes >= startMinutes && currentMinutes < endMinutes);
        }

        public bool IsTradingAllowed(DateTime serverTime)
        {
            // If no sessions are enabled, block trading
            if (!_enableAsian && !_enableEuropean && !_enableUS && !_enableOverlap)
                return false;

            // cTrader serverTime is in UTC. SpecifyKind to Utc to avoid OS timezone shifting issues.
            DateTime gmtTime = DateTime.SpecifyKind(serverTime, DateTimeKind.Utc);
            int hour = gmtTime.Hour;
            int minute = gmtTime.Minute;

            if (_enableOverlap && IsInSession(_overlapSession, hour, minute)) return true;
            if (_enableAsian && IsInSession(_asianSession, hour, minute)) return true;
            if (_enableEuropean && IsInSession(_europeanSession, hour, minute)) return true;
            if (_enableUS && IsInSession(_usSession, hour, minute)) return true;

            return false;
        }
    }
}
