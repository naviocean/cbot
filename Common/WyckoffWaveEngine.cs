using System;
using System.Collections.Generic;
using cAlgo.API;

namespace RedWave.Common
{
    public enum WyckoffWaveDirection
    {
        None = 0,
        Up = 1,
        Down = -1
    }

    public class WyckoffPivot
    {
        public int Index { get; set; }
        public DateTime Time { get; set; }
        public double Price { get; set; }
        public WyckoffWaveDirection Type { get; set; } // Up = Pivot High, Down = Pivot Low
        public double WaveVolume { get; set; }
    }

    /// <summary>
    /// Advanced Wyckoff Wave & Pivot Analysis Engine v1.0 for cBots.
    /// Calculates ATR / Percentage based ZigZag waves, Wave Volume,
    /// and detects Higher Lows, Lower Highs, Springs, and Upthrusts.
    /// </summary>
    public class CWyckoffWaveEngine
    {
        private readonly List<WyckoffPivot> _pivots = new();
        private WyckoffWaveDirection _currentDirection = WyckoffWaveDirection.None;
        private double _extremePrice;
        private int _extremeIndex;
        private double _accumulatedVolume;

        public IReadOnlyList<WyckoffPivot> Pivots => _pivots;
        public WyckoffWaveDirection CurrentDirection => _currentDirection;
        public double CurrentWaveVolume => _accumulatedVolume;

        public void Reset()
        {
            _pivots.Clear();
            _currentDirection = WyckoffWaveDirection.None;
            _extremePrice = 0;
            _extremeIndex = 0;
            _accumulatedVolume = 0;
        }

        public void Calculate(Bars bars, int index, double minPivotsDistAtrMult, double atrValue)
        {
            if (index < 1 || bars.Count <= index) return;

            Bar bar = bars[index];
            double minThreshold = Math.Max(0.0001, minPivotsDistAtrMult * atrValue);

            if (_currentDirection == WyckoffWaveDirection.None)
            {
                _extremePrice = bar.High;
                _extremeIndex = index;
                _currentDirection = WyckoffWaveDirection.Up;
                _accumulatedVolume = bar.TickVolume;
                return;
            }

            _accumulatedVolume += bar.TickVolume;

            if (_currentDirection == WyckoffWaveDirection.Up)
            {
                if (bar.High > _extremePrice)
                {
                    _extremePrice = bar.High;
                    _extremeIndex = index;
                }
                else if (_extremePrice - bar.Low >= minThreshold)
                {
                    // Confirm Pivot High
                    _pivots.Add(new WyckoffPivot
                    {
                        Index = _extremeIndex,
                        Time = bars[_extremeIndex].OpenTime,
                        Price = _extremePrice,
                        Type = WyckoffWaveDirection.Up,
                        WaveVolume = _accumulatedVolume
                    });

                    _currentDirection = WyckoffWaveDirection.Down;
                    _extremePrice = bar.Low;
                    _extremeIndex = index;
                    _accumulatedVolume = bar.TickVolume;
                }
            }
            else if (_currentDirection == WyckoffWaveDirection.Down)
            {
                if (bar.Low < _extremePrice)
                {
                    _extremePrice = bar.Low;
                    _extremeIndex = index;
                }
                else if (bar.High - _extremePrice >= minThreshold)
                {
                    // Confirm Pivot Low
                    _pivots.Add(new WyckoffPivot
                    {
                        Index = _extremeIndex,
                        Time = bars[_extremeIndex].OpenTime,
                        Price = _extremePrice,
                        Type = WyckoffWaveDirection.Down,
                        WaveVolume = _accumulatedVolume
                    });

                    _currentDirection = WyckoffWaveDirection.Up;
                    _extremePrice = bar.High;
                    _extremeIndex = index;
                    _accumulatedVolume = bar.TickVolume;
                }
            }

            // Keep pivot list bounded to last 50 pivots
            if (_pivots.Count > 50)
            {
                _pivots.RemoveAt(0);
            }
        }

        public bool IsHigherLow()
        {
            WyckoffPivot lastLow = GetLastPivot(WyckoffWaveDirection.Down, 0);
            WyckoffPivot prevLow = GetLastPivot(WyckoffWaveDirection.Down, 1);

            if (lastLow == null || prevLow == null) return false;
            return lastLow.Price > prevLow.Price;
        }

        public bool IsLowerHigh()
        {
            WyckoffPivot lastHigh = GetLastPivot(WyckoffWaveDirection.Up, 0);
            WyckoffPivot prevHigh = GetLastPivot(WyckoffWaveDirection.Up, 1);

            if (lastHigh == null || prevHigh == null) return false;
            return lastHigh.Price < prevHigh.Price;
        }

        public bool IsSpringPattern(Bar bar, double supportPrice, double maxPenetrationPips, double pipSize)
        {
            double maxPenetration = maxPenetrationPips * pipSize;
            bool dippedBelow = bar.Low < supportPrice && (supportPrice - bar.Low) <= maxPenetration;
            bool closedAbove = bar.Close > supportPrice;
            return dippedBelow && closedAbove;
        }

        public bool IsUpthrustPattern(Bar bar, double resistancePrice, double maxPenetrationPips, double pipSize)
        {
            double maxPenetration = maxPenetrationPips * pipSize;
            bool pokedAbove = bar.High > resistancePrice && (bar.High - resistancePrice) <= maxPenetration;
            bool closedBelow = bar.Close < resistancePrice;
            return pokedAbove && closedBelow;
        }

        public WyckoffPivot GetLastPivot(WyckoffWaveDirection type, int skipCount = 0)
        {
            int found = 0;
            for (int i = _pivots.Count - 1; i >= 0; i--)
            {
                if (_pivots[i].Type == type)
                {
                    if (found == skipCount) return _pivots[i];
                    found++;
                }
            }
            return null;
        }
    }
}
