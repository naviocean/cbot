using System;
using System.Collections.Generic;
using System.Linq;

namespace cAlgo.Robots
{
    public enum SignalSide
    {
        None = 0,
        Long = 1,
        Short = -1
    }

    /// <summary>OHLC snapshot for one closed bar (shift index in series).</summary>
    public readonly struct BarSnap
    {
        public readonly double Open;
        public readonly double High;
        public readonly double Low;
        public readonly double Close;
        /// <summary>1 = most recent closed bar.</summary>
        public readonly int Shift;

        public BarSnap(double open, double high, double low, double close, int shift)
        {
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Shift = shift;
        }
    }

    public sealed class ClusterInfo
    {
        public SignalSide Side { get; set; }
        public double Level { get; set; }
        public double Extreme { get; set; }
        public int Touches { get; set; }
        public int NewestTouchShift { get; set; }
    }

    public sealed class SignalContext
    {
        /// <summary>Closed bars newest-first: index 0 = shift 1, index k = shift k+1.</summary>
        public IReadOnlyList<BarSnap> Bars { get; set; }

        public double SpreadPrice { get; set; }
        public double TickSize { get; set; }

        public bool SessionOk { get; set; }
        public bool NewsOk { get; set; }
        public bool EquityOk { get; set; }
        public bool HasOpenPosition { get; set; }
        public bool HasPending { get; set; }
        public int TradesToday { get; set; }
        public int MaxTradesPerDay { get; set; }

        public int LookbackBars { get; set; }
        public int MaxClusterAgeBars { get; set; }
        public int MinTouches { get; set; }
        public double BaseBand { get; set; }
        public double TolFactor { get; set; }
        /// <summary>Hard cap on cluster tol (price). 0 = no cap.</summary>
        public double MaxTol { get; set; }
        public int RangeBars { get; set; }
        public double MinRange { get; set; }
        public double MaxApproach { get; set; }

        public double WickBodyMin { get; set; }
        /// <summary>Reject wick / barRange minimum (0 = disabled).</summary>
        public double WickRangeMin { get; set; }
        /// <summary>Absolute min reject wick size in price (0 = disabled).</summary>
        public double MinWickPrice { get; set; }
        public double MaxBody { get; set; }
        public double ClosePosMax { get; set; }
        public double ClosePosMin { get; set; }
        public double EntryOffsetK { get; set; }

        public double SlBuffer { get; set; }
        public double SlMin { get; set; }
        public double SlMax { get; set; }

        public double MaxSpreadPrice { get; set; }

        /// <summary>Last traded cluster level for anti-spam; 0 = none.</summary>
        public double LastTradedClusterLevel { get; set; }
        public double LastTradedTol { get; set; }
        public bool HasLastTradedCluster { get; set; }

        /// <summary>When true, only Long if HtfBias==Long, only Short if HtfBias==Short. Flat → reject.</summary>
        public bool RequireHtfBias { get; set; }
        /// <summary>Long / Short / None (flat or disabled).</summary>
        public SignalSide HtfBias { get; set; }
    }

    public sealed class SignalResult
    {
        public bool IsValid { get; set; }
        public SignalSide Side { get; set; }
        public string Reason { get; set; }
        public double ClusterLevel { get; set; }
        public double ClusterExtreme { get; set; }
        public int Touches { get; set; }
        public double Tol { get; set; }
        public double Range20 { get; set; }
        public double Entry { get; set; }
        public double StopLoss { get; set; }
        public double SlDist { get; set; }
        public double ClosePos { get; set; }

        public static SignalResult Reject(string reason)
        {
            return new SignalResult
            {
                IsValid = false,
                Side = SignalSide.None,
                Reason = reason
            };
        }

        public static SignalResult Pass(
            SignalSide side,
            string reason,
            double clusterLevel,
            double clusterExtreme,
            int touches,
            double tol,
            double range20,
            double entry,
            double stopLoss,
            double slDist,
            double closePos)
        {
            return new SignalResult
            {
                IsValid = true,
                Side = side,
                Reason = reason,
                ClusterLevel = clusterLevel,
                ClusterExtreme = clusterExtreme,
                Touches = touches,
                Tol = tol,
                Range20 = range20,
                Entry = entry,
                StopLoss = stopLoss,
                SlDist = slDist,
                ClosePos = closePos
            };
        }
    }

    /// <summary>
    /// Pure closed-bar evaluator: liquidity cluster + wick reject → limit arm prices.
    /// No platform / order APIs.
    /// </summary>
    public sealed class SignalEngine
    {
        public SignalResult Evaluate(SignalContext ctx)
        {
            if (ctx == null)
                return SignalResult.Reject("REJECT:NULL_CTX");
            if (!ctx.SessionOk)
                return SignalResult.Reject("REJECT:F_SESSION");
            if (!ctx.NewsOk)
                return SignalResult.Reject("REJECT:F_NEWS");
            if (!ctx.EquityOk)
                return SignalResult.Reject("REJECT:F_EQUITY");
            if (ctx.TradesToday >= ctx.MaxTradesPerDay)
                return SignalResult.Reject("REJECT:F_MAX_TRADES");
            if (ctx.HasOpenPosition)
                return SignalResult.Reject("REJECT:F_BUSY_POS");
            if (ctx.HasPending)
                return SignalResult.Reject("REJECT:F_BUSY_PEND");
            if (ctx.SpreadPrice > ctx.MaxSpreadPrice)
                return SignalResult.Reject($"REJECT:F_SPREAD:{ctx.SpreadPrice:F4}>{ctx.MaxSpreadPrice:F4}");

            if (ctx.Bars == null || ctx.Bars.Count < Math.Max(ctx.RangeBars, 5) + 1)
                return SignalResult.Reject("REJECT:F_BARS");

            int lookback = Math.Min(ctx.LookbackBars, ctx.Bars.Count);
            if (lookback < ctx.MinTouches + 2)
                return SignalResult.Reject("REJECT:F_LOOKBACK");

            double range20 = ComputeRange(ctx.Bars, Math.Min(ctx.RangeBars, ctx.Bars.Count));
            if (range20 < ctx.MinRange)
                return SignalResult.Reject($"REJECT:F_RANGE:{range20:F4}<{ctx.MinRange:F4}");

            double tol = Math.Max(ctx.BaseBand, ctx.TolFactor * range20);
            if (ctx.MaxTol > 0 && tol > ctx.MaxTol)
                tol = ctx.MaxTol;
            if (tol <= 0)
                return SignalResult.Reject("REJECT:F_TOL");

            var signalBar = ctx.Bars[0];
            double close = signalBar.Close;

            // Anti-spam: while price still near last traded level, block re-arm.
            // Robot clears HasLastTradedCluster once price leaves > 2×tol (so return later is allowed).
            if (ctx.HasLastTradedCluster && ctx.LastTradedTol > 0)
            {
                double leave = 2.0 * ctx.LastTradedTol;
                if (Math.Abs(close - ctx.LastTradedClusterLevel) <= leave)
                    return SignalResult.Reject("REJECT:F_ANTISPAM");
            }

            // Candidates within MaxApproach, nearest first; first wick-confirm wins (PRD §5.1).
            var sellCandidates = FindClusters(
                ctx.Bars, lookback, tol, ctx.MinTouches, ctx.MaxClusterAgeBars,
                highSide: true, close, ctx.MaxApproach);
            var buyCandidates = FindClusters(
                ctx.Bars, lookback, tol, ctx.MinTouches, ctx.MaxClusterAgeBars,
                highSide: false, close, ctx.MaxApproach);

            ClusterInfo sellCluster = null;
            ClusterInfo buyCluster = null;
            double shortClosePos = 0;
            double longClosePos = 0;
            string shortWickFail = null;
            string longWickFail = null;

            foreach (var c in sellCandidates)
            {
                if (IsShortWickConfirm(signalBar, c, tol, ctx, out shortClosePos, out shortWickFail))
                {
                    sellCluster = c;
                    shortWickFail = null;
                    break;
                }
            }

            foreach (var c in buyCandidates)
            {
                if (IsLongWickConfirm(signalBar, c, tol, ctx, out longClosePos, out longWickFail))
                {
                    buyCluster = c;
                    longWickFail = null;
                    break;
                }
            }

            bool sellWick = sellCluster != null;
            bool buyWick = buyCluster != null;

            if (sellWick && buyWick)
                return SignalResult.Reject("REJECT:F_BOTH_SIDES");

            // HTF bias: block fade against higher-TF direction (v1.1)
            if (ctx.RequireHtfBias)
            {
                if (ctx.HtfBias == SignalSide.None)
                    return SignalResult.Reject("REJECT:F_HTF_FLAT");
                if (sellWick && ctx.HtfBias != SignalSide.Short)
                    return SignalResult.Reject($"REJECT:F_HTF_BIAS:short blocked (bias={ctx.HtfBias})");
                if (buyWick && ctx.HtfBias != SignalSide.Long)
                    return SignalResult.Reject($"REJECT:F_HTF_BIAS:long blocked (bias={ctx.HtfBias})");
            }

            if (sellWick)
            {
                double entry = sellCluster.Level + ctx.EntryOffsetK * tol;
                // Structure SL, then expand to SlMin (v1.1) — do not reject tight structure.
                double sl = sellCluster.Extreme + ctx.SlBuffer;
                if (sl <= entry)
                    sl = entry + ctx.SlMin;
                if (sl - entry < ctx.SlMin)
                    sl = entry + ctx.SlMin;
                double slDist = sl - entry;
                if (slDist > ctx.SlMax)
                    return SignalResult.Reject(
                        $"REJECT:F_SL_BAND:wide {slDist:F2}>{ctx.SlMax:F2} (struct expanded to min {ctx.SlMin:F2})");

                return SignalResult.Pass(
                    SignalSide.Short,
                    "E_ARM:SHORT",
                    sellCluster.Level,
                    sellCluster.Extreme,
                    sellCluster.Touches,
                    tol,
                    range20,
                    entry,
                    sl,
                    slDist,
                    shortClosePos);
            }

            if (buyWick)
            {
                double entry = buyCluster.Level - ctx.EntryOffsetK * tol;
                double sl = buyCluster.Extreme - ctx.SlBuffer;
                if (sl >= entry)
                    sl = entry - ctx.SlMin;
                if (entry - sl < ctx.SlMin)
                    sl = entry - ctx.SlMin;
                double slDist = entry - sl;
                if (slDist > ctx.SlMax)
                    return SignalResult.Reject(
                        $"REJECT:F_SL_BAND:wide {slDist:F2}>{ctx.SlMax:F2} (struct expanded to min {ctx.SlMin:F2})");

                return SignalResult.Pass(
                    SignalSide.Long,
                    "E_ARM:LONG",
                    buyCluster.Level,
                    buyCluster.Extreme,
                    buyCluster.Touches,
                    tol,
                    range20,
                    entry,
                    sl,
                    slDist,
                    longClosePos);
            }

            if (sellCandidates.Count == 0 && buyCandidates.Count == 0)
            {
                // Relaxed diagnostic: densest touch count ignoring approach (helps BT journals)
                int maxHi = MaxTouchDensity(ctx.Bars, lookback, tol, highSide: true);
                int maxLo = MaxTouchDensity(ctx.Bars, lookback, tol, highSide: false);
                return SignalResult.Reject(
                    $"REJECT:F_TOUCHES:tol={tol:F2} range20={range20:F2} appr={ctx.MaxApproach:F2} " +
                    $"maxTouchHi={maxHi} maxTouchLo={maxLo} need>={ctx.MinTouches}");
            }

            string wickDetail = shortWickFail ?? longWickFail ?? "no_confirm";
            return SignalResult.Reject(
                $"REJECT:F_WICK:{wickDetail} sellCand={sellCandidates.Count} buyCand={buyCandidates.Count} " +
                $"tol={tol:F2} body={Math.Abs(signalBar.Close - signalBar.Open):F2} maxBody={ctx.MaxBody:F2}");
        }

        private static int MaxTouchDensity(IReadOnlyList<BarSnap> bars, int lookback, double tol, bool highSide)
        {
            int best = 0;
            for (int i = 0; i < lookback; i++)
            {
                double seed = highSide ? bars[i].High : bars[i].Low;
                int n = 0;
                for (int j = 0; j < lookback; j++)
                {
                    double ex = highSide ? bars[j].High : bars[j].Low;
                    if (Math.Abs(ex - seed) <= tol)
                        n++;
                }
                if (n > best) best = n;
            }
            return best;
        }

        private static double ComputeRange(IReadOnlyList<BarSnap> bars, int n)
        {
            double hi = double.MinValue;
            double lo = double.MaxValue;
            int count = Math.Min(n, bars.Count);
            for (int i = 0; i < count; i++)
            {
                if (bars[i].High > hi) hi = bars[i].High;
                if (bars[i].Low < lo) lo = bars[i].Low;
            }
            return hi - lo;
        }

        /// <summary>
        /// Density clusters: seed each extreme, recount vs median, dedupe,
        /// filter by MaxApproach, order nearest to close first.
        /// </summary>
        private static List<ClusterInfo> FindClusters(
            IReadOnlyList<BarSnap> bars,
            int lookback,
            double tol,
            int minTouches,
            int maxAge,
            bool highSide,
            double close,
            double maxApproach)
        {
            var raw = new List<ClusterInfo>();

            for (int i = 0; i < lookback; i++)
            {
                double seed = highSide ? bars[i].High : bars[i].Low;
                var extremes = new List<double>();
                int newest = int.MaxValue;

                for (int j = 0; j < lookback; j++)
                {
                    double ex = highSide ? bars[j].High : bars[j].Low;
                    if (Math.Abs(ex - seed) <= tol)
                    {
                        extremes.Add(ex);
                        int shift = bars[j].Shift;
                        if (shift < newest) newest = shift;
                    }
                }

                if (extremes.Count < minTouches)
                    continue;

                double median = Median(extremes);
                extremes.Clear();
                newest = int.MaxValue;
                for (int j = 0; j < lookback; j++)
                {
                    double ex = highSide ? bars[j].High : bars[j].Low;
                    if (Math.Abs(ex - median) <= tol)
                    {
                        extremes.Add(ex);
                        int shift = bars[j].Shift;
                        if (shift < newest) newest = shift;
                    }
                }

                if (extremes.Count < minTouches || newest > maxAge)
                    continue;

                // PRD: only clusters with approach ≤ MaxApproach are candidates
                if (Math.Abs(median - close) > maxApproach)
                    continue;

                raw.Add(new ClusterInfo
                {
                    Side = highSide ? SignalSide.Short : SignalSide.Long,
                    Level = median,
                    Extreme = highSide ? extremes.Max() : extremes.Min(),
                    Touches = extremes.Count,
                    NewestTouchShift = newest
                });
            }

            if (raw.Count == 0)
                return new List<ClusterInfo>();

            // Dedupe near-identical medians — keep more touches, then fresher.
            raw = raw
                .OrderByDescending(c => c.Touches)
                .ThenBy(c => c.NewestTouchShift)
                .ToList();

            var unique = new List<ClusterInfo>();
            foreach (var c in raw)
            {
                bool near = unique.Any(u => Math.Abs(u.Level - c.Level) <= tol);
                if (!near)
                    unique.Add(c);
            }

            return unique
                .OrderBy(c => Math.Abs(c.Level - close))
                .ThenByDescending(c => c.Touches)
                .ToList();
        }

        /// <summary>
        /// Rejection wick: pass if ANY of (wick/body, wick/range, abs wick pips) holds,
        /// plus body/closePos/level filters.
        /// </summary>
        private static bool RejectWickOk(double rejectWick, double body, double barRange, SignalContext ctx, out string failReason)
        {
            failReason = null;
            double tick = ctx.TickSize > 0 ? ctx.TickSize : 1e-5;
            double bodyForRatio = Math.Max(body, tick);
            bool byBody = rejectWick >= ctx.WickBodyMin * bodyForRatio;
            bool byRange = ctx.WickRangeMin > 0 && barRange > 0 && (rejectWick / barRange) >= ctx.WickRangeMin;
            bool byAbs = ctx.MinWickPrice > 0 && rejectWick >= ctx.MinWickPrice;
            if (byBody || byRange || byAbs)
                return true;

            failReason =
                $"wick_fail:w={rejectWick:F2} need body>={ctx.WickBodyMin:F1}x{bodyForRatio:F2}" +
                (ctx.WickRangeMin > 0 ? $" OR range>={ctx.WickRangeMin:F2}x{barRange:F2}" : "") +
                (ctx.MinWickPrice > 0 ? $" OR abs>={ctx.MinWickPrice:F2}" : "");
            return false;
        }

        private static bool IsShortWickConfirm(
            BarSnap bar,
            ClusterInfo cluster,
            double tol,
            SignalContext ctx,
            out double closePos,
            out string failReason)
        {
            closePos = 0;
            failReason = null;
            double body = Math.Abs(bar.Close - bar.Open);
            double upperWick = bar.High - Math.Max(bar.Open, bar.Close);
            double barRange = bar.High - bar.Low;
            if (barRange <= 0)
            {
                failReason = "zero_range";
                return false;
            }

            closePos = (bar.Close - bar.Low) / barRange;

            if (!RejectWickOk(upperWick, body, barRange, ctx, out failReason))
                return false;
            if (body > ctx.MaxBody)
            {
                failReason = $"body:{body:F2}>{ctx.MaxBody:F2}";
                return false;
            }
            if (closePos > ctx.ClosePosMax)
            {
                failReason = $"closePos:{closePos:F2}>{ctx.ClosePosMax:F2}";
                return false;
            }

            bool atLevel =
                Math.Abs(bar.High - cluster.Level) <= tol ||
                (bar.High >= cluster.Level - tol && bar.High <= cluster.Extreme + tol);
            if (!atLevel)
            {
                failReason = $"high_not_at_cluster:h={bar.High:F2} L={cluster.Level:F2}";
                return false;
            }

            if (bar.Close >= cluster.Level)
            {
                failReason = "close_not_below_cluster";
                return false;
            }

            return true;
        }

        private static bool IsLongWickConfirm(
            BarSnap bar,
            ClusterInfo cluster,
            double tol,
            SignalContext ctx,
            out double closePos,
            out string failReason)
        {
            closePos = 0;
            failReason = null;
            double body = Math.Abs(bar.Close - bar.Open);
            double lowerWick = Math.Min(bar.Open, bar.Close) - bar.Low;
            double barRange = bar.High - bar.Low;
            if (barRange <= 0)
            {
                failReason = "zero_range";
                return false;
            }

            closePos = (bar.Close - bar.Low) / barRange;

            if (!RejectWickOk(lowerWick, body, barRange, ctx, out failReason))
                return false;
            if (body > ctx.MaxBody)
            {
                failReason = $"body:{body:F2}>{ctx.MaxBody:F2}";
                return false;
            }
            if (closePos < ctx.ClosePosMin)
            {
                failReason = $"closePos:{closePos:F2}<{ctx.ClosePosMin:F2}";
                return false;
            }

            bool atLevel =
                Math.Abs(bar.Low - cluster.Level) <= tol ||
                (bar.Low <= cluster.Level + tol && bar.Low >= cluster.Extreme - tol);
            if (!atLevel)
            {
                failReason = $"low_not_at_cluster:l={bar.Low:F2} L={cluster.Level:F2}";
                return false;
            }

            if (bar.Close <= cluster.Level)
            {
                failReason = "close_not_above_cluster";
                return false;
            }

            return true;
        }

        private static double Median(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0;
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            if (n % 2 == 1)
                return sorted[n / 2];
            return 0.5 * (sorted[n / 2 - 1] + sorted[n / 2]);
        }
    }
}
