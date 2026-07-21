using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using RedWave.Common;

namespace cAlgo.Robots
{
    /// <summary>How the single take-profit price is computed (full position size).</summary>
    public enum TpMode
    {
        RiskReward = 0,
        Structure = 1,
        FixedPrice = 2
    }

    /// <summary>Position size mode.</summary>
    public enum LotSizeMode
    {
        RiskPercent = 0,
        FixedLots = 1
    }

    /// <summary>
    /// HMPD: HVN Magnet Pullback + Delta Confirmation.
    /// Pullback into strong HVN with HTF bias, shape, and tick-delta filters.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class HvnMagnet : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "HvnMagnet")]
        public string BotLabel { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk", DefaultValue = LotSizeMode.RiskPercent)]
        public LotSizeMode SizeMode { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.50, MinValue = 0.1, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Trades / Day", Group = "Trade & Risk", DefaultValue = 3, MinValue = 1, MaxValue = 20)]
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

        // ─── Stop Loss ──────────────────────────────────────
        [Parameter("ATR Period", Group = "Stop Loss", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        /// <summary>Long SL = HVN.Low − ATR×this; Short SL = HVN.High + ATR×this.</summary>
        [Parameter("HVN buffer (×ATR)", Group = "Stop Loss", DefaultValue = 0.5, MinValue = 0.0)]
        public double SlAtrMult { get; set; }

        [Parameter("Min SL distance (×ATR)", Group = "Stop Loss", DefaultValue = 0.8, MinValue = 0.1)]
        public double MinSlAtrMult { get; set; }

        // ─── Take Profit ────────────────────────────────────
        [Parameter("TP Mode", Group = "Take Profit", DefaultValue = TpMode.RiskReward)]
        public TpMode TakeProfitMode { get; set; }

        [Parameter("RR Multiple", Group = "Take Profit", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0)]
        public double RrMultiple { get; set; }

        [Parameter("Fixed TP ($)", Group = "Take Profit", DefaultValue = 20.0, MinValue = 0.5)]
        public double FixedTpPrice { get; set; }

        [Parameter("Min First Target R", Group = "Take Profit", DefaultValue = 1.0, MinValue = 0.0, MaxValue = 5.0)]
        public double MinFirstTargetR { get; set; }

        // ─── Break-even ─────────────────────────────────────
        [Parameter("Use Break Even", Group = "Break Even", DefaultValue = true)]
        public bool UseBreakEven { get; set; }

        [Parameter("BE Start (R)", Group = "Break Even", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0)]
        public double BeStartR { get; set; }

        [Parameter("BE Lock (R)", Group = "Break Even", DefaultValue = 0.05, MinValue = 0.0, MaxValue = 2.0)]
        public double BeLockR { get; set; }

        [Parameter("BE Add Spread", Group = "Break Even", DefaultValue = true)]
        public bool BeAddSpread { get; set; }

        // ─── Trailing ───────────────────────────────────────
        [Parameter("Use Trailing", Group = "Trailing", DefaultValue = false)]
        public bool UseTrailing { get; set; }

        [Parameter("Trail Start (R)", Group = "Trailing", DefaultValue = 1.5, MinValue = 0.1, MaxValue = 20.0)]
        public double TrailStartR { get; set; }

        [Parameter("Trail Step (R)", Group = "Trailing", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 5.0)]
        public double TrailStepR { get; set; }

        // ─── Volume Profile ─────────────────────────────────
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

        [Parameter("Min HVN Strength", Group = "Volume Profile", DefaultValue = 1.25, MinValue = 0.0)]
        public double MinHvnStrength { get; set; }

        [Parameter("Max HVN Width ($)", Group = "Volume Profile", DefaultValue = 15.0, MinValue = 1.0)]
        public double MaxHvnWidth { get; set; }

        [Parameter("Top N HVN", Group = "Volume Profile", DefaultValue = 3, MinValue = 1, MaxValue = 10)]
        public int TopNHvn { get; set; }

        [Parameter("Visualize Profile", Group = "Volume Profile", DefaultValue = false)]
        public bool VisualizeProfile { get; set; }

        // ─── Signal filters ─────────────────────────────────
        [Parameter("Min Delta Strength", Group = "Signal Filters", DefaultValue = 1.2, MinValue = 1.0)]
        public double MinDeltaStrength { get; set; }

        [Parameter("Min Delta Ticks", Group = "Signal Filters", DefaultValue = 15, MinValue = 5)]
        public int MinDeltaTicks { get; set; }

        [Parameter("Delta Window (ms)", Group = "Signal Filters", DefaultValue = 300000, MinValue = 10000)]
        public int DeltaWindowMs { get; set; }

        [Parameter("Rejection Wick/Body", Group = "Signal Filters", DefaultValue = 0.35, MinValue = 0.1)]
        public double RejectionWickBodyRatio { get; set; }

        [Parameter("Require Delta Filter", Group = "Signal Filters", DefaultValue = true)]
        public bool RequireDeltaFilter { get; set; }

        [Parameter("Require Shape Filter", Group = "Signal Filters", DefaultValue = true)]
        public bool RequireShapeFilter { get; set; }

        [Parameter("Require HTF Filter", Group = "Signal Filters", DefaultValue = true)]
        public bool RequireHtfFilter { get; set; }

        [Parameter("Block Neutral Shape", Group = "Signal Filters", DefaultValue = true)]
        public bool BlockNeutralShape { get; set; }

        [Parameter("Require HVN POC Side", Group = "Signal Filters", DefaultValue = false)]
        public bool RequireHvnPocSide { get; set; }

        [Parameter("Allow POC/VA Targets", Group = "Signal Filters", DefaultValue = true)]
        public bool AllowPocVaTargets { get; set; }

        [Parameter("Touch Buffer ATR Mult", Group = "Signal Filters", DefaultValue = 0.15, MinValue = 0.0)]
        public double TouchBufferAtrMult { get; set; }

        [Parameter("HTF Timeframe", Group = "Signal Filters", DefaultValue = "Hour")]
        public TimeFrame HtfTimeframe { get; set; }

        /// <summary>Long: require close still ≥ HVN.Low−buffer (reject pierce-through bars). Default off.</summary>
        [Parameter("Require Close In Zone", Group = "Signal Filters", DefaultValue = false)]
        public bool RequireCloseInZone { get; set; }

        /// <summary>M1: flatten if closed bar fails through entry HVN (default off).</summary>
        [Parameter("Use Failed Acceptance Exit", Group = "Management", DefaultValue = false)]
        public bool UseFailedAcceptanceExit { get; set; }

        /// <summary>M2: 0 = off. Flatten if bars in trade ≥ N and profit &lt; 0.3R.</summary>
        [Parameter("Max Bars In Trade", Group = "Management", DefaultValue = 0, MinValue = 0)]
        public int MaxBarsInTrade { get; set; }

        // ─── Session ────────────────────────────────────────
        [Parameter("Trade Asia", Group = "Session", DefaultValue = false)]
        public bool TradeAsia { get; set; }

        [Parameter("Trade London", Group = "Session", DefaultValue = true)]
        public bool TradeLondon { get; set; }

        [Parameter("Trade New York", Group = "Session", DefaultValue = true)]
        public bool TradeNewYork { get; set; }

        [Parameter("Trade Overlap (Lon-NY)", Group = "Session", DefaultValue = false)]
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
        private AverageTrueRange _atr;
        private Bars _htfBars;

        private ProfileData _profile;
        private int _tradesToday;
        private int _tradeDayKey;
        private DateTime _lastSignalBarTime;
        private int _rejectCountToday;
        private string _lastRejectReason;

        // Active trade management (M1/M2) + recovery after restart
        private VolumeNode _activeHvn;
        private SignalSide _activeSide;
        private double _activeSlDist;
        private DateTime _activeEntryTime;
        private double _activeHvnLow;
        private double _activeHvnHigh;

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("HvnMagnet", DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);

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
                MaxHvnWidth,
                true);

            _deltaEngine = new CTickDeltaEngine();
            _deltaEngine.Init(50000, _logger);

            _riskManager = new CRiskManager();
            _riskManager.Init(this, Symbol, BotLabel, _logger);
            _riskManager.SetEquityProtection(MaxEquityDrawdownPct, FlattenOnEquityDd);
            _riskManager.SetDailyLimits(MaxDailyLossAmount, MaxDailyProfitAmount, FlattenOnDailyLoss, FlattenOnDailyProfit);

            _sessionFilter = new CSessionFilter();
            _sessionFilter.Init(TradeAsia, TradeLondon, TradeNewYork, TradeOverlap, _logger);

            if (!TradeAsia && !TradeLondon && !TradeNewYork && !TradeOverlap)
                _logger.Warn("No session enabled — bot will never enter (enable Asia/London/NY/Overlap)");

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
            _htfBars = MarketData.GetBars(HtfTimeframe, SymbolName);

            _tradeDayKey = Server.TimeInUtc.Year * 1000 + Server.TimeInUtc.DayOfYear;
            _lastSignalBarTime = DateTime.MinValue;
            _rejectCountToday = 0;
            _lastRejectReason = null;
            _activeHvn = null;
            _activeSide = SignalSide.None;
            _activeSlDist = 0;
            _activeEntryTime = DateTime.MinValue;
            _activeHvnLow = 0;
            _activeHvnHigh = 0;

            Positions.Closed += OnPositionClosed;

            _profile = _volumeProfile.BuildComposite(Server.TimeInUtc);

            // Rebuild daily trade count from history + open positions (survives restart)
            _tradesToday = CountBotEntriesTodayUtc();
            RecoverOpenPositionState();

            _logger.Info(
                $"Started {SymbolName} TF={TimeFrame} risk={RiskPercent}% TP={TakeProfitMode} " +
                $"RR={RrMultiple} MinR={MinFirstTargetR} BE={UseBreakEven} Trail={UseTrailing} " +
                $"delta={RequireDeltaFilter} shape={RequireShapeFilter} htf={RequireHtfFilter} " +
                $"sessions={_sessionFilter.DescribeEnabled()} " +
                $"HVN={_profile?.Hvns?.Count ?? 0} lookback={ProfileLookbackDays}d " +
                $"tradesToday={_tradesToday} debug={DebugLogging}");
        }

        /// <summary>
        /// Count bot entries for current UTC day: closed history + still-open positions.
        /// Used so Max Trades/Day survives cBot restart.
        /// </summary>
        private int CountBotEntriesTodayUtc()
        {
            DateTime dayStart = Server.TimeInUtc.Date;
            int n = 0;

            try
            {
                foreach (var h in History)
                {
                    if (h == null) continue;
                    if (!string.Equals(h.Label, BotLabel, StringComparison.Ordinal)) continue;
                    if (!string.Equals(h.SymbolName, SymbolName, StringComparison.Ordinal)) continue;
                    DateTime entry = h.EntryTime.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(h.EntryTime, DateTimeKind.Utc)
                        : h.EntryTime.ToUniversalTime();
                    if (entry >= dayStart)
                        n++;
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"CountBotEntriesTodayUtc History scan failed: {ex.Message}");
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

        /// <summary>
        /// Restore BE/Trail config and M1/M2 anchors if a bot position is already open (restart).
        /// </summary>
        private void RecoverOpenPositionState()
        {
            var pos = Positions.FindAll(BotLabel, SymbolName).FirstOrDefault();
            if (pos == null)
                return;

            _activeSide = pos.TradeType == TradeType.Buy ? SignalSide.Long : SignalSide.Short;
            _activeEntryTime = pos.EntryTime;

            double sl = pos.StopLoss ?? 0;
            double slDist = sl > 0 ? Math.Abs(pos.EntryPrice - sl) : 0;
            if (slDist <= 0 && pos.TakeProfit.HasValue)
            {
                // Fallback: infer ~0.5 of TP distance if RR~2 was used
                slDist = Math.Abs(pos.TakeProfit.Value - pos.EntryPrice) * 0.5;
            }

            _activeSlDist = slDist;

            if (slDist > 0 && (UseBreakEven || UseTrailing))
            {
                ConfigureExitsForTrade(slDist);
                _logger.Info(
                    $"Recovered BE/Trail for open #{pos.Id} side={_activeSide} slDist={slDist:F1}");
            }
            else if (UseBreakEven || UseTrailing)
            {
                _logger.Warn($"Open #{pos.Id} has no usable SL for BE/Trail recovery");
            }

            // Approximate entry HVN from current profile (best-effort after restart)
            if (_profile != null && _profile.IsValid)
            {
                VolumeNode hvn = _activeSide == SignalSide.Long
                    ? _profile.FindNearestHvnBelow(pos.EntryPrice + 1e-6)
                    : _profile.FindNearestHvnAbove(pos.EntryPrice - 1e-6);
                if (hvn == null)
                    hvn = _profile.FindNearestHvnBelow(pos.EntryPrice) ?? _profile.FindNearestHvnAbove(pos.EntryPrice);
                if (hvn != null)
                {
                    _activeHvn = hvn;
                    _activeHvnLow = hvn.Low;
                    _activeHvnHigh = hvn.High;
                    _logger.Info(
                        $"Recovered active HVN≈[{hvn.Low:F1}-{hvn.High:F1}] for M1 (approx after restart)");
                }
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
            _profile = _volumeProfile.BuildComposite(Server.TimeInUtc);
            ResetDailyCounters();

            if (Bars.Count < 5 || _htfBars == null || _htfBars.Count < 3)
                return;

            int bi = Bars.Count - 2;
            DateTime barTime = Bars.OpenTimes[bi];
            if (barTime <= _lastSignalBarTime)
                return;
            _lastSignalBarTime = barTime;

            double atr = _atr != null && _atr.Result.Count > 1 ? _atr.Result.Last(1) : Symbol.PipSize * 50;
            double touchBuf = atr * Math.Max(0, TouchBufferAtrMult);

            // M1/M2 management on closed bar (before new entries)
            ManageOpenPositionOnBar(bi, touchBuf, atr);

            double htfClose = _htfBars.ClosePrices.Last(1);
            double estSl = atr * Math.Max(0.1, MinSlAtrMult);

            var ctx = new SignalContext
            {
                Profile = _profile,
                BarHigh = Bars.HighPrices[bi],
                BarLow = Bars.LowPrices[bi],
                BarOpen = Bars.OpenPrices[bi],
                BarClose = Bars.ClosePrices[bi],
                HtfClose = htfClose,
                Atr = atr,
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
                MinHvnStrength = MinHvnStrength,
                MaxHvnWidth = MaxHvnWidth,
                TopNHvn = TopNHvn,
                MinDeltaStrength = MinDeltaStrength,
                RejectionWickBodyRatio = RejectionWickBodyRatio,
                RequireShapeFilter = RequireShapeFilter,
                RequireDeltaFilter = RequireDeltaFilter,
                RequireHtfFilter = RequireHtfFilter,
                BlockNeutralShape = BlockNeutralShape,
                RequireHvnPocSide = RequireHvnPocSide,
                AllowPocVaTargets = AllowPocVaTargets,
                TouchBuffer = touchBuf,
                MinFirstTargetR = MinFirstTargetR,
                EstimatedSlDistance = estSl,
                ApplyStructureMinR = TakeProfitMode == TpMode.Structure,
                RequireCloseInZone = RequireCloseInZone
            };

            var result = _signalEngine.Evaluate(ctx);
            if (!result.IsValid)
            {
                LogReject(result, ctx);
                return;
            }

            _logger.Info(
                $"{result.Reason} HVN=[{result.Hvn.Low:F1}-{result.Hvn.High:F1}] str={result.Hvn.Strength:F2} " +
                $"tp={result.TargetLabel}:{result.TargetPrice:F1} imb={result.Imbalance:F2} shape={result.Shape}");

            if (!EnableTrading)
            {
                _logger.Info("Dry-run (EnableTrading=false) — order skipped");
                return;
            }

            ExecuteSignal(result);
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
                sl = Math.Min(signal.Hvn.Low - atrBuf, entry - minSlDist);
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
                sl = Math.Max(signal.Hvn.High + atrBuf, entry + minSlDist);
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

            // Re-check Min R with live SL distance for Structure mode (signal used estimate)
            if (TakeProfitMode == TpMode.Structure && MinFirstTargetR > 0 && signal.TargetPrice > 0)
            {
                double liveR = Math.Abs(signal.TargetPrice - entry) / slDist;
                if (liveR < MinFirstTargetR)
                {
                    _logger.Warn($"Execute aborted: live E7 RR={liveR:F2} < MinFirstTargetR={MinFirstTargetR:F2}");
                    return;
                }
            }

            if (!TryComputeTakeProfit(signal, tradeType, entry, slDist, out double tp, out string tpNote))
            {
                _logger.Warn($"Execute aborted: {tpNote}");
                return;
            }

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
                double minRiskHint = slDist; // ~$1/unit × min vol 1 on typical XAU
                _logger.Warn(
                    $"Execute aborted: volume sizing failed — {sizeNote} slDist={slDist:F1} " +
                    $"(hint: raise balance/risk% so risk$ ≳ {minRiskHint:F0} for ~1 unit @ this SL)");
                return;
            }

            if (_riskManager.IsDailyLossLimitEnabled && expectedRisk > dailyRoom * 1.05)
            {
                _logger.Error($"Execute SAFETY ABORT: expected risk ${expectedRisk:F0} > daily room ${dailyRoom:F0}");
                return;
            }

            double riskPctActual = Account.Balance > 0 ? expectedRisk / Account.Balance * 100.0 : 0;
            double tpDist = Math.Abs(tp - entry);
            double rr = slDist > 0 ? tpDist / slDist : 0;

            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, sl, tp, null, false);
            if (!result.IsSuccessful || result.Position == null)
            {
                _logger.Error($"Order failed: {result.Error}");
                return;
            }

            ConfigureExitsForTrade(slDist);
            _activeHvn = signal.Hvn;
            _activeSide = signal.Side;
            _activeSlDist = slDist;
            _activeEntryTime = result.Position.EntryTime;
            _activeHvnLow = signal.Hvn.Low;
            _activeHvnHigh = signal.Hvn.High;

            _tradesToday++;
            _logger.Info(
                $"OPEN {tradeType} #{result.Position.Id} vol={volume} SL={sl:F1} TP={tp:F1} " +
                $"slDist={slDist:F1} tpDist={tpDist:F1} RR={rr:F2} ({tpNote}) size={sizeNote} " +
                $"hvn=[{signal.Hvn.Low:F1}-{signal.Hvn.High:F1}] str={signal.Hvn.Strength:F2} " +
                $"delta={signal.Imbalance:F2} " +
                $"BE={(UseBreakEven ? $"{BeStartR:F2}R" : "off")} " +
                $"Trail={(UseTrailing ? $"{TrailStartR:F2}R/{TrailStepR:F2}R" : "off")} " +
                $"risk=${expectedRisk:F0} ({riskPctActual:F2}%) dailyPnl=${dailyPnl:F0} dayRoom=${(dailyRoom > 1e12 ? -1 : dailyRoom):F0}");
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
                    _logger.Info(
                        $"Fixed lots scaled for daily room: {FixedLots} → vol {volume:F0}→{reduced:F0} " +
                        $"(dailyPnl=${dailyPnl:F0}, room=${dailyRoom:F0})");
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
                _logger.Info($"Risk capped by daily room: ${riskMoney:F0} → ${dailyRoom:F0} (dailyPnl=${dailyPnl:F0})");
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

        private bool TryComputeTakeProfit(SignalResult signal, TradeType tradeType, double entry, double slDist,
            out double tp, out string note)
        {
            tp = 0;
            note = "";
            bool isBuy = tradeType == TradeType.Buy;

            switch (TakeProfitMode)
            {
                case TpMode.RiskReward:
                {
                    double dist = slDist * Math.Max(0.5, RrMultiple);
                    tp = isBuy ? entry + dist : entry - dist;
                    note = $"RR×{RrMultiple:F1}";
                    return true;
                }
                case TpMode.FixedPrice:
                {
                    double dist = Math.Max(0.5, FixedTpPrice);
                    tp = isBuy ? entry + dist : entry - dist;
                    note = $"Fixed${dist:F1}";
                    return true;
                }
                case TpMode.Structure:
                {
                    double target = signal.TargetPrice > 0
                        ? signal.TargetPrice
                        : (signal.StructureTarget != null ? signal.StructureTarget.Mid : 0);

                    if (target <= 0)
                    {
                        note = "Structure TP: no magnet";
                        return false;
                    }

                    tp = target;
                    if (isBuy && tp <= entry)
                    {
                        note = $"Structure TP {tp:F1} <= entry";
                        return false;
                    }
                    if (!isBuy && tp >= entry)
                    {
                        note = $"Structure TP {tp:F1} >= entry";
                        return false;
                    }

                    double dist = Math.Abs(tp - entry);
                    double minR = Math.Max(0.5, MinFirstTargetR > 0 ? MinFirstTargetR : 0.5);
                    if (dist < slDist * minR)
                    {
                        note = $"Structure TP too close (RR={dist / slDist:F2} < {minR:F2})";
                        return false;
                    }

                    note = $"Structure:{signal.TargetLabel ?? "node"}";
                    return true;
                }
                default:
                    note = "Unknown TP mode";
                    return false;
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args?.Position == null) return;
            if (args.Position.Label != BotLabel || args.Position.SymbolName != SymbolName) return;

            ClearActiveTradeState();

            _logger.Info(
                $"CLOSE #{args.Position.Id} net={args.Position.NetProfit:F2} {args.Reason} " +
                $"eqDailyPnl=${_riskManager.GetDailyPnlMoney(Account.Equity):F0}");
        }

        private void ClearActiveTradeState()
        {
            _activeHvn = null;
            _activeSide = SignalSide.None;
            _activeSlDist = 0;
            _activeEntryTime = DateTime.MinValue;
            _activeHvnLow = 0;
            _activeHvnHigh = 0;
        }

        /// <summary>
        /// M1 failed acceptance + M2 max bars (closed bar only). Defaults off.
        /// </summary>
        private void ManageOpenPositionOnBar(int closedBarIndex, double touchBuffer, double atr)
        {
            var pos = Positions.FindAll(BotLabel, SymbolName).FirstOrDefault();
            if (pos == null)
            {
                if (_activeSide != SignalSide.None)
                    ClearActiveTradeState();
                return;
            }

            // Keep side/entry synced if we missed OPEN path (restart already set)
            if (_activeSide == SignalSide.None)
            {
                _activeSide = pos.TradeType == TradeType.Buy ? SignalSide.Long : SignalSide.Short;
                _activeEntryTime = pos.EntryTime;
                if (pos.StopLoss.HasValue)
                    _activeSlDist = Math.Abs(pos.EntryPrice - pos.StopLoss.Value);
            }

            double barClose = Bars.ClosePrices[closedBarIndex];
            double hvnLow = _activeHvn != null ? _activeHvn.Low : _activeHvnLow;
            double hvnHigh = _activeHvn != null ? _activeHvn.High : _activeHvnHigh;

            // M1 — failed acceptance through HVN
            if (UseFailedAcceptanceExit && hvnLow > 0 && hvnHigh > 0)
            {
                bool failLong = _activeSide == SignalSide.Long && barClose < hvnLow - touchBuffer;
                bool failShort = _activeSide == SignalSide.Short && barClose > hvnHigh + touchBuffer;
                if (failLong || failShort)
                {
                    _logger.Info(
                        $"M1 failed acceptance: close={barClose:F1} hvn=[{hvnLow:F1}-{hvnHigh:F1}] — flatten #{pos.Id}");
                    ClosePosition(pos);
                    ClearActiveTradeState();
                    return;
                }
            }

            // M2 — time stop if underwater &lt; 0.3R
            if (MaxBarsInTrade > 0 && _activeEntryTime != DateTime.MinValue)
            {
                int barsIn = CountBarsSince(_activeEntryTime, closedBarIndex);
                if (barsIn >= MaxBarsInTrade)
                {
                    double slDist = _activeSlDist;
                    if (slDist <= 0 && pos.StopLoss.HasValue)
                        slDist = Math.Abs(pos.EntryPrice - pos.StopLoss.Value);
                    if (slDist <= 0)
                        slDist = atr * Math.Max(0.1, MinSlAtrMult);

                    double profitPrice = pos.TradeType == TradeType.Buy
                        ? barClose - pos.EntryPrice
                        : pos.EntryPrice - barClose;
                    double rMultiple = slDist > 1e-9 ? profitPrice / slDist : 0;

                    if (rMultiple < 0.3)
                    {
                        _logger.Info(
                            $"M2 max bars: bars={barsIn} R={rMultiple:F2} < 0.3 — flatten #{pos.Id}");
                        ClosePosition(pos);
                        ClearActiveTradeState();
                    }
                }
            }
        }

        private int CountBarsSince(DateTime entryTime, int upToBarIndex)
        {
            int n = 0;
            for (int i = upToBarIndex; i >= 0; i--)
            {
                if (Bars.OpenTimes[i] < entryTime)
                    break;
                n++;
            }
            return n;
        }

        private void ResetDailyCounters()
        {
            int dayKey = Server.TimeInUtc.Year * 1000 + Server.TimeInUtc.DayOfYear;
            if (dayKey != _tradeDayKey)
            {
                if (_tradeDayKey > 0 && _rejectCountToday > 0)
                    _logger.Info($"Day summary: rejects={_rejectCountToday} last={_lastRejectReason} trades={_tradesToday}");

                _tradeDayKey = dayKey;
                // Prefer history rebuild over blind zero (handles restart mid-day already done in OnStart;
                // on day rollover open positions from prior day should not count as "today")
                _tradesToday = CountBotEntriesTodayUtc();
                _rejectCountToday = 0;
                _lastRejectReason = null;
            }
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
                $"{result.Reason} close={ctx.BarClose:F1} hvns={ctx.Profile?.Hvns?.Count ?? 0} " +
                $"htf={ctx.HtfClose:F1} poc={ctx.Profile?.POC:F1} shape={ctx.Profile?.Shape}");
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
