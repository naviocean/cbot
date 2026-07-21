using System;

namespace cAlgo.Robots
{
    public enum RmsSide
    {
        None = 0,
        Long = 1,
        Short = 2
    }

    public enum RmsRegime
    {
        Normal = 0,
        Aggressive = 1,
        Conservative = 2
    }

    public enum RmsSizeMode
    {
        RiskPercent = 0,
        FixedLots = 1
    }

    public enum RmsHtfTf
    {
        H1 = 0,
        H4 = 1
    }

    /// <summary>Inputs for one closed-bar evaluation (pure; no orders).</summary>
    public sealed class RmsSignalContext
    {
        public int BarIndex { get; set; }
        public DateTime BarTime { get; set; }

        /// <summary>M1 closes; length must be &gt; BarIndex.</summary>
        public double[] Closes { get; set; }

        public double Atr { get; set; }
        public double AtrAvg { get; set; }

        /// <summary>Last closed HTF close and close N bars ago.</summary>
        public double HtfClose { get; set; }
        public double HtfCloseLookback { get; set; }
        public double HtfAtr { get; set; }
        public bool HtfReady { get; set; }

        public int MomPeriod { get; set; }
        public int VarWindow { get; set; }
        public double BaseAccelThresh { get; set; }
        public double VarMinBase { get; set; }
        public double VolMultMin { get; set; }
        public double VolMultMax { get; set; }
        public double SlMult { get; set; }
        public double TpRr { get; set; }
        public double MinSlPips { get; set; }
        public double MaxSlPips { get; set; }
        public double PipSize { get; set; }

        public bool UseHtfStrength { get; set; }
        public double HtfMinStrength { get; set; }

        public int RegimeEvalBars { get; set; }
        public double VolHigh { get; set; }
        public double VolLow { get; set; }
        public double StrengthHigh { get; set; }
        public double StrengthLow { get; set; }

        public int CooldownBase { get; set; }
        public int MaxTradesDayBase { get; set; }
        public int BarsSinceLastExit { get; set; }
        public int TradesToday { get; set; }

        public bool SessionOk { get; set; }
        public bool SpreadOk { get; set; }
        public bool RiskOk { get; set; }
        public bool NewsOk { get; set; }
        public bool HasOpenPosition { get; set; }
        public bool ForceRegimeEval { get; set; }
    }

    public sealed class RmsSignalResult
    {
        public bool IsEntry { get; set; }
        public RmsSide Side { get; set; }
        public string Reason { get; set; } = "";
        public RmsRegime Regime { get; set; }
        public double Momentum { get; set; }
        public double Accel { get; set; }
        public double Variance { get; set; }
        public double Threshold { get; set; }
        public double VolMult { get; set; }
        public double VolRatio { get; set; }
        public double HtfStrength { get; set; }
        public double SlDist { get; set; }
        public double TpDist { get; set; }
        public int CooldownBars { get; set; }
        public int MaxTradesDay { get; set; }
        public double AccelScale { get; set; }
        public double CooldownScale { get; set; }
        public double MaxTradesScale { get; set; }
        public double VarMinScale { get; set; }
        public double SlDistScale { get; set; }
    }

    /// <summary>
    /// Pure RMS signal: HTF bias + micro momentum/accel + vol-scaled threshold + regime scales.
    /// PRD-rms §5–§6.
    /// </summary>
    public sealed class SignalEngine
    {
        private RmsRegime _regime = RmsRegime.Normal;
        private int _lastRegimeBarIndex = int.MinValue;
        private bool _regimeInitialized;

        public RmsRegime CurrentRegime => _regime;

        public void Reset()
        {
            _regime = RmsRegime.Normal;
            _lastRegimeBarIndex = int.MinValue;
            _regimeInitialized = false;
        }

        public RmsSignalResult Evaluate(RmsSignalContext ctx)
        {
            var result = new RmsSignalResult
            {
                IsEntry = false,
                Side = RmsSide.None,
                Regime = _regime
            };

            if (ctx == null || ctx.Closes == null)
            {
                result.Reason = "F_WARMUP";
                return result;
            }

            int t = ctx.BarIndex;
            int k = Math.Max(2, ctx.MomPeriod);
            int w = Math.Max(2, ctx.VarWindow);
            int need = Math.Max(k + 2, w + 2);
            if (t < need || t >= ctx.Closes.Length)
            {
                result.Reason = "F_WARMUP";
                return result;
            }

            if (ctx.Atr <= 0 || ctx.AtrAvg <= 0 || ctx.PipSize <= 0
                || double.IsNaN(ctx.Atr) || double.IsNaN(ctx.AtrAvg)
                || double.IsInfinity(ctx.Atr) || double.IsInfinity(ctx.AtrAvg))
            {
                result.Reason = "F_WARMUP";
                return result;
            }

            if (!ctx.HtfReady || double.IsNaN(ctx.HtfClose) || double.IsNaN(ctx.HtfCloseLookback)
                || double.IsNaN(ctx.HtfAtr) || ctx.HtfAtr <= 0)
            {
                result.Reason = "F_WARMUP";
                return result;
            }

            // ── Regime (sticky between evals) ──
            double volRatio = ctx.Atr / ctx.AtrAvg;
            result.VolRatio = volRatio;
            double volMult = Clamp(volRatio, ctx.VolMultMin, ctx.VolMultMax);
            result.VolMult = volMult;

            double htfDelta = ctx.HtfClose - ctx.HtfCloseLookback;
            double htfStrength = ctx.HtfAtr > 0 ? Math.Abs(htfDelta) / ctx.HtfAtr : 0;
            result.HtfStrength = htfStrength;

            bool shouldEvalRegime = ctx.ForceRegimeEval
                                    || !_regimeInitialized
                                    || (ctx.RegimeEvalBars > 0
                                        && t - _lastRegimeBarIndex >= ctx.RegimeEvalBars);
            if (shouldEvalRegime)
            {
                _regime = ClassifyRegime(volRatio, htfStrength, ctx);
                _lastRegimeBarIndex = t;
                _regimeInitialized = true;
            }

            result.Regime = _regime;
            ApplyScales(_regime, result);

            int cooldownBars = Math.Max(1, (int)Math.Round(ctx.CooldownBase * result.CooldownScale));
            int maxTradesDay = Math.Max(1, (int)Math.Round(ctx.MaxTradesDayBase * result.MaxTradesScale));
            result.CooldownBars = cooldownBars;
            result.MaxTradesDay = maxTradesDay;

            // ── Pre-filters ──
            if (ctx.HasOpenPosition)
            {
                result.Reason = "F_POSITION";
                return result;
            }

            if (!ctx.SessionOk)
            {
                result.Reason = "F_SESSION";
                return result;
            }

            if (!ctx.SpreadOk)
            {
                result.Reason = "F_SPREAD";
                return result;
            }

            if (!ctx.RiskOk)
            {
                result.Reason = "F_RISK";
                return result;
            }

            if (!ctx.NewsOk)
            {
                result.Reason = "F_NEWS";
                return result;
            }

            if (ctx.TradesToday >= maxTradesDay)
            {
                result.Reason = "F_MAXTRADES";
                return result;
            }

            // No prior exit uses a large BarsSinceLastExit from the robot.
            // Negative (exit on forming bar not yet closed as signal bar) must still block.
            if (ctx.BarsSinceLastExit < cooldownBars)
            {
                result.Reason = "F_COOLDOWN";
                return result;
            }

            // ── HTF bias ──
            if (htfDelta == 0)
            {
                result.Reason = "E_BIAS_FLAT";
                return result;
            }

            bool biasBull = htfDelta > 0;
            if (ctx.UseHtfStrength && htfStrength < ctx.HtfMinStrength)
            {
                result.Reason = "E_BIAS_WEAK";
                return result;
            }

            // ── Micro series ──
            if (!TryMomentumAccel(ctx.Closes, t, k, out double mom, out double accel))
            {
                result.Reason = "F_WARMUP";
                return result;
            }

            result.Momentum = mom;
            result.Accel = accel;

            if (!TryVariance(ctx.Closes, t, w, out double variance))
            {
                result.Reason = "F_WARMUP";
                return result;
            }

            result.Variance = variance;

            double varFloor = ctx.VarMinBase * result.VarMinScale;
            if (variance < varFloor)
            {
                result.Reason = "E_VAR";
                return result;
            }

            double thresh = ctx.BaseAccelThresh * volMult * result.AccelScale;
            result.Threshold = thresh;

            bool longOk = biasBull && mom > 0 && accel > thresh;
            bool shortOk = !biasBull && mom < 0 && accel < -thresh;

            if (longOk && shortOk)
            {
                result.Reason = "E_BOTH";
                return result;
            }

            if (!longOk && !shortOk)
            {
                if (biasBull)
                {
                    if (mom <= 0) result.Reason = "E_MOM";
                    else result.Reason = "E_ACC";
                }
                else
                {
                    if (mom >= 0) result.Reason = "E_MOM";
                    else result.Reason = "E_ACC";
                }

                return result;
            }

            // ── SL / TP ──
            double slDist = ctx.Atr * ctx.SlMult * result.SlDistScale;
            double tpDist = slDist * Math.Max(0.1, ctx.TpRr);
            double slPips = slDist / ctx.PipSize;

            if (slPips < ctx.MinSlPips)
            {
                result.Reason = "E_SL_TINY";
                result.SlDist = slDist;
                return result;
            }

            if (slPips > ctx.MaxSlPips)
            {
                result.Reason = "E_SL_WIDE";
                result.SlDist = slDist;
                return result;
            }

            result.SlDist = slDist;
            result.TpDist = tpDist;
            result.Side = longOk ? RmsSide.Long : RmsSide.Short;
            result.IsEntry = true;
            result.Reason = longOk ? "ENT_L" : "ENT_S";
            return result;
        }

        private static RmsRegime ClassifyRegime(double volRatio, double htfStrength, RmsSignalContext ctx)
        {
            bool aggressive = volRatio >= ctx.VolHigh && htfStrength >= ctx.StrengthHigh;
            bool conservative = volRatio <= ctx.VolLow || htfStrength < ctx.StrengthLow;

            // Prefer Conservative when both (safety)
            if (aggressive && conservative)
                return RmsRegime.Conservative;
            if (aggressive)
                return RmsRegime.Aggressive;
            if (conservative)
                return RmsRegime.Conservative;
            return RmsRegime.Normal;
        }

        private static void ApplyScales(RmsRegime regime, RmsSignalResult result)
        {
            switch (regime)
            {
                case RmsRegime.Aggressive:
                    result.AccelScale = 0.85;
                    result.CooldownScale = 0.70;
                    result.MaxTradesScale = 1.25;
                    result.VarMinScale = 0.90;
                    result.SlDistScale = 1.10;
                    break;
                case RmsRegime.Conservative:
                    result.AccelScale = 1.25;
                    result.CooldownScale = 1.40;
                    result.MaxTradesScale = 0.70;
                    result.VarMinScale = 1.15;
                    result.SlDistScale = 0.90;
                    break;
                default:
                    result.AccelScale = 1.00;
                    result.CooldownScale = 1.00;
                    result.MaxTradesScale = 1.00;
                    result.VarMinScale = 1.00;
                    result.SlDistScale = 1.00;
                    break;
            }
        }

        /// <summary>M[t]=ln(C[t]/C[t-K]); A[t]=M[t]-M[t-1].</summary>
        public static bool TryMomentumAccel(double[] closes, int t, int k, out double mom, out double accel)
        {
            mom = 0;
            accel = 0;
            if (closes == null || t < k + 1 || t >= closes.Length)
                return false;

            double cT = closes[t];
            double cTk = closes[t - k];
            double cT1 = closes[t - 1];
            double cT1k = closes[t - 1 - k];
            if (cT <= 0 || cTk <= 0 || cT1 <= 0 || cT1k <= 0)
                return false;

            mom = Math.Log(cT / cTk);
            double momPrev = Math.Log(cT1 / cT1k);
            accel = mom - momPrev;
            return true;
        }

        /// <summary>Population variance of 1-bar log returns over window W ending at t.</summary>
        public static bool TryVariance(double[] closes, int t, int w, out double variance)
        {
            variance = 0;
            if (closes == null || t < w || t >= closes.Length)
                return false;

            // r[t-j] for j=0..W-1 needs C[t-j] and C[t-j-1]
            if (t - w < 0)
                return false;

            double sum = 0;
            double[] rets = new double[w];
            for (int j = 0; j < w; j++)
            {
                int i = t - j;
                double c0 = closes[i];
                double c1 = closes[i - 1];
                if (c0 <= 0 || c1 <= 0)
                    return false;
                rets[j] = Math.Log(c0 / c1);
                sum += rets[j];
            }

            double mean = sum / w;
            double ss = 0;
            for (int j = 0; j < w; j++)
            {
                double d = rets[j] - mean;
                ss += d * d;
            }

            variance = ss / w;
            return true;
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
