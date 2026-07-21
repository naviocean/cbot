using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using RedWave.Common;

namespace cAlgo.Robots
{
    public enum LotSizeMode
    {
        RiskPercent = 0,
        FixedLots = 1
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class PmLh : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "PmLh")]
        public string BotLabel { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk", DefaultValue = LotSizeMode.RiskPercent)]
        public LotSizeMode SizeMode { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.50, MinValue = 0.1, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Trades / Day", Group = "Trade & Risk", DefaultValue = 10, MinValue = 1, MaxValue = 50)]
        public int MaxTradesPerDay { get; set; }

        [Parameter("Max Spread (pips)", Group = "Trade & Risk", DefaultValue = 80.0, MinValue = 0.1)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Max Equity DD %", Group = "Trade & Risk", DefaultValue = 10.0, MinValue = 0.0)]
        public double MaxEquityDrawdownPct { get; set; }

        [Parameter("Flatten On Equity DD", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnEquityDd { get; set; }

        [Parameter("Max Daily Loss ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyLossAmount { get; set; }

        [Parameter("Flatten On Daily Loss", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnDailyLoss { get; set; }

        [Parameter("Max Daily Profit ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyProfitAmount { get; set; }

        [Parameter("Flatten On Daily Profit", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnDailyProfit { get; set; }

        [Parameter("Debug Logging", Group = "Trade & Risk", DefaultValue = false)]
        public bool DebugLogging { get; set; }

        // ─── Stop / TP (RR only) ────────────────────────────
        [Parameter("ATR Period", Group = "Stop Loss", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        [Parameter("LVN buffer (×ATR)", Group = "Stop Loss", DefaultValue = 0.5, MinValue = 0.0)]
        public double SlAtrMult { get; set; }

        [Parameter("Min SL distance (×ATR)", Group = "Stop Loss", DefaultValue = 0.8, MinValue = 0.1)]
        public double MinSlAtrMult { get; set; }

        [Parameter("Max SL distance (×ATR)", Group = "Stop Loss", DefaultValue = 2.5, MinValue = 0.0)]
        public double MaxSlAtrMult { get; set; }

        [Parameter("RR Multiple", Group = "Take Profit", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0)]
        public double RrMultiple { get; set; }

        // ─── BE / Trail ─────────────────────────────────────
        [Parameter("Use Break Even", Group = "Break Even", DefaultValue = true)]
        public bool UseBreakEven { get; set; }

        [Parameter("BE Start (R)", Group = "Break Even", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0)]
        public double BeStartR { get; set; }

        [Parameter("BE Lock (R)", Group = "Break Even", DefaultValue = 0.05, MinValue = 0.0, MaxValue = 2.0)]
        public double BeLockR { get; set; }

        [Parameter("BE Add Spread", Group = "Break Even", DefaultValue = true)]
        public bool BeAddSpread { get; set; }

        [Parameter("Use Trailing", Group = "Trailing", DefaultValue = false)]
        public bool UseTrailing { get; set; }

        [Parameter("Trail Start (R)", Group = "Trailing", DefaultValue = 1.5, MinValue = 0.1, MaxValue = 20.0)]
        public double TrailStartR { get; set; }

        [Parameter("Trail Step (R)", Group = "Trailing", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 5.0)]
        public double TrailStepR { get; set; }

        // ─── Volume Profile ─────────────────────────────────
        [Parameter("LVN Source", Group = "Volume Profile", DefaultValue = LvnSourceMode.Composite)]
        public LvnSourceMode LvnSource { get; set; }

        [Parameter("Lookback Days", Group = "Volume Profile", DefaultValue = 3, MinValue = 1, MaxValue = 10)]
        public int ProfileLookbackDays { get; set; }

        [Parameter("Bin Size", Group = "Volume Profile", DefaultValue = 0.5, MinValue = 0.1)]
        public double BinSize { get; set; }

        [Parameter("Value Area %", Group = "Volume Profile", DefaultValue = 70.0, MinValue = 50.0, MaxValue = 90.0)]
        public double ValueAreaPercent { get; set; }

        [Parameter("LVN Threshold", Group = "Volume Profile", DefaultValue = 0.65, MinValue = 0.1, MaxValue = 1.0)]
        public double LvnThreshold { get; set; }

        [Parameter("HVN Threshold", Group = "Volume Profile", DefaultValue = 1.25, MinValue = 1.0)]
        public double HvnThreshold { get; set; }

        [Parameter("Weight Decay", Group = "Volume Profile", DefaultValue = 0.8, MinValue = 0.1, MaxValue = 1.0)]
        public double WeightDecay { get; set; }

        [Parameter("Min LVN Strength", Group = "Volume Profile", DefaultValue = 0.20, MinValue = 0.0, MaxValue = 1.0)]
        public double MinLvnStrength { get; set; }

        [Parameter("Max LVN Width ($)", Group = "Volume Profile", DefaultValue = 25.0, MinValue = 1.0)]
        public double MaxLvnWidth { get; set; }

        [Parameter("Top N LVN", Group = "Volume Profile", DefaultValue = 3, MinValue = 1, MaxValue = 10)]
        public int TopNLvn { get; set; }

        [Parameter("Visualize Profile", Group = "Volume Profile", DefaultValue = false)]
        public bool VisualizeProfile { get; set; }

        // ─── POC Migration ──────────────────────────────────
        [Parameter("POC Window Bars", Group = "POC Migration", DefaultValue = 24, MinValue = 5, MaxValue = 80)]
        public int PocWindowBars { get; set; }

        [Parameter("Migrate Lookback Bars", Group = "POC Migration", DefaultValue = 6, MinValue = 1, MaxValue = 40)]
        public int MigrateLookbackBars { get; set; }

        [Parameter("Min Migration M", Group = "POC Migration", DefaultValue = 0.40, MinValue = 0.05, MaxValue = 5.0)]
        public double MinMigrationM { get; set; }

        /// <summary>0 = off. Rolling POC often stays flat many bars; 1 bin is enough for net Δ.</summary>
        [Parameter("Min POC Move Bins", Group = "POC Migration", DefaultValue = 1, MinValue = 0, MaxValue = 20)]
        public int MinPocMoveBins { get; set; }

        /// <summary>
        /// Path filter on non-zero POC steps only. Default off for research — net M is primary.
        /// When on, plateaus do not count against streak (see tracker).
        /// </summary>
        [Parameter("Use Streak Filter", Group = "POC Migration", DefaultValue = false)]
        public bool UseStreakFilter { get; set; }

        [Parameter("Streak Need", Group = "POC Migration", DefaultValue = 2, MinValue = 1, MaxValue = 20)]
        public int StreakNeed { get; set; }

        [Parameter("Streak Of", Group = "POC Migration", DefaultValue = 6, MinValue = 1, MaxValue = 30)]
        public int StreakOf { get; set; }

        /// <summary>If |M| ≥ this, skip streak (clear migration). 0 = never bypass.</summary>
        [Parameter("Strong M Bypass", Group = "POC Migration", DefaultValue = 1.0, MinValue = 0.0, MaxValue = 10.0)]
        public double StrongMBypass { get; set; }

        /// <summary>
        /// Max |close − POC| on wrong side in ATR units (long below / short above).
        /// Blocks stale migration (POC still bull while price dumped). 0 = off.
        /// </summary>
        [Parameter("Max Price-POC (×ATR)", Group = "POC Migration", DefaultValue = 1.5, MinValue = 0.0, MaxValue = 10.0)]
        public double MaxPricePocAtr { get; set; }

        /// <summary>Long: close not fully under LVN; short: not fully above LVN.</summary>
        [Parameter("Require LVN Side", Group = "POC Migration", DefaultValue = true)]
        public bool RequireLvnSide { get; set; }

        // ─── Signal ─────────────────────────────────────────
        [Parameter("Entry Mode", Group = "Signal", DefaultValue = PmLhEntryMode.ShallowRetest)]
        public PmLhEntryMode EntryMode { get; set; }

        [Parameter("Touch Buffer ATR Mult", Group = "Signal", DefaultValue = 0.15, MinValue = 0.0)]
        public double TouchBufferAtrMult { get; set; }

        [Parameter("Prior Break Bars", Group = "Signal", DefaultValue = 8, MinValue = 1, MaxValue = 40)]
        public int PriorBreakBars { get; set; }

        [Parameter("Max Dwell Bars", Group = "Signal", DefaultValue = 3, MinValue = 0, MaxValue = 20)]
        public int MaxDwellBars { get; set; }

        [Parameter("Prefer Full Clear", Group = "Signal", DefaultValue = false)]
        public bool PreferFullClear { get; set; }

        [Parameter("Body ATR Mult", Group = "Signal", DefaultValue = 0.5, MinValue = 0.0)]
        public double BodyAtrMult { get; set; }

        [Parameter("Min Delta Strength", Group = "Signal", DefaultValue = 1.2, MinValue = 1.0)]
        public double MinDeltaStrength { get; set; }

        [Parameter("Min Delta Ticks", Group = "Signal", DefaultValue = 15, MinValue = 5)]
        public int MinDeltaTicks { get; set; }

        [Parameter("Delta Window (ms)", Group = "Signal", DefaultValue = 300000, MinValue = 10000)]
        public int DeltaWindowMs { get; set; }

        [Parameter("Require Delta Filter", Group = "Signal", DefaultValue = false)]
        public bool RequireDeltaFilter { get; set; }

        [Parameter("Require Shape Filter", Group = "Signal", DefaultValue = false)]
        public bool RequireShapeFilter { get; set; }

        [Parameter("Require HTF Filter", Group = "Signal", DefaultValue = false)]
        public bool RequireHtfFilter { get; set; }

        [Parameter("Require Expand Filter", Group = "Signal", DefaultValue = false)]
        public bool RequireExpandFilter { get; set; }

        [Parameter("Block Neutral Shape", Group = "Signal", DefaultValue = false)]
        public bool BlockNeutralShape { get; set; }

        [Parameter("Block DShape", Group = "Signal", DefaultValue = false)]
        public bool BlockDShape { get; set; }

        [Parameter("Expand ATR Mult", Group = "Signal", DefaultValue = 0.7, MinValue = 0.0)]
        public double ExpandAtrMult { get; set; }

        [Parameter("Expand ATR Fast", Group = "Signal", DefaultValue = 5, MinValue = 2)]
        public int ExpandAtrFastPeriod { get; set; }

        [Parameter("Expand ATR Slow", Group = "Signal", DefaultValue = 20, MinValue = 5)]
        public int ExpandAtrSlowPeriod { get; set; }

        [Parameter("Expand Ratio", Group = "Signal", DefaultValue = 1.1, MinValue = 1.0)]
        public double ExpandRatio { get; set; }

        [Parameter("HTF Timeframe", Group = "Signal", DefaultValue = "Hour")]
        public TimeFrame HtfTimeframe { get; set; }

        // ─── Session ────────────────────────────────────────
        [Parameter("Trade Asia", Group = "Session", DefaultValue = true)]
        public bool TradeAsia { get; set; }

        [Parameter("Trade London", Group = "Session", DefaultValue = true)]
        public bool TradeLondon { get; set; }

        [Parameter("Trade New York", Group = "Session", DefaultValue = true)]
        public bool TradeNewYork { get; set; }

        [Parameter("Trade Overlap (Lon-NY)", Group = "Session", DefaultValue = true)]
        public bool TradeOverlap { get; set; }

        // ─── News ───────────────────────────────────────────
        [Parameter("Enable News Filter", Group = "News", DefaultValue = false)]
        public bool EnableNewsFilter { get; set; }

        [Parameter("News Blackout (min)", Group = "News", DefaultValue = 30, MinValue = 0)]
        public int NewsBlackoutMinutes { get; set; }

        [Parameter("News Schedule UTC", Group = "News", DefaultValue = "")]
        public string NewsSchedule { get; set; }

        // ─── Internals ──────────────────────────────────────
        private CLogger _logger;
        private CVolumeProfile _volumeProfile;
        private CTickDeltaEngine _deltaEngine;
        private CRiskManager _riskManager;
        private CSessionFilter _sessionFilter;
        private CNewsFilter _newsFilter;
        private CMarketCondition _marketCondition;
        private CTrailingManager _trailingManager;
        private SignalEngine _signalEngine;
        private PocMigrationTracker _pocTracker;
        private AverageTrueRange _atr;
        private AverageTrueRange _atrFast;
        private AverageTrueRange _atrSlow;
        private Bars _htfBars;

        private ProfileData _structProfile;
        private ProfileData _rollProfile;
        private int _tradesToday;
        private int _tradeDayKey;
        private DateTime _lastSignalBarTime;
        private int _rejectCountToday;
        private string _lastRejectReason;
        private int _lastWarmBarIndex = -1;

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("PmLh", DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);

            _volumeProfile = new CVolumeProfile();
            _volumeProfile.Init(Bars, Chart, 100, VisualizeProfile, _logger);
            _volumeProfile.ConfigureComposite(
                BinSize,
                ProfileLookbackDays,
                ValueAreaPercent / 100.0,
                LvnThreshold,
                HvnThreshold,
                WeightDecay,
                1.25,
                1.25,
                MaxLvnWidth,
                true);

            _pocTracker = new PocMigrationTracker();
            _pocTracker.Configure(
                PocWindowBars,
                MigrateLookbackBars,
                MinMigrationM,
                MinPocMoveBins,
                BinSize,
                UseStreakFilter,
                StreakNeed,
                StreakOf,
                StrongMBypass);

            _deltaEngine = new CTickDeltaEngine();
            _deltaEngine.Init(50000, _logger);

            _riskManager = new CRiskManager();
            _riskManager.Init(this, Symbol, BotLabel, _logger);
            _riskManager.SetEquityProtection(MaxEquityDrawdownPct, FlattenOnEquityDd);
            _riskManager.SetDailyLimits(MaxDailyLossAmount, MaxDailyProfitAmount, FlattenOnDailyLoss, FlattenOnDailyProfit);

            _sessionFilter = new CSessionFilter();
            _sessionFilter.Init(TradeAsia, TradeLondon, TradeNewYork, TradeOverlap, _logger);

            if (!TradeAsia && !TradeLondon && !TradeNewYork && !TradeOverlap)
                _logger.Warn("No session enabled — bot will never enter");

            _newsFilter = new CNewsFilter();
            _newsFilter.Init(EnableNewsFilter, NewsBlackoutMinutes, _logger);
            if (!string.IsNullOrWhiteSpace(NewsSchedule))
                _newsFilter.LoadFromString(NewsSchedule);

            _marketCondition = new CMarketCondition();
            _marketCondition.Init(Symbol, _logger);
            _marketCondition.SetSpreadCheck(true, MaxSpreadPips);

            _trailingManager = new CTrailingManager();
            _trailingManager.Init(this, Symbol, BotLabel, _logger);

            _signalEngine = new SignalEngine();
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);
            _atrFast = Indicators.AverageTrueRange(ExpandAtrFastPeriod, MovingAverageType.Simple);
            _atrSlow = Indicators.AverageTrueRange(ExpandAtrSlowPeriod, MovingAverageType.Simple);
            _htfBars = MarketData.GetBars(HtfTimeframe, SymbolName);

            _tradesToday = 0;
            _tradeDayKey = -1;
            _lastSignalBarTime = DateTime.MinValue;
            _rejectCountToday = 0;
            _lastRejectReason = null;

            Positions.Closed += OnPositionClosed;
            RecoverOpenPositionState();

            WarmPocHistory();
            _structProfile = _volumeProfile.BuildComposite(Server.TimeInUtc);

            _logger.Info(
                $"Started {SymbolName} TF={TimeFrame} risk={RiskPercent}% RR={RrMultiple} " +
                $"mode={EntryMode} lvnSrc={LvnSource} N={PocWindowBars} K={MigrateLookbackBars} Mmin={MinMigrationM} " +
                $"BE={UseBreakEven} Trail={UseTrailing} sessions={_sessionFilter.DescribeEnabled()} " +
                $"LVN={_structProfile?.Lvns?.Count ?? 0} warm={_pocTracker.IsWarm} debug={DebugLogging}");
        }

        private void WarmPocHistory()
        {
            int lastClosed = Bars.Count - 2;
            if (lastClosed < 2) return;

            int need = Math.Max(PocWindowBars, MigrateLookbackBars + StreakOf) + PocWindowBars + 2;
            int start = Math.Max(PocWindowBars, lastClosed - need + 1);

            for (int bi = start; bi <= lastClosed; bi++)
            {
                var roll = BuildRollingProfile(bi);
                if (roll == null || !roll.IsValid) continue;
                double atr = AtrAt(bi);
                _pocTracker.Push(roll.POC, atr);
                _lastWarmBarIndex = bi;
            }
        }

        private ProfileData BuildRollingProfile(int endBarIndex)
        {
            int n = Math.Max(2, PocWindowBars);
            int first = Math.Max(0, endBarIndex - n + 1);
            if (endBarIndex < first || endBarIndex > Bars.Count - 2)
                return new ProfileData { IsValid = false };

            DateTime startUtc = Bars.OpenTimes[first];
            DateTime endUtc = endBarIndex + 1 < Bars.Count
                ? Bars.OpenTimes[endBarIndex + 1]
                : Bars.OpenTimes[endBarIndex].AddMinutes(1);

            if (endUtc <= startUtc)
                endUtc = startUtc.AddMinutes(1);

            return _volumeProfile.BuildRange(startUtc, endUtc, updateLastProfile: false, draw: false);
        }

        private double AtrAt(int barIndex)
        {
            if (_atr == null || _atr.Result.Count == 0)
                return Symbol.PipSize * 50;
            int idx = Math.Min(barIndex, _atr.Result.Count - 1);
            if (idx < 0) return Symbol.PipSize * 50;
            double v = _atr.Result[idx];
            return v > 0 ? v : Symbol.PipSize * 50;
        }

        private void RecoverOpenPositionState()
        {
            var pos = Positions.FindAll(BotLabel, SymbolName).FirstOrDefault();
            if (pos == null) return;

            double sl = pos.StopLoss ?? 0;
            double slDist = sl > 0 ? Math.Abs(pos.EntryPrice - sl) : 0;
            if (slDist <= 0 && pos.TakeProfit.HasValue)
                slDist = Math.Abs(pos.TakeProfit.Value - pos.EntryPrice) / Math.Max(0.5, RrMultiple);

            if (slDist > 0 && (UseBreakEven || UseTrailing))
            {
                ConfigureExitsForTrade(slDist);
                _logger.Info($"Recovered BE/Trail for open #{pos.Id} slDist={slDist:F1}");
            }
        }

        private void ConfigureExitsForTrade(double slDistPrice)
        {
            if (slDistPrice <= 0) return;

            if (UseBreakEven)
            {
                double startPips = PriceUtils.PriceToPips(slDistPrice * BeStartR, Symbol);
                double lockPips = PriceUtils.PriceToPips(slDistPrice * Math.Max(0, BeLockR), Symbol);
                _trailingManager.SetBreakevenPoints(startPips, lockPips, BeAddSpread);
            }

            if (UseTrailing)
            {
                double startPips = PriceUtils.PriceToPips(slDistPrice * TrailStartR, Symbol);
                double stepPips = PriceUtils.PriceToPips(slDistPrice * TrailStepR, Symbol);
                double sensPips = Math.Max(1.0, stepPips * 0.25);
                _trailingManager.SetTrailPoints(startPips, stepPips, sensPips);
            }
        }

        protected override void OnTick()
        {
            _deltaEngine.OnTick(Symbol.Bid, Symbol.Ask, Server.TimeInUtc);
            _riskManager.OnTick();

            if (UseBreakEven || UseTrailing)
                _trailingManager.OnTick();
        }

        protected override void OnBar()
        {
            _structProfile = _volumeProfile.BuildComposite(Server.TimeInUtc);
            ResetDailyCounters();

            if (Bars.Count < Math.Max(10, PocWindowBars + MigrateLookbackBars + 2))
                return;
            if (_htfBars == null || _htfBars.Count < 3)
                return;

            int bi = Bars.Count - 2;
            DateTime barTime = Bars.OpenTimes[bi];
            if (barTime <= _lastSignalBarTime)
                return;
            _lastSignalBarTime = barTime;

            // Advance POC tracker for new bars; reuse last BuildRollingProfile for signal LVN
            ProfileData currentRoll = null;
            if (bi > _lastWarmBarIndex)
            {
                for (int i = _lastWarmBarIndex + 1; i <= bi; i++)
                {
                    if (i < 0) continue;
                    var rollStep = BuildRollingProfile(i);
                    if (rollStep == null || !rollStep.IsValid) continue;
                    _pocTracker.Push(rollStep.POC, AtrAt(i));
                    _lastWarmBarIndex = i;
                    if (i == bi)
                        currentRoll = rollStep;
                }
            }

            _rollProfile = currentRoll ?? BuildRollingProfile(bi);
            double atr = AtrAt(bi);
            double atrFast = _atrFast != null && _atrFast.Result.Count > 1
                ? _atrFast.Result.Last(1) : atr;
            double atrSlow = _atrSlow != null && _atrSlow.Result.Count > 1
                ? _atrSlow.Result.Last(1) : atr;

            double htfClose = _htfBars.ClosePrices.Last(1);
            // Proxy HTF POC = structure composite POC (PRD impl note)
            double htfPoc = _structProfile != null && _structProfile.IsValid
                ? _structProfile.POC
                : 0;

            int histLen = Math.Max(PriorBreakBars, MaxDwellBars) + 4;
            BuildHist(bi, histLen, out double[] hO, out double[] hH, out double[] hL, out double[] hC);

            var ctx = new SignalContext
            {
                StructProfile = _structProfile,
                RollProfile = _rollProfile,
                LvnSource = LvnSource,
                BarHigh = Bars.HighPrices[bi],
                BarLow = Bars.LowPrices[bi],
                BarOpen = Bars.OpenPrices[bi],
                BarClose = Bars.ClosePrices[bi],
                HtfClose = htfClose,
                HtfPoc = htfPoc,
                Atr = atr,
                MigrationWarm = _pocTracker.IsWarm,
                MigrationM = _pocTracker.M,
                MigrationDelta = _pocTracker.Delta,
                MigrationDirection = _pocTracker.Direction,
                MigrationFailCode = _pocTracker.FailCode,
                PocNow = _pocTracker.PocNow,
                MaxPricePocAtr = MaxPricePocAtr,
                RequireLvnSide = RequireLvnSide,
                BuyImbalance = _deltaEngine.GetImbalance(DeltaWindowMs, MinDeltaTicks),
                SellImbalance = _deltaEngine.GetSellImbalance(DeltaWindowMs, MinDeltaTicks),
                DeltaTickCount = _deltaEngine.GetTickCount(DeltaWindowMs),
                MinDeltaTicks = MinDeltaTicks,
                SessionOk = _sessionFilter.IsTradingAllowed(Server.TimeInUtc),
                NewsOk = _newsFilter.IsTradingAllowed(Server.TimeInUtc),
                SpreadOk = _marketCondition.IsTradingOK(),
                EquityOk = _riskManager.IsTradingAllowed(Account.Equity, Server.TimeInUtc),
                TradesToday = _tradesToday,
                MaxTradesPerDay = MaxTradesPerDay,
                HasOpenPosition = Positions.FindAll(BotLabel, SymbolName).Length > 0,
                MinLvnStrength = MinLvnStrength,
                MaxLvnWidth = MaxLvnWidth,
                TopNLvn = TopNLvn,
                TouchBuffer = atr * Math.Max(0, TouchBufferAtrMult),
                MinDeltaStrength = MinDeltaStrength,
                EntryMode = EntryMode,
                PriorBreakBars = PriorBreakBars,
                MaxDwellBars = MaxDwellBars,
                PreferFullClear = PreferFullClear,
                BodyAtrMult = BodyAtrMult,
                HistOpen = hO,
                HistHigh = hH,
                HistLow = hL,
                HistClose = hC,
                RequireDeltaFilter = RequireDeltaFilter,
                RequireShapeFilter = RequireShapeFilter,
                RequireHtfFilter = RequireHtfFilter,
                RequireExpandFilter = RequireExpandFilter,
                BlockNeutralShape = BlockNeutralShape,
                BlockDShape = BlockDShape,
                ExpandAtrMult = ExpandAtrMult,
                ExpandRatio = ExpandRatio,
                AtrFast = atrFast,
                AtrSlow = atrSlow,
                LvnBufferAtrMult = Math.Max(0, SlAtrMult),
                MinSlAtrMult = MinSlAtrMult,
                MaxSlAtrMult = MaxSlAtrMult,
                EntryPriceEstimate = Bars.ClosePrices[bi]
            };

            var result = _signalEngine.Evaluate(ctx);
            if (!result.IsValid)
            {
                LogReject(result, ctx);
                return;
            }

            _logger.Info(
                $"{result.Reason} M={result.MigrationM:F2} Δ={_pocTracker.Delta:F2} poc={_pocTracker.PocNow:F1} " +
                $"N={PocWindowBars} K={MigrateLookbackBars} lvnSrc={LvnSource} " +
                $"LVN=[{result.Lvn.Low:F1}-{result.Lvn.High:F1}] str={result.Lvn.Strength:F2} " +
                $"dwell={result.DwellBars} mode={EntryMode} imb={result.Imbalance:F2}");

            if (!EnableTrading)
            {
                _logger.Info("Dry-run (EnableTrading=false) — order skipped");
                return;
            }

            ExecuteSignal(result);
        }

        private void BuildHist(int bi, int len,
            out double[] o, out double[] h, out double[] l, out double[] c)
        {
            int n = Math.Min(len, bi + 1);
            int start = bi - n + 1;
            o = new double[n];
            h = new double[n];
            l = new double[n];
            c = new double[n];
            for (int i = 0; i < n; i++)
            {
                int idx = start + i;
                o[i] = Bars.OpenPrices[idx];
                h[i] = Bars.HighPrices[idx];
                l[i] = Bars.LowPrices[idx];
                c[i] = Bars.ClosePrices[idx];
            }
        }

        private void ExecuteSignal(SignalResult signal)
        {
            double atr = _atr != null && _atr.Result.Count > 1 ? _atr.Result.Last(1) : Symbol.PipSize * 50;
            double atrBuf = atr * Math.Max(0, SlAtrMult);
            double minSlDist = atr * Math.Max(0.1, MinSlAtrMult);

            TradeType tradeType;
            double entry;
            double sl;

            if (signal.Side == SignalSide.Long)
            {
                tradeType = TradeType.Buy;
                entry = Symbol.Ask;
                sl = Math.Min(signal.Lvn.Low - atrBuf, entry - minSlDist);
                if (sl >= entry)
                {
                    _logger.Warn($"Execute aborted: invalid long SL={sl} entry={entry}");
                    return;
                }
            }
            else
            {
                tradeType = TradeType.Sell;
                entry = Symbol.Bid;
                sl = Math.Max(signal.Lvn.High + atrBuf, entry + minSlDist);
                if (sl <= entry)
                {
                    _logger.Warn($"Execute aborted: invalid short SL={sl} entry={entry}");
                    return;
                }
            }

            sl = PriceUtils.NormalizePrice(sl, Symbol);
            double slDist = Math.Abs(entry - sl);
            if (slDist < minSlDist * 0.5)
            {
                _logger.Warn($"Execute aborted: SL too tight slDist={slDist:F2}");
                return;
            }

            if (MaxSlAtrMult > 0 && slDist > atr * MaxSlAtrMult)
            {
                _logger.Warn($"Execute aborted: SL too wide slDist={slDist:F1} max={atr * MaxSlAtrMult:F1}");
                return;
            }

            double tpDist = slDist * Math.Max(0.5, RrMultiple);
            double tp = signal.Side == SignalSide.Long ? entry + tpDist : entry - tpDist;
            tp = PriceUtils.NormalizePrice(tp, Symbol);

            double dailyPnl = _riskManager.GetDailyPnlMoney(Account.Equity);
            double dailyRoom = _riskManager.GetRemainingDailyLossBudget(Account.Equity);

            if (_riskManager.IsDailyLossLimitEnabled && dailyRoom <= 1.0)
            {
                _logger.Warn($"Execute aborted: daily loss room exhausted (dailyPnl=${dailyPnl:F0}, room=${dailyRoom:F0})");
                return;
            }

            if (!TryComputeVolume(slDist, dailyRoom, dailyPnl, out double volume, out double expectedRisk, out string sizeNote))
            {
                _logger.Warn($"Execute aborted: volume sizing failed — {sizeNote}");
                return;
            }

            if (_riskManager.IsDailyLossLimitEnabled && expectedRisk > dailyRoom * 1.05)
            {
                _logger.Error($"Execute SAFETY ABORT: expected risk ${expectedRisk:F0} > daily room ${dailyRoom:F0}");
                return;
            }

            double riskPctActual = Account.Balance > 0 ? expectedRisk / Account.Balance * 100.0 : 0;

            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, sl, tp, null, false);
            if (!result.IsSuccessful || result.Position == null)
            {
                _logger.Error($"Order failed: {result.Error}");
                return;
            }

            ConfigureExitsForTrade(slDist);
            _tradesToday++;

            _logger.Info(
                $"OPEN {tradeType} #{result.Position.Id} vol={volume} SL={sl:F1} TP={tp:F1} " +
                $"slDist={slDist:F1} RR={RrMultiple:F2} M={signal.MigrationM:F2} " +
                $"Δ={_pocTracker.Delta:F2} poc={_pocTracker.PocNow:F1} " +
                $"N={PocWindowBars} K={MigrateLookbackBars} lvnSrc={LvnSource} " +
                $"LVN=[{signal.Lvn.Low:F1}-{signal.Lvn.High:F1}] mode={EntryMode} size={sizeNote} " +
                $"BE={(UseBreakEven ? $"{BeStartR:F2}R" : "off")} " +
                $"Trail={(UseTrailing ? $"{TrailStartR:F2}R/{TrailStepR:F2}R" : "off")} " +
                $"risk=${expectedRisk:F0} ({riskPctActual:F2}%) dailyPnl=${dailyPnl:F0}");
        }

        private bool TryComputeVolume(double slDist, double dailyRoom, double dailyPnl,
            out double volume, out double expectedRisk, out string sizeNote)
        {
            volume = 0;
            expectedRisk = 0;
            sizeNote = "";

            if (SizeMode == LotSizeMode.FixedLots)
            {
                volume = _riskManager.CalculateVolume(FixedLots);
                if (volume <= 0)
                {
                    sizeNote = $"FixedLots={FixedLots} → vol=0";
                    return false;
                }

                expectedRisk = volume * slDist;
                if (_riskManager.IsDailyLossLimitEnabled && expectedRisk > dailyRoom)
                {
                    double scale = (dailyRoom * 0.98) / expectedRisk;
                    double reduced = _riskManager.CalculateVolume(FixedLots * scale);
                    if (reduced <= 0)
                    {
                        sizeNote = $"FixedLots={FixedLots} exceeds daily room ${dailyRoom:F0}";
                        return false;
                    }
                    volume = reduced;
                    expectedRisk = volume * slDist;
                    sizeNote = $"FixedLots~{volume / Math.Max(1e-9, Symbol.LotSize):F2}";
                }
                else
                {
                    sizeNote = $"FixedLots={FixedLots}";
                }
                return true;
            }

            double riskMoney = Account.Balance * (RiskPercent / 100.0);
            if (_riskManager.IsDailyLossLimitEnabled && riskMoney > dailyRoom)
            {
                _logger.Info($"Risk capped by daily room: ${riskMoney:F0} → ${dailyRoom:F0}");
                riskMoney = dailyRoom * 0.98;
            }

            volume = _riskManager.CalculateVolumeFromRiskMoney(riskMoney, slDist, out expectedRisk);
            if (volume <= 0)
            {
                sizeNote = $"RiskPercent={RiskPercent}% money=${riskMoney:F0}";
                return false;
            }

            sizeNote = $"RiskPercent={RiskPercent}%";
            return true;
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args?.Position == null) return;
            if (args.Position.Label != BotLabel || args.Position.SymbolName != SymbolName) return;

            string xReason = MapCloseReason(args.Reason);
            _logger.Info(
                $"CLOSE #{args.Position.Id} net={args.Position.NetProfit:F2} {args.Reason} ({xReason}) " +
                $"eqDailyPnl=${_riskManager.GetDailyPnlMoney(Account.Equity):F0}");
        }

        private static string MapCloseReason(PositionCloseReason reason)
        {
            // Best-effort journal codes per PRD
            string s = reason.ToString();
            if (s.IndexOf("Stop", StringComparison.OrdinalIgnoreCase) >= 0)
                return "X_SL_or_TRAIL";
            if (s.IndexOf("Take", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Target", StringComparison.OrdinalIgnoreCase) >= 0)
                return "X_TP";
            if (s.IndexOf("Closed", StringComparison.OrdinalIgnoreCase) >= 0)
                return "X_MANUAL_or_FLATTEN";
            return "X_" + s;
        }

        private void ResetDailyCounters()
        {
            int dayKey = Server.TimeInUtc.Year * 1000 + Server.TimeInUtc.DayOfYear;
            if (dayKey != _tradeDayKey)
            {
                if (_tradeDayKey > 0 && _rejectCountToday > 0)
                    _logger.Info($"Day summary: rejects={_rejectCountToday} last={_lastRejectReason} trades={_tradesToday}");

                _tradeDayKey = dayKey;
                _tradesToday = CountBotEntriesTodayUtc();
                _rejectCountToday = 0;
                _lastRejectReason = null;
            }
        }

        private int CountBotEntriesTodayUtc()
        {
            DateTime dayStart = Server.TimeInUtc.Date;
            int n = 0;
            try
            {
                foreach (var h in History)
                {
                    if (h.Label != BotLabel || h.SymbolName != SymbolName) continue;
                    DateTime entry = h.EntryTime.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(h.EntryTime, DateTimeKind.Utc)
                        : h.EntryTime.ToUniversalTime();
                    if (entry >= dayStart)
                        n++;
                }
            }
            catch
            {
                // History may be limited
            }

            foreach (var p in Positions.FindAll(BotLabel, SymbolName))
            {
                DateTime entry = p.EntryTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(p.EntryTime, DateTimeKind.Utc)
                    : p.EntryTime.ToUniversalTime();
                if (entry >= dayStart)
                    n++;
            }
            return n;
        }

        private void LogReject(SignalResult result, SignalContext ctx)
        {
            if (result?.Reason == null) return;

            if (result.Reason.Contains("F1_SESSION"))
            {
                if (DebugLogging) _logger.Debug(result.Reason);
                return;
            }

            _rejectCountToday++;
            string code = result.Reason;
            if (code.StartsWith("REJECT:", StringComparison.Ordinal))
                code = code.Substring("REJECT:".Length);
            int pipe = code.IndexOf('|');
            if (pipe > 0) code = code.Substring(0, pipe);
            int colon = code.IndexOf(':');
            if (colon > 0) code = code.Substring(0, colon);
            _lastRejectReason = code;

            if (!DebugLogging) return;
            _logger.Debug(
                $"{result.Reason} close={ctx.BarClose:F1} M={ctx.MigrationM:F2} Δ={_pocTracker.Delta:F2} " +
                $"dir={ctx.MigrationDirection} poc={_pocTracker.PocNow:F1} " +
                $"streak=+/−/0={_pocTracker.StreakSame}/{_pocTracker.StreakOpp}/{_pocTracker.StreakFlat} " +
                $"lvns={ctx.StructProfile?.Lvns?.Count ?? 0}");
        }

        protected override void OnStop()
        {
            if (_rejectCountToday > 0)
                _logger?.Info($"Stop summary: rejects={_rejectCountToday} last={_lastRejectReason} tradesToday={_tradesToday}");

            Positions.Closed -= OnPositionClosed;
            _volumeProfile?.ClearVisuals();
            _logger?.Info("Stopped");
        }
    }
}
