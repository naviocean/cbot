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

    public enum HtfBias
    {
        Neutral = 0,
        Bullish = 1,
        Bearish = -1
    }

    public sealed class SignalContext
    {
        public ProfileData Profile { get; set; }
        public double BarOpen { get; set; }
        public double BarHigh { get; set; }
        public double BarLow { get; set; }
        public double BarClose { get; set; }
        public double Atr { get; set; }
        public double PipSize { get; set; }
        
        // HTF Trend Bias
        public bool RequireHtfFilter { get; set; } = true;
        public HtfBias HtfTrend { get; set; } = HtfBias.Neutral;

        // Delta Data
        public double Cvd { get; set; }
        public double BuyImbalance { get; set; }
        public double SellImbalance { get; set; }

        // Strategy Parameters
        public double VolumeSpikeMultiplier { get; set; } = 1.5;
        public double PocProximityPips { get; set; } = 15.0;
        public double NodeSlBufferPips { get; set; } = 8.0;
        public double MinSlAtrMult { get; set; } = 0.8;

        // Take Profit & R:R
        public TpMode TakeProfitMode { get; set; } = TpMode.RiskReward;
        public double RrMultiple { get; set; } = 2.0;
        public double FixedTpPrice { get; set; } = 20.0;

        // Safety & Execution Gates
        public bool SessionOk { get; set; } = true;
        public bool NewsOk { get; set; } = true;
        public bool SpreadOk { get; set; } = true;
        public bool EquityOk { get; set; } = true;
        public int TradesToday { get; set; }
        public int MaxTradesPerDay { get; set; } = 3;
        public bool HasOpenPosition { get; set; }
    }

    public sealed class SignalResult
    {
        public bool IsValid { get; set; }
        public SignalSide Side { get; set; }
        public string Reason { get; set; }
        public double PocPrice { get; set; }
        public double VolSpikeRatio { get; set; }
        public double NodeTopPrice { get; set; }
        public double NodeBottomPrice { get; set; }
        public double StopLossPrice { get; set; }
        public double TakeProfitPrice { get; set; }
        public double RiskDistancePips { get; set; }
        public double RewardDistancePips { get; set; }
        public double CalculatedRr { get; set; }

        public static SignalResult Reject(string reason, double poc = 0, double spikeRatio = 0)
        {
            return new SignalResult
            {
                IsValid = false,
                Side = SignalSide.None,
                Reason = reason,
                PocPrice = poc,
                VolSpikeRatio = spikeRatio
            };
        }

        public static SignalResult Pass(
            SignalSide side,
            string reason,
            double pocPrice,
            double spikeRatio,
            double nodeTop,
            double nodeBottom,
            double slPrice,
            double tpPrice,
            double riskPips,
            double rewardPips,
            double rr)
        {
            return new SignalResult
            {
                IsValid = true,
                Side = side,
                Reason = reason,
                PocPrice = pocPrice,
                VolSpikeRatio = spikeRatio,
                NodeTopPrice = nodeTop,
                NodeBottomPrice = nodeBottom,
                StopLossPrice = slPrice,
                TakeProfitPrice = tpPrice,
                RiskDistancePips = riskPips,
                RewardDistancePips = rewardPips,
                CalculatedRr = rr
            };
        }
    }

    public static class SignalEngine
    {
        public static SignalResult Evaluate(SignalContext ctx)
        {
            // 1. Safety Gates
            if (!ctx.SessionOk) return SignalResult.Reject("Out of session window");
            if (!ctx.NewsOk) return SignalResult.Reject("Inside news blackout window");
            if (!ctx.SpreadOk) return SignalResult.Reject("Spread exceeds threshold");
            if (!ctx.EquityOk) return SignalResult.Reject("Equity risk gate active");
            if (ctx.HasOpenPosition) return SignalResult.Reject("Position already open");
            if (ctx.MaxTradesPerDay > 0 && ctx.TradesToday >= ctx.MaxTradesPerDay)
                return SignalResult.Reject($"Daily trade limit reached ({ctx.TradesToday}/{ctx.MaxTradesPerDay})");

            // 2. Profile Validity
            if (ctx.Profile == null || !ctx.Profile.IsValid || ctx.Profile.POC <= 0 || ctx.Profile.VAH <= 0 || ctx.Profile.VAL <= 0)
                return SignalResult.Reject("Invalid or uninitialized volume profile");

            double poc = ctx.Profile.POC;
            double vah = ctx.Profile.VAH;
            double val = ctx.Profile.VAL;
            double proxDist = ctx.PocProximityPips * ctx.PipSize;

            // 3. Check Volume Spike at POC Node
            double spikeRatio = CalculateVolumeSpikeRatio(ctx.Profile);
            if (spikeRatio < ctx.VolumeSpikeMultiplier)
                return SignalResult.Reject($"Volume spike ratio {spikeRatio:F2}x < {ctx.VolumeSpikeMultiplier:F2}x required", poc, spikeRatio);

            // 4. Calculate Node Boundaries (Node Top & Node Bottom)
            GetNodeBoundaries(ctx.Profile, out double nodeTop, out double nodeBottom);

            // 5. Evaluate Long Entry Signal (BUY Absorption at POC)
            bool isLongProximity = ctx.BarLow <= poc + proxDist && ctx.BarHigh >= poc - proxDist;
            bool isLongRejection = isLongProximity && ctx.BarClose > poc && (ctx.BarClose >= ctx.BarOpen);
            bool isLongDeltaDivergence = ctx.Cvd > 0 || ctx.BuyImbalance > ctx.SellImbalance;
            bool isLongHtfOk = !ctx.RequireHtfFilter || ctx.HtfTrend == HtfBias.Bullish;

            if (isLongRejection && isLongDeltaDivergence && isLongHtfOk)
            {
                double sl = nodeBottom - (ctx.NodeSlBufferPips * ctx.PipSize);
                double minSlDist = Math.Max(ctx.BarClose - sl, ctx.Atr * ctx.MinSlAtrMult);
                sl = ctx.BarClose - minSlDist;

                double tp = CalculateTakeProfit(SignalSide.Long, ctx.BarClose, minSlDist, vah, val, ctx);
                double riskPriceDist = ctx.BarClose - sl;
                double rewardPriceDist = tp - ctx.BarClose;
                double riskPips = riskPriceDist / ctx.PipSize;
                double rewardPips = rewardPriceDist / ctx.PipSize;
                double rr = rewardPips / Math.Max(0.1, riskPips);

                return SignalResult.Pass(
                    SignalSide.Long,
                    $"Long POC Absorption: POC={poc:F2}, Spike={spikeRatio:F2}x, HTF={ctx.HtfTrend}, RR={rr:F2}",
                    poc, spikeRatio, nodeTop, nodeBottom, sl, tp, riskPips, rewardPips, rr);
            }

            // 6. Evaluate Short Entry Signal (SELL Absorption at POC)
            bool isShortProximity = ctx.BarHigh >= poc - proxDist && ctx.BarLow <= poc + proxDist;
            bool isShortRejection = isShortProximity && ctx.BarClose < poc && (ctx.BarClose <= ctx.BarOpen);
            bool isShortDeltaDivergence = ctx.Cvd < 0 || ctx.SellImbalance > ctx.BuyImbalance;
            bool isShortHtfOk = !ctx.RequireHtfFilter || ctx.HtfTrend == HtfBias.Bearish;

            if (isShortRejection && isShortDeltaDivergence && isShortHtfOk)
            {
                double sl = nodeTop + (ctx.NodeSlBufferPips * ctx.PipSize);
                double minSlDist = Math.Max(sl - ctx.BarClose, ctx.Atr * ctx.MinSlAtrMult);
                sl = ctx.BarClose + minSlDist;

                double tp = CalculateTakeProfit(SignalSide.Short, ctx.BarClose, minSlDist, vah, val, ctx);
                double riskPriceDist = sl - ctx.BarClose;
                double rewardPriceDist = ctx.BarClose - tp;
                double riskPips = riskPriceDist / ctx.PipSize;
                double rewardPips = rewardPriceDist / ctx.PipSize;
                double rr = rewardPips / Math.Max(0.1, riskPips);

                return SignalResult.Pass(
                    SignalSide.Short,
                    $"Short POC Absorption: POC={poc:F2}, Spike={spikeRatio:F2}x, HTF={ctx.HtfTrend}, RR={rr:F2}",
                    poc, spikeRatio, nodeTop, nodeBottom, sl, tp, riskPips, rewardPips, rr);
            }

            // Detailed reason for reject
            string detailReason = "No absorption signal (";
            if (!isLongHtfOk && !isShortHtfOk) detailReason += $"HTF Trend {ctx.HtfTrend} mismatch";
            else if (!isLongProximity && !isShortProximity) detailReason += "Price far from POC";
            else if (!isLongRejection && !isShortRejection) detailReason += "Bar close didn't reject POC";
            else if (!isLongDeltaDivergence && !isShortDeltaDivergence) detailReason += "Delta divergence failed";
            detailReason += ")";

            return SignalResult.Reject(detailReason, poc, spikeRatio);
        }

        private static double CalculateTakeProfit(SignalSide side, double entry, double slDist, double vah, double val, SignalContext ctx)
        {
            if (ctx.TakeProfitMode == TpMode.FixedPrice)
            {
                return side == SignalSide.Long ? entry + (ctx.FixedTpPrice * ctx.PipSize) : entry - (ctx.FixedTpPrice * ctx.PipSize);
            }
            else if (ctx.TakeProfitMode == TpMode.Structure)
            {
                return side == SignalSide.Long ? (vah > entry ? vah : entry + slDist * ctx.RrMultiple) : (val < entry ? val : entry - slDist * ctx.RrMultiple);
            }
            else // RiskReward (default)
            {
                return side == SignalSide.Long ? entry + (slDist * ctx.RrMultiple) : entry - (slDist * ctx.RrMultiple);
            }
        }

        public static double CalculateVolumeSpikeRatio(ProfileData profile)
        {
            if (profile?.Histogram == null || profile.BinCount < 5) return 1.0;

            int pocBin = profile.PocBin;
            double pocVol = profile.Histogram[pocBin];
            if (pocVol <= 0) return 1.0;

            int count = 0;
            double sumVol = 0;
            for (int i = Math.Max(0, pocBin - 3); i <= Math.Min(profile.BinCount - 1, pocBin + 3); i++)
            {
                if (i == pocBin) continue;
                sumVol += profile.Histogram[i];
                count++;
            }

            double avgNeighborVol = count > 0 ? sumVol / count : 1.0;
            return pocVol / Math.Max(1.0, avgNeighborVol);
        }

        private static void GetNodeBoundaries(ProfileData profile, out double nodeTop, out double nodeBottom)
        {
            nodeTop = profile.POC;
            nodeBottom = profile.POC;

            if (profile.Histogram == null || profile.BinCount == 0) return;

            int pocBin = profile.PocBin;
            double pocVol = profile.Histogram[pocBin];
            if (pocVol <= 0) return;

            double thresholdVol = pocVol * 0.5;

            // Scan Up
            int upBin = pocBin;
            while (upBin < profile.BinCount - 1 && profile.Histogram[upBin] >= thresholdVol)
            {
                upBin++;
            }
            nodeTop = profile.BinHigh(upBin);

            // Scan Down
            int downBin = pocBin;
            while (downBin > 0 && profile.Histogram[downBin] >= thresholdVol)
            {
                downBin--;
            }
            nodeBottom = profile.BinLow(downBin);
        }
    }
}
