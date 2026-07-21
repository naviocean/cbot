using System;
using System.Collections.Generic;

namespace cAlgo.Indicators
{
    /// <summary>
    /// Single source for cTrader Guru ZigZag HighLow (Depth / Deviation / BackStep).
    /// Used by chart <see cref="ZigZag"/> indicator AND ZigZagPocPullback cBot.
    /// Deviation is in points × pointSize (TickSize).
    /// Indexing: 0 = oldest bar (cAlgo Bars index).
    /// </summary>
    public static class ZigZagCore
    {
        /// <summary>
        /// Full HighLow ZigZag → Result series (0 / NaN = empty, else price at turning point).
        /// Logic mirrors cTrader Guru PerformIndicatorHighLow.
        /// </summary>
        public static double[] ComputeHighLow(
            IReadOnlyList<double> high,
            IReadOnlyList<double> low,
            int depth,
            int deviation,
            int backStep,
            double pointSize)
        {
            int n = high?.Count ?? 0;
            var result = new double[n];
            if (n == 0 || low == null || low.Count != n)
                return result;

            depth = Math.Max(1, depth);
            deviation = Math.Max(1, deviation);
            backStep = Math.Max(1, backStep);
            double point = pointSize > 0 ? pointSize : 0.01;

            var highZigZags = new double[n];
            var lowZigZags = new double[n];

            double lastLow = 0;
            double lastHigh = 0;
            double lowExt = 0;
            double highExt = 0;
            int lastHighIndex = 0;
            int lastLowIndex = 0;
            int type = 0;

            for (int index = 0; index < n; index++)
            {
                if (index < depth)
                {
                    result[index] = 0;
                    highZigZags[index] = 0;
                    lowZigZags[index] = 0;
                    continue;
                }

                double currentLow = Minimum(low, index, depth);

                if (AlmostEq(currentLow, lastLow))
                    currentLow = 0.0;
                else
                {
                    lastLow = currentLow;
                    if (low[index] - currentLow > deviation * point)
                        currentLow = 0.0;
                    else
                    {
                        for (int i = 1; i <= backStep; i++)
                        {
                            int j = index - i;
                            if (j < 0) break;
                            if (!AlmostEq(lowZigZags[j], 0.0) && lowZigZags[j] > currentLow)
                                lowZigZags[j] = 0.0;
                        }
                    }
                }

                if (AlmostEq(low[index], currentLow))
                    lowZigZags[index] = currentLow;
                else
                    lowZigZags[index] = 0.0;

                double currentHigh = Maximum(high, index, depth);

                if (AlmostEq(currentHigh, lastHigh))
                    currentHigh = 0.0;
                else
                {
                    lastHigh = currentHigh;
                    if (currentHigh - high[index] > deviation * point)
                        currentHigh = 0.0;
                    else
                    {
                        for (int i = 1; i <= backStep; i++)
                        {
                            int j = index - i;
                            if (j < 0) break;
                            if (!AlmostEq(highZigZags[j], 0.0) && highZigZags[j] < currentHigh)
                                highZigZags[j] = 0.0;
                        }
                    }
                }

                if (AlmostEq(high[index], currentHigh))
                    highZigZags[index] = currentHigh;
                else
                    highZigZags[index] = 0.0;

                switch (type)
                {
                    case 0:
                        if (AlmostEq(lowExt, 0) && AlmostEq(highExt, 0))
                        {
                            if (!AlmostEq(highZigZags[index], 0.0))
                            {
                                highExt = high[index];
                                lastHighIndex = index;
                                type = -1;
                                result[index] = highExt;
                            }
                            if (!AlmostEq(lowZigZags[index], 0.0))
                            {
                                lowExt = low[index];
                                lastLowIndex = index;
                                type = 1;
                                result[index] = lowExt;
                            }
                        }
                        break;

                    case 1:
                        if (!AlmostEq(lowZigZags[index], 0.0) &&
                            lowZigZags[index] < lowExt &&
                            AlmostEq(highZigZags[index], 0.0))
                        {
                            result[lastLowIndex] = double.NaN;
                            lastLowIndex = index;
                            lowExt = lowZigZags[index];
                            result[index] = lowExt;
                        }
                        if (!AlmostEq(highZigZags[index], 0.0) &&
                            AlmostEq(lowZigZags[index], 0.0))
                        {
                            highExt = highZigZags[index];
                            lastHighIndex = index;
                            result[index] = highExt;
                            type = -1;
                        }
                        break;

                    case -1:
                        if (!AlmostEq(highZigZags[index], 0.0) &&
                            highZigZags[index] > highExt &&
                            AlmostEq(lowZigZags[index], 0.0))
                        {
                            result[lastHighIndex] = double.NaN;
                            lastHighIndex = index;
                            highExt = highZigZags[index];
                            result[index] = highExt;
                        }
                        if (!AlmostEq(lowZigZags[index], 0.0) &&
                            AlmostEq(highZigZags[index], 0.0))
                        {
                            lowExt = lowZigZags[index];
                            lastLowIndex = index;
                            result[index] = lowExt;
                            type = 1;
                        }
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Turning points oldest→newest from a Result series + OHLC for H/L classification.
        /// </summary>
        public static List<ZigZagPoint> ExtractPoints(
            double[] result,
            IReadOnlyList<double> high,
            IReadOnlyList<double> low,
            IReadOnlyList<DateTime> openTimes,
            int lastIndexInclusive)
        {
            var list = new List<ZigZagPoint>();
            if (result == null || high == null || low == null)
                return list;

            int end = Math.Min(lastIndexInclusive, result.Length - 1);
            end = Math.Min(end, high.Count - 1);

            for (int i = 0; i <= end; i++)
            {
                double v = result[i];
                if (double.IsNaN(v) || AlmostEq(v, 0.0))
                    continue;

                bool isHigh = Math.Abs(high[i] - v) <= Math.Abs(low[i] - v);
                DateTime t = openTimes != null && i < openTimes.Count ? openTimes[i] : DateTime.MinValue;
                list.Add(new ZigZagPoint(isHigh, v, i, t));
            }

            return EnsureAlternating(list);
        }

        public readonly struct ZigZagPoint
        {
            public readonly bool IsHigh;
            public readonly double Price;
            public readonly int BarIndex;
            public readonly DateTime OpenTime;

            public ZigZagPoint(bool isHigh, double price, int barIndex, DateTime openTime)
            {
                IsHigh = isHigh;
                Price = price;
                BarIndex = barIndex;
                OpenTime = openTime;
            }
        }

        private static List<ZigZagPoint> EnsureAlternating(List<ZigZagPoint> src)
        {
            if (src.Count == 0)
                return src;
            var dst = new List<ZigZagPoint> { src[0] };
            for (int i = 1; i < src.Count; i++)
            {
                var p = src[i];
                var last = dst[dst.Count - 1];
                if (p.IsHigh == last.IsHigh)
                {
                    bool better = p.IsHigh ? p.Price >= last.Price : p.Price <= last.Price;
                    if (better)
                        dst[dst.Count - 1] = p;
                }
                else
                    dst.Add(p);
            }
            return dst;
        }

        private static double Minimum(IReadOnlyList<double> series, int index, int periods)
        {
            double m = series[index];
            for (int i = 1; i < periods; i++)
            {
                int j = index - i;
                if (j < 0) break;
                if (series[j] < m)
                    m = series[j];
            }
            return m;
        }

        private static double Maximum(IReadOnlyList<double> series, int index, int periods)
        {
            double m = series[index];
            for (int i = 1; i < periods; i++)
            {
                int j = index - i;
                if (j < 0) break;
                if (series[j] > m)
                    m = series[j];
            }
            return m;
        }

        private static bool AlmostEq(double a, double b)
        {
            return Math.Abs(a - b) < double.Epsilon || Math.Abs(a - b) < 1e-12;
        }
    }
}
