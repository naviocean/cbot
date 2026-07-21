using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace RedWave.Common
{
    /// <summary>
    /// Adaptive composite volume profile engine (bar TickVolume → OHLC bins).
    /// Also keeps a legacy precision/lookback API for older bots.
    /// </summary>
    public class CVolumeProfile
    {
        private Bars _bars;
        private Chart _chart;
        private CLogger _logger;

        // Legacy
        private int _precision;
        private bool _visualize;
        private Color _colorPOC;
        private Color _colorVA;
        private Color _colorHvn;
        private Color _colorLvn;

        private double _poc;
        private double _vah;
        private double _val;
        private ProfileData _lastProfile;

        // Composite defaults
        private double _binSize;
        private double _valueAreaPercent;
        private double _lvnThreshold;
        private double _hvnThreshold;
        private double _weightDecay;
        private int _profileLookbackDays;
        private double _shapeBullRatio;
        private double _shapeBearRatio;
        private double _maxLvnWidthPrice;
        private bool _requireLvnBetweenHvns;

        // Visual state (avoid leftover objects / full-chart paint)
        private int _lastDrawnVisualBins;
        private int _lastDrawnHvnLines;
        private int _lastDrawnLvnLines;
        private DateTime _lastVisualDay;
        private const int MaxVisualBins = 80; // merge histogram for display only
        private const int MaxNodeLines = 8;

        public double POC => _lastProfile != null && _lastProfile.IsValid ? _lastProfile.POC : _poc;
        public double VAH => _lastProfile != null && _lastProfile.IsValid ? _lastProfile.VAH : _vah;
        public double VAL => _lastProfile != null && _lastProfile.IsValid ? _lastProfile.VAL : _val;
        public double VAWidth => VAH - VAL;
        public ProfileData LastProfile => _lastProfile;

        public CVolumeProfile()
        {
            _precision = 100;
            _visualize = false;
            _colorPOC = Color.Orange;
            _colorVA = Color.DarkCyan;
            _colorHvn = Color.FromArgb(120, 46, 204, 113);
            _colorLvn = Color.FromArgb(120, 231, 76, 60);
            _poc = 0;
            _vah = 0;
            _val = 0;
            _lastProfile = new ProfileData();

            _binSize = 0.5;
            _valueAreaPercent = 0.70;
            _lvnThreshold = 0.65;
            _hvnThreshold = 1.5;
            _weightDecay = 0.8;
            _profileLookbackDays = 4;
            _shapeBullRatio = 1.25;
            _shapeBearRatio = 1.25;
            _maxLvnWidthPrice = 25.0; // XAU: split/reject voids wider than this
            _requireLvnBetweenHvns = true;
            _lastDrawnVisualBins = 0;
            _lastDrawnHvnLines = 0;
            _lastDrawnLvnLines = 0;
            _lastVisualDay = DateTime.MinValue;
        }

        public bool Init(Bars bars, Chart chart, int precision = 100, bool visualize = false, CLogger logger = null)
        {
            if (logger != null) _logger = logger;
            _bars = bars;
            _chart = chart;
            _precision = precision;
            _visualize = visualize;
            return bars != null;
        }

        public void ConfigureComposite(
            double binSize = 0.5,
            int lookbackDays = 4,
            double valueAreaPercent = 0.70,
            double lvnThreshold = 0.65,
            double hvnThreshold = 1.5,
            double weightDecay = 0.8,
            double shapeBullRatio = 1.25,
            double shapeBearRatio = 1.25,
            double maxLvnWidthPrice = 15.0,
            bool requireLvnBetweenHvns = true)
        {
            _binSize = Math.Max(1e-6, binSize);
            _profileLookbackDays = Math.Max(1, lookbackDays);
            _valueAreaPercent = Math.Clamp(valueAreaPercent, 0.1, 0.99);
            _lvnThreshold = Math.Max(0.05, lvnThreshold);
            _hvnThreshold = Math.Max(1.0, hvnThreshold);
            _weightDecay = Math.Clamp(weightDecay, 0.1, 1.0);
            _shapeBullRatio = Math.Max(1.0, shapeBullRatio);
            _shapeBearRatio = Math.Max(1.0, shapeBearRatio);
            _maxLvnWidthPrice = Math.Max(0, maxLvnWidthPrice);
            _requireLvnBetweenHvns = requireLvnBetweenHvns;
        }

        public void SetVisualize(bool visualize) => _visualize = visualize;

        public void SetColors(Color pocColor, Color vaColor)
        {
            _colorPOC = pocColor;
            _colorVA = vaColor;
        }

        public void SetNodeColors(Color hvn, Color lvn)
        {
            _colorHvn = hvn;
            _colorLvn = lvn;
        }

        public void ClearVisuals()
        {
            if (_chart == null) return;

            // Deterministic cleanup of known names (FindAllObjects alone can miss / lag)
            for (int i = 0; i < Math.Max(_lastDrawnVisualBins, MaxVisualBins + 5); i++)
                _chart.RemoveObject("VP_Bin_" + i);
            for (int i = 0; i < Math.Max(_lastDrawnHvnLines, MaxNodeLines + 5); i++)
            {
                _chart.RemoveObject("VP_HVN_" + i);
                _chart.RemoveObject("VP_HVN_L_" + i);
            }
            for (int i = 0; i < Math.Max(_lastDrawnLvnLines, MaxNodeLines + 5); i++)
            {
                _chart.RemoveObject("VP_LVN_" + i);
                _chart.RemoveObject("VP_LVN_L_" + i);
            }

            _chart.RemoveObject("VP_Range");
            _chart.RemoveObject("VP_POC");
            _chart.RemoveObject("VP_VAH");
            _chart.RemoveObject("VP_VAL");
            _chart.RemoveObject("VP_Shape");

            // Fallback sweep
            try
            {
                foreach (var obj in _chart.FindAllObjects(ChartObjectType.Rectangle)
                             .Where(o => o.Name.StartsWith("VP_")).ToList())
                    _chart.RemoveObject(obj.Name);
                foreach (var obj in _chart.FindAllObjects(ChartObjectType.TrendLine)
                             .Where(o => o.Name.StartsWith("VP_")).ToList())
                    _chart.RemoveObject(obj.Name);
                foreach (var obj in _chart.FindAllObjects(ChartObjectType.Text)
                             .Where(o => o.Name.StartsWith("VP_")).ToList())
                    _chart.RemoveObject(obj.Name);
            }
            catch
            {
                // chart may be disposed
            }

            _lastDrawnVisualBins = 0;
            _lastDrawnHvnLines = 0;
            _lastDrawnLvnLines = 0;
        }

        /// <summary>Legacy: fixed precision bins over lookback bars.</summary>
        public void OnBar(int lookbackBars, double valueAreaPercent = 0.70)
        {
            if (_bars == null) return;
            CalculateProfileLegacy(lookbackBars, valueAreaPercent);
        }

        /// <summary>
        /// Build adaptive composite profile over recent trading days (closed bars only).
        /// </summary>
        public ProfileData BuildComposite(DateTime asOfUtc)
        {
            if (_bars == null || _bars.Count < 10)
            {
                _logger?.Warn("VP Composite: insufficient bars");
                _lastProfile = new ProfileData { IsValid = false };
                return _lastProfile;
            }

            int endIndex = _bars.Count - 2; // last closed bar
            if (endIndex < 1)
            {
                _lastProfile = new ProfileData { IsValid = false };
                return _lastProfile;
            }

            // Collect day keys (UTC date of bar open) from newest backwards
            var dayEnds = new List<(DateTime Day, int LastBarIndex)>();
            DateTime? currentDay = null;
            for (int i = endIndex; i >= 0; i--)
            {
                DateTime day = _bars.OpenTimes[i].Date;
                if (currentDay == null || day != currentDay.Value)
                {
                    currentDay = day;
                    dayEnds.Add((day, i));
                    if (dayEnds.Count >= _profileLookbackDays)
                        break;
                }
            }

            if (dayEnds.Count == 0)
            {
                _lastProfile = new ProfileData { IsValid = false };
                return _lastProfile;
            }

            // Find global min/max across selected days
            DateTime oldestDay = dayEnds[dayEnds.Count - 1].Day;
            int startIndex = 0;
            for (int i = 0; i <= endIndex; i++)
            {
                if (_bars.OpenTimes[i].Date >= oldestDay)
                {
                    startIndex = i;
                    break;
                }
            }

            double min = double.MaxValue;
            double max = double.MinValue;
            int barsUsed = 0;
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (_bars.HighPrices[i] > max) max = _bars.HighPrices[i];
                if (_bars.LowPrices[i] < min) min = _bars.LowPrices[i];
                barsUsed++;
            }

            if (max <= min || barsUsed <= 0)
            {
                _logger?.Warn($"VP Composite: invalid range max={max} min={min}");
                _lastProfile = new ProfileData { IsValid = false };
                return _lastProfile;
            }

            // Snap min down to bin grid
            double binSize = _binSize;
            double gridMin = Math.Floor(min / binSize) * binSize;
            double gridMax = Math.Ceiling(max / binSize) * binSize;
            if (gridMax <= gridMin) gridMax = gridMin + binSize;

            int binCount = (int)Math.Round((gridMax - gridMin) / binSize);
            if (binCount < 2) binCount = 2;
            if (binCount > 2000)
            {
                // Safety: widen bin if range is huge
                binSize = (gridMax - gridMin) / 500.0;
                binCount = 500;
                gridMin = Math.Floor(min / binSize) * binSize;
            }

            double[] composite = new double[binCount];

            // Weight by day age: newest dayEnds[0] weight=1, then decay
            for (int d = 0; d < dayEnds.Count; d++)
            {
                DateTime day = dayEnds[d].Day;
                double weight = Math.Pow(_weightDecay, d);
                double[] dayHist = new double[binCount];

                for (int i = startIndex; i <= endIndex; i++)
                {
                    if (_bars.OpenTimes[i].Date != day) continue;
                    DistributeBarVolume(dayHist, gridMin, binSize, binCount,
                        _bars.LowPrices[i], _bars.HighPrices[i], _bars.TickVolumes[i]);
                }

                for (int b = 0; b < binCount; b++)
                    composite[b] += dayHist[b] * weight;
            }

            var profile = FinalizeProfile(composite, gridMin, binSize, asOfUtc, barsUsed, dayEnds.Count);
            _lastProfile = profile;
            _poc = profile.POC;
            _vah = profile.VAH;
            _val = profile.VAL;

            if (_visualize && profile.IsValid)
            {
                // Redraw every bar is OK if draw is cheap; still only paint a clean right-side histogram
                DrawComposite(profile, _bars.OpenTimes[startIndex], _bars.OpenTimes[endIndex]);
                _lastVisualDay = _bars.OpenTimes[endIndex].Date;
            }

            return profile;
        }

        /// <summary>
        /// Build a volume profile from bars whose open time is in [startUtc, endUtc).
        /// Closed bars only: ignores the forming bar (Count-1). Does not update visuals unless requested.
        /// </summary>
        public ProfileData BuildRange(DateTime startUtc, DateTime endUtc, bool updateLastProfile = true, bool draw = false)
        {
            if (_bars == null || _bars.Count < 3)
            {
                var bad = new ProfileData { IsValid = false, BuiltAt = endUtc };
                if (updateLastProfile) _lastProfile = bad;
                return bad;
            }

            if (endUtc <= startUtc)
            {
                var bad = new ProfileData { IsValid = false, BuiltAt = endUtc };
                if (updateLastProfile) _lastProfile = bad;
                return bad;
            }

            int lastClosed = _bars.Count - 2;
            if (lastClosed < 0)
            {
                var bad = new ProfileData { IsValid = false, BuiltAt = endUtc };
                if (updateLastProfile) _lastProfile = bad;
                return bad;
            }

            double min = double.MaxValue;
            double max = double.MinValue;
            int barsUsed = 0;
            int firstIdx = -1;
            int lastIdx = -1;

            for (int i = 0; i <= lastClosed; i++)
            {
                DateTime t = _bars.OpenTimes[i];
                if (t < startUtc) continue;
                if (t >= endUtc) break;

                if (firstIdx < 0) firstIdx = i;
                lastIdx = i;
                if (_bars.HighPrices[i] > max) max = _bars.HighPrices[i];
                if (_bars.LowPrices[i] < min) min = _bars.LowPrices[i];
                barsUsed++;
            }

            if (barsUsed < 2 || max <= min || firstIdx < 0)
            {
                _logger?.Debug($"VP Range: insufficient bars={barsUsed} [{startUtc:u} → {endUtc:u})");
                var bad = new ProfileData { IsValid = false, BuiltAt = endUtc, BarsUsed = barsUsed };
                if (updateLastProfile) _lastProfile = bad;
                return bad;
            }

            double binSize = _binSize;
            double gridMin = Math.Floor(min / binSize) * binSize;
            double gridMax = Math.Ceiling(max / binSize) * binSize;
            if (gridMax <= gridMin) gridMax = gridMin + binSize;

            int binCount = (int)Math.Round((gridMax - gridMin) / binSize);
            if (binCount < 2) binCount = 2;
            if (binCount > 2000)
            {
                binSize = (gridMax - gridMin) / 500.0;
                binCount = 500;
                gridMin = Math.Floor(min / binSize) * binSize;
            }

            double[] hist = new double[binCount];
            for (int i = firstIdx; i <= lastIdx; i++)
            {
                DistributeBarVolume(hist, gridMin, binSize, binCount,
                    _bars.LowPrices[i], _bars.HighPrices[i], _bars.TickVolumes[i]);
            }

            var profile = FinalizeProfile(hist, gridMin, binSize, endUtc, barsUsed, 1);
            if (updateLastProfile)
            {
                _lastProfile = profile;
                if (profile.IsValid)
                {
                    _poc = profile.POC;
                    _vah = profile.VAH;
                    _val = profile.VAL;
                }
            }

            if (draw && _visualize && profile.IsValid)
                DrawComposite(profile, _bars.OpenTimes[firstIdx], _bars.OpenTimes[lastIdx]);

            return profile;
        }

        private void DistributeBarVolume(double[] profile, double min, double range, int precision,
            double low, double high, double volume)
        {
            if (volume <= 0) volume = 1.0;

            int floorBin = (int)Math.Floor((low - min) / range);
            int ceilBin = (int)Math.Floor((high - min) / range);

            if (floorBin < 0) floorBin = 0;
            if (floorBin >= precision) floorBin = precision - 1;
            if (ceilBin < 0) ceilBin = 0;
            if (ceilBin >= precision) ceilBin = precision - 1;

            double body = high - low;
            if (body <= 0)
            {
                profile[floorBin] += volume;
                return;
            }

            double tail = min + (floorBin + 1) * range - low;
            double wick = high - (min + ceilBin * range);

            for (int n = floorBin; n <= ceilBin; n++)
            {
                double volToAdd;
                if (ceilBin == floorBin)
                    volToAdd = volume;
                else if (n == floorBin)
                    volToAdd = (tail / body) * volume;
                else if (n == ceilBin)
                    volToAdd = (wick / body) * volume;
                else
                    volToAdd = (range / body) * volume;

                profile[n] += volToAdd;
            }
        }

        private ProfileData FinalizeProfile(double[] histogram, double minPrice, double binSize,
            DateTime builtAt, int barsUsed, int lookbackDays)
        {
            var result = new ProfileData
            {
                BinSize = binSize,
                MinPrice = minPrice,
                Histogram = histogram,
                BuiltAt = builtAt,
                BarsUsed = barsUsed,
                LookbackDays = lookbackDays,
                Hvns = new List<VolumeNode>(),
                Lvns = new List<VolumeNode>()
            };

            int n = histogram.Length;
            if (n == 0)
            {
                result.IsValid = false;
                return result;
            }

            result.MaxPrice = minPrice + n * binSize;

            double totalVolume = 0;
            for (int i = 0; i < n; i++) totalVolume += histogram[i];
            result.TotalVolume = totalVolume;
            if (totalVolume <= 0)
            {
                result.IsValid = false;
                return result;
            }

            int pocIndex = 0;
            double maxVol = 0;
            for (int i = 0; i < n; i++)
            {
                if (histogram[i] > maxVol)
                {
                    maxVol = histogram[i];
                    pocIndex = i;
                }
            }
            result.PocBin = pocIndex;
            result.POC = minPrice + pocIndex * binSize + binSize * 0.5;

            // Value Area expand from POC
            double targetVA = totalVolume * _valueAreaPercent;
            double currentVA = histogram[pocIndex];
            int upperIdx = pocIndex + 1;
            int lowerIdx = pocIndex - 1;
            while (currentVA < targetVA && (upperIdx < n || lowerIdx >= 0))
            {
                double upVol = upperIdx < n ? histogram[upperIdx] : -1.0;
                double dnVol = lowerIdx >= 0 ? histogram[lowerIdx] : -1.0;
                if (upVol < 0 && dnVol < 0) break;
                if (upVol >= dnVol)
                {
                    currentVA += upVol;
                    upperIdx++;
                }
                else
                {
                    currentVA += dnVol;
                    lowerIdx--;
                }
            }
            int finalUpper = Math.Max(pocIndex, upperIdx - 1);
            int finalLower = Math.Min(pocIndex, lowerIdx + 1);
            result.VAH = minPrice + (finalUpper + 1) * binSize;
            result.VAL = minPrice + finalLower * binSize;

            double volAbove = 0, volBelow = 0;
            for (int i = 0; i < n; i++)
            {
                if (i > pocIndex) volAbove += histogram[i];
                else if (i < pocIndex) volBelow += histogram[i];
            }
            result.VolAbovePoc = volAbove;
            result.VolBelowPoc = volBelow;
            result.PocRelative = n > 1 ? pocIndex / (double)(n - 1) : 0.5;
            result.Shape = ClassifyShape(volAbove, volBelow, result.PocRelative);

            DetectNodes(result);
            result.IsValid = true;
            return result;
        }

        private ProfileShape ClassifyShape(double volAbove, double volBelow, double pocRelative)
        {
            double below = Math.Max(volBelow, 1e-9);
            double above = Math.Max(volAbove, 1e-9);
            double ratioAbove = above / below; // >1 top-heavy
            double ratioBelow = below / above; // >1 bottom-heavy

            // b-shape (bullish): volume mass below POC / bottom-heavy
            if (ratioBelow >= _shapeBullRatio && pocRelative <= 0.55)
                return ProfileShape.Bullish;

            // P-shape (bearish): volume mass above POC / top-heavy
            if (ratioAbove >= _shapeBearRatio && pocRelative >= 0.45)
                return ProfileShape.Bearish;

            // Classic D: POC near center and balanced mass
            if (pocRelative > 0.35 && pocRelative < 0.65 && ratioAbove < 1.15 && ratioBelow < 1.15)
                return ProfileShape.DShape;

            return ProfileShape.Neutral;
        }

        private void DetectNodes(ProfileData profile)
        {
            var h = profile.Histogram;
            int n = h.Length;
            if (n == 0) return;

            // Mean of positive bins
            double sum = 0;
            int pos = 0;
            for (int i = 0; i < n; i++)
            {
                if (h[i] > 0)
                {
                    sum += h[i];
                    pos++;
                }
            }
            double mean = pos > 0 ? sum / pos : 0;
            if (mean <= 0) return;

            double hvnCut = mean * _hvnThreshold;
            double lvnCut = mean * _lvnThreshold;

            // Contiguous HVN regions
            int iBin = 0;
            while (iBin < n)
            {
                if (h[iBin] >= hvnCut)
                {
                    int start = iBin;
                    double vol = 0;
                    while (iBin < n && h[iBin] >= hvnCut)
                    {
                        vol += h[iBin];
                        iBin++;
                    }
                    int end = iBin - 1;
                    int width = end - start + 1;
                    double avg = vol / width;
                    profile.Hvns.Add(new VolumeNode
                    {
                        Type = VolumeNodeType.HVN,
                        StartBin = start,
                        EndBin = end,
                        Low = profile.BinLow(start),
                        High = profile.BinHigh(end),
                        Volume = vol,
                        AvgVolume = avg,
                        Strength = avg / mean
                    });
                }
                else iBin++;
            }

            // Contiguous LVN regions (low but may include zeros)
            // Also split oversized runs into local-minimum pockets so we never get $80 "voids".
            iBin = 0;
            var rawLvns = new List<VolumeNode>();
            while (iBin < n)
            {
                if (h[iBin] <= lvnCut)
                {
                    int start = iBin;
                    double vol = 0;
                    while (iBin < n && h[iBin] <= lvnCut)
                    {
                        vol += h[iBin];
                        iBin++;
                    }
                    int end = iBin - 1;
                    AddLvnCandidates(profile, rawLvns, h, mean, start, end, vol);
                }
                else iBin++;
            }

            // Vacuum filter: prefer LVN between HVNs, but allow one-sided if shoulders are elevated.
            foreach (var lvn in rawLvns)
            {
                if (_maxLvnWidthPrice > 0 && (lvn.High - lvn.Low) > _maxLvnWidthPrice + 1e-9)
                    continue;

                bool hvnBelow = profile.Hvns.Any(x => x.High <= lvn.Low + 1e-9);
                bool hvnAbove = profile.Hvns.Any(x => x.Low >= lvn.High - 1e-9);

                if (_requireLvnBetweenHvns)
                {
                    // Strict sandwich OR single-side HVN with decent strength (research-friendly)
                    if (hvnBelow && hvnAbove)
                    {
                        // ideal vacuum
                    }
                    else if ((hvnBelow || hvnAbove) && lvn.Strength >= 0.25)
                    {
                        // one-sided shelf — still tradeable as rejection into thin air
                    }
                    else
                        continue;
                }

                profile.Lvns.Add(lvn);
            }

            // Sort HVN/LVN by price
            profile.Hvns = profile.Hvns.OrderBy(x => x.Low).ToList();
            profile.Lvns = profile.Lvns.OrderBy(x => x.Low).ToList();
        }

        private void AddLvnCandidates(ProfileData profile, List<VolumeNode> rawLvns, double[] h, double mean,
            int start, int end, double vol)
        {
            int width = end - start + 1;
            double widthPrice = width * profile.BinSize;

            // If region is too wide, keep only local-minimum sub-windows of max width
            int maxBins = _maxLvnWidthPrice > 0 && profile.BinSize > 0
                ? Math.Max(1, (int)Math.Floor(_maxLvnWidthPrice / profile.BinSize))
                : width;

            if (width <= maxBins)
            {
                TryAddLvn(profile, rawLvns, h, mean, start, end);
                return;
            }

            // Sliding window: pick non-overlapping local minima
            int i = start;
            while (i <= end)
            {
                int wEnd = Math.Min(end, i + maxBins - 1);
                // Find lowest-avg sub-window of size maxBins inside [i, end]
                int bestS = i;
                double bestAvg = double.MaxValue;
                int lastStart = Math.Max(i, end - maxBins + 1);
                for (int s = i; s <= Math.Min(lastStart, end); s++)
                {
                    int e = Math.Min(end, s + maxBins - 1);
                    if (e - s + 1 < Math.Min(maxBins, width)) break;
                    double sum = 0;
                    for (int b = s; b <= e; b++) sum += h[b];
                    double avg = sum / (e - s + 1);
                    if (avg < bestAvg)
                    {
                        bestAvg = avg;
                        bestS = s;
                    }
                    if (s + maxBins - 1 > end) break;
                }
                int bestE = Math.Min(end, bestS + maxBins - 1);
                TryAddLvn(profile, rawLvns, h, mean, bestS, bestE);
                i = bestE + 1;
            }
        }

        private void TryAddLvn(ProfileData profile, List<VolumeNode> rawLvns, double[] h, double mean, int start, int end)
        {
            int width = end - start + 1;
            if (width <= 0) return;
            double vol = 0;
            for (int b = start; b <= end; b++) vol += h[b];
            double avg = vol / width;

            double leftShoulder = 0, rightShoulder = 0;
            int leftCount = 0, rightCount = 0;
            for (int k = start - 1; k >= 0 && k >= start - 3; k--)
            {
                leftShoulder += h[k];
                leftCount++;
            }
            for (int k = end + 1; k < h.Length && k <= end + 3; k++)
            {
                rightShoulder += h[k];
                rightCount++;
            }
            double leftAvg = leftCount > 0 ? leftShoulder / leftCount : 0;
            double rightAvg = rightCount > 0 ? rightShoulder / rightCount : 0;
            double shoulderAvg = 0;
            int sc = 0;
            if (leftAvg > 0) { shoulderAvg += leftAvg; sc++; }
            if (rightAvg > 0) { shoulderAvg += rightAvg; sc++; }
            shoulderAvg = sc > 0 ? shoulderAvg / sc : mean;

            // Strength vs shoulders (relative vacuum) and vs global mean
            double strengthShoulder = shoulderAvg > 0
                ? Math.Clamp((shoulderAvg - avg) / shoulderAvg, 0, 1)
                : 0;
            double strengthMean = mean > 0
                ? Math.Clamp(1.0 - (avg / mean), 0, 1)
                : 0;
            // If bin is below LVN cut, mean-based strength is usually positive
            double strength = Math.Max(strengthShoulder, strengthMean * 0.85);

            bool hasShoulders = leftAvg >= mean * 0.7 || rightAvg >= mean * 0.7;
            if (!hasShoulders && strength < 0.15)
                return;

            rawLvns.Add(new VolumeNode
            {
                Type = VolumeNodeType.LVN,
                StartBin = start,
                EndBin = end,
                Low = profile.BinLow(start),
                High = profile.BinHigh(end),
                Volume = vol,
                AvgVolume = avg,
                Strength = strength
            });
        }

        /// <summary>
        /// Clean right-side volume profile (classic VP look).
        /// Does NOT paint full-chart filled HVN/LVN zones (that was causing the "messy" overlay).
        /// Logic histogram stays full-resolution; display may merge bins.
        /// </summary>
        private void DrawComposite(ProfileData profile, DateTime timeStart, DateTime timeEnd)
        {
            if (_chart == null || profile == null || !profile.IsValid) return;
            ClearVisuals();

            string prefix = "VP_";

            // Display span: only the right edge of the chart window (not multi-day full width)
            long lookbackSec = (long)Math.Abs((timeEnd - timeStart).TotalSeconds);
            if (lookbackSec < 3600) lookbackSec = 3600;
            // Histogram bar max width ≈ 12% of lookback, clamped
            long maxWidthSec = Math.Clamp((long)(lookbackSec * 0.12), 1800, 6 * 3600);
            // Level lines only across a short recent segment so chart stays readable
            long levelSpanSec = Math.Clamp((long)(lookbackSec * 0.25), 3600, 12 * 3600);
            DateTime levelStart = timeEnd.AddSeconds(-levelSpanSec);

            // --- Merge histogram into <= MaxVisualBins for drawing ---
            int srcBins = profile.BinCount;
            int visBins = Math.Min(MaxVisualBins, Math.Max(1, srcBins));
            double merge = srcBins / (double)visBins;

            double[] visVol = new double[visBins];
            double[] visLow = new double[visBins];
            double[] visHigh = new double[visBins];
            for (int v = 0; v < visBins; v++)
            {
                int s = (int)Math.Floor(v * merge);
                int e = (int)Math.Floor((v + 1) * merge) - 1;
                if (e < s) e = s;
                if (e >= srcBins) e = srcBins - 1;
                double sum = 0;
                for (int i = s; i <= e; i++) sum += profile.Histogram[i];
                visVol[v] = sum;
                visLow[v] = profile.BinLow(s);
                visHigh[v] = profile.BinHigh(e);
            }

            double maxVol = visVol.Max();
            if (maxVol <= 0) maxVol = 1.0;

            // Precompute which visual bins touch HVN/LVN (tint only — no full-chart fill)
            bool[] isHvn = new bool[visBins];
            bool[] isLvn = new bool[visBins];
            foreach (var hvn in profile.Hvns)
            {
                for (int v = 0; v < visBins; v++)
                {
                    if (visLow[v] < hvn.High && visHigh[v] > hvn.Low)
                        isHvn[v] = true;
                }
            }
            foreach (var lvn in profile.Lvns)
            {
                for (int v = 0; v < visBins; v++)
                {
                    if (visLow[v] < lvn.High && visHigh[v] > lvn.Low)
                        isLvn[v] = true;
                }
            }

            int drawnBins = 0;
            for (int v = 0; v < visBins; v++)
            {
                if (visVol[v] <= 0) continue;

                long widthSec = (long)((visVol[v] / maxVol) * maxWidthSec);
                if (widthSec < 30) widthSec = 30;
                DateTime rectLeft = timeEnd.AddSeconds(-widthSec);

                Color binColor = Color.FromArgb(160, 100, 110, 120); // slate
                if (visLow[v] <= profile.POC && visHigh[v] >= profile.POC)
                    binColor = Color.FromArgb(200, _colorPOC);
                else if (isLvn[v])
                    binColor = Color.FromArgb(140, 220, 80, 70); // soft red vacuum
                else if (isHvn[v])
                    binColor = Color.FromArgb(140, 60, 180, 100); // soft green HVN
                else if (visLow[v] >= profile.VAL && visHigh[v] <= profile.VAH)
                    binColor = Color.FromArgb(150, _colorVA);

                var rect = _chart.DrawRectangle(prefix + "Bin_" + drawnBins, timeEnd, visHigh[v], rectLeft, visLow[v], binColor);
                rect.IsFilled = true;
                drawnBins++;
            }
            _lastDrawnVisualBins = drawnBins;

            // --- POC / VA lines (short span, not entire multi-day mess) ---
            var pocLine = _chart.DrawTrendLine(prefix + "POC", levelStart, profile.POC, timeEnd, profile.POC, _colorPOC, 2, LineStyle.Solid);
            pocLine.IsInteractive = false;
            var vahLine = _chart.DrawTrendLine(prefix + "VAH", levelStart, profile.VAH, timeEnd, profile.VAH, _colorVA, 1, LineStyle.Dots);
            vahLine.IsInteractive = false;
            var valLine = _chart.DrawTrendLine(prefix + "VAL", levelStart, profile.VAL, timeEnd, profile.VAL, _colorVA, 1, LineStyle.Dots);
            valLine.IsInteractive = false;

            // --- HVN / LVN: thin mid lines only (top N by strength) — no filled bands ---
            int hi = 0;
            foreach (var hvn in profile.Hvns.OrderByDescending(x => x.Strength).Take(MaxNodeLines))
            {
                var line = _chart.DrawTrendLine(prefix + "HVN_L_" + hi, levelStart, hvn.Mid, timeEnd, hvn.Mid,
                    Color.FromArgb(180, 46, 204, 113), 1, LineStyle.DotsRare);
                line.IsInteractive = false;
                hi++;
            }
            _lastDrawnHvnLines = hi;

            int li = 0;
            foreach (var lvn in profile.Lvns.OrderByDescending(x => x.Strength).Take(MaxNodeLines))
            {
                var line = _chart.DrawTrendLine(prefix + "LVN_L_" + li, levelStart, lvn.Mid, timeEnd, lvn.Mid,
                    Color.FromArgb(200, 231, 76, 60), 1, LineStyle.DotsRare);
                line.IsInteractive = false;
                li++;
            }
            _lastDrawnLvnLines = li;

            _chart.DrawText(prefix + "Shape",
                $"VP {profile.Shape} | POC {profile.POC:F1} | HVN {profile.Hvns.Count} LVN {profile.Lvns.Count}",
                timeEnd, profile.POC, Color.FromArgb(220, 255, 255, 255));
        }

        private bool CalculateProfileLegacy(int lookbackBars, double valueAreaPercent)
        {
            if (lookbackBars <= 0 || _precision <= 0 || _bars.Count < lookbackBars)
            {
                _logger?.Warn($"VP Calculate failed: lookbackBars={lookbackBars}, precision={_precision}, bars.Count={_bars?.Count ?? 0}");
                return false;
            }

            int endIndex = _bars.Count - 2;
            int startIndex = Math.Max(0, endIndex - lookbackBars + 1);
            int barsCount = endIndex - startIndex + 1;
            if (barsCount <= 0) return false;

            double max = double.MinValue;
            double min = double.MaxValue;
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (_bars.HighPrices[i] > max) max = _bars.HighPrices[i];
                if (_bars.LowPrices[i] < min) min = _bars.LowPrices[i];
            }
            if (max <= min) return false;

            double range = (max - min) / _precision;
            if (range <= 0) return false;

            double[] profile = new double[_precision];
            for (int i = startIndex; i <= endIndex; i++)
            {
                DistributeBarVolume(profile, min, range, _precision,
                    _bars.LowPrices[i], _bars.HighPrices[i], _bars.TickVolumes[i]);
            }

            // Temporarily use configured VA percent override
            double oldVa = _valueAreaPercent;
            _valueAreaPercent = valueAreaPercent;
            var data = FinalizeProfile(profile, min, range, _bars.OpenTimes[endIndex], barsCount, 0);
            _valueAreaPercent = oldVa;

            // Legacy path: skip node detection thresholds tuned for composite if empty days
            _lastProfile = data;
            _poc = data.POC;
            _vah = data.VAH;
            _val = data.VAL;

            if (_visualize && data.IsValid)
                DrawComposite(data, _bars.OpenTimes[startIndex], _bars.OpenTimes[endIndex]);

            return data.IsValid;
        }
    }
}
