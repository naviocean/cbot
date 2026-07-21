using System;
using System.Collections.Generic;

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

    public sealed class SignalContext
    {
        /// <summary>Closed bars newest-first: index 0 = shift 1.</summary>
        public IReadOnlyList<BarSnap> Bars { get; set; }

        public double SpreadPrice { get; set; }
        public double MaxSpreadPrice { get; set; }
        public double Atr { get; set; }

        public bool SessionOk { get; set; }
        public bool NewsOk { get; set; }
        public bool EquityOk { get; set; }
        public bool HasOpenPosition { get; set; }
        public int TradesToday { get; set; }
        public int MaxTradesPerDay { get; set; }

        /// <summary>Bars remaining in cooldown (0 = free).</summary>
        public int CooldownBarsLeft { get; set; }

        public int PivotStrength { get; set; }
        public int LookbackBars { get; set; }
        public int MaxLegAgeBars { get; set; }

        public double MinImpulseAtr { get; set; }
        public double FibLevel { get; set; }
        public double ZoneHalf { get; set; }
        public double SlBuffer { get; set; }
        public double SlMin { get; set; }
        public double SlMax { get; set; }

        /// <summary>Anti-spam: last traded impulse anchors (0 = none).</summary>
        public bool HasLastTradedLeg { get; set; }
        public double LastTradedLegHigh { get; set; }
        public double LastTradedLegLow { get; set; }

        /// <summary>
        /// 0 = Off, 1 = Align (must match HTF; flat reject), 2 = BlockCounter (only reject opposite; flat OK).
        /// </summary>
        public int HtfMode { get; set; }
        /// <summary>Long / Short / None (flat).</summary>
        public SignalSide HtfBias { get; set; }
        /// <summary>Apply HTF gate to long setups.</summary>
        public bool HtfFilterLong { get; set; }
        /// <summary>Apply HTF gate to short setups.</summary>
        public bool HtfFilterShort { get; set; }
    }

    public sealed class SignalResult
    {
        public bool IsValid { get; set; }
        public SignalSide Side { get; set; }
        public string Reason { get; set; }

        public double LegHigh { get; set; }
        public double LegLow { get; set; }
        public double FibLevelPrice { get; set; }
        public double ImpulseSize { get; set; }
        public int ExtremeShift { get; set; }
        public int OriginShift { get; set; }

        public double Entry { get; set; }
        public double StopLoss { get; set; }
        public double SlDist { get; set; }

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
            double legHigh,
            double legLow,
            double fibLevelPrice,
            double impulseSize,
            int extremeShift,
            int originShift,
            double entry,
            double stopLoss,
            double slDist)
        {
            return new SignalResult
            {
                IsValid = true,
                Side = side,
                Reason = reason,
                LegHigh = legHigh,
                LegLow = legLow,
                FibLevelPrice = fibLevelPrice,
                ImpulseSize = impulseSize,
                ExtremeShift = extremeShift,
                OriginShift = originShift,
                Entry = entry,
                StopLoss = stopLoss,
                SlDist = slDist
            };
        }
    }

    internal readonly struct Pivot
    {
        public readonly bool IsHigh;
        public readonly double Price;
        /// <summary>Index in Bars list (0 = newest closed).</summary>
        public readonly int Index;
        public readonly int Shift;

        public Pivot(bool isHigh, double price, int index, int shift)
        {
            IsHigh = isHigh;
            Price = price;
            Index = index;
            Shift = shift;
        }
    }

    /// <summary>
    /// Pure closed-bar evaluator: N-bar swings → HH/LL impulse → Fib deep pullback + candle confirm.
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
            if (ctx.CooldownBarsLeft > 0)
                return SignalResult.Reject($"REJECT:F_COOLDOWN:{ctx.CooldownBarsLeft}");
            if (ctx.SpreadPrice > ctx.MaxSpreadPrice)
                return SignalResult.Reject($"REJECT:F_SPREAD:{ctx.SpreadPrice:F4}>{ctx.MaxSpreadPrice:F4}");

            int n = Math.Max(2, ctx.PivotStrength);
            int need = Math.Max(ctx.LookbackBars, n * 2 + 5);
            if (ctx.Bars == null || ctx.Bars.Count < need)
                return SignalResult.Reject("REJECT:F_BARS");

            if (ctx.Atr <= 0)
                return SignalResult.Reject("REJECT:F_ATR");

            double fibFrac = ctx.FibLevel > 0 && ctx.FibLevel < 1 ? ctx.FibLevel : 0.786;
            double minImpulse = ctx.MinImpulseAtr * ctx.Atr;
            if (minImpulse <= 0)
                return SignalResult.Reject("REJECT:F_MIN_IMPULSE_CFG");

            var pivots = CollectPivots(ctx.Bars, n, Math.Min(ctx.LookbackBars, ctx.Bars.Count));
            if (pivots.Count < 3)
                return SignalResult.Reject("REJECT:F_PIVOTS");

            if (!TryBuildActiveLeg(pivots, minImpulse, ctx.MaxLegAgeBars, out var side, out double legHigh, out double legLow,
                    out int extremeIdx, out int originIdx, out string legFail))
                return SignalResult.Reject(legFail);

            double impulse = legHigh - legLow;
            if (impulse < minImpulse)
                return SignalResult.Reject($"REJECT:F_IMPULSE:{impulse:F4}<{minImpulse:F4}");

            // Same leg already traded
            if (ctx.HasLastTradedLeg &&
                NearlyEqual(legHigh, ctx.LastTradedLegHigh) &&
                NearlyEqual(legLow, ctx.LastTradedLegLow))
                return SignalResult.Reject("REJECT:F_SAME_LEG");

            // HTF gate (v1.2): Align = must match; BlockCounter = only kill opposite; flat OK in BlockCounter
            if (ctx.HtfMode > 0)
            {
                bool apply = (side == SignalSide.Long && ctx.HtfFilterLong) ||
                             (side == SignalSide.Short && ctx.HtfFilterShort);
                if (apply)
                {
                    // Mode 1 Align: require same direction; flat → reject
                    if (ctx.HtfMode == 1)
                    {
                        if (ctx.HtfBias == SignalSide.None)
                            return SignalResult.Reject("REJECT:F_HTF_FLAT");
                        if (side != ctx.HtfBias)
                            return SignalResult.Reject($"REJECT:F_HTF_ALIGN:{side} vs bias={ctx.HtfBias}");
                    }
                    // Mode 2 BlockCounter: only reject clear opposite; flat allowed
                    else if (ctx.HtfMode == 2)
                    {
                        if (side == SignalSide.Long && ctx.HtfBias == SignalSide.Short)
                            return SignalResult.Reject("REJECT:F_HTF_COUNTER:long vs bias=Short");
                        if (side == SignalSide.Short && ctx.HtfBias == SignalSide.Long)
                            return SignalResult.Reject("REJECT:F_HTF_COUNTER:short vs bias=Long");
                    }
                }
            }

            var signalBar = ctx.Bars[0];
            double invBuf = Math.Max(ctx.SlBuffer, ctx.Atr * 0.05);

            // Invalidate if structure broken through origin
            if (side == SignalSide.Long && signalBar.Close < legLow - invBuf)
                return SignalResult.Reject($"REJECT:F_INV_LONG:{signalBar.Close:F2}<{legLow - invBuf:F2}");
            if (side == SignalSide.Short && signalBar.Close > legHigh + invBuf)
                return SignalResult.Reject($"REJECT:F_INV_SHORT:{signalBar.Close:F2}>{legHigh + invBuf:F2}");

            double l786 = side == SignalSide.Long
                ? legHigh - fibFrac * impulse
                : legLow + fibFrac * impulse;

            double zone = Math.Max(ctx.ZoneHalf, ctx.Atr * 0.02);
            if (!TouchedZone(signalBar, l786, zone))
                return SignalResult.Reject($"REJECT:F_NO_TOUCH:L786={l786:F2} z={zone:F4}");

            if (!CandleConfirm(signalBar, side, l786))
                return SignalResult.Reject($"REJECT:F_NO_CONFIRM:side={side} close={signalBar.Close:F2}");

            // Entry reference = signal close (market); SL from structure origin
            double entry = signalBar.Close;
            double sl;
            if (side == SignalSide.Long)
            {
                sl = legLow - ctx.SlBuffer;
                if (entry - sl < ctx.SlMin)
                    sl = entry - ctx.SlMin;
                if (entry - sl > ctx.SlMax)
                    sl = entry - ctx.SlMax;
                if (sl >= entry)
                    return SignalResult.Reject("REJECT:F_SL_LONG");
            }
            else
            {
                sl = legHigh + ctx.SlBuffer;
                if (sl - entry < ctx.SlMin)
                    sl = entry + ctx.SlMin;
                if (sl - entry > ctx.SlMax)
                    sl = entry + ctx.SlMax;
                if (sl <= entry)
                    return SignalResult.Reject("REJECT:F_SL_SHORT");
            }

            double slDist = Math.Abs(entry - sl);
            if (slDist < ctx.SlMin * 0.99)
                return SignalResult.Reject($"REJECT:F_SL_MIN:{slDist:F4}<{ctx.SlMin:F4}");
            if (ctx.SlMax > 0 && slDist > ctx.SlMax * 1.01)
                return SignalResult.Reject($"REJECT:F_SL_MAX:{slDist:F4}>{ctx.SlMax:F4}");

            string passCode = side == SignalSide.Long ? "E_LONG" : "E_SHORT";
            return SignalResult.Pass(
                side,
                passCode,
                legHigh,
                legLow,
                l786,
                impulse,
                ctx.Bars[extremeIdx].Shift,
                ctx.Bars[originIdx].Shift,
                entry,
                sl,
                slDist);
        }

        /// <summary>
        /// Build most recent HH or LL impulse leg that still has age ≤ MaxLegAgeBars.
        /// Prefers the newer extreme between competing long/short candidates.
        /// </summary>
        private static bool TryBuildActiveLeg(
            List<Pivot> pivotsChronological,
            double minImpulse,
            int maxLegAgeBars,
            out SignalSide side,
            out double legHigh,
            out double legLow,
            out int extremeIdx,
            out int originIdx,
            out string fail)
        {
            side = SignalSide.None;
            legHigh = 0;
            legLow = 0;
            extremeIdx = -1;
            originIdx = -1;
            fail = "REJECT:F_NO_LEG";

            // pivotsChronological: oldest → newest (by chart time = decreasing list index)
            Pivot? lastHigh = null;
            Pivot? prevHigh = null;
            Pivot? lastLow = null;
            Pivot? prevLow = null;

            // Track running last high/low for origin lookup while scanning
            Pivot? runLastHigh = null;
            Pivot? runLastLow = null;

            SignalSide bestSide = SignalSide.None;
            double bestHigh = 0, bestLow = 0;
            int bestExt = -1, bestOrg = -1;
            int bestAge = int.MaxValue;

            foreach (var p in pivotsChronological)
            {
                if (p.IsHigh)
                {
                    prevHigh = lastHigh;
                    lastHigh = p;

                    // HH if previous high exists and this is higher
                    if (prevHigh.HasValue && p.Price > prevHigh.Value.Price + 1e-9)
                    {
                        // Origin = last swing low before this high (runLastLow updated only by lows so far)
                        if (runLastLow.HasValue && p.Price - runLastLow.Value.Price >= minImpulse)
                        {
                            int age = p.Index; // bars since extreme (list index ≈ age of center)
                            if (age <= maxLegAgeBars && age < bestAge)
                            {
                                bestSide = SignalSide.Long;
                                bestHigh = p.Price;
                                bestLow = runLastLow.Value.Price;
                                bestExt = p.Index;
                                bestOrg = runLastLow.Value.Index;
                                bestAge = age;
                            }
                        }
                    }

                    runLastHigh = p;
                }
                else
                {
                    prevLow = lastLow;
                    lastLow = p;

                    if (prevLow.HasValue && p.Price < prevLow.Value.Price - 1e-9)
                    {
                        if (runLastHigh.HasValue && runLastHigh.Value.Price - p.Price >= minImpulse)
                        {
                            int age = p.Index;
                            if (age <= maxLegAgeBars && age < bestAge)
                            {
                                bestSide = SignalSide.Short;
                                bestHigh = runLastHigh.Value.Price;
                                bestLow = p.Price;
                                bestExt = p.Index;
                                bestOrg = runLastHigh.Value.Index;
                                bestAge = age;
                            }
                        }
                    }

                    runLastLow = p;
                }
            }

            // Prefer the most recent structure event: re-scan for newest HH/LL only
            // (bestAge already picks youngest extreme among valid legs)
            if (bestSide == SignalSide.None)
            {
                fail = "REJECT:F_NO_HH_LL";
                return false;
            }

            side = bestSide;
            legHigh = bestHigh;
            legLow = bestLow;
            extremeIdx = bestExt;
            originIdx = bestOrg;
            fail = null;
            return true;
        }

        /// <summary>
        /// Confirmed N-bar pivots, returned oldest → newest.
        /// Bars: newest-first; pivot center at index i needs N bars with lower index (newer)
        /// and N bars with higher index (older).
        /// </summary>
        private static List<Pivot> CollectPivots(IReadOnlyList<BarSnap> bars, int n, int lookback)
        {
            var raw = new List<Pivot>();
            int maxI = Math.Min(lookback - 1, bars.Count - 1);
            // Center must have n newer (i-n >= 0) and n older (i+n <= max usable)
            for (int i = n; i <= maxI - n; i++)
            {
                if (IsPivotHigh(bars, i, n))
                    raw.Add(new Pivot(true, bars[i].High, i, bars[i].Shift));
                if (IsPivotLow(bars, i, n))
                    raw.Add(new Pivot(false, bars[i].Low, i, bars[i].Shift));
            }

            // Oldest first: higher index first
            raw.Sort((a, b) => b.Index.CompareTo(a.Index));
            return raw;
        }

        private static bool IsPivotHigh(IReadOnlyList<BarSnap> bars, int i, int n)
        {
            double h = bars[i].High;
            for (int j = 1; j <= n; j++)
            {
                // newer (right on chart)
                if (bars[i - j].High >= h)
                    return false;
                // older (left)
                if (bars[i + j].High >= h)
                    return false;
            }
            return true;
        }

        private static bool IsPivotLow(IReadOnlyList<BarSnap> bars, int i, int n)
        {
            double l = bars[i].Low;
            for (int j = 1; j <= n; j++)
            {
                if (bars[i - j].Low <= l)
                    return false;
                if (bars[i + j].Low <= l)
                    return false;
            }
            return true;
        }

        private static bool TouchedZone(BarSnap bar, double level, double zone)
        {
            double lo = level - zone;
            double hi = level + zone;
            // Bar range overlaps zone
            return bar.Low <= hi && bar.High >= lo;
        }

        private static bool CandleConfirm(BarSnap bar, SignalSide side, double level)
        {
            if (side == SignalSide.Long)
                return bar.Close > bar.Open && bar.Close >= level;
            if (side == SignalSide.Short)
                return bar.Close < bar.Open && bar.Close <= level;
            return false;
        }

        private static bool NearlyEqual(double a, double b)
        {
            return Math.Abs(a - b) <= 1e-6 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
        }
    }
}
