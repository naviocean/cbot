using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using RedWave.Common;

namespace cAlgo.Robots
{
    /// <summary>
    /// RMS: Responsive Micro Scalper — HTF bias + M1 momentum/accel, vol-scaled SL/TP (1.5R), regime scales.
    /// Spec: docs/v1.0/1-prds/PRD-rms.md
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class ResponsiveMicroScalper : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "Rms")]
        public string BotLabel { get; set; }

        [Parameter("Position Size Mode", Group = "Trade & Risk", DefaultValue = RmsSizeMode.RiskPercent)]
        public RmsSizeMode SizeMode { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.50, MinValue = 0.1, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Trades / Day (Base)", Group = "Trade & Risk", DefaultValue = 12, MinValue = 1, MaxValue = 50)]
        public int MaxTradesDayBase { get; set; }

        [Parameter("Cooldown Bars (Base)", Group = "Trade & Risk", DefaultValue = 5, MinValue = 1, MaxValue = 60)]
        public int CooldownBase { get; set; }

        [Parameter("Max Hold Minutes", Group = "Trade & Risk", DefaultValue = 12, MinValue = 1, MaxValue = 120)]
        public int MaxHoldMinutes { get; set; }

        [Parameter("Max Spread (pips)", Group = "Trade & Risk", DefaultValue = 50.0, MinValue = 0.1)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Max Equity DD %", Group = "Trade & Risk", DefaultValue = 12.0, MinValue = 0.0)]
        public double MaxEquityDrawdownPct { get; set; }

        [Parameter("Flatten On Equity DD", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnEquityDd { get; set; }

        [Parameter("Max Daily Loss ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyLossAmount { get; set; }

        [Parameter("Flatten On Daily Loss", Group = "Trade & Risk", DefaultValue = true)]
        public bool FlattenOnDailyLoss { get; set; }

        [Parameter("Max Daily Profit ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyProfitAmount { get; set; }

        [Parameter("Flatten On Daily Profit", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnDailyProfit { get; set; }

        [Parameter("Debug Logging", Group = "Trade & Risk", DefaultValue = false)]
        public bool DebugLogging { get; set; }

        // ─── Session ────────────────────────────────────────
        [Parameter("Trade Asia", Group = "Session", DefaultValue = false)]
        public bool TradeAsia { get; set; }

        [Parameter("Trade London", Group = "Session", DefaultValue = true)]
        public bool TradeLondon { get; set; }

        [Parameter("Trade New York", Group = "Session", DefaultValue = true)]
        public bool TradeNewYork { get; set; }

        [Parameter("Trade Overlap (Lon-NY)", Group = "Session", DefaultValue = true)]
        public bool TradeOverlap { get; set; }

        // ─── Signal ─────────────────────────────────────────
        [Parameter("HTF Timeframe", Group = "Signal", DefaultValue = RmsHtfTf.H1)]
        public RmsHtfTf HtfTimeframe { get; set; }

        [Parameter("HTF Lookback", Group = "Signal", DefaultValue = 20, MinValue = 5, MaxValue = 100)]
        public int HtfLookback { get; set; }

        [Parameter("Use HTF Strength", Group = "Signal", DefaultValue = true)]
        public bool UseHtfStrength { get; set; }

        [Parameter("HTF Min Strength", Group = "Signal", DefaultValue = 0.50, MinValue = 0.0, MaxValue = 5.0)]
        public double HtfMinStrength { get; set; }

        [Parameter("Mom Period (K)", Group = "Signal", DefaultValue = 3, MinValue = 2, MaxValue = 8)]
        public int MomPeriod { get; set; }

        [Parameter("Base Accel Threshold", Group = "Signal", DefaultValue = 0.00008, MinValue = 0.0)]
        public double BaseAccelThresh { get; set; }

        [Parameter("Var Window (W)", Group = "Signal", DefaultValue = 10, MinValue = 5, MaxValue = 30)]
        public int VarWindow { get; set; }

        [Parameter("Var Min (Base)", Group = "Signal", DefaultValue = 0.00000005, MinValue = 0.0)]
        public double VarMinBase { get; set; }

        [Parameter("ATR Period", Group = "Signal", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        [Parameter("ATR Avg Period", Group = "Signal", DefaultValue = 100, MinValue = 20)]
        public int AtrAvgPeriod { get; set; }

        [Parameter("Vol Mult Min", Group = "Signal", DefaultValue = 0.50, MinValue = 0.1, MaxValue = 1.0)]
        public double VolMultMin { get; set; }

        [Parameter("Vol Mult Max", Group = "Signal", DefaultValue = 2.50, MinValue = 1.0, MaxValue = 5.0)]
        public double VolMultMax { get; set; }

        [Parameter("SL Mult (×ATR)", Group = "Signal", DefaultValue = 1.20, MinValue = 0.5, MaxValue = 3.0)]
        public double SlMult { get; set; }

        [Parameter("TP RR", Group = "Signal", DefaultValue = 1.50, MinValue = 0.5, MaxValue = 5.0)]
        public double TpRr { get; set; }

        [Parameter("Min SL (pips)", Group = "Signal", DefaultValue = 20.0, MinValue = 1.0)]
        public double MinSlPips { get; set; }

        [Parameter("Max SL (pips)", Group = "Signal", DefaultValue = 400.0, MinValue = 10.0)]
        public double MaxSlPips { get; set; }

        [Parameter("Regime Eval Bars", Group = "Signal", DefaultValue = 90, MinValue = 10, MaxValue = 500)]
        public int RegimeEvalBars { get; set; }

        [Parameter("Vol High", Group = "Signal", DefaultValue = 1.30, MinValue = 1.0)]
        public double VolHigh { get; set; }

        [Parameter("Vol Low", Group = "Signal", DefaultValue = 0.70, MinValue = 0.1, MaxValue = 1.0)]
        public double VolLow { get; set; }

        [Parameter("Strength High", Group = "Signal", DefaultValue = 1.00, MinValue = 0.0)]
        public double StrengthHigh { get; set; }

        [Parameter("Strength Low", Group = "Signal", DefaultValue = 0.35, MinValue = 0.0)]
        public double StrengthLow { get; set; }

        // ─── Break Even / Trail ─────────────────────────────
        [Parameter("Use Break Even", Group = "Break Even", DefaultValue = false)]
        public bool UseBreakEven { get; set; }

        [Parameter("BE Trigger (R)", Group = "Break Even", DefaultValue = 0.80, MinValue = 0.1)]
        public double BeTriggerR { get; set; }

        [Parameter("BE Lock (pips)", Group = "Break Even", DefaultValue = 2.0, MinValue = 0.0)]
        public double BeLockPips { get; set; }

        [Parameter("BE Add Spread", Group = "Break Even", DefaultValue = true)]
        public bool BeAddSpread { get; set; }

        [Parameter("Use Trailing", Group = "Trailing", DefaultValue = false)]
        public bool UseTrailing { get; set; }

        [Parameter("Trail Start (R)", Group = "Trailing", DefaultValue = 1.00, MinValue = 0.1)]
        public double TrailStartR { get; set; }

        [Parameter("Trail Step (R)", Group = "Trailing", DefaultValue = 0.40, MinValue = 0.1)]
        public double TrailStepR { get; set; }

        // ─── News ───────────────────────────────────────────
        [Parameter("Enable News Filter", Group = "News", DefaultValue = false)]
        public bool EnableNewsFilter { get; set; }

        [Parameter("News Blackout (min)", Group = "News", DefaultValue = 15, MinValue = 0)]
        public int NewsBlackoutMinutes { get; set; }

        [Parameter("News Schedule UTC", Group = "News", DefaultValue = "")]
        public string NewsSchedule { get; set; }

        // ─── Internals ──────────────────────────────────────
        private CLogger _logger;
        private CRiskManager _riskManager;
        private CTrailingManager _trailingManager;
        private CMarketCondition _marketCondition;
        private CNewsFilter _newsFilter;
        private CSessionFilter _sessionFilter;
        private SignalEngine _signalEngine;
        private AverageTrueRange _atr;
        private AverageTrueRange _htfAtr;
        private Bars _htfBars;

        private int _tradesToday;
        private int _tradeDayKey = -1;
        private DateTime _lastSignalBarTime = DateTime.MinValue;
        private int _lastExitBarIndex = -1;
        private RmsRegime _loggedRegime = RmsRegime.Normal;
        private string _lastReject = "";
        private int _rejectCountToday;

        private readonly Dictionary<int, OpenTradeMeta> _openMeta = new Dictionary<int, OpenTradeMeta>();

        private sealed class OpenTradeMeta
        {
            public DateTime EntryUtc;
            public double SlDistPrice;
            public double EntryPrice;
        }

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("Rms", DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);

            if (!IsGoldSymbol(SymbolName))
                _logger.Warn($"Symbol {SymbolName} is not XAU/GOLD — sizing still runs; verify spread units");

            if (TimeFrame != TimeFrame.Minute)
                _logger.Warn($"Chart TF is {TimeFrame}; PRD expects M1 closed-bar signals");

            _sessionFilter = new CSessionFilter();
            _sessionFilter.Init(TradeAsia, TradeLondon, TradeNewYork, TradeOverlap, _logger);
            if (!TradeAsia && !TradeLondon && !TradeNewYork && !TradeOverlap)
                _logger.Warn("No session enabled — bot will never enter");

            _riskManager = new CRiskManager();
            _riskManager.Init(this, Symbol, BotLabel, _logger);
            _riskManager.SetEquityProtection(MaxEquityDrawdownPct, FlattenOnEquityDd);
            _riskManager.SetDailyLimits(
                MaxDailyLossAmount,
                MaxDailyProfitAmount,
                FlattenOnDailyLoss,
                FlattenOnDailyProfit);

            _trailingManager = new CTrailingManager();
            _trailingManager.Init(this, Symbol, BotLabel, _logger);

            _marketCondition = new CMarketCondition();
            _marketCondition.Init(Symbol, _logger);
            _marketCondition.SetSpreadCheck(true, MaxSpreadPips);

            _newsFilter = new CNewsFilter();
            _newsFilter.Init(EnableNewsFilter, NewsBlackoutMinutes, _logger);
            if (!string.IsNullOrWhiteSpace(NewsSchedule))
                _newsFilter.LoadFromString(NewsSchedule);

            _signalEngine = new SignalEngine();
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);

            TimeFrame htfTf = HtfTimeframe == RmsHtfTf.H4 ? TimeFrame.Hour4 : TimeFrame.Hour;
            _htfBars = MarketData.GetBars(htfTf);
            _htfAtr = Indicators.AverageTrueRange(_htfBars, AtrPeriod, MovingAverageType.Simple);

            _tradesToday = 0;
            Positions.Closed += OnPositionClosed;

            string sizeDesc = SizeMode == RmsSizeMode.FixedLots
                ? $"fixedLots={FixedLots}"
                : $"risk={RiskPercent}%";
            _logger.Info(
                $"Started {SymbolName} chartTF={TimeFrame} htf={htfTf} size={sizeDesc} " +
                $"RR={TpRr:F2} momK={MomPeriod} accelBase={BaseAccelThresh:G4} " +
                $"maxHold={MaxHoldMinutes}m cooldownBase={CooldownBase} maxTradesBase={MaxTradesDayBase} " +
                $"BE={UseBreakEven} Trail={UseTrailing} sessions={_sessionFilter.DescribeEnabled()}");
        }

        protected override void OnTick()
        {
            _riskManager.OnTick();

            if (UseBreakEven || UseTrailing)
                _trailingManager.OnTick();

            ManageTimeExits();
        }

        protected override void OnBar()
        {
            DateTime utc = Server.TimeInUtc;
            ResetDailyCounters(utc);

            int minBars = Math.Max(MomPeriod + VarWindow + 5, AtrPeriod + AtrAvgPeriod + 5);
            if (Bars.Count < minBars)
                return;

            int bi = Bars.Count - 2;
            DateTime barTime = Bars.OpenTimes[bi];
            if (barTime <= _lastSignalBarTime)
                return;
            _lastSignalBarTime = barTime;

            ManageTimeExits();

            if (Positions.FindAll(BotLabel, SymbolName).Length > 0)
                return;

            if (!TryBuildCloses(bi, out double[] closes))
                return;

            if (!TryGetAtr(bi, out double atr, out double atrAvg))
                return;

            if (!TryGetHtf(out double htfClose, out double htfLookbackClose, out double htfAtr, out bool htfReady))
                return;

            int barsSinceExit = _lastExitBarIndex < 0
                ? int.MaxValue / 4
                : bi - _lastExitBarIndex;

            var ctx = new RmsSignalContext
            {
                BarIndex = bi,
                BarTime = barTime,
                Closes = closes,
                Atr = atr,
                AtrAvg = atrAvg,
                HtfClose = htfClose,
                HtfCloseLookback = htfLookbackClose,
                HtfAtr = htfAtr,
                HtfReady = htfReady,
                MomPeriod = MomPeriod,
                VarWindow = VarWindow,
                BaseAccelThresh = BaseAccelThresh,
                VarMinBase = VarMinBase,
                VolMultMin = VolMultMin,
                VolMultMax = VolMultMax,
                SlMult = SlMult,
                TpRr = TpRr,
                MinSlPips = MinSlPips,
                MaxSlPips = MaxSlPips,
                PipSize = Symbol.PipSize,
                UseHtfStrength = UseHtfStrength,
                HtfMinStrength = HtfMinStrength,
                RegimeEvalBars = RegimeEvalBars,
                VolHigh = VolHigh,
                VolLow = VolLow,
                StrengthHigh = StrengthHigh,
                StrengthLow = StrengthLow,
                CooldownBase = CooldownBase,
                MaxTradesDayBase = MaxTradesDayBase,
                BarsSinceLastExit = barsSinceExit,
                TradesToday = _tradesToday,
                SessionOk = _sessionFilter.IsTradingAllowed(utc),
                SpreadOk = _marketCondition.IsTradingOK(),
                RiskOk = _riskManager.CanOpenNewTrade,
                NewsOk = _newsFilter.IsTradingAllowed(utc),
                HasOpenPosition = false,
                ForceRegimeEval = false
            };

            var result = _signalEngine.Evaluate(ctx);

            if (result.Regime != _loggedRegime)
            {
                _loggedRegime = result.Regime;
                string code = result.Regime == RmsRegime.Aggressive ? "REGIME_AGG"
                    : result.Regime == RmsRegime.Conservative ? "REGIME_CON" : "REGIME_NRM";
                _logger.Info(
                    $"{code} volRatio={result.VolRatio:F2} htfS={result.HtfStrength:F2} " +
                    $"accelScale={result.AccelScale:F2} cd={result.CooldownBars} maxT={result.MaxTradesDay}");
            }

            if (!result.IsEntry)
            {
                LogReject(result);
                return;
            }

            _logger.Info(
                $"{result.Reason} regime={result.Regime} M={result.Momentum:G4} A={result.Accel:G4} " +
                $"thresh={result.Threshold:G4} V={result.Variance:G4} volM={result.VolMult:F2} " +
                $"slDist={result.SlDist:F2} tpDist={result.TpDist:F2}");

            if (!EnableTrading)
            {
                _logger.Info("Dry-run (EnableTrading=false) — order skipped");
                return;
            }

            ExecuteEntry(result);
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnPositionClosed;
            _logger?.Info("Rms stopped");
        }

        // ─── Entry ──────────────────────────────────────────

        private void ExecuteEntry(RmsSignalResult signal)
        {
            bool isLong = signal.Side == RmsSide.Long;
            TradeType tradeType = isLong ? TradeType.Buy : TradeType.Sell;
            // Signal ref: side-aware quote at decision; mid for slippage vs fill
            double signalSidePrice = isLong ? Symbol.Ask : Symbol.Bid;
            double signalMid = (Symbol.Bid + Symbol.Ask) / 2.0;
            double entry = signalSidePrice;
            double slDist = signal.SlDist;
            double tpDist = signal.TpDist;

            if (slDist <= 0 || tpDist <= 0 || double.IsNaN(slDist) || double.IsNaN(tpDist))
            {
                _logger.Warn("Execute aborted: invalid SL/TP distance");
                return;
            }

            double sl = PriceUtils.NormalizePrice(isLong ? entry - slDist : entry + slDist, Symbol);
            double tp = PriceUtils.NormalizePrice(isLong ? entry + tpDist : entry - tpDist, Symbol);
            slDist = Math.Abs(entry - sl);
            if (slDist <= 0)
            {
                _logger.Warn("Execute aborted: SL collapsed after normalize");
                return;
            }

            if (!TrySizeVolume(slDist, out double volume, out double expectedRisk, out string sizeNote))
            {
                _logger.Warn("Execute aborted: E_LOT");
                return;
            }

            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, sl, tp, "rms", false);
            if (!result.IsSuccessful || result.Position == null)
            {
                _logger.Error($"Order failed: {result.Error}");
                return;
            }

            ConfigureExitsForTrade(slDist);

            var pos = result.Position;
            DateTime entryUtc = pos.EntryTime.Kind == DateTimeKind.Utc
                ? pos.EntryTime
                : pos.EntryTime.ToUniversalTime();

            _openMeta[pos.Id] = new OpenTradeMeta
            {
                EntryUtc = entryUtc,
                SlDistPrice = Math.Abs(pos.EntryPrice - (pos.StopLoss ?? sl)),
                EntryPrice = pos.EntryPrice
            };

            // PRD §6.4: log fill vs signal mid / side quote
            double slipVsSide = isLong
                ? pos.EntryPrice - signalSidePrice
                : signalSidePrice - pos.EntryPrice;
            double slipVsMid = isLong
                ? pos.EntryPrice - signalMid
                : signalMid - pos.EntryPrice;
            double slipSidePips = PriceUtils.PriceToPips(slipVsSide, Symbol);
            double slipMidPips = PriceUtils.PriceToPips(slipVsMid, Symbol);

            _tradesToday++;
            _logger.Info(
                $"OPEN {tradeType} #{pos.Id} vol={pos.VolumeInUnits} entry={pos.EntryPrice:F2} " +
                $"sigSide={signalSidePrice:F2} mid={signalMid:F2} " +
                $"slipSide={slipVsSide:F3} ({slipSidePips:F1}p) slipMid={slipVsMid:F3} ({slipMidPips:F1}p) " +
                $"SL={sl:F2} TP={tp:F2} slDist={slDist:F2} size={sizeNote} riskEst=${expectedRisk:F0} " +
                $"tradesToday={_tradesToday}/{signal.MaxTradesDay} regime={signal.Regime}");
        }

        private void ConfigureExitsForTrade(double slDistPrice)
        {
            if (slDistPrice <= 0) return;

            if (UseBreakEven)
            {
                double startPips = PriceUtils.PriceToPips(slDistPrice * BeTriggerR, Symbol);
                _trailingManager.SetBreakevenPoints(startPips, BeLockPips, BeAddSpread);
            }

            if (UseTrailing)
            {
                double startPips = PriceUtils.PriceToPips(slDistPrice * TrailStartR, Symbol);
                double stepPips = PriceUtils.PriceToPips(slDistPrice * TrailStepR, Symbol);
                double sensPips = Math.Max(1.0, stepPips * 0.25);
                _trailingManager.SetTrailPoints(startPips, stepPips, sensPips);
            }
        }

        // ─── Exits ──────────────────────────────────────────

        private void ManageTimeExits()
        {
            if (MaxHoldMinutes <= 0) return;

            var positions = Positions.FindAll(BotLabel, SymbolName);
            if (positions == null || positions.Length == 0) return;

            DateTime utc = Server.TimeInUtc;
            foreach (var pos in positions)
            {
                if (!_openMeta.TryGetValue(pos.Id, out var meta))
                {
                    DateTime entryUtc = pos.EntryTime.Kind == DateTimeKind.Utc
                        ? pos.EntryTime
                        : pos.EntryTime.ToUniversalTime();
                    meta = new OpenTradeMeta
                    {
                        EntryUtc = entryUtc,
                        EntryPrice = pos.EntryPrice,
                        SlDistPrice = pos.StopLoss.HasValue
                            ? Math.Abs(pos.EntryPrice - pos.StopLoss.Value)
                            : 0
                    };
                    _openMeta[pos.Id] = meta;
                }

                double holdMin = (utc - meta.EntryUtc).TotalMinutes;
                if (holdMin >= MaxHoldMinutes)
                {
                    _logger.Info($"X_TIME #{pos.Id} hold={holdMin:F1}m >= {MaxHoldMinutes}");
                    ClosePosition(pos);
                }
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if (pos == null || pos.SymbolName != SymbolName || pos.Label != BotLabel)
                return;

            _openMeta.Remove(pos.Id);
            // Anchor = forming bar at exit (Count-1), not last closed (Count-2).
            // Using Count-2 shortens cooldown by ~1 closed bar (see PRD F7).
            if (Bars.Count >= 1)
                _lastExitBarIndex = Bars.Count - 1;

            string reason = args.Reason.ToString();
            _logger.Info($"CLOSE #{pos.Id} reason={reason} net={pos.NetProfit:F2} → cooldown from bar={_lastExitBarIndex}");
        }

        // ─── Data helpers ───────────────────────────────────

        private bool TryBuildCloses(int bi, out double[] closes)
        {
            closes = null;
            if (bi < 0 || bi >= Bars.Count) return false;
            // Copy [0..bi] so engine indices match bar indices
            closes = new double[bi + 1];
            for (int i = 0; i <= bi; i++)
                closes[i] = Bars.ClosePrices[i];
            return true;
        }

        private bool TryGetAtr(int bi, out double atr, out double atrAvg)
        {
            atr = 0;
            atrAvg = 0;
            if (_atr == null || _atr.Result.Count < AtrPeriod + AtrAvgPeriod)
                return false;

            // Last(1) = previous completed bar ATR aligned with bi when OnBar fires
            atr = _atr.Result.Last(1);
            // NaN comparisons (atr <= 0) are always false — gate explicitly
            if (double.IsNaN(atr) || double.IsInfinity(atr) || atr <= 0)
                return false;

            int n = AtrAvgPeriod;
            double sum = 0;
            int used = 0;
            for (int i = 1; i <= n; i++)
            {
                if (_atr.Result.Count <= i) break;
                double v = _atr.Result.Last(i);
                if (!double.IsNaN(v) && !double.IsInfinity(v) && v > 0)
                {
                    sum += v;
                    used++;
                }
            }

            if (used < Math.Max(10, n / 2))
                return false;

            atrAvg = sum / used;
            return !double.IsNaN(atrAvg) && atrAvg > 0;
        }

        private bool TryGetHtf(
            out double htfClose,
            out double htfLookbackClose,
            out double htfAtr,
            out bool ready)
        {
            htfClose = 0;
            htfLookbackClose = 0;
            htfAtr = 0;
            ready = false;

            if (_htfBars == null || _htfBars.Count < HtfLookback + 3)
                return false;

            // Last closed HTF bar
            int h = _htfBars.Count - 2;
            int look = HtfLookback;
            if (h - look < 0)
                return false;

            htfClose = _htfBars.ClosePrices[h];
            htfLookbackClose = _htfBars.ClosePrices[h - look];
            if (double.IsNaN(htfClose) || double.IsNaN(htfLookbackClose) || htfClose <= 0 || htfLookbackClose <= 0)
                return false;

            if (_htfAtr != null && _htfAtr.Result.Count > 1)
                htfAtr = _htfAtr.Result.Last(1);
            // NaN fails (htfAtr <= 0); force fallback so strength / regime stay finite
            if (double.IsNaN(htfAtr) || double.IsInfinity(htfAtr) || htfAtr <= 0)
                htfAtr = Math.Abs(htfClose - htfLookbackClose) + Symbol.PipSize;

            if (double.IsNaN(htfAtr) || htfAtr <= 0)
                return false;

            ready = true;
            return ready;
        }

        private void ResetDailyCounters(DateTime utc)
        {
            int dayKey = utc.Year * 1000 + utc.DayOfYear;
            if (dayKey == _tradeDayKey)
                return;
            _tradeDayKey = dayKey;
            _tradesToday = 0;
            _rejectCountToday = 0;
            _lastReject = "";
            _logger.Debug($"UTC day reset key={dayKey}");
        }

        private void LogReject(RmsSignalResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.Reason))
                return;

            // Rate-limit: log on change or debug
            if (result.Reason != _lastReject || DebugLogging)
            {
                _lastReject = result.Reason;
                _rejectCountToday++;
                if (DebugLogging || ShouldLogReject(result.Reason))
                {
                    _logger.Debug(
                        $"reject={result.Reason} regime={result.Regime} " +
                        $"M={result.Momentum:G4} A={result.Accel:G4} thr={result.Threshold:G4} " +
                        $"V={result.Variance:G4} trades={_tradesToday}");
                }
            }
        }

        private static bool ShouldLogReject(string code)
        {
            // Always surface risk / max trades / both
            return code == "F_RISK" || code == "F_MAXTRADES" || code == "E_BOTH" || code == "E_LOT";
        }

        // ─── Sizing (XAU-safe, same family as SvbsX) ────────

        private bool TrySizeVolume(double slDist, out double volume, out double expectedRisk, out string sizeNote)
        {
            volume = 0;
            expectedRisk = 0;
            sizeNote = "";

            if (SizeMode == RmsSizeMode.FixedLots)
            {
                volume = _riskManager.CalculateVolume(FixedLots);
                if (volume <= 0)
                {
                    _logger.Warn($"Execute aborted: fixed lots volume failed lots={FixedLots}");
                    return false;
                }
                expectedRisk = EstimateRiskMoney(slDist, volume);
                sizeNote = $"fixedLots={FixedLots}";
                return true;
            }

            double riskMoney = Account.Equity * (RiskPercent / 100.0);
            if (riskMoney <= 0 || slDist <= 0)
            {
                _logger.Warn("Execute aborted: invalid risk money / slDist");
                return false;
            }

            double pipSize = PriceUtils.GetPipSize(Symbol);
            double slPips = pipSize > 0 ? slDist / pipSize : 0;

            double volOz = riskMoney / slDist;
            bool tickMetaOk = IsTickMetaConsistentForGold();
            bool gold = IsGoldSymbol(SymbolName);

            double volFixed = 0;
            if (slPips >= 0.1 && (!gold || tickMetaOk))
            {
                try
                {
                    volFixed = Symbol.VolumeForFixedRisk(riskMoney, slPips, RoundingMode.Down);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"VolumeForFixedRisk failed: {ex.Message}");
                }
            }

            double volTick = 0;
            if (tickMetaOk && Symbol.TickSize > 0 && Symbol.TickValue > 0 && Symbol.LotSize > 0)
            {
                double lossPerUnit = (slDist / Symbol.TickSize) * Symbol.TickValue / Symbol.LotSize;
                if (lossPerUnit > 0)
                    volTick = riskMoney / lossPerUnit;
            }

            double raw = double.MaxValue;
            string picked = "";
            if (gold && !tickMetaOk)
            {
                raw = volOz;
                picked = "Oz(goldMeta)";
            }
            else
            {
                if (volFixed > 0 && volFixed < raw) { raw = volFixed; picked = "FixedRisk"; }
                if (volOz > 0 && volOz < raw) { raw = volOz; picked = "Oz"; }
                if (volTick > 0 && volTick < raw) { raw = volTick; picked = "Tick"; }
            }

            if (raw <= 0 || raw >= double.MaxValue / 2)
            {
                _logger.Warn(
                    $"Execute aborted: no volume model volFixed={volFixed:F1} volOz={volOz:F1} volTick={volTick:F1}");
                return false;
            }

            volume = NormalizeVolumeUnits(raw);
            if (volume <= 0)
            {
                _logger.Warn($"Execute aborted: vol normalize 0 raw={raw:F2}");
                return false;
            }

            double maxOz = NormalizeVolumeUnits(volOz * 1.02);
            if (maxOz > 0 && volume > maxOz)
            {
                volume = maxOz;
                picked += "+OzCap";
            }

            expectedRisk = EstimateRiskMoney(slDist, volume);
            double ozRisk = volume * slDist;
            if (ozRisk > expectedRisk * 1.5)
                expectedRisk = ozRisk;

            if (ozRisk > riskMoney * 3.0)
            {
                _logger.Error(
                    $"Execute SAFETY ABORT: ozRisk=${ozRisk:F0} vol={volume:F0} >> target=${riskMoney:F0}");
                return false;
            }

            sizeNote = $"risk%={RiskPercent} via={picked}";
            return true;
        }

        private bool IsTickMetaConsistentForGold()
        {
            if (Symbol.TickSize <= 0 || Symbol.TickValue <= 0 || Symbol.LotSize <= 0)
                return false;
            if (!IsGoldSymbol(SymbolName))
                return true;

            double expect = Symbol.LotSize * Symbol.TickSize;
            if (expect <= 0) return false;
            double ratio = Symbol.TickValue / expect;
            return ratio >= 0.5 && ratio <= 2.0;
        }

        private double EstimateRiskMoney(double slDist, double volumeUnits)
        {
            if (slDist <= 0 || volumeUnits <= 0) return 0;

            double oz = volumeUnits * slDist;
            if (!IsTickMetaConsistentForGold())
                return oz;

            double tick = PriceUtils.PriceToAmount(slDist, volumeUnits, Symbol);
            if (IsGoldSymbol(SymbolName) && oz > 0 && tick > 0)
                return Math.Max(tick, oz);
            return tick > 0 ? tick : oz;
        }

        private double NormalizeVolumeUnits(double volume)
        {
            if (volume <= 0) return 0;
            double step = Symbol.VolumeInUnitsStep > 0 ? Symbol.VolumeInUnitsStep : 1;
            double min = Symbol.VolumeInUnitsMin > 0 ? Symbol.VolumeInUnitsMin : step;
            double max = Symbol.VolumeInUnitsMax > 0 ? Symbol.VolumeInUnitsMax : volume;
            double n = Math.Floor(volume / step) * step;
            if (n < min) return 0;
            return Math.Min(max, n);
        }

        private static bool IsGoldSymbol(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string u = name.ToUpperInvariant();
            return u.Contains("XAU") || u.Contains("GOLD");
        }
    }
}
