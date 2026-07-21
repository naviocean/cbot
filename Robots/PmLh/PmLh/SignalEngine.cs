using System;
using System.Collections.Generic;
using RedWave.Common;

namespace cAlgo.Robots
{
    public enum SignalSide
    {
        None = 0,
        Long = 1,
        Short = -1
    }

    public enum PmLhEntryMode
    {
        ShallowRetest = 0,
        Pierce = 1,
        TouchOnly = 2
    }

    public enum LvnSourceMode
    {
        Composite = 0,
        Rolling = 1,
        DualPreferComposite = 2
    }

    public sealed class SignalContext
    {
        public ProfileData StructProfile { get; set; }
        public ProfileData RollProfile { get; set; }
        public LvnSourceMode LvnSource { get; set; }

        public double BarHigh { get; set; }
        public double BarLow { get; set; }
        public double BarOpen { get; set; }
        public double BarClose { get; set; }
        public double HtfClose { get; set; }
        public double HtfPoc { get; set; }
        public double Atr { get; set; }

        public bool MigrationWarm { get; set; }
        public double MigrationM { get; set; }
        public double MigrationDelta { get; set; }
        public MigrationDir MigrationDirection { get; set; }
        public string MigrationFailCode { get; set; }
        /// <summary>Latest rolling POC (for price alignment).</summary>
        public double PocNow { get; set; }
        /// <summary>
        /// Reject if price is more than this × ATR on the wrong side of POC
        /// (long too far below / short too far above). 0 = off.
        /// </summary>
        public double MaxPricePocAtr { get; set; }
        /// <summary>When true, long requires close ≥ LVN.Mid (not knife under void); short ≤ LVN.Mid.</summary>
        public bool RequireLvnSide { get; set; }

        public double BuyImbalance { get; set; }
        public double SellImbalance { get; set; }
        public int DeltaTickCount { get; set; }
        public int MinDeltaTicks { get; set; }

        public bool SessionOk { get; set; }
        public bool NewsOk { get; set; }
        public bool SpreadOk { get; set; }
        public bool EquityOk { get; set; }
        public int TradesToday { get; set; }
        public int MaxTradesPerDay { get; set; }
        public bool HasOpenPosition { get; set; }

        public double MinLvnStrength { get; set; }
        public double MaxLvnWidth { get; set; }
        public int TopNLvn { get; set; }
        public double TouchBuffer { get; set; }
        public double MinDeltaStrength { get; set; }

        public PmLhEntryMode EntryMode { get; set; }
        public int PriorBreakBars { get; set; }
        public int MaxDwellBars { get; set; }
        public bool PreferFullClear { get; set; }
        public double BodyAtrMult { get; set; }

        /// <summary>Oldest → newest; last index = signal bar. OHLC aligned.</summary>
        public double[] HistOpen { get; set; }
        public double[] HistHigh { get; set; }
        public double[] HistLow { get; set; }
        public double[] HistClose { get; set; }

        public bool RequireDeltaFilter { get; set; }
        public bool RequireShapeFilter { get; set; }
        public bool RequireHtfFilter { get; set; }
        public bool RequireExpandFilter { get; set; }
        public bool BlockNeutralShape { get; set; }
        public bool BlockDShape { get; set; }

        public double ExpandAtrMult { get; set; }
        public double ExpandRatio { get; set; }
        public double AtrFast { get; set; }
        public double AtrSlow { get; set; }

        public double LvnBufferAtrMult { get; set; }
        public double MinSlAtrMult { get; set; }
        public double MaxSlAtrMult { get; set; }
        public double EntryPriceEstimate { get; set; }
    }

    public sealed class SignalResult
    {
        public bool IsValid { get; set; }
        public SignalSide Side { get; set; }
        public string Reason { get; set; }
        public VolumeNode Lvn { get; set; }
        public double MigrationM { get; set; }
        public double Imbalance { get; set; }
        public ProfileShape Shape { get; set; }
        public double SuggestedSl { get; set; }
        public int DwellBars { get; set; }

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
            double migrationM, double imbalance, ProfileShape shape, double suggestedSl, int dwell)
        {
            return new SignalResult
            {
                IsValid = true,
                Side = side,
                Reason = reason,
                Lvn = lvn,
                MigrationM = migrationM,
                Imbalance = imbalance,
                Shape = shape,
                SuggestedSl = suggestedSl,
                DwellBars = dwell
            };
        }
    }

    /// <summary>
    /// Pure setup evaluator for PM-LH (closed-bar). Exit TP is orchestrator-only (RR×R).
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
                return SignalResult.Reject("REJECT:F5_OPEN");

            var lvnProfile = ResolveLvnProfile(ctx, out string profileFail);
            if (lvnProfile == null || !lvnProfile.IsValid)
                return SignalResult.Reject(profileFail ?? "REJECT:E_PROFILE");

            if (!ctx.MigrationWarm)
                return SignalResult.Reject("REJECT:E_POC_INVALID");

            if (ctx.MigrationDirection == MigrationDir.Flat)
            {
                string code = string.IsNullOrEmpty(ctx.MigrationFailCode)
                    ? "E_POC_FLAT"
                    : ctx.MigrationFailCode;
                return SignalResult.Reject("REJECT:" + code);
            }

            SignalSide side = ctx.MigrationDirection == MigrationDir.Bull
                ? SignalSide.Long
                : SignalSide.Short;

            // Stale migration: POC still bull/bear while price already far on wrong side
            if (ctx.MaxPricePocAtr > 0 && ctx.PocNow > 0 && ctx.Atr > 0)
            {
                double lim = ctx.MaxPricePocAtr * ctx.Atr;
                if (side == SignalSide.Long && ctx.BarClose < ctx.PocNow - lim)
                    return SignalResult.Reject(
                        $"REJECT:E_PRICE_POC:long close={ctx.BarClose:F1} poc={ctx.PocNow:F1}");
                if (side == SignalSide.Short && ctx.BarClose > ctx.PocNow + lim)
                    return SignalResult.Reject(
                        $"REJECT:E_PRICE_POC:short close={ctx.BarClose:F1} poc={ctx.PocNow:F1}");
            }

            var candidates = CollectEligibleLvns(lvnProfile, ctx);
            if (candidates.Count == 0)
                return SignalResult.Reject("REJECT:E2_NO_LVN");

            VolumeNode bestLvn = null;
            string lastFail = null;
            int bestDwell = 0;

            foreach (var lvn in candidates)
            {
                var inter = EvaluateInteraction(ctx, lvn, side);
                if (!inter.ok)
                {
                    lastFail = inter.reason;
                    continue;
                }

                if (bestLvn == null
                    || Math.Abs(lvn.Mid - ctx.BarClose) < Math.Abs(bestLvn.Mid - ctx.BarClose)
                    || (Math.Abs(lvn.Mid - ctx.BarClose) <= Math.Abs(bestLvn.Mid - ctx.BarClose) + 1e-9
                        && lvn.Strength > bestLvn.Strength))
                {
                    bestLvn = lvn;
                    bestDwell = inter.dwell;
                }
            }

            if (bestLvn == null)
                return SignalResult.Reject(lastFail ?? "REJECT:E1_NO_INTERACT");

            // Highway retest: long should not buy an LVN entirely above a collapsing close
            if (ctx.RequireLvnSide)
            {
                if (side == SignalSide.Long && ctx.BarClose < bestLvn.Low - ctx.TouchBuffer)
                    return SignalResult.Reject("REJECT:E_LVN_SIDE:long_under");
                if (side == SignalSide.Short && ctx.BarClose > bestLvn.High + ctx.TouchBuffer)
                    return SignalResult.Reject("REJECT:E_LVN_SIDE:short_over");
            }

            if (!IsAcceptance(ctx, side, bestLvn))
                return SignalResult.Reject("REJECT:E3_NO_ACCEPT");

            if (ctx.RequireDeltaFilter)
            {
                if (ctx.DeltaTickCount < ctx.MinDeltaTicks)
                    return SignalResult.Reject($"REJECT:E4_DELTA:samples={ctx.DeltaTickCount}");
                double imb = side == SignalSide.Long ? ctx.BuyImbalance : ctx.SellImbalance;
                if (imb < ctx.MinDeltaStrength)
                    return SignalResult.Reject($"REJECT:E4_DELTA:{imb:F2}");
            }

            if (ctx.RequireShapeFilter)
            {
                var shape = lvnProfile.Shape;
                if (ctx.BlockDShape && shape == ProfileShape.DShape)
                    return SignalResult.Reject("REJECT:E5_SHAPE:D");
                if (ctx.BlockNeutralShape && shape == ProfileShape.Neutral)
                    return SignalResult.Reject("REJECT:E5_SHAPE:N");
                if (side == SignalSide.Long && shape == ProfileShape.Bearish)
                    return SignalResult.Reject("REJECT:E5_SHAPE:BEAR");
                if (side == SignalSide.Short && shape == ProfileShape.Bullish)
                    return SignalResult.Reject("REJECT:E5_SHAPE:BULL");
            }

            if (ctx.RequireHtfFilter)
            {
                double refPoc = ctx.HtfPoc > 0 ? ctx.HtfPoc : lvnProfile.POC;
                if (side == SignalSide.Long && ctx.HtfClose <= refPoc)
                    return SignalResult.Reject("REJECT:E6_HTF");
                if (side == SignalSide.Short && ctx.HtfClose >= refPoc)
                    return SignalResult.Reject("REJECT:E6_HTF");
            }

            if (ctx.RequireExpandFilter && !ExpandOk(ctx))
                return SignalResult.Reject("REJECT:E7_EXPAND");

            double entryEst = ctx.EntryPriceEstimate > 0 ? ctx.EntryPriceEstimate : ctx.BarClose;
            double sl = ComputeSl(side, entryEst, bestLvn, ctx.Atr, ctx.LvnBufferAtrMult, ctx.MinSlAtrMult);
            double slDist = Math.Abs(entryEst - sl);
            if (ctx.MaxSlAtrMult > 0 && ctx.Atr > 0 && slDist > ctx.Atr * ctx.MaxSlAtrMult)
                return SignalResult.Reject($"REJECT:E8_SL_WIDE:{slDist:F1}");

            double imbalance = side == SignalSide.Long ? ctx.BuyImbalance : ctx.SellImbalance;
            string pass = side == SignalSide.Long ? "PASS:LONG" : "PASS:SHORT";
            return SignalResult.Pass(side, pass, bestLvn, ctx.MigrationM, imbalance,
                lvnProfile.Shape, sl, bestDwell);
        }

        public static double ComputeSl(SignalSide side, double entry, VolumeNode lvn,
            double atr, double lvnBufMult, double minSlAtrMult)
        {
            double atrBuf = atr * Math.Max(0, lvnBufMult);
            double minDist = atr * Math.Max(0.1, minSlAtrMult);
            if (side == SignalSide.Long)
                return Math.Min(lvn.Low - atrBuf, entry - minDist);
            return Math.Max(lvn.High + atrBuf, entry + minDist);
        }

        private static ProfileData ResolveLvnProfile(SignalContext ctx, out string fail)
        {
            fail = null;
            switch (ctx.LvnSource)
            {
                case LvnSourceMode.Rolling:
                    if (ctx.RollProfile == null || !ctx.RollProfile.IsValid)
                    {
                        fail = "REJECT:E_PROFILE";
                        return null;
                    }
                    return ctx.RollProfile;

                case LvnSourceMode.DualPreferComposite:
                    if (ctx.StructProfile != null && ctx.StructProfile.IsValid
                        && ctx.StructProfile.Lvns != null && ctx.StructProfile.Lvns.Count > 0)
                        return ctx.StructProfile;
                    if (ctx.RollProfile != null && ctx.RollProfile.IsValid)
                        return ctx.RollProfile;
                    fail = "REJECT:E_PROFILE";
                    return null;

                default:
                    if (ctx.StructProfile == null || !ctx.StructProfile.IsValid)
                    {
                        fail = "REJECT:E_PROFILE";
                        return null;
                    }
                    return ctx.StructProfile;
            }
        }

        private static List<VolumeNode> CollectEligibleLvns(ProfileData profile, SignalContext ctx)
        {
            var list = new List<VolumeNode>();
            if (profile.Lvns == null) return list;

            foreach (var lvn in profile.Lvns)
            {
                if (lvn == null) continue;
                if (lvn.Strength < ctx.MinLvnStrength) continue;
                double w = lvn.High - lvn.Low;
                if (ctx.MaxLvnWidth > 0 && w > ctx.MaxLvnWidth) continue;
                list.Add(lvn);
            }

            // PRD §5.6: nearest to price first, strength breaks ties (then Top-N by that order)
            list.Sort((a, b) =>
            {
                double da = Math.Abs(a.Mid - ctx.BarClose);
                double db = Math.Abs(b.Mid - ctx.BarClose);
                int c = da.CompareTo(db);
                if (c != 0) return c;
                return b.Strength.CompareTo(a.Strength);
            });

            int top = ctx.TopNLvn > 0 ? ctx.TopNLvn : list.Count;
            if (list.Count > top)
                list.RemoveRange(top, list.Count - top);
            return list;
        }

        private static (bool ok, string reason, int dwell) EvaluateInteraction(
            SignalContext ctx, VolumeNode lvn, SignalSide side)
        {
            double lo = lvn.Low - ctx.TouchBuffer;
            double hi = lvn.High + ctx.TouchBuffer;

            switch (ctx.EntryMode)
            {
                case PmLhEntryMode.Pierce:
                    return EvalPierce(ctx, lvn, side, lo, hi);
                case PmLhEntryMode.TouchOnly:
                    if (!BarTouchesZone(ctx.BarHigh, ctx.BarLow, lo, hi, 0))
                        return (false, "REJECT:E1_NO_INTERACT", 0);
                    return (true, null, 0);
                default:
                    return EvalShallowRetest(ctx, lvn, side, lo, hi);
            }
        }

        private static (bool ok, string reason, int dwell) EvalShallowRetest(
            SignalContext ctx, VolumeNode lvn, SignalSide side, double lo, double hi)
        {
            if (!BarTouchesZone(ctx.BarHigh, ctx.BarLow, lo, hi, 0))
                return (false, "REJECT:E1_NO_INTERACT", 0);

            if (!HasPriorBreak(ctx, lvn, side))
                return (false, "REJECT:E_NO_PRIOR_BREAK", 0);

            int dwell = CountDwell(ctx, lvn);
            if (ctx.MaxDwellBars > 0 && dwell > ctx.MaxDwellBars)
                return (false, $"REJECT:E_LVN_DWELL:{dwell}", dwell);

            return (true, null, dwell);
        }

        private static (bool ok, string reason, int dwell) EvalPierce(
            SignalContext ctx, VolumeNode lvn, SignalSide side, double lo, double hi)
        {
            if (ctx.BarHigh < lvn.Low || ctx.BarLow > lvn.High)
                return (false, "REJECT:E1_NO_INTERACT", 0);

            // Must span through mid (or full node)
            bool spansMid = ctx.BarLow <= lvn.Mid && ctx.BarHigh >= lvn.Mid;
            bool full = ctx.BarLow <= lvn.Low && ctx.BarHigh >= lvn.High;
            if (!spansMid && !full)
                return (false, "REJECT:E1_NO_INTERACT", 0);

            if (ctx.BodyAtrMult > 0 && ctx.Atr > 0)
            {
                double body = Math.Abs(ctx.BarClose - ctx.BarOpen);
                if (body < ctx.BodyAtrMult * ctx.Atr)
                    return (false, "REJECT:E1_NO_INTERACT", 0);
            }

            // PreferFullClear → must close beyond LVN edge; else clear mid is enough (PRD Pierce)
            if (side == SignalSide.Long)
            {
                double clear = ctx.PreferFullClear
                    ? lvn.High - ctx.TouchBuffer
                    : lvn.Mid;
                if (ctx.BarClose < clear)
                    return (false, "REJECT:E1_NO_INTERACT", 0);
            }
            else
            {
                double clear = ctx.PreferFullClear
                    ? lvn.Low + ctx.TouchBuffer
                    : lvn.Mid;
                if (ctx.BarClose > clear)
                    return (false, "REJECT:E1_NO_INTERACT", 0);
            }

            return (true, null, 0);
        }

        private static bool HasPriorBreak(SignalContext ctx, VolumeNode lvn, SignalSide side)
        {
            var closes = ctx.HistClose;
            if (closes == null || closes.Length < 2)
                return false;

            int signal = closes.Length - 1;
            int look = Math.Max(1, ctx.PriorBreakBars);
            int from = Math.Max(0, signal - look);

            for (int i = from; i < signal; i++)
            {
                if (side == SignalSide.Long)
                {
                    double thr = ctx.PreferFullClear ? lvn.High : lvn.Mid;
                    if (closes[i] >= thr) return true;
                }
                else
                {
                    double thr = ctx.PreferFullClear ? lvn.Low : lvn.Mid;
                    if (closes[i] <= thr) return true;
                }
            }
            return false;
        }

        private static int CountDwell(SignalContext ctx, VolumeNode lvn)
        {
            var highs = ctx.HistHigh;
            var lows = ctx.HistLow;
            if (highs == null || lows == null || highs.Length == 0)
                return 0;

            int dwell = 0;
            for (int i = highs.Length - 1; i >= 0; i--)
            {
                double mid = (highs[i] + lows[i]) * 0.5;
                if (mid >= lvn.Low && mid <= lvn.High)
                    dwell++;
                else
                    break;
            }
            return dwell;
        }

        private static bool IsAcceptance(SignalContext ctx, SignalSide side, VolumeNode lvn)
        {
            double range = ctx.BarHigh - ctx.BarLow;
            if (range <= 1e-12) return false;
            double closePos = (ctx.BarClose - ctx.BarLow) / range;

            if (side == SignalSide.Long)
            {
                if (closePos < 0.45) return false;
                if (!(ctx.BarClose >= ctx.BarOpen || closePos >= 0.55)) return false;
                if (ctx.EntryMode == PmLhEntryMode.ShallowRetest && ctx.BarClose < lvn.Mid
                    && closePos < 0.55)
                    return false;
                return true;
            }

            if (closePos > 0.55) return false;
            if (!(ctx.BarClose <= ctx.BarOpen || closePos <= 0.45)) return false;
            if (ctx.EntryMode == PmLhEntryMode.ShallowRetest && ctx.BarClose > lvn.Mid
                && closePos > 0.45)
                return false;
            return true;
        }

        private static bool ExpandOk(SignalContext ctx)
        {
            double range = ctx.BarHigh - ctx.BarLow;
            if (ctx.Atr > 0 && ctx.ExpandAtrMult > 0 && range >= ctx.ExpandAtrMult * ctx.Atr)
                return true;
            if (ctx.AtrSlow > 1e-12 && ctx.ExpandRatio > 0
                && ctx.AtrFast / ctx.AtrSlow >= ctx.ExpandRatio)
                return true;
            return false;
        }

        private static bool BarTouchesZone(double high, double low, double zLo, double zHi, double extra)
        {
            double lo = zLo - extra;
            double hi = zHi + extra;
            return high >= lo && low <= hi;
        }
    }
}
