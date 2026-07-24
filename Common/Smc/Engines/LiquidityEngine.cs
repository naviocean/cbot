using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Engine for tracking Liquidity Pools (BSL, SSL, EQH, EQL) and detecting Liquidity Sweeps (Judas Swings).
    /// </summary>
    public class LiquidityEngine
    {
        private readonly List<LiquidityPool> _pools = new List<LiquidityPool>();
        private readonly List<SweepEvent> _sweeps = new List<SweepEvent>();
        private int _idCounter = 0;

        public double EqualTolerancePips { get; set; } = 2.0;

        public double PreviousDayHigh { get; private set; }
        public double PreviousDayLow { get; private set; }
        public double PreviousWeekHigh { get; private set; }
        public double PreviousWeekLow { get; private set; }
        public double AsianSessionHigh { get; private set; }
        public double AsianSessionLow { get; private set; }

        private DateTime _lastBarDate = DateTime.MinValue.Date;
        private double _currentDayHigh = 0;
        private double _currentDayLow = double.MaxValue;

        private int _lastWeekNumber = -1;
        private double _currentWeekHigh = 0;
        private double _currentWeekLow = double.MaxValue;

        public IReadOnlyList<LiquidityPool> ActivePools => _pools.Where(p => !p.IsSwept).ToList().AsReadOnly();
        public IReadOnlyList<SweepEvent> Sweeps => _sweeps.AsReadOnly();

        public void SetSessionLevels(double asianHigh, double asianLow)
        {
            AsianSessionHigh = asianHigh;
            AsianSessionLow = asianLow;
        }

        public void Update(Bars bars, int currBarIndex = -1, double pipSize = 0.0001, DateTime? barTime = null)
        {
            if (bars == null || bars.Count < 5)
                return;

            if (currBarIndex < 0)
                currBarIndex = bars.Count - 1;

            if (currBarIndex < 0 || currBarIndex >= bars.Count)
                return;

            double high = bars.HighPrices[currBarIndex];
            double low = bars.LowPrices[currBarIndex];
            double close = bars.ClosePrices[currBarIndex];
            DateTime bTime = barTime ?? bars.OpenTimes[currBarIndex];

            // 1. Roll Daily PDH/PDL & Weekly PWH/PWL
            if (_lastBarDate != DateTime.MinValue.Date && bTime.Date != _lastBarDate)
            {
                PreviousDayHigh = _currentDayHigh;
                PreviousDayLow = _currentDayLow;
                _currentDayHigh = high;
                _currentDayLow = low;
                _lastBarDate = bTime.Date;

                if (PreviousDayHigh > 0) AddPool(LiquidityType.PDH, PreviousDayHigh, currBarIndex, bTime);
                if (PreviousDayLow > 0 && PreviousDayLow < double.MaxValue) AddPool(LiquidityType.PDL, PreviousDayLow, currBarIndex, bTime);
            }
            else
            {
                if (_lastBarDate == DateTime.MinValue.Date) _lastBarDate = bTime.Date;
                if (high > _currentDayHigh) _currentDayHigh = high;
                if (low < _currentDayLow) _currentDayLow = low;
            }

            int currWeek = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                bTime, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            if (_lastWeekNumber != -1 && currWeek != _lastWeekNumber)
            {
                PreviousWeekHigh = _currentWeekHigh;
                PreviousWeekLow = _currentWeekLow;
                _currentWeekHigh = high;
                _currentWeekLow = low;
                _lastWeekNumber = currWeek;

                if (PreviousWeekHigh > 0) AddPool(LiquidityType.PWH, PreviousWeekHigh, currBarIndex, bTime);
                if (PreviousWeekLow > 0 && PreviousWeekLow < double.MaxValue) AddPool(LiquidityType.PWL, PreviousWeekLow, currBarIndex, bTime);
            }
            else
            {
                if (_lastWeekNumber == -1) _lastWeekNumber = currWeek;
                if (high > _currentWeekHigh) _currentWeekHigh = high;
                if (low < _currentWeekLow) _currentWeekLow = low;
            }

            // 2. Check if current bar sweeps any active Liquidity Pool
            foreach (var pool in ActivePools)
            {
                if (pool.Type == LiquidityType.BSL || pool.Type == LiquidityType.EQH || pool.Type == LiquidityType.AsianHigh || pool.Type == LiquidityType.PDH || pool.Type == LiquidityType.PWH)
                {
                    if (high > pool.PriceLevel)
                    {
                        pool.IsSwept = true;
                        bool closedBackInside = close < pool.PriceLevel;

                        _sweeps.Add(new SweepEvent
                        {
                            Pool = pool,
                            SweptExtremumPrice = high,
                            SweepBarIndex = currBarIndex,
                            SweepTime = bTime,
                            ClosedBackInside = closedBackInside
                        });
                    }
                }
                else if (pool.Type == LiquidityType.SSL || pool.Type == LiquidityType.EQL || pool.Type == LiquidityType.AsianLow || pool.Type == LiquidityType.PDL || pool.Type == LiquidityType.PWL)
                {
                    if (low < pool.PriceLevel)
                    {
                        pool.IsSwept = true;
                        bool closedBackInside = close > pool.PriceLevel;

                        _sweeps.Add(new SweepEvent
                        {
                            Pool = pool,
                            SweptExtremumPrice = low,
                            SweepBarIndex = currBarIndex,
                            SweepTime = bTime,
                            ClosedBackInside = closedBackInside
                        });
                    }
                }
            }

            // 3. Automatically register new Swing High/Low Liquidity Pools from recent bar
            if (currBarIndex >= 4)
            {
                int checkIndex = currBarIndex - 2;
                if (checkIndex > 2)
                {
                    double candHigh = bars.HighPrices[checkIndex];
                    double candLow = bars.LowPrices[checkIndex];

                    if (candHigh > bars.HighPrices[checkIndex - 1] && candHigh > bars.HighPrices[checkIndex - 2] &&
                        candHigh > bars.HighPrices[checkIndex + 1] && candHigh > bars.HighPrices[checkIndex + 2])
                    {
                        AddPool(LiquidityType.BSL, candHigh, checkIndex, bars.OpenTimes[checkIndex]);
                    }

                    if (candLow < bars.LowPrices[checkIndex - 1] && candLow < bars.LowPrices[checkIndex - 2] &&
                        candLow < bars.LowPrices[checkIndex + 1] && candLow < bars.LowPrices[checkIndex + 2])
                    {
                        AddPool(LiquidityType.SSL, candLow, checkIndex, bars.OpenTimes[checkIndex]);
                    }
                }
            }
        }

        public void AddPool(LiquidityType type, double price, int barIndex, DateTime time)
        {
            _pools.Add(new LiquidityPool
            {
                Id = ++_idCounter,
                Type = type,
                PriceLevel = price,
                CreatedBarIndex = barIndex,
                CreatedTime = time,
                IsSwept = false
            });
        }

        public bool HasRecentSweep(LiquidityType type, int currBarIndex = -1, int withinBars = 10)
        {
            var lastSweep = _sweeps.LastOrDefault(s =>
                s.Pool.Type == type &&
                s.ClosedBackInside &&
                (currBarIndex < 0 || Math.Abs(s.SweepBarIndex - currBarIndex) <= withinBars));
            return lastSweep != null;
        }

        public void Reset()
        {
            _pools.Clear();
            _sweeps.Clear();
            _idCounter = 0;
            PreviousDayHigh = 0;
            PreviousDayLow = 0;
            PreviousWeekHigh = 0;
            PreviousWeekLow = 0;
            AsianSessionHigh = 0;
            AsianSessionLow = 0;
            _lastBarDate = DateTime.MinValue.Date;
            _currentDayHigh = 0;
            _currentDayLow = double.MaxValue;
            _lastWeekNumber = -1;
            _currentWeekHigh = 0;
            _currentWeekLow = double.MaxValue;
        }
    }
}
