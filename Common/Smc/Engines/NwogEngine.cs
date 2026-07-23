using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for detecting and tracking ICT New Week Open Gaps (NWOG) and New Day Open Gaps (NDOG).
    /// </summary>
    public class NwogEngine
    {
        private readonly List<OpenGapLevel> _gaps = new List<OpenGapLevel>();
        private int _idCounter = 0;

        /// <summary>
        /// Minimum gap width in pips required to register an NWOG/NDOG.
        /// </summary>
        public double MinGapPips { get; set; } = 0.5;

        public IReadOnlyList<OpenGapLevel> AllGaps => _gaps.AsReadOnly();
        public IEnumerable<OpenGapLevel> ActiveGaps => _gaps.Where(g => !g.IsFilled);

        public void Update(Bars bars, int currBarIndex = -1, double pipSize = 0.0001)
        {
            if (bars == null || bars.Count < 2)
                return;

            if (currBarIndex < 0)
                currBarIndex = bars.Count - 1;

            if (currBarIndex < 1 || currBarIndex >= bars.Count)
                return;

            DateTime currTime = bars.OpenTimes[currBarIndex];
            DateTime prevTime = bars.OpenTimes[currBarIndex - 1];

            double high = bars.HighPrices[currBarIndex];
            double low = bars.LowPrices[currBarIndex];

            // 1. Update fill status of active gaps (mitigated when price retests 50% MidPrice)
            foreach (var gap in ActiveGaps.ToList())
            {
                if (low <= gap.MidPrice && high >= gap.MidPrice)
                {
                    gap.IsFilled = true;
                }
            }

            // Memory cleanup for old filled gaps
            if (_gaps.Count > 100)
            {
                _gaps.RemoveAll(g => g.IsFilled && (currBarIndex - g.BarIndex > 500));
            }

            // 2. Detect New Week Open Gap (NWOG)
            if (currTime.DayOfWeek == DayOfWeek.Monday && prevTime.DayOfWeek != DayOfWeek.Monday)
            {
                double prevClose = bars.ClosePrices[currBarIndex - 1];
                double currOpen = bars.OpenPrices[currBarIndex];

                double gapPips = Math.Abs(currOpen - prevClose) / pipSize;
                if (gapPips >= MinGapPips)
                {
                    _gaps.Add(new OpenGapLevel
                    {
                        Id = ++_idCounter,
                        Type = OpenGapType.NWOG,
                        TopPrice = Math.Max(currOpen, prevClose),
                        BottomPrice = Math.Min(currOpen, prevClose),
                        OpenTime = currTime,
                        BarIndex = currBarIndex,
                        IsFilled = false
                    });
                }
            }
            // 3. Detect New Day Open Gap (NDOG)
            else if (currTime.Day != prevTime.Day)
            {
                double prevClose = bars.ClosePrices[currBarIndex - 1];
                double currOpen = bars.OpenPrices[currBarIndex];

                double gapPips = Math.Abs(currOpen - prevClose) / pipSize;
                if (gapPips >= MinGapPips)
                {
                    _gaps.Add(new OpenGapLevel
                    {
                        Id = ++_idCounter,
                        Type = OpenGapType.NDOG,
                        TopPrice = Math.Max(currOpen, prevClose),
                        BottomPrice = Math.Min(currOpen, prevClose),
                        OpenTime = currTime,
                        BarIndex = currBarIndex,
                        IsFilled = false
                    });
                }
            }
        }

        public OpenGapLevel GetLatestNwog()
        {
            return _gaps.LastOrDefault(g => g.Type == OpenGapType.NWOG && !g.IsFilled);
        }

        public OpenGapLevel GetLatestNdog()
        {
            return _gaps.LastOrDefault(g => g.Type == OpenGapType.NDOG && !g.IsFilled);
        }

        public void Reset()
        {
            _gaps.Clear();
            _idCounter = 0;
        }
    }
}
