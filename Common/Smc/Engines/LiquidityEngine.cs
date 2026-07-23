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

        public IReadOnlyList<LiquidityPool> ActivePools => _pools.Where(p => !p.IsSwept).ToList().AsReadOnly();
        public IReadOnlyList<SweepEvent> Sweeps => _sweeps.AsReadOnly();

        public void Update(Bars bars, int currBarIndex = -1, double pipSize = 0.0001)
        {
            if (bars == null || bars.Count < 5)
                return;

            if (currBarIndex < 0)
                currBarIndex = bars.Count - 1;

            if (currBarIndex < 4 || currBarIndex >= bars.Count)
                return;

            double high = bars.HighPrices[currBarIndex];
            double low = bars.LowPrices[currBarIndex];
            double close = bars.ClosePrices[currBarIndex];

            // 1. Check if current bar sweeps any active Liquidity Pool
            foreach (var pool in ActivePools)
            {
                if (pool.Type == LiquidityType.BSL || pool.Type == LiquidityType.EQH || pool.Type == LiquidityType.AsianHigh)
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
                            SweepTime = bars.OpenTimes[currBarIndex],
                            ClosedBackInside = closedBackInside
                        });
                    }
                }
                else if (pool.Type == LiquidityType.SSL || pool.Type == LiquidityType.EQL || pool.Type == LiquidityType.AsianLow)
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
                            SweepTime = bars.OpenTimes[currBarIndex],
                            ClosedBackInside = closedBackInside
                        });
                    }
                }
            }

            // 2. Automatically register new Swing High/Low Liquidity Pools from recent bar
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

        public bool HasRecentSweep(LiquidityType type, int withinBars = 10)
        {
            var lastSweep = _sweeps.LastOrDefault();
            if (lastSweep == null) return false;
            return lastSweep.Pool.Type == type && lastSweep.ClosedBackInside;
        }

        public void Reset()
        {
            _pools.Clear();
            _sweeps.Clear();
            _idCounter = 0;
        }
    }
}
