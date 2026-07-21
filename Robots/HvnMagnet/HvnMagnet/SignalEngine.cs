using System;
using System.Collections.Generic;
using System.Linq;
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
        public double MinHvnStrength { get; set; }
        public double MaxHvnWidth { get; set; }
        public int TopNHvn { get; set; }
        public double MinDeltaStrength { get; set; }
        public double RejectionWickBodyRatio { get; set; }
        public bool RequireShapeFilter { get; set; }
        public bool RequireDeltaFilter { get; set; }
        public bool RequireHtfFilter { get; set; }
        public bool BlockNeutralShape { get; set; }
        public bool RequireHvnPocSide { get; set; }
        public bool AllowPocVaTargets { get; set; }
        public double TouchBuffer { get; set; }
        public int DeltaTickCount { get; set; }
        public int MinDeltaTicks { get; set; }
        /// <summary>Min |target-entry| / estimatedSlDist for E7 (structure R gate).</summary>
        public double MinFirstTargetR { get; set; }
        /// <summary>Approx SL distance for E7: ATR-based floor used before live entry SL.</summary>
        public double EstimatedSlDistance { get; set; }
        /// <summary>
        /// When true, E7 requires a structure magnet with min R (Structure TP mode only).
        /// RiskReward / FixedPrice skip hard E7 so missing magnets do not block entry.
        /// </summary>
        public bool ApplyStructureMinR { get; set; }
        /// <summary>Long: close must stay ≥ HVN.Low−buffer; short mirror (optional pierce filter).</summary>
        public bool RequireCloseInZone { get; set; }
    }

    public sealed class SignalResult
    {
        public bool IsValid { get; set; }
        public SignalSide Side { get; set; }
        public string Reason { get; set; }
        public VolumeNode Hvn { get; set; }
        /// <summary>Structure magnet for Structure TP / E7 (node or synthetic).</summary>
        public VolumeNode StructureTarget { get; set; }
        public double TargetPrice { get; set; }
        public string TargetLabel { get; set; }
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

        public static SignalResult Pass(
            SignalSide side,
            string reason,
            VolumeNode hvn,
            VolumeNode structureTarget,
            double targetPrice,
            string targetLabel,
            double imbalance,
            ProfileShape shape)
        {
            return new SignalResult
            {
                IsValid = true,
                Side = side,
                Reason = reason,
                Hvn = hvn,
                StructureTarget = structureTarget,
                TargetPrice = targetPrice,
                TargetLabel = targetLabel,
                Imbalance = imbalance,
                Shape = shape
            };
        }
    }

    /// <summary>
    /// Pure setup evaluator for HvnMagnet / HMPD (closed-bar rules).
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
            if (ctx.Profile.Hvns == null || ctx.Profile.Hvns.Count == 0)
                return SignalResult.Reject("REJECT:NO_HVN");

            if (ctx.RequireDeltaFilter && ctx.DeltaTickCount < ctx.MinDeltaTicks)
                return SignalResult.Reject($"REJECT:E4_DELTA_SAMPLES:{ctx.DeltaTickCount}");

            var eligible = GetEligibleHvns(ctx);
            if (eligible.Count == 0)
                return SignalResult.Reject("REJECT:E2_WEAK_HVN");

            VolumeNode touched = null;
            foreach (var hvn in eligible)
            {
                if (BarTouchesZone(ctx.BarHigh, ctx.BarLow, hvn.Low, hvn.High, ctx.TouchBuffer))
                {
                    if (touched == null || hvn.Strength > touched.Strength)
                        touched = hvn;
                }
            }

            if (touched == null)
            {
                double nearest = double.MaxValue;
                foreach (var hvn in eligible)
                {
                    double d = DistanceToZone(ctx.BarClose, hvn.Low, hvn.High);
                    if (d < nearest) nearest = d;
                }
                return SignalResult.Reject($"REJECT:E1_NO_TOUCH:near={nearest:F1}");
            }

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

        private static List<VolumeNode> GetEligibleHvns(SignalContext ctx)
        {
            int topN = Math.Max(1, ctx.TopNHvn);
            var ranked = ctx.Profile.Hvns
                .Where(h => h != null && h.Strength >= ctx.MinHvnStrength)
                .Where(h =>
                {
                    double w = h.High - h.Low;
                    return ctx.MaxHvnWidth <= 0 || w <= ctx.MaxHvnWidth;
                })
                .OrderByDescending(h => h.Strength)
                .Take(topN)
                .ToList();
            return ranked;
        }

        private SignalResult TryLong(SignalContext ctx, VolumeNode hvn)
        {
            // Support-side touch: bar low interacts with HVN (pullback into magnet)
            if (ctx.BarLow > hvn.High + ctx.TouchBuffer)
                return SignalResult.Reject("REJECT:E1_NO_TOUCH");

            if (ctx.RequireHvnPocSide && hvn.Mid > ctx.Profile.POC + 1e-9)
                return SignalResult.Reject("REJECT:E2_POC_SIDE");

            if (!IsBullishRejection(ctx.BarOpen, ctx.BarHigh, ctx.BarLow, ctx.BarClose, ctx.RejectionWickBodyRatio))
                return SignalResult.Reject("REJECT:E3_NO_BULL_REJECT");

            // Optional: reject closes that finished through support (break, not bounce)
            if (ctx.RequireCloseInZone && ctx.BarClose < hvn.Low - ctx.TouchBuffer)
                return SignalResult.Reject("REJECT:E1_CLOSE_THROUGH");

            if (ctx.RequireShapeFilter)
            {
                if (ctx.Profile.Shape == ProfileShape.Bearish)
                    return SignalResult.Reject("REJECT:E5_SHAPE_BEAR");
                if (ctx.BlockNeutralShape && ctx.Profile.Shape == ProfileShape.Neutral)
                    return SignalResult.Reject("REJECT:E5_SHAPE_NEUTRAL");
            }

            if (ctx.RequireHtfFilter && ctx.HtfClose <= ctx.Profile.POC)
                return SignalResult.Reject("REJECT:E6_HTF_BEAR");

            double imb = ctx.BuyImbalance;
            if (ctx.RequireDeltaFilter && imb < ctx.MinDeltaStrength)
                return SignalResult.Reject($"REJECT:E4_DELTA:{imb:F2}");

            double fromPrice = Math.Max(ctx.BarClose, hvn.High);
            VolumeNode targetNode = null;
            double targetPx = 0;
            string label = null;

            if (ctx.ApplyStructureMinR)
            {
                if (!TryResolveTarget(ctx, SignalSide.Long, fromPrice, hvn, out targetNode, out targetPx, out label))
                    return SignalResult.Reject("REJECT:E7_RR:no_target");
                if (!PassesMinR(ctx, fromPrice, targetPx, SignalSide.Long))
                    return SignalResult.Reject($"REJECT:E7_RR:{label}");
            }
            else
            {
                // Best-effort magnet for journal / Structure fields; do not reject
                TryResolveTarget(ctx, SignalSide.Long, fromPrice, hvn, out targetNode, out targetPx, out label);
            }

            return SignalResult.Pass(
                SignalSide.Long, "PASS:LONG", hvn, targetNode, targetPx, label ?? "", imb, ctx.Profile.Shape);
        }

        private SignalResult TryShort(SignalContext ctx, VolumeNode hvn)
        {
            if (ctx.BarHigh < hvn.Low - ctx.TouchBuffer)
                return SignalResult.Reject("REJECT:E1_NO_TOUCH");

            if (ctx.RequireHvnPocSide && hvn.Mid < ctx.Profile.POC - 1e-9)
                return SignalResult.Reject("REJECT:E2_POC_SIDE");

            if (!IsBearishRejection(ctx.BarOpen, ctx.BarHigh, ctx.BarLow, ctx.BarClose, ctx.RejectionWickBodyRatio))
                return SignalResult.Reject("REJECT:E3_NO_BEAR_REJECT");

            if (ctx.RequireCloseInZone && ctx.BarClose > hvn.High + ctx.TouchBuffer)
                return SignalResult.Reject("REJECT:E1_CLOSE_THROUGH");

            if (ctx.RequireShapeFilter)
            {
                if (ctx.Profile.Shape == ProfileShape.Bullish)
                    return SignalResult.Reject("REJECT:E5_SHAPE_BULL");
                if (ctx.BlockNeutralShape && ctx.Profile.Shape == ProfileShape.Neutral)
                    return SignalResult.Reject("REJECT:E5_SHAPE_NEUTRAL");
            }

            if (ctx.RequireHtfFilter && ctx.HtfClose >= ctx.Profile.POC)
                return SignalResult.Reject("REJECT:E6_HTF_BULL");

            double imb = ctx.SellImbalance;
            if (ctx.RequireDeltaFilter && imb < ctx.MinDeltaStrength)
                return SignalResult.Reject($"REJECT:E4_DELTA:{imb:F2}");

            double fromPrice = Math.Min(ctx.BarClose, hvn.Low);
            VolumeNode targetNode = null;
            double targetPx = 0;
            string label = null;

            if (ctx.ApplyStructureMinR)
            {
                if (!TryResolveTarget(ctx, SignalSide.Short, fromPrice, hvn, out targetNode, out targetPx, out label))
                    return SignalResult.Reject("REJECT:E7_RR:no_target");
                if (!PassesMinR(ctx, fromPrice, targetPx, SignalSide.Short))
                    return SignalResult.Reject($"REJECT:E7_RR:{label}");
            }
            else
            {
                TryResolveTarget(ctx, SignalSide.Short, fromPrice, hvn, out targetNode, out targetPx, out label);
            }

            return SignalResult.Pass(
                SignalSide.Short, "PASS:SHORT", hvn, targetNode, targetPx, label ?? "", imb, ctx.Profile.Shape);
        }

        private static bool TryResolveTarget(
            SignalContext ctx,
            SignalSide side,
            double fromPrice,
            VolumeNode entryHvn,
            out VolumeNode node,
            out double price,
            out string label)
        {
            node = null;
            price = 0;
            label = null;

            if (side == SignalSide.Long)
            {
                // Next HVN above (not entry)
                VolumeNode bestHvn = null;
                double bestMid = double.MaxValue;
                if (ctx.Profile.Hvns != null)
                {
                    foreach (var h in ctx.Profile.Hvns)
                    {
                        if (h == null) continue;
                        if (ReferenceEquals(h, entryHvn)) continue;
                        if (h.Mid <= fromPrice + 1e-9) continue;
                        if (h.Mid < bestMid)
                        {
                            bestMid = h.Mid;
                            bestHvn = h;
                        }
                    }
                }
                if (bestHvn != null)
                {
                    node = bestHvn;
                    price = bestHvn.Mid;
                    label = "HVN";
                    return true;
                }

                if (ctx.AllowPocVaTargets)
                {
                    if (ctx.Profile.POC > fromPrice + 1e-9)
                    {
                        node = SyntheticNode(ctx.Profile.POC);
                        price = ctx.Profile.POC;
                        label = "POC";
                        return true;
                    }
                    if (ctx.Profile.VAH > fromPrice + 1e-9)
                    {
                        node = SyntheticNode(ctx.Profile.VAH);
                        price = ctx.Profile.VAH;
                        label = "VAH";
                        return true;
                    }
                }
            }
            else
            {
                VolumeNode bestHvn = null;
                double bestMid = double.MinValue;
                if (ctx.Profile.Hvns != null)
                {
                    foreach (var h in ctx.Profile.Hvns)
                    {
                        if (h == null) continue;
                        if (ReferenceEquals(h, entryHvn)) continue;
                        if (h.Mid >= fromPrice - 1e-9) continue;
                        if (h.Mid > bestMid)
                        {
                            bestMid = h.Mid;
                            bestHvn = h;
                        }
                    }
                }
                if (bestHvn != null)
                {
                    node = bestHvn;
                    price = bestHvn.Mid;
                    label = "HVN";
                    return true;
                }

                if (ctx.AllowPocVaTargets)
                {
                    if (ctx.Profile.POC < fromPrice - 1e-9)
                    {
                        node = SyntheticNode(ctx.Profile.POC);
                        price = ctx.Profile.POC;
                        label = "POC";
                        return true;
                    }
                    if (ctx.Profile.VAL < fromPrice - 1e-9)
                    {
                        node = SyntheticNode(ctx.Profile.VAL);
                        price = ctx.Profile.VAL;
                        label = "VAL";
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool PassesMinR(SignalContext ctx, double fromPrice, double targetPrice, SignalSide side)
        {
            double minR = Math.Max(0, ctx.MinFirstTargetR);
            if (minR <= 0) return true;

            double slDist = ctx.EstimatedSlDistance;
            if (slDist <= 1e-9)
                slDist = Math.Max(ctx.Atr * 0.8, 0.5);

            double dist = Math.Abs(targetPrice - fromPrice);
            if (side == SignalSide.Long && targetPrice <= fromPrice) return false;
            if (side == SignalSide.Short && targetPrice >= fromPrice) return false;

            return dist / slDist >= minR - 1e-9;
        }

        private static VolumeNode SyntheticNode(double mid)
        {
            double half = Math.Max(0.1, Math.Abs(mid) * 0.0001 + 0.25);
            return new VolumeNode
            {
                Type = VolumeNodeType.HVN,
                StartBin = -1,
                EndBin = -1,
                Low = mid - half,
                High = mid + half,
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
