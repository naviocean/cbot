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
        /// <summary>TP = entry ± SL_distance × RR Multiple (default).</summary>
        RiskReward = 0,
        /// <summary>TP = single structure magnet (HVN / POC / VA).</summary>
        Structure = 1,
        /// <summary>TP = entry ± Fixed TP ($) price distance.</summary>
        FixedPrice = 2
    }

    /// <summary>Position size mode.</summary>
    public enum LotSizeMode
    {
        /// <summary>Volume from Risk % of balance and SL distance (default).</summary>
        RiskPercent = 0,
        /// <summary>Volume from fixed cash risk amount ($) and SL distance.</summary>
        RiskAmount = 1,
        /// <summary>Fixed lots (broker lot → volume units via LotSize).</summary>
        FixedLots = 2
    }

    /// <summary>Volume Profile lookback calculation mode.</summary>
    public enum VpLookbackMode
    {
        /// <summary>Composite over recent N daily sessions (default).</summary>
        Daily = 0,
        /// <summary>Rolling intraday window over last N hours.</summary>
        RollingHours = 1
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class VacuumHunter : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "VacuumHunter")]
        public string BotLabel { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk", DefaultValue = LotSizeMode.RiskPercent)]
        public LotSizeMode SizeMode { get; set; }

        /// <summary>Used when Lot Size Mode = RiskPercent.</summary>
        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.75, MinValue = 0.1, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        /// <summary>Used when Lot Size Mode = RiskAmount ($ cash risk per trade).</summary>
        [Parameter("Risk Amount ($)", Group = "Trade & Risk", DefaultValue = 50.0, MinValue = 1.0)]
        public double RiskAmount { get; set; }

        /// <summary>Used when Lot Size Mode = FixedLots (e.g. 0.01 standard lots).</summary>
        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Trades / Day", Group = "Trade & Risk", DefaultValue = 2, MinValue = 1, MaxValue = 20)]
        public int MaxTradesPerDay { get; set; }

        [Parameter("Max Spread (pips)", Group = "Trade & Risk", DefaultValue = 80.0, MinValue = 0.1)]
        public double MaxSpreadPips { get; set; }

        /// <summary>Peak equity drawdown % (high-water mark). 0 = off. Always blocks new entries when hit.</summary>
        [Parameter("Max Equity DD %", Group = "Trade & Risk", DefaultValue = 10.0, MinValue = 0.0)]
        public double MaxEquityDrawdownPct { get; set; }

        /// <summary>If true, also market-close all bot positions when peak equity DD is hit.</summary>
        [Parameter("Flatten On Equity DD", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnEquityDd { get; set; }

        /// <summary>
        /// Max loss today in account currency (e.g. USD) vs equity at UTC day start.
        /// 0 = off. Blocks new entries when equity ≤ dayStart − this amount.
        /// </summary>
        [Parameter("Max Daily Loss ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyLossAmount { get; set; }

        /// <summary>If true, also close all bot positions when daily loss $ limit is hit.</summary>
        [Parameter("Flatten On Daily Loss", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnDailyLoss { get; set; }

        /// <summary>
        /// Daily profit target in account currency. 0 = off.
        /// Blocks new entries when equity ≥ dayStart + this amount.
        /// </summary>
        [Parameter("Max Daily Profit ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyProfitAmount { get; set; }

        /// <summary>If true, also close all bot positions when daily profit $ target is hit.</summary>
        [Parameter("Flatten On Daily Profit", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnDailyProfit { get; set; }

        [Parameter("Debug Logging", Group = "Trade & Risk", DefaultValue = false)]
        public bool DebugLogging { get; set; }

        // ─── Stop Loss ──────────────────────────────────────
        // SL = structure (LVN ± buffer) but never tighter than min distance from entry.
        //   Buffer beyond LVN  = ATR × "LVN buffer (×ATR)"
        //   Min SL from entry  = ATR × "Min SL distance (×ATR)"
        [Parameter("SL TimeFrame", Group = "Stop Loss", DefaultValue = "Hour")]
        public TimeFrame SlTimeFrame { get; set; }

        [Parameter("ATR Period", Group = "Stop Loss", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        /// <summary>
        /// Extra distance past the LVN edge (in ATR units).
        /// Long SL = LVN.Low − ATR×this; Short SL = LVN.High + ATR×this.
        /// </summary>
        [Parameter("LVN buffer (×ATR)", Group = "Stop Loss", DefaultValue = 0.5, MinValue = 0.0)]
        public double SlAtrMult { get; set; }

        // ─── Take Profit (single, full size) ─────────────────
        [Parameter("TP Mode", Group = "Take Profit", DefaultValue = TpMode.RiskReward)]
        public TpMode TakeProfitMode { get; set; }

        [Parameter("RR Multiple", Group = "Take Profit", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0)]
        public double RrMultiple { get; set; }

        [Parameter("Fixed TP ($)", Group = "Take Profit", DefaultValue = 20.0, MinValue = 0.5)]
        public double FixedTpPrice { get; set; }

        // ─── Break-even (units = R = multiples of this trade's SL distance) ───
        // Not raw $: SL width varies; 1R always means "same distance as SL".
        // Not bare "pips": on XAU PipSize is often 0.01 → 20 pips = $0.20 (misleading).
        [Parameter("Use Break Even", Group = "Break Even", DefaultValue = true)]
        public bool UseBreakEven { get; set; }

        /// <summary>Move SL to BE after unrealized profit ≥ this × SL distance (e.g. 1.0 = 1R).</summary>
        [Parameter("BE Start (R)", Group = "Break Even", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0)]
        public double BeStartR { get; set; }

        /// <summary>Lock this × SL distance beyond entry (e.g. 0.05R). Plus spread if enabled.</summary>
        [Parameter("BE Lock (R)", Group = "Break Even", DefaultValue = 0.05, MinValue = 0.0, MaxValue = 2.0)]
        public double BeLockR { get; set; }

        [Parameter("BE Add Spread", Group = "Break Even", DefaultValue = true)]
        public bool BeAddSpread { get; set; }

        // ─── Trailing (also in R vs this trade's SL distance) ───
        [Parameter("Use Trailing", Group = "Trailing", DefaultValue = false)]
        public bool UseTrailing { get; set; }

        /// <summary>Start trailing after profit ≥ this × SL distance (e.g. 1.5R).</summary>
        [Parameter("Trail Start (R)", Group = "Trailing", DefaultValue = 1.5, MinValue = 0.1, MaxValue = 20.0)]
        public double TrailStartR { get; set; }

        /// <summary>Trail SL distance behind price = this × SL distance (e.g. 0.5R).</summary>
        [Parameter("Trail Step (R)", Group = "Trailing", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 5.0)]
        public double TrailStepR { get; set; }

        // ─── Volume Profile V2 ──────────────────────────────
        [Parameter("VP Mode", Group = "Volume Profile", DefaultValue = VpLookbackMode.Daily)]
        public VpLookbackMode ProfileMode { get; set; }

        [Parameter("VP Lookback (Hours)", Group = "Volume Profile", DefaultValue = 8.0, MinValue = 1.0, MaxValue = 168.0)]
        public double VpLookbackHours { get; set; }

        [Parameter("Lookback Days", Group = "Volume Profile", DefaultValue = 4, MinValue = 1, MaxValue = 10)]
        public int ProfileLookbackDays { get; set; }

        [Parameter("Bin Size", Group = "Volume Profile", DefaultValue = 0.5, MinValue = 0.01)]
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

        [Parameter("Use M1 Source Bars", Group = "Volume Profile", DefaultValue = true)]
        public bool UseM1SourceBars { get; set; }

        [Parameter("Use Gaussian Smooth", Group = "Volume Profile", DefaultValue = true)]
        public bool UseGaussianSmooth { get; set; }

        [Parameter("Visualize Profile", Group = "Volume Profile", DefaultValue = true)]
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

        [Parameter("Require Delta Filter", Group = "Signal Filters", DefaultValue = false)]
        public bool RequireDeltaFilter { get; set; }

        [Parameter("Require Shape Filter", Group = "Signal Filters", DefaultValue = false)]
        public bool RequireShapeFilter { get; set; }

        [Parameter("Require HTF Filter", Group = "Signal Filters", DefaultValue = true)]
        public bool RequireHtfFilter { get; set; }

        [Parameter("Allow POC/VA Targets", Group = "Signal Filters", DefaultValue = true)]
        public bool AllowPocVaTargets { get; set; }

        [Parameter("Touch Buffer ATR Mult", Group = "Signal Filters", DefaultValue = 0.15, MinValue = 0.0)]
        public double TouchBufferAtrMult { get; set; }

        [Parameter("HTF Timeframe", Group = "Signal Filters", DefaultValue = "Hour")]
        public TimeFrame HtfTimeframe { get; set; }

        // ─── Session (UTC fixed windows in CSessionFilter; OR logic) ───
        // Asia 00–09 | London 07–16 | NY 13:30–23 | Overlap 13–16
        [Parameter("Trade Asia", Group = "Session", DefaultValue = false)]
        public bool TradeAsia { get; set; }

        [Parameter("Trade London", Group = "Session", DefaultValue = false)]
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
        private CVolumeProfileV2 _volumeProfile;
        private CTickDeltaEngine _deltaEngine;
        private CRiskManager _riskManager;
        private CSessionFilter _sessionFilter;
        private CNewsFilter _newsFilter;
        private CMarketCondition _marketCondition;
        private CTrailingManager _trailingManager;
        private SignalEngine _signalEngine;
        private AverageTrueRange _atr;
        private AverageTrueRange _slAtr;
        private Bars _htfBars;
        private Bars _slBars;
        private Bars _m1Bars;

        private ProfileData _profile;
        private int _tradesToday;
        private int _tradeDayKey;
        private DateTime _lastSignalBarTime;
        private int _rejectCountToday;
        private string _lastRejectReason;

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("VacuumHunter", DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);

            _m1Bars = UseM1SourceBars ? MarketData.GetBars(TimeFrame.Minute, SymbolName) : null;
            _volumeProfile = new CVolumeProfileV2();
            _volumeProfile.Init(Bars, _m1Bars, Chart, 100, VisualizeProfile, _logger);
            _volumeProfile.ConfigureComposite(
                BinSize,
                ProfileLookbackDays,
                ValueAreaPercent > 1.0 ? ValueAreaPercent / 100.0 : ValueAreaPercent,
                LvnThreshold,
                HvnThreshold,
                WeightDecay,
                1.25,
                1.25,
                MaxLvnWidth,
                true,
                UseGaussianSmooth);

            _deltaEngine = new CTickDeltaEngine();
            _deltaEngine.Init(50000, _logger);

            _riskManager = new CRiskManager();
            _riskManager.Init(this, Symbol, BotLabel, _logger);
            _riskManager.SetEquityProtection(MaxEquityDrawdownPct, FlattenOnEquityDd);
            _riskManager.SetDailyLimits(MaxDailyLossAmount, MaxDailyProfitAmount, FlattenOnDailyLoss, FlattenOnDailyProfit);

            _sessionFilter = new CSessionFilter();
            _sessionFilter.Init(TradeAsia, TradeLondon, TradeNewYork, TradeOverlap, _logger);
            // Hours fixed in CSessionFilter defaults (no UI knobs).

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
            // BE/Trail distances are in R → applied per trade from that order's SL (see ConfigureExitsForTrade).

            _signalEngine = new SignalEngine();
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);
            _slBars = SlTimeFrame != TimeFrame ? MarketData.GetBars(SlTimeFrame, SymbolName) : Bars;
            _slAtr = Indicators.AverageTrueRange(_slBars, AtrPeriod, MovingAverageType.Simple);
            _htfBars = MarketData.GetBars(HtfTimeframe, SymbolName);

            _tradesToday = 0;
            _tradeDayKey = -1;
            _lastSignalBarTime = DateTime.MinValue;
            _rejectCountToday = 0;
            _lastRejectReason = null;

            Positions.Closed += OnPositionClosed;

            _profile = ProfileMode == VpLookbackMode.RollingHours
                ? _volumeProfile.BuildRollingHours(Server.TimeInUtc, VpLookbackHours)
                : _volumeProfile.BuildComposite(Server.TimeInUtc);
            string sizeDesc = SizeMode switch
            {
                LotSizeMode.RiskAmount => $"RiskAmount=${RiskAmount:F2}",
                LotSizeMode.FixedLots => $"FixedLots={FixedLots}",
                _ => $"RiskPercent={RiskPercent}%"
            };

            _logger.Info(
                $"Started {SymbolName} TF={TimeFrame} sizeMode={SizeMode} ({sizeDesc}) TP={TakeProfitMode} " +
                $"RR={RrMultiple} BE={UseBreakEven} Trail={UseTrailing} " +
                $"sessions={_sessionFilter.DescribeEnabled()} " +
                $"HVN={_profile?.Hvns?.Count ?? 0} LVN={_profile?.Lvns?.Count ?? 0} debug={DebugLogging}");
        }

        /// <summary>
        /// Map R-multiples → symbol pips using this trade's SL price distance.
        /// 1R = slDistPrice; TrailingManager still works in cTrader pips internally.
        /// </summary>
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

            // Risk module owns evaluate + optional flatten (independent of bot trade logic)
            _riskManager.OnTick();

            if (UseBreakEven || UseTrailing)
                _trailingManager.OnTick();
        }

        protected override void OnBar()
        {
            // Risk flatten/gates run only in OnTick → _riskManager.OnTick() (must not wait for bar close).
            _profile = ProfileMode == VpLookbackMode.RollingHours
                ? _volumeProfile.BuildRollingHours(Server.TimeInUtc, VpLookbackHours)
                : _volumeProfile.BuildComposite(Server.TimeInUtc);
            ResetDailyCounters();

            if (Bars.Count < 5 || _htfBars == null || _htfBars.Count < 3)
                return;

            int bi = Bars.Count - 2;
            DateTime barTime = Bars.OpenTimes[bi];
            if (barTime <= _lastSignalBarTime)
                return;
            _lastSignalBarTime = barTime;

            double htfClose = _htfBars.ClosePrices.Last(1);
            double atr = _atr != null && _atr.Result.Count > 1 ? _atr.Result.Last(1) : Symbol.PipSize * 50;

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
                // Gate only (no flatten) — flatten already handled on ticks
                EquityOk = _riskManager.IsTradingAllowed(Account.Equity, Server.TimeInUtc),
                TradesToday = _tradesToday,
                MaxTradesPerDay = MaxTradesPerDay,
                HasOpenPosition = Positions.FindAll(BotLabel, SymbolName).Length > 0,
                MinLvnStrength = MinLvnStrength,
                MinDeltaStrength = MinDeltaStrength,
                RejectionWickBodyRatio = RejectionWickBodyRatio,
                RequireShapeFilter = RequireShapeFilter,
                RequireDeltaFilter = RequireDeltaFilter,
                RequireHtfFilter = RequireHtfFilter,
                AllowPocVaTargets = AllowPocVaTargets,
                MaxLvnWidth = MaxLvnWidth,
                TouchBuffer = atr * Math.Max(0, TouchBufferAtrMult)
            };

            var result = _signalEngine.Evaluate(ctx);
            if (!result.IsValid)
            {
                LogReject(result, ctx);
                return;
            }

            _logger.Info(
                $"{result.Reason} LVN=[{result.Lvn.Low:F1}-{result.Lvn.High:F1}] str={result.Lvn.Strength:F2} " +
                $"structTp={result.StructureTarget?.Mid:F1} imb={result.Imbalance:F2}");

            if (!EnableTrading)
            {
                _logger.Info("Dry-run (EnableTrading=false) — order skipped");
                return;
            }

            ExecuteSignal(result);
        }

        private void ExecuteSignal(SignalResult signal)
        {
            double slAtrVal = _slAtr != null && _slAtr.Result.Count > 1 ? _slAtr.Result.Last(1) : Symbol.PipSize * 50;
            double atrBuf = slAtrVal * Math.Max(0, SlAtrMult);

            TradeType tradeType;
            double entry;
            double sl;

            if (signal.Side == SignalSide.Long)
            {
                tradeType = TradeType.Buy;
                entry = Symbol.Ask;
                sl = signal.Lvn.Low - atrBuf;
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
                sl = signal.Lvn.High + atrBuf;
                if (sl <= entry)
                {
                    _logger.Warn($"Execute aborted: invalid short SL={sl} entry={entry}");
                    return;
                }
            }

            double lvnWidth = signal.Lvn.High - signal.Lvn.Low;
            if (MaxLvnWidth > 0 && lvnWidth > MaxLvnWidth)
            {
                _logger.Warn($"Execute aborted: LVN width ${lvnWidth:F2} exceeds MaxLvnWidth ${MaxLvnWidth:F2}");
                return;
            }

            sl = PriceUtils.NormalizePrice(sl, Symbol);
            double slDist = Math.Abs(entry - sl);

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
                _logger.Warn($"Execute aborted: volume sizing failed — {sizeNote}");
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

            if (DebugLogging)
            {
                _logger.Debug(
                    $"[RISK DIAGNOSTIC] Symbol={SymbolName} PipSize={Symbol.PipSize} PipValue={Symbol.PipValue} " +
                    $"TickSize={Symbol.TickSize} TickValue={Symbol.TickValue} LotSize={Symbol.LotSize} " +
                    $"MinVol={Symbol.VolumeInUnitsMin} StepVol={Symbol.VolumeInUnitsStep}");
            }

            double slPips = PriceUtils.PriceToPips(slDist, Symbol);
            double tpPips = PriceUtils.PriceToPips(tpDist, Symbol);

            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, slPips, tpPips);
            if (!result.IsSuccessful || result.Position == null)
            {
                _logger.Error($"Order failed: {result.Error}");
                return;
            }

            // Refine SL and TP to exact absolute structure prices
            ModifyPosition(result.Position, sl, tp, ProtectionType.Absolute);

            // BE/Trail thresholds scale with this order's SL (1R = slDist)
            ConfigureExitsForTrade(slDist);

            _tradesToday++;
            var pos = result.Position;
            double actualSlDist = pos.StopLoss.HasValue ? Math.Abs(pos.EntryPrice - pos.StopLoss.Value) : slDist;
            double actualEstRisk = PriceUtils.PriceToAmount(actualSlDist, pos.VolumeInUnits, Symbol);

            _logger.Info(
                $"OPEN {tradeType} #{pos.Id} vol={volume} units ({volume / Symbol.LotSize:F2} lots) " +
                $"Entry={pos.EntryPrice:F2} SL={pos.StopLoss:F2} TP={pos.TakeProfit:F2} " +
                $"slDist={actualSlDist:F2} tpDist={tpDist:F2} RR={rr:F2} ({tpNote}) size={sizeNote} " +
                $"BE={(UseBreakEven ? $"{BeStartR:F2}R" : "off")} " +
                $"Trail={(UseTrailing ? $"{TrailStartR:F2}R/{TrailStepR:F2}R" : "off")} " +
                $"targetRisk=${expectedRisk:F2} actualEstLoss=${actualEstRisk:F2} ({riskPctActual:F2}%) dailyPnl=${dailyPnl:F0}");
        }

        /// <summary>
        /// RiskPercent: volume from $ risk and SL. FixedLots: fixed lots, optionally reduced if daily room requires.
        /// </summary>
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

                // Estimate $ at risk for this volume (price-unit heuristic used elsewhere)
                expectedRisk = volume * slDist;

                if (_riskManager.IsDailyLossLimitEnabled && expectedRisk > dailyRoom)
                {
                    // Scale lots down so estimated risk fits remaining daily room
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

            // RiskAmount or RiskPercent (default)
            double riskMoney = SizeMode == LotSizeMode.RiskAmount
                ? RiskAmount
                : Account.Balance * (RiskPercent / 100.0);

            if (_riskManager.IsDailyLossLimitEnabled && riskMoney > dailyRoom)
            {
                _logger.Info($"Risk capped by daily room: ${riskMoney:F0} → ${dailyRoom:F0} (dailyPnl=${dailyPnl:F0})");
                riskMoney = dailyRoom * 0.98;
            }

            volume = _riskManager.CalculateVolumeFromRiskMoney(riskMoney, slDist, out expectedRisk);
            if (volume <= 0)
            {
                sizeNote = SizeMode == LotSizeMode.RiskAmount
                    ? $"RiskAmount=${RiskAmount}"
                    : $"RiskPercent={RiskPercent}% money=${riskMoney:F0}";
                return false;
            }

            sizeNote = SizeMode == LotSizeMode.RiskAmount
                ? $"RiskAmount=${RiskAmount}"
                : $"RiskPercent={RiskPercent}%";
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
                    if (signal.StructureTarget == null)
                    {
                        note = "Structure TP: no magnet";
                        return false;
                    }
                    tp = signal.StructureTarget.Mid;
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
                    // Minimum RR sanity: at least 0.5R
                    double dist = Math.Abs(tp - entry);
                    if (dist < slDist * 0.5)
                    {
                        note = $"Structure TP too close (RR={dist / slDist:F2})";
                        return false;
                    }
                    note = "Structure";
                    return true;
                }
                default:
                    note = "Unknown TP mode";
                    return false;
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var pos = args?.Position;
            if (pos == null || pos.Label != BotLabel || pos.SymbolName != SymbolName) return;

            double closedSlDist = pos.StopLoss.HasValue ? Math.Abs(pos.EntryPrice - pos.StopLoss.Value) : 0;
            _logger.Info(
                $"CLOSE #{pos.Id} Side={pos.TradeType} Vol={pos.VolumeInUnits} units ({pos.VolumeInUnits / Symbol.LotSize:F2} lots) " +
                $"Entry={pos.EntryPrice:F2} SL={pos.StopLoss:F2} TP={pos.TakeProfit:F2} slDist={closedSlDist:F2} " +
                $"GrossPnL=${pos.GrossProfit:F2} NetPnL=${pos.NetProfit:F2} Swap=${pos.Swap:F2} Comm=${pos.Commissions:F2} " +
                $"Reason={args.Reason} eqDailyPnl=${_riskManager.GetDailyPnlMoney(Account.Equity):F0}");
        }

        private void ResetDailyCounters()
        {
            int dayKey = Server.TimeInUtc.Year * 1000 + Server.TimeInUtc.DayOfYear;
            if (dayKey != _tradeDayKey)
            {
                if (_tradeDayKey > 0 && _rejectCountToday > 0)
                    _logger.Info($"Day summary: rejects={_rejectCountToday} last={_lastRejectReason} trades={_tradesToday}");

                _tradeDayKey = dayKey;
                _tradesToday = 0;
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
                $"{result.Reason} close={ctx.BarClose:F1} lvns={ctx.Profile?.Lvns?.Count ?? 0} " +
                $"hvns={ctx.Profile?.Hvns?.Count ?? 0} htf={ctx.HtfClose:F1} poc={ctx.Profile?.POC:F1}");
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
