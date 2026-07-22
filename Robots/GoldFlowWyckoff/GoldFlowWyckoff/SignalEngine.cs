using System;
using cAlgo.API;
using RedWave.Common;

namespace cAlgo.Robots
{
    public struct SignalContext
    {
        public ProfileData Profile { get; set; }
        public Bar ClosedBar { get; set; }
        public double Atr { get; set; }
        public double PipSize { get; set; }

        public bool RequireDeltaFilter { get; set; }
        public double BuyImbalance { get; set; }
        public double SellImbalance { get; set; }
        public double MinDeltaImbalance { get; set; }

        public bool RequireStructureBias { get; set; }
        public bool IsHigherLow { get; set; }
        public bool IsLowerHigh { get; set; }
        public bool IsSpring { get; set; }
        public bool IsUpthrust { get; set; }

        public bool RequireHtfTrend { get; set; }
        public bool IsHtfHigherLow { get; set; }
        public bool IsHtfLowerHigh { get; set; }

        public double RecentStructureLow { get; set; }
        public double RecentStructureHigh { get; set; }
        public double LastWyckoffPivotLow { get; set; }
        public double LastWyckoffPivotHigh { get; set; }

        public double TouchBuffer { get; set; }
        public double SlAtrMult { get; set; }
        public double MinSlAtrMult { get; set; }

        public GoldFlowTpMode TakeProfitMode { get; set; }
        public double RrMultiple { get; set; }
        public double FixedTpPrice { get; set; }
    }

    public struct SignalResult
    {
        public bool IsValid { get; set; }
        public TradeType Side { get; set; }
        public double StopLossPrice { get; set; }
        public double TakeProfitPrice { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Pure, decoupled Signal Evaluation Engine for Gold Flow Wyckoff cBot.
    /// Evaluates 3/3 Confluence (Wyckoff Wave Structure + Volume Profile V2 + Order Flow Delta).
    /// </summary>
    public static class SignalEngine
    {
        public static SignalResult Evaluate(SignalContext ctx)
        {
            var res = new SignalResult { IsValid = false, Reason = "No confluence signal" };

            if (ctx.Profile == null || !ctx.Profile.IsValid)
            {
                res.Reason = "Invalid Volume Profile Data";
                return res;
            }

            double val = ctx.Profile.VAL;
            double vah = ctx.Profile.VAH;

            if (val <= 0 || vah <= 0)
            {
                res.Reason = "Invalid VAH/VAL levels";
                return res;
            }

            Bar bar = ctx.ClosedBar;
            double touchBuffer = ctx.TouchBuffer;

            // ─── 1. Check Long Signal ─────────────────────────────────────────
            bool wyckoffLongBias = !ctx.RequireStructureBias || ctx.IsHigherLow;
            bool springLong = ctx.IsSpring;
            bool nearValSupport = Math.Abs(bar.Low - val) <= touchBuffer || (bar.Low <= val && bar.Close >= val);
            bool deltaLongConfirmed = !ctx.RequireDeltaFilter || ctx.BuyImbalance >= ctx.MinDeltaImbalance;
            bool htfLongConfirmed = !ctx.RequireHtfTrend || ctx.IsHtfHigherLow;

            if ((wyckoffLongBias || springLong) && nearValSupport && deltaLongConfirmed && htfLongConfirmed)
            {
                res.IsValid = true;
                res.Side = TradeType.Buy;
                res.Reason = springLong ? "Wyckoff Spring + VAL Support + Buy Delta Spike" : "Wyckoff Higher Low + VAL Support + Buy Delta";

                double entryPrice = bar.Close;
                double pivotLow = ctx.LastWyckoffPivotLow > 0 ? ctx.LastWyckoffPivotLow : bar.Low;
                double recentLow = ctx.RecentStructureLow > 0 ? ctx.RecentStructureLow : bar.Low;
                double lowestStructure = Math.Min(bar.Low, Math.Min(recentLow, pivotLow));

                double structureSl = Math.Min(lowestStructure, val) - (ctx.SlAtrMult * ctx.Atr);
                double minSlPrice = entryPrice - (ctx.MinSlAtrMult * ctx.Atr);
                res.StopLossPrice = Math.Min(structureSl, minSlPrice);

                double slDist = Math.Abs(entryPrice - res.StopLossPrice);

                if (ctx.TakeProfitMode == GoldFlowTpMode.RiskReward)
                {
                    res.TakeProfitPrice = entryPrice + (slDist * ctx.RrMultiple);
                }
                else if (ctx.TakeProfitMode == GoldFlowTpMode.FixedPrice)
                {
                    res.TakeProfitPrice = entryPrice + ctx.FixedTpPrice;
                }
                else // StructureMagnet
                {
                    res.TakeProfitPrice = vah;
                }

                return res;
            }

            // ─── 2. Check Short Signal ────────────────────────────────────────
            bool wyckoffShortBias = !ctx.RequireStructureBias || ctx.IsLowerHigh;
            bool upthrustShort = ctx.IsUpthrust;
            bool nearVahResistance = Math.Abs(bar.High - vah) <= touchBuffer || (bar.High >= vah && bar.Close <= vah);
            bool deltaShortConfirmed = !ctx.RequireDeltaFilter || ctx.SellImbalance >= ctx.MinDeltaImbalance;
            bool htfShortConfirmed = !ctx.RequireHtfTrend || ctx.IsHtfLowerHigh;

            if ((wyckoffShortBias || upthrustShort) && nearVahResistance && deltaShortConfirmed && htfShortConfirmed)
            {
                res.IsValid = true;
                res.Side = TradeType.Sell;
                res.Reason = upthrustShort ? "Wyckoff Upthrust + VAH Resistance + Sell Delta Spike" : "Wyckoff Lower High + VAH Resistance + Sell Delta";

                double entryPrice = bar.Close;
                double pivotHigh = ctx.LastWyckoffPivotHigh > 0 ? ctx.LastWyckoffPivotHigh : bar.High;
                double recentHigh = ctx.RecentStructureHigh > 0 ? ctx.RecentStructureHigh : bar.High;
                double highestStructure = Math.Max(bar.High, Math.Max(recentHigh, pivotHigh));

                double structureSl = Math.Max(highestStructure, vah) + (ctx.SlAtrMult * ctx.Atr);
                double minSlPrice = entryPrice + (ctx.MinSlAtrMult * ctx.Atr);
                res.StopLossPrice = Math.Max(structureSl, minSlPrice);

                double slDist = Math.Abs(entryPrice - res.StopLossPrice);

                if (ctx.TakeProfitMode == GoldFlowTpMode.RiskReward)
                {
                    res.TakeProfitPrice = entryPrice - (slDist * ctx.RrMultiple);
                }
                else if (ctx.TakeProfitMode == GoldFlowTpMode.FixedPrice)
                {
                    res.TakeProfitPrice = entryPrice - ctx.FixedTpPrice;
                }
                else // StructureMagnet
                {
                    res.TakeProfitPrice = val;
                }

                return res;
            }

            // Diagnostics reason for rejection
            string longFail = $"LongFail(Wyckoff={wyckoffLongBias}/Spring={springLong}, NearVAL={nearValSupport}[dist={Math.Abs(bar.Low - val):F2},buf={touchBuffer:F2}], Delta={deltaLongConfirmed}[buy={ctx.BuyImbalance:F2}])";
            string shortFail = $"ShortFail(Wyckoff={wyckoffShortBias}/Upthrust={upthrustShort}, NearVAH={nearVahResistance}[dist={Math.Abs(bar.High - vah):F2},buf={touchBuffer:F2}], Delta={deltaShortConfirmed}[sell={ctx.SellImbalance:F2}])";

            res.Reason = $"{longFail} | {shortFail}";
            return res;
        }
    }
}
