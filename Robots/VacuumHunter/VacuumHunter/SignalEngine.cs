using System;
using RedWave.Common;

namespace cAlgo.Robots
{
    public enum SignalSide
    {
        None = 0,
        Long = 1,
        Short = -1
    }

    public sealed class SignalContext
    {
        public ProfileData Profile { get; set; }
        public double BarHigh { get; set; }
        public double BarLow { get; set; }
        public double BarOpen { get; set; }
        public double BarClose { get; set; }
        public double HtfClose { get; set; }
        public double Atr { get; set; }
        public double BuyImbalance { get; set; }
        public double SellImbalance { get; set; }
        public bool SessionOk { get; set; }
        public bool NewsOk { get; set; }
        public bool SpreadOk { get; set; }
        public bool EquityOk { get; set; }
        public int TradesToday { get; set; }
        public int MaxTradesPerDay { get; set; }
        public bool HasOpenPosition { get; set; }
        public double MinLvnStrength { get; set; }
        public double MinDeltaStrength { get; set; }
        public double RejectionWickBodyRatio { get; set; }
        public bool RequireShapeFilter { get; set; }
        public bool RequireDeltaFilter { get; set; }
        public bool RequireHtfFilter { get; set; }
        /// <summary>POC/VAH/VAL as support/resistance magnets when HVN sparse.</summary>
        public bool AllowPocVaTargets { get; set; }
        public double MaxLvnWidth { get; set; }
        public double TouchBuffer { get; set; }
        public int DeltaTickCount { get; set; }
        public int MinDeltaTicks { get; set; }
    }

    public sealed class SignalResult
    {
        public bool IsValid { get; set; }
        public SignalSide Side { get; set; }
        public string Reason { get; set; }
        public VolumeNode Lvn { get; set; }
        public VolumeNode Support { get; set; }
        public VolumeNode Resistance { get; set; }
        /// <summary>Single structure magnet for Structure TP mode (HVN/POC/VA).</summary>
        public VolumeNode StructureTarget { get; set; }
        public double Imbalance { get; set; }
        public ProfileShape Shape { get; set; }

        public static SignalResult Reject(string reason)
        {
            return new SignalResult
            {
                IsValid = false,
                Side = SignalSide.None,
                Reason = reason
            };
        }

        public static SignalResult Pass(SignalSide side, string reason, VolumeNode lvn,
            VolumeNode support, VolumeNode resistance, VolumeNode structureTarget,
            double imbalance, ProfileShape shape)
        {
            return new SignalResult
            {
                IsValid = true,
                Side = side,
                Reason = reason,
                Lvn = lvn,
                Support = support,
                Resistance = resistance,
                StructureTarget = structureTarget,
                Imbalance = imbalance,
                Shape = shape
            };
        }
    }

    /// <summary>
    /// Pure setup evaluator for Vacuum Hunter (closed-bar rules). Exit TP is not decided here.
    /// </summary>
    public sealed class SignalEngine
    {
        public SignalResult Evaluate(SignalContext ctx)
        {
            if (ctx == null)
                return SignalResult.Reject("REJECT:NULL_CTX");

            if (!ctx.SessionOk)
                return SignalResult.Reject("REJECT:F1_SESSION");
            if (!ctx.NewsOk)
                return SignalResult.Reject("REJECT:F2_NEWS");
            if (!ctx.SpreadOk)
                return SignalResult.Reject("REJECT:F3_SPREAD");
            if (!ctx.EquityOk)
                return SignalResult.Reject("REJECT:F3_EQUITY");
            if (ctx.TradesToday >= ctx.MaxTradesPerDay)
                return SignalResult.Reject("REJECT:F4_MAX_TRADES");
            if (ctx.HasOpenPosition)
                return SignalResult.Reject("REJECT:F5_OPEN_POS");

            if (ctx.Profile == null || !ctx.Profile.IsValid)
                return SignalResult.Reject("REJECT:PROFILE_INVALID");
            if (ctx.Profile.Lvns == null || ctx.Profile.Lvns.Count == 0)
                return SignalResult.Reject("REJECT:NO_LVN");

            VolumeNode touched = null;
            foreach (var lvn in ctx.Profile.Lvns)
            {
                if (BarTouchesZone(ctx.BarHigh, ctx.BarLow, lvn.Low, lvn.High, ctx.TouchBuffer))
                {
                    if (touched == null || lvn.Strength > touched.Strength)
                        touched = lvn;
                }
            }

            if (touched == null)
            {
                double nearest = double.MaxValue;
                foreach (var lvn in ctx.Profile.Lvns)
                {
                    double d = DistanceToZone(ctx.BarClose, lvn.Low, lvn.High);
                    if (d < nearest) nearest = d;
                }
                return SignalResult.Reject($"REJECT:E1_NO_TOUCH:near={nearest:F1}");
            }

            if (touched.Strength < ctx.MinLvnStrength)
                return SignalResult.Reject($"REJECT:E2_WEAK_LVN:{touched.Strength:F2}");

            double lvnWidth = touched.High - touched.Low;
            if (ctx.MaxLvnWidth > 0 && lvnWidth > ctx.MaxLvnWidth)
                return SignalResult.Reject($"REJECT:E2_LVN_TOO_WIDE:{lvnWidth:F1}");

            if (ctx.RequireDeltaFilter && ctx.DeltaTickCount < ctx.MinDeltaTicks)
                return SignalResult.Reject($"REJECT:E4_DELTA_SAMPLES:{ctx.DeltaTickCount}");

            var longSig = TryLong(ctx, touched);
            var shortSig = TryShort(ctx, touched);

            if (longSig.IsValid && !shortSig.IsValid) return longSig;
            if (shortSig.IsValid && !longSig.IsValid) return shortSig;
            if (longSig.IsValid && shortSig.IsValid)
                return longSig.Imbalance >= shortSig.Imbalance ? longSig : shortSig;

            if (longSig.Reason != null && shortSig.Reason != null)
                return SignalResult.Reject($"{longSig.Reason}|{shortSig.Reason}");
            return longSig.Reason != null ? longSig : shortSig;
        }

        private SignalResult TryLong(SignalContext ctx, VolumeNode lvn)
        {
            var support = FindSupportBelow(ctx, lvn.Low);
            if (support == null)
                return SignalResult.Reject("REJECT:E2_NO_SUPPORT");

            if (!IsBullishRejection(ctx.BarOpen, ctx.BarHigh, ctx.BarLow, ctx.BarClose, ctx.RejectionWickBodyRatio))
                return SignalResult.Reject("REJECT:E3_NO_BULL_REJECT");

            double imb = ctx.BuyImbalance;
            if (ctx.RequireDeltaFilter && imb < ctx.MinDeltaStrength)
                return SignalResult.Reject($"REJECT:E4_DELTA:{imb:F2}");

            if (ctx.RequireShapeFilter && ctx.Profile.Shape == ProfileShape.Bearish)
                return SignalResult.Reject("REJECT:E5_SHAPE_BEAR");

            if (ctx.RequireHtfFilter && ctx.HtfClose <= ctx.Profile.POC)
                return SignalResult.Reject("REJECT:E6_HTF_BEAR");

            double fromPrice = Math.Max(ctx.BarClose, lvn.High);
            var structureTp = FindTargetAbove(ctx, fromPrice);

            return SignalResult.Pass(
                SignalSide.Long, "PASS:LONG", lvn, support, null, structureTp, imb, ctx.Profile.Shape);
        }

        private SignalResult TryShort(SignalContext ctx, VolumeNode lvn)
        {
            var resistance = FindResistanceAbove(ctx, lvn.High);
            if (resistance == null)
                return SignalResult.Reject("REJECT:E2_NO_RESIST");

            if (!IsBearishRejection(ctx.BarOpen, ctx.BarHigh, ctx.BarLow, ctx.BarClose, ctx.RejectionWickBodyRatio))
                return SignalResult.Reject("REJECT:E3_NO_BEAR_REJECT");

            double imb = ctx.SellImbalance;
            if (ctx.RequireDeltaFilter && imb < ctx.MinDeltaStrength)
                return SignalResult.Reject($"REJECT:E4_DELTA:{imb:F2}");

            if (ctx.RequireShapeFilter && ctx.Profile.Shape == ProfileShape.Bullish)
                return SignalResult.Reject("REJECT:E5_SHAPE_BULL");

            if (ctx.RequireHtfFilter && ctx.HtfClose >= ctx.Profile.POC)
                return SignalResult.Reject("REJECT:E6_HTF_BULL");

            double fromPrice = Math.Min(ctx.BarClose, lvn.Low);
            var structureTp = FindTargetBelow(ctx, fromPrice);

            return SignalResult.Pass(
                SignalSide.Short, "PASS:SHORT", lvn, null, resistance, structureTp, imb, ctx.Profile.Shape);
        }

        private static VolumeNode FindSupportBelow(SignalContext ctx, double price)
        {
            var hvn = ctx.Profile.FindNearestHvnBelow(price);
            if (hvn != null) return hvn;
            if (!ctx.AllowPocVaTargets) return null;

            VolumeNode best = null;
            TryConsiderLevel(ref best, price, ctx.Profile.VAL, preferBelow: true);
            TryConsiderLevel(ref best, price, ctx.Profile.POC, preferBelow: true);
            return best;
        }

        private static VolumeNode FindResistanceAbove(SignalContext ctx, double price)
        {
            var hvn = ctx.Profile.FindNearestHvnAbove(price);
            if (hvn != null) return hvn;
            if (!ctx.AllowPocVaTargets) return null;

            VolumeNode best = null;
            TryConsiderLevel(ref best, price, ctx.Profile.POC, preferBelow: false);
            TryConsiderLevel(ref best, price, ctx.Profile.VAH, preferBelow: false);
            return best;
        }

        private static VolumeNode FindTargetAbove(SignalContext ctx, double price)
        {
            var hvn = ctx.Profile.FindNearestHvnAbove(price);
            if (hvn != null) return hvn;
            if (!ctx.AllowPocVaTargets) return null;

            VolumeNode best = null;
            TryConsiderLevel(ref best, price, ctx.Profile.POC, preferBelow: false);
            TryConsiderLevel(ref best, price, ctx.Profile.VAH, preferBelow: false);
            return best;
        }

        private static VolumeNode FindTargetBelow(SignalContext ctx, double price)
        {
            var hvn = ctx.Profile.FindNearestHvnBelow(price);
            if (hvn != null) return hvn;
            if (!ctx.AllowPocVaTargets) return null;

            VolumeNode best = null;
            TryConsiderLevel(ref best, price, ctx.Profile.POC, preferBelow: true);
            TryConsiderLevel(ref best, price, ctx.Profile.VAL, preferBelow: true);
            return best;
        }

        private static void TryConsiderLevel(ref VolumeNode best, double price, double level, bool preferBelow)
        {
            if (preferBelow)
            {
                if (level >= price - 1e-9) return;
                if (best == null || level > best.Mid)
                    best = SyntheticNode(level, Math.Abs(price - level) * 0.02 + 0.25);
            }
            else
            {
                if (level <= price + 1e-9) return;
                if (best == null || level < best.Mid)
                    best = SyntheticNode(level, Math.Abs(level - price) * 0.02 + 0.25);
            }
        }

        private static VolumeNode SyntheticNode(double mid, double halfWidth)
        {
            halfWidth = Math.Max(halfWidth, 0.1);
            return new VolumeNode
            {
                Type = VolumeNodeType.HVN,
                StartBin = -1,
                EndBin = -1,
                Low = mid - halfWidth,
                High = mid + halfWidth,
                Volume = 0,
                AvgVolume = 0,
                Strength = 0.5
            };
        }

        private static bool BarTouchesZone(double high, double low, double zLow, double zHigh, double buffer)
        {
            buffer = Math.Max(0, buffer);
            return low <= zHigh + buffer && high >= zLow - buffer;
        }

        private static double DistanceToZone(double price, double zLow, double zHigh)
        {
            if (price >= zLow && price <= zHigh) return 0;
            if (price < zLow) return zLow - price;
            return price - zHigh;
        }

        private static bool IsBullishRejection(double open, double high, double low, double close, double wickBodyRatio)
        {
            double range = high - low;
            if (range <= 0) return false;
            double body = Math.Abs(close - open);
            if (body <= 0) body = range * 0.01;
            double lowerWick = Math.Min(open, close) - low;
            double closePos = (close - low) / range;
            bool posOk = closePos >= 0.45;
            bool wickOk = lowerWick >= body * wickBodyRatio || lowerWick >= range * 0.25;
            bool bullClose = close >= open;
            return posOk && wickOk && (bullClose || closePos >= 0.55);
        }

        private static bool IsBearishRejection(double open, double high, double low, double close, double wickBodyRatio)
        {
            double range = high - low;
            if (range <= 0) return false;
            double body = Math.Abs(close - open);
            if (body <= 0) body = range * 0.01;
            double upperWick = high - Math.Max(open, close);
            double closePos = (close - low) / range;
            bool posOk = closePos <= 0.55;
            bool wickOk = upperWick >= body * wickBodyRatio || upperWick >= range * 0.25;
            bool bearClose = close <= open;
            return posOk && wickOk && (bearClose || closePos <= 0.45);
        }
    }
}
