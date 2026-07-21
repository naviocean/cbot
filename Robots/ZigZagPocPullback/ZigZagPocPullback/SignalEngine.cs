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

    public enum ZoneMode
    {
        Poc = 0,
        Fib = 1
    }

    /// <summary>OHLC snapshot; index 0 = most recent closed bar.</summary>
    public readonly struct BarSnap
    {
        public readonly double Open;
        public readonly double High;
        public readonly double Low;
        public readonly double Close;
        public readonly DateTime OpenTime;
        /// <summary>1 = most recent closed bar.</summary>
        public readonly int Shift;

        public BarSnap(double open, double high, double low, double close, DateTime openTime, int shift)
        {
            Open = open;
            High = high;
            Low = low;
            Close = close;
            OpenTime = openTime;
            Shift = shift;
        }
    }

    public sealed class SignalContext
    {
        /// <summary>Closed bars newest-first: index 0 = shift 1.</summary>
        public IReadOnlyList<BarSnap> Bars { get; set; }

        public double Atr { get; set; }
        public double SpreadPrice { get; set; }
        public double MaxSpreadPrice { get; set; }

        public bool SessionOk { get; set; }
        public bool EquityOk { get; set; }

        public int LongPositions { get; set; }
        public int ShortPositions { get; set; }
        public int MaxPositionsPerSide { get; set; }

        /// <summary>
        /// Turning points from the real ZigZag indicator Result (oldest→newest).
        /// tip=last, z1=second-last, z2/z3 before. Robot fills this — engine does not recompute ZZ.
        /// </summary>
        public IReadOnlyList<ZzPivot> ZigZagPoints { get; set; }

        public bool UseStructureFilter { get; set; }

        public ZoneMode ZoneMode { get; set; }
        public double BufferAtrRatio { get; set; }
        public double SlAtrRatio { get; set; }

        /// <summary>Rolling POC price when ZoneMode=Poc; ignored for Fib.</summary>
        public double PocPrice { get; set; }
        public bool PocValid { get; set; }

        /// <summary>Live mid/last for zone fill check (0 = use bar close only / arm path).</summary>
        public double LivePrice { get; set; }

        /// <summary>If true, require LivePrice (or close) inside zone for PASS.</summary>
        public bool RequireInZone { get; set; }

        public bool HasLastTradedZ1 { get; set; }
        public long LastTradedZ1Key { get; set; }
    }

    public sealed class SignalResult
    {
        public bool IsValid { get; set; }
        public SignalSide Side { get; set; }
        public string Reason { get; set; }

        public double Z1Price { get; set; }
        public double Z2Price { get; set; }
        public double Z3Price { get; set; }
        public bool Z1IsHigh { get; set; }
        public long Z1Key { get; set; }

        public double ZoneLow { get; set; }
        public double ZoneHigh { get; set; }
        public double StopLoss { get; set; }
        public double SlDist { get; set; }
        public double EntryRef { get; set; }

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
            double z1,
            double z2,
            double z3,
            bool z1IsHigh,
            long z1Key,
            double zoneLow,
            double zoneHigh,
            double stopLoss,
            double slDist,
            double entryRef)
        {
            return new SignalResult
            {
                IsValid = true,
                Side = side,
                Reason = reason,
                Z1Price = z1,
                Z2Price = z2,
                Z3Price = z3,
                Z1IsHigh = z1IsHigh,
                Z1Key = z1Key,
                ZoneLow = zoneLow,
                ZoneHigh = zoneHigh,
                StopLoss = stopLoss,
                SlDist = slDist,
                EntryRef = entryRef
            };
        }
    }

    /// <summary>Confirmed or tip ZigZag pivot for logic + chart visuals.</summary>
    public readonly struct ZzPivot
    {
        public readonly bool IsHigh;
        public readonly double Price;
        /// <summary>Bar index in chronological array (0 = oldest).</summary>
        public readonly int BarIndex;
        public readonly DateTime OpenTime;

        public ZzPivot(bool isHigh, double price, int barIndex, DateTime openTime)
        {
            IsHigh = isHigh;
            Price = price;
            BarIndex = barIndex;
            OpenTime = openTime;
        }

        public long Key => OpenTime.Ticks ^ (IsHigh ? 1L : 0L);
    }

    /// <summary>
    /// Pure evaluator: confirmed ZigZag pullback + POC/Fib zone.
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
            if (!ctx.EquityOk)
                return SignalResult.Reject("REJECT:F_EQUITY");
            if (ctx.SpreadPrice > ctx.MaxSpreadPrice && ctx.MaxSpreadPrice > 0)
                return SignalResult.Reject($"REJECT:F_SPREAD:{ctx.SpreadPrice:F4}>{ctx.MaxSpreadPrice:F4}");
            if (ctx.Atr <= 0)
                return SignalResult.Reject("REJECT:F_ATR");
            if (ctx.Bars == null || ctx.Bars.Count < 30)
                return SignalResult.Reject("REJECT:F_BARS");

            // z1/z2/z3 from indicator only — no second ZZ implementation
            var pivots = ctx.ZigZagPoints;
            if (pivots == null || pivots.Count < 4)
                return SignalResult.Reject("REJECT:F_ZZ_NONE");

            // tip = last (can repaint); z1 confirmed once tip exists
            var tip = pivots[pivots.Count - 1];
            var z1 = pivots[pivots.Count - 2];
            var z2 = pivots[pivots.Count - 3];
            var z3 = pivots[pivots.Count - 4];

            if (z1.IsHigh == tip.IsHigh)
                return SignalResult.Reject("REJECT:F_ZZ_UNCONF");

            long z1Key = z1.Key;
            if (ctx.HasLastTradedZ1 && ctx.LastTradedZ1Key == z1Key)
                return SignalResult.Reject("REJECT:F_Z1_USED");

            SignalSide side;
            if (!z1.IsHigh && z1.Price < z3.Price)
                side = SignalSide.Long;
            else if (z1.IsHigh && z1.Price > z3.Price)
                side = SignalSide.Short;
            else
                return SignalResult.Reject($"REJECT:F_SIDE:z1={(z1.IsHigh ? "H" : "L")}@{z1.Price:F2} z3={z3.Price:F2}");

            if (side == SignalSide.Long && ctx.LongPositions >= ctx.MaxPositionsPerSide)
                return SignalResult.Reject($"REJECT:F_MAX_POS:long={ctx.LongPositions}>={ctx.MaxPositionsPerSide}");
            if (side == SignalSide.Short && ctx.ShortPositions >= ctx.MaxPositionsPerSide)
                return SignalResult.Reject($"REJECT:F_MAX_POS:short={ctx.ShortPositions}>={ctx.MaxPositionsPerSide}");

            if (ctx.UseStructureFilter && !StructureOk(pivots, side))
                return SignalResult.Reject("REJECT:F_STRUCT");

            double buf = Math.Max(ctx.Atr * Math.Max(0.0, ctx.BufferAtrRatio), ctx.Atr * 0.02);
            if (!TryBuildZone(ctx, side, z1, z2, buf, out double zoneLow, out double zoneHigh, out string zoneFail))
                return SignalResult.Reject(zoneFail);

            double price = ctx.LivePrice > 0
                ? ctx.LivePrice
                : (ctx.Bars.Count > 0 ? ctx.Bars[0].Close : 0);
            if (ctx.RequireInZone && (price < zoneLow || price > zoneHigh))
                return SignalResult.Reject($"REJECT:F_ZONE:px={price:F2} [{zoneLow:F2},{zoneHigh:F2}]");

            double slRatio = ctx.SlAtrRatio > 0 ? ctx.SlAtrRatio : 1.0;
            double slPad = slRatio * ctx.Atr;
            double sl;
            if (side == SignalSide.Long)
            {
                // z2 should be the prior high (impulse top) for a bottom z1 pullback
                sl = z2.Price - slPad;
                // If z2 is not above entry path, still use z2 as structural anchor
                if (sl >= price)
                    sl = Math.Min(z1.Price, z2.Price) - slPad;
            }
            else
            {
                sl = z2.Price + slPad;
                if (sl <= price)
                    sl = Math.Max(z1.Price, z2.Price) + slPad;
            }

            double slDist = Math.Abs(price - sl);
            if (slDist < ctx.Atr * 0.15)
                return SignalResult.Reject($"REJECT:F_SL_TINY:{slDist:F4}");

            string code = side == SignalSide.Long ? "E_LONG" : "E_SHORT";
            return SignalResult.Pass(
                side,
                code,
                z1.Price,
                z2.Price,
                z3.Price,
                z1.IsHigh,
                z1Key,
                zoneLow,
                zoneHigh,
                sl,
                slDist,
                price);
        }

        /// <summary>Arm path: setup valid without requiring price in zone yet.</summary>
        public SignalResult EvaluateSetup(SignalContext ctx)
        {
            bool prev = ctx.RequireInZone;
            double live = ctx.LivePrice;
            ctx.RequireInZone = false;
            ctx.LivePrice = 0;
            var r = Evaluate(ctx);
            ctx.RequireInZone = prev;
            ctx.LivePrice = live;
            return r;
        }

        private static bool TryBuildZone(
            SignalContext ctx,
            SignalSide side,
            ZzPivot z1,
            ZzPivot z2,
            double buf,
            out double zoneLow,
            out double zoneHigh,
            out string fail)
        {
            zoneLow = 0;
            zoneHigh = 0;
            fail = "REJECT:F_ZONE";

            if (ctx.ZoneMode == ZoneMode.Poc)
            {
                if (!ctx.PocValid || ctx.PocPrice <= 0)
                {
                    fail = "REJECT:F_POC_INVALID";
                    return false;
                }
                zoneLow = ctx.PocPrice - buf;
                zoneHigh = ctx.PocPrice + buf;
                return zoneHigh > zoneLow;
            }

            // Fib 38.2–61.8 of leg z1–z2
            double hi = Math.Max(z1.Price, z2.Price);
            double lo = Math.Min(z1.Price, z2.Price);
            double range = hi - lo;
            if (range <= 0)
            {
                fail = "REJECT:F_FIB_RANGE";
                return false;
            }

            // Retracement from z2 (impulse extreme) toward z1
            // Band between 38.2% and 61.8% of the leg
            double level382;
            double level618;
            if (side == SignalSide.Long)
            {
                // Impulse up to z2 high, pullback to z1 low: fib from high
                level382 = hi - 0.382 * range;
                level618 = hi - 0.618 * range;
                zoneLow = Math.Min(level382, level618) - buf;
                zoneHigh = Math.Max(level382, level618) + buf;
            }
            else
            {
                level382 = lo + 0.382 * range;
                level618 = lo + 0.618 * range;
                zoneLow = Math.Min(level382, level618) - buf;
                zoneHigh = Math.Max(level382, level618) + buf;
            }

            return zoneHigh > zoneLow;
        }

        private static bool StructureOk(IReadOnlyList<ZzPivot> pivots, SignalSide side)
        {
            // Last 5 confirmed ZZ points excluding tip: lows for HL, highs for LH
            int n = pivots.Count;
            if (n < 6)
                return false;

            var window = new List<ZzPivot>();
            for (int i = Math.Max(0, n - 6); i <= n - 2; i++)
                window.Add(pivots[i]);

            if (side == SignalSide.Long)
            {
                double? prevLow = null;
                int hl = 0;
                foreach (var p in window)
                {
                    if (p.IsHigh) continue;
                    if (prevLow.HasValue && p.Price > prevLow.Value)
                        hl++;
                    prevLow = p.Price;
                }
                return hl >= 2;
            }
            else
            {
                double? prevHigh = null;
                int lh = 0;
                foreach (var p in window)
                {
                    if (!p.IsHigh) continue;
                    if (prevHigh.HasValue && p.Price < prevHigh.Value)
                        lh++;
                    prevHigh = p.Price;
                }
                return lh >= 2;
            }
        }

    }
}
