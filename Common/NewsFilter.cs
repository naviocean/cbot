using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RedWave.Common
{
    /// <summary>
    /// Schedule-based high-impact news blackout (UTC).
    /// Events can be loaded from a semicolon/newline separated list:
    /// "2026-03-06 13:30;2026-03-20 18:00" or "yyyy-MM-dd HH:mm|label"
    /// </summary>
    public class CNewsFilter
    {
        private readonly List<DateTime> _events = new List<DateTime>();
        private int _blackoutMinutes;
        private bool _enabled;
        private CLogger _logger;

        public CNewsFilter()
        {
            _blackoutMinutes = 30;
            _enabled = false;
        }

        public void Init(bool enabled, int blackoutMinutes = 30, CLogger logger = null)
        {
            _enabled = enabled;
            _blackoutMinutes = Math.Max(0, blackoutMinutes);
            _logger = logger;
            _logger?.Debug($"NewsFilter enabled={_enabled} ±{_blackoutMinutes}m events={_events.Count}");
        }

        public void Clear() => _events.Clear();

        public void AddEvent(DateTime utcEvent)
        {
            _events.Add(DateTime.SpecifyKind(utcEvent, DateTimeKind.Utc));
        }

        /// <summary>
        /// Parse schedule string. Separators: semicolon, newline, comma.
        /// Each token: "yyyy-MM-dd HH:mm" or "yyyy-MM-dd HH:mm|NFP"
        /// </summary>
        public int LoadFromString(string schedule)
        {
            _events.Clear();
            if (string.IsNullOrWhiteSpace(schedule)) return 0;

            var parts = schedule.Split(new[] { ';', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
            int loaded = 0;
            foreach (var raw in parts)
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;
                int pipe = token.IndexOf('|');
                if (pipe >= 0) token = token.Substring(0, pipe).Trim();

                if (DateTime.TryParseExact(token,
                        new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out DateTime dt))
                {
                    _events.Add(dt);
                    loaded++;
                }
                else if (DateTime.TryParse(token, CultureInfo.InvariantCulture,
                             DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                {
                    _events.Add(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
                    loaded++;
                }
            }

            _events.Sort();
            _logger?.Debug($"NewsFilter loaded {loaded} events");
            return loaded;
        }

        public bool IsEnabled => _enabled;

        public int EventCount => _events.Count;

        /// <summary>True when trading is allowed (not inside any blackout window).</summary>
        public bool IsTradingAllowed(DateTime serverTimeUtc)
        {
            if (!_enabled || _events.Count == 0) return true;

            DateTime t = DateTime.SpecifyKind(serverTimeUtc, DateTimeKind.Utc);
            foreach (var ev in _events)
            {
                double minutes = Math.Abs((t - ev).TotalMinutes);
                if (minutes <= _blackoutMinutes)
                    return false;
            }
            return true;
        }

        public DateTime? GetNearestEvent(DateTime serverTimeUtc)
        {
            if (_events.Count == 0) return null;
            DateTime t = DateTime.SpecifyKind(serverTimeUtc, DateTimeKind.Utc);
            return _events.OrderBy(e => Math.Abs((e - t).TotalMinutes)).First();
        }
    }
}
