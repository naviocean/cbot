using System;

namespace RedWave.Common
{
    public struct STickData
    {
        public DateTime Time { get; set; }
        public double Price { get; set; }
    }

    public class CTickVolumeProfiler
    {
        private string _symbolName;
        private STickData[] _ticks;
        private int _maxTicks;
        private int _head;
        private int _tail;
        private int _count;
        private long _windowMs;
        private CLogger _logger;

        public CTickVolumeProfiler()
        {
            _maxTicks = 10000;
            _ticks = new STickData[_maxTicks];
            _head = 0;
            _tail = 0;
            _count = 0;
            _windowMs = 10000;
        }

        public bool Init(string symbol, long windowMs, CLogger logger = null)
        {
            if (logger != null)
            {
                _logger = logger;
            }
            _symbolName = symbol;
            _windowMs = windowMs;
            _head = 0;
            _tail = 0;
            _count = 0;
            return true;
        }

        public void Deinit()
        {
        }

        public void OnTick(double bid, double ask, DateTime time)
        {
            _ticks[_head].Time = time;
            _ticks[_head].Price = (bid + ask) / 2.0;

            _head++;
            if (_head >= _maxTicks)
            {
                _head = 0;
            }

            if (_count < _maxTicks)
            {
                _count++;
            }
            else
            {
                _tail++;
                if (_tail >= _maxTicks)
                {
                    _tail = 0;
                }
            }

            DateTime cutoff = time.AddMilliseconds(-_windowMs);
            while (_count > 0)
            {
                if (_ticks[_tail].Time < cutoff)
                {
                    _tail++;
                    if (_tail >= _maxTicks)
                    {
                        _tail = 0;
                    }
                    _count--;
                }
                else
                {
                    break;
                }
            }
        }

        public int GetTicksInWindow(long ms)
        {
            if (_count == 0) return 0;

            int headPrev = _head - 1;
            if (headPrev < 0)
            {
                headPrev = _maxTicks - 1;
            }

            DateTime currentTime = _ticks[headPrev].Time;
            DateTime cutoff = currentTime.AddMilliseconds(-ms);

            int ticks = 0;
            int curr = headPrev;
            for (int i = 0; i < _count; i++)
            {
                if (_ticks[curr].Time >= cutoff)
                {
                    ticks++;
                    curr--;
                    if (curr < 0)
                    {
                        curr = _maxTicks - 1;
                    }
                }
                else
                {
                    break;
                }
            }
            return ticks;
        }

        public double GetAverageTicksPerWindow(long ms)
        {
            if (_count == 0 || _windowMs <= 0) return 0.0;

            int headPrev = _head - 1;
            if (headPrev < 0)
            {
                headPrev = _maxTicks - 1;
            }

            double actualWindow = (_ticks[headPrev].Time - _ticks[_tail].Time).TotalMilliseconds;
            if (actualWindow <= 0) return (double)_count;

            double ticksPerMs = (double)_count / actualWindow;
            return ticksPerMs * ms;
        }

        public double GetPriceDelta(long ms)
        {
            if (_count < 2) return 0.0;

            int headPrev = _head - 1;
            if (headPrev < 0)
            {
                headPrev = _maxTicks - 1;
            }

            DateTime currentTime = _ticks[headPrev].Time;
            DateTime cutoff = currentTime.AddMilliseconds(-ms);

            int curr = headPrev;
            double startPrice = _ticks[curr].Price;
            double endPrice = startPrice;

            for (int i = 0; i < _count; i++)
            {
                endPrice = _ticks[curr].Price;
                if (_ticks[curr].Time <= cutoff)
                {
                    break;
                }
                curr--;
                if (curr < 0)
                {
                    curr = _maxTicks - 1;
                }
            }

            return Math.Abs(startPrice - endPrice);
        }
    }
}
