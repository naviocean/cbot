using System;

namespace RedWave.Common
{
    /// <summary>
    /// Tick up/down delta proxy from mid price changes (retail CFD order-flow substitute).
    /// Unchanged mid ticks are ignored.
    /// </summary>
    public class CTickDeltaEngine
    {
        private struct TickSample
        {
            public DateTime Time;
            public sbyte Side; // +1 buy (uptick), -1 sell (downtick)
        }

        private TickSample[] _samples;
        private int _maxSamples;
        private int _head;
        private int _count;
        private double _lastMid;
        private bool _hasLastMid;
        private CLogger _logger;

        public CTickDeltaEngine()
        {
            _maxSamples = 50000;
            _samples = new TickSample[_maxSamples];
            _head = 0;
            _count = 0;
            _lastMid = 0;
            _hasLastMid = false;
        }

        public bool Init(int maxSamples = 50000, CLogger logger = null)
        {
            _logger = logger;
            _maxSamples = Math.Max(1000, maxSamples);
            _samples = new TickSample[_maxSamples];
            _head = 0;
            _count = 0;
            _lastMid = 0;
            _hasLastMid = false;
            return true;
        }

        public void Reset()
        {
            _head = 0;
            _count = 0;
            _hasLastMid = false;
        }

        public void OnTick(double bid, double ask, DateTime time)
        {
            double mid = (bid + ask) * 0.5;
            if (!_hasLastMid)
            {
                _lastMid = mid;
                _hasLastMid = true;
                return;
            }

            sbyte side = 0;
            if (mid > _lastMid) side = 1;
            else if (mid < _lastMid) side = -1;
            _lastMid = mid;

            if (side == 0) return; // ignore zero ticks

            _samples[_head].Time = time;
            _samples[_head].Side = side;
            _head++;
            if (_head >= _maxSamples) _head = 0;
            if (_count < _maxSamples) _count++;
        }

        /// <summary>
        /// Buy-volume / sell-volume ratio over the window. Returns 1.0 if empty/insufficient.
        /// Never returns inflated 999 — one-sided flow is capped and requires min ticks on both sides bias.
        /// </summary>
        public double GetImbalance(long windowMs, int minTicks = 20)
        {
            GetVolumes(windowMs, out double buy, out double sell);
            double total = buy + sell;
            if (total < minTicks) return 1.0; // not enough data → neutral (fails MinDeltaStrength > 1)
            if (sell <= 0) return Math.Min(buy, 10.0); // cap; still needs minTicks all buy
            return buy / sell;
        }

        /// <summary>Sell/Buy ratio (short strength).</summary>
        public double GetSellImbalance(long windowMs, int minTicks = 20)
        {
            GetVolumes(windowMs, out double buy, out double sell);
            double total = buy + sell;
            if (total < minTicks) return 1.0;
            if (buy <= 0) return Math.Min(sell, 10.0);
            return sell / buy;
        }

        public int GetTickCount(long windowMs)
        {
            GetVolumes(windowMs, out double buy, out double sell);
            return (int)(buy + sell);
        }

        public double GetCvd(long windowMs)
        {
            GetVolumes(windowMs, out double buy, out double sell);
            return buy - sell;
        }

        /// <summary>
        /// Approx slope: CVD in second half of window minus CVD in first half.
        /// Positive = accelerating buy pressure.
        /// </summary>
        public double GetCvdSlope(long windowMs)
        {
            if (windowMs < 2) return 0;
            long half = windowMs / 2;
            // Recent half
            double recent = GetCvd(half);
            // Older half: total window CVD - recent ≈ older contribution when windows nest
            // Better: compute buy-sell for [window, half] vs [half, 0]
            GetVolumesRange(windowMs, half, out double buyOld, out double sellOld);
            GetVolumes(half, out double buyNew, out double sellNew);
            double cvdOld = buyOld - sellOld;
            double cvdNew = buyNew - sellNew;
            return cvdNew - cvdOld;
        }

        public void GetVolumes(long windowMs, out double buyVol, out double sellVol)
        {
            buyVol = 0;
            sellVol = 0;
            if (_count == 0 || windowMs <= 0) return;

            int headPrev = _head - 1;
            if (headPrev < 0) headPrev = _maxSamples - 1;
            DateTime cutoff = _samples[headPrev].Time.AddMilliseconds(-windowMs);

            int curr = headPrev;
            for (int i = 0; i < _count; i++)
            {
                if (_samples[curr].Time < cutoff) break;
                if (_samples[curr].Side > 0) buyVol++;
                else if (_samples[curr].Side < 0) sellVol++;
                curr--;
                if (curr < 0) curr = _maxSamples - 1;
            }
        }

        private void GetVolumesRange(long fromMs, long toMs, out double buyVol, out double sellVol)
        {
            buyVol = 0;
            sellVol = 0;
            if (_count == 0 || fromMs <= toMs) return;

            int headPrev = _head - 1;
            if (headPrev < 0) headPrev = _maxSamples - 1;
            DateTime newest = _samples[headPrev].Time;
            DateTime outer = newest.AddMilliseconds(-fromMs);
            DateTime inner = newest.AddMilliseconds(-toMs);

            int curr = headPrev;
            for (int i = 0; i < _count; i++)
            {
                DateTime t = _samples[curr].Time;
                if (t < outer) break;
                if (t <= inner)
                {
                    if (_samples[curr].Side > 0) buyVol++;
                    else if (_samples[curr].Side < 0) sellVol++;
                }
                curr--;
                if (curr < 0) curr = _maxSamples - 1;
            }
        }
    }
}
