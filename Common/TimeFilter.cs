using System;

namespace RedWave.Common
{
    public class CTimeFilter
    {
        private CLogger _logger;
        private bool _enablePerDayHourFilter;
        private bool[] _dayEnabled;
        private bool[][] _allowedHoursPerDay;

        public CTimeFilter()
        {
            _enablePerDayHourFilter = false;
            _dayEnabled = new bool[7];
            _allowedHoursPerDay = new bool[7][];

            for (int d = 0; d < 7; d++)
            {
                _dayEnabled[d] = (d >= 1 && d <= 5); // Default Mon-Fri enabled
                _allowedHoursPerDay[d] = new bool[24];
                for (int h = 0; h < 24; h++)
                {
                    _allowedHoursPerDay[d][h] = true; // All hours allowed by default
                }
            }
        }

        public void Init(CLogger logger = null)
        {
            _logger = logger;
            DateTime gmtNow = DateTime.UtcNow;
            _logger?.Info($"TimeFilter initialized. Current GMT: {gmtNow.Hour:D2}:{gmtNow.Minute:D2}");
        }

        public void SetDayHours(int dayOfWeek, bool enable, string hourList)
        {
            if (dayOfWeek < 0 || dayOfWeek > 6) return;

            _enablePerDayHourFilter = true;
            _dayEnabled[dayOfWeek] = enable;

            // Reset all hours for this day to false
            for (int h = 0; h < 24; h++)
            {
                _allowedHoursPerDay[dayOfWeek][h] = false;
            }

            if (!enable || string.IsNullOrEmpty(hourList)) return;

            // Parse list: "0,1,2,3,4,5,6..."
            string[] hours = hourList.Split(',');
            foreach (var hStr in hours)
            {
                if (int.TryParse(hStr.Trim(), out int h) && h >= 0 && h < 24)
                {
                    _allowedHoursPerDay[dayOfWeek][h] = true;
                }
            }
        }

        public bool IsTradingAllowed(DateTime serverTime)
        {
            if (!_enablePerDayHourFilter) return true;

            // cTrader serverTime is in UTC. SpecifyKind to Utc to avoid OS timezone shifting issues.
            DateTime gmtTime = DateTime.SpecifyKind(serverTime, DateTimeKind.Utc);
            int dayOfWeek = (int)gmtTime.DayOfWeek; // DayOfWeek enum: Sunday = 0, Monday = 1, ..., Saturday = 6
            int hour = gmtTime.Hour;

            if (!_dayEnabled[dayOfWeek])
                return false;

            return _allowedHoursPerDay[dayOfWeek][hour];
        }
    }
}
