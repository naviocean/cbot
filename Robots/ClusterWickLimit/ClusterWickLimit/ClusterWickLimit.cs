using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Structure-scale liquidity cluster + wick reject → limit retest (v1.1).
    /// Geometry in strategy pips (100=1.0 XAU) with optional ATR floors. Not micro 1$ scalping.
    /// Spec: docs/v1.0/1-prds/PRD-cluster-wick-limit.md
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class ClusterWickLimit : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "ClusterWickLimit")]
        public string BotLabel { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk", DefaultValue = LotSizeMode.RiskPercent)]
        public LotSizeMode SizeMode { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.5, MinValue = 0.05, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Trades / Day", Group = "Trade & Risk", DefaultValue = 4, MinValue = 1, MaxValue = 50)]
        public int MaxTradesPerDay { get; set; }

        /// <summary>Max spread. Default 30 pips = 0.30 price (100 pips = 1.0).</summary>
        [Parameter("Max Spread (pips)", Group = "Trade & Risk", DefaultValue = 50.0, MinValue = 1.0)]
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

        // ─── Cluster ────────────────────────────────────────
        [Parameter("Lookback Bars", Group = "Cluster", DefaultValue = 60, MinValue = 20, MaxValue = 300)]
        public int LookbackBars { get; set; }

        [Parameter("Max Cluster Age (bars)", Group = "Cluster", DefaultValue = 80, MinValue = 10)]
        public int MaxClusterAgeBars { get; set; }

        [Parameter("Min Touches", Group = "Cluster", DefaultValue = 3, MinValue = 2, MaxValue = 20)]
        public int MinTouches { get; set; }

        /// <summary>Min cluster band floor. v1.1 floor. Default 100 pips = 1.0 price.</summary>
        [Parameter("Base Band (pips)", Group = "Cluster", DefaultValue = 100.0, MinValue = 1.0)]
        public double BaseBandPips { get; set; }

        [Parameter("Tol Factor", Group = "Cluster", DefaultValue = 0.25, MinValue = 0.05, MaxValue = 1.0)]
        public double TolFactor { get; set; }

        [Parameter("Range Bars", Group = "Cluster", DefaultValue = 20, MinValue = 5)]
        public int RangeBars { get; set; }

        /// <summary>Min range20. v1.1. Default 150 pips = 1.50.</summary>
        [Parameter("Min Range (pips)", Group = "Cluster", DefaultValue = 150.0, MinValue = 1.0)]
        public double MinRangePips { get; set; }

        /// <summary>Max |close−cluster|. v1.1. Default 500 pips = 5.0.</summary>
        [Parameter("Max Approach (pips)", Group = "Cluster", DefaultValue = 500.0, MinValue = 10.0)]
        public double MaxApproachPips { get; set; }

        /// <summary>Cap cluster tol so huge range20 does not make whole book one cluster. v1.1. Default 400 pips = 4.0.</summary>
        [Parameter("Max Tol (pips)", Group = "Cluster", DefaultValue = 400.0, MinValue = 10.0)]
        public double MaxTolPips { get; set; }

        // ─── Wick ───────────────────────────────────────────
        /// <summary>Pass if rejectWick >= this × body. XAU M1 rarely hits 1.8; default 0.8.</summary>
        [Parameter("Wick/Body Min", Group = "Wick", DefaultValue = 0.8, MinValue = 0.2, MaxValue = 5.0)]
        public double WickBodyMin { get; set; }

        /// <summary>OR pass if rejectWick / barRange >= this (pin by range). Default 0.45. 0 = off.</summary>
        [Parameter("Wick/Range Min", Group = "Wick", DefaultValue = 0.40, MinValue = 0.0, MaxValue = 0.95)]
        public double WickRangeMin { get; set; }

        /// <summary>OR pass if reject wick >= this many strat pips. v1.1. Default 50. 0 = off.</summary>
        [Parameter("Min Wick (pips)", Group = "Wick", DefaultValue = 50.0, MinValue = 0.0)]
        public double MinWickPips { get; set; }

        /// <summary>Max body size. v1.1. Default 600 pips = 6.0.</summary>
        [Parameter("Max Body (pips)", Group = "Wick", DefaultValue = 600.0, MinValue = 1.0)]
        public double MaxBodyPips { get; set; }

        [Parameter("Close Pos Max (short)", Group = "Wick", DefaultValue = 0.45, MinValue = 0.05, MaxValue = 0.55)]
        public double ClosePosMax { get; set; }

        [Parameter("Close Pos Min (long)", Group = "Wick", DefaultValue = 0.55, MinValue = 0.45, MaxValue = 0.95)]
        public double ClosePosMin { get; set; }

        [Parameter("Entry Offset K", Group = "Wick", DefaultValue = 0.35, MinValue = 0.0, MaxValue = 2.0)]
        public double EntryOffsetK { get; set; }

        // ─── Stop Loss (XAU: 100 pips = 1.0 price → 1 pip = 0.01) ───
        /// <summary>Beyond extreme wick. v1.1. Default 40 pips = 0.40.</summary>
        [Parameter("SL Buffer (pips)", Group = "Stop Loss", DefaultValue = 40.0, MinValue = 0.0)]
        public double SlBufferPips { get; set; }

        /// <summary>Min |entry−SL|. v1.1. Default 250 pips = 2.50. Below → skip.</summary>
        [Parameter("SL Min (pips)", Group = "Stop Loss", DefaultValue = 250.0, MinValue = 1.0)]
        public double SlMinPips { get; set; }

        /// <summary>Max |entry−SL|. v1.1. Default 600 pips = 6.00. Above → skip.</summary>
        [Parameter("SL Max (pips)", Group = "Stop Loss", DefaultValue = 600.0, MinValue = 2.0)]
        public double SlMaxPips { get; set; }

        // ─── ATR scaling (v1.1: floors grow with volatility) ───
        [Parameter("Use ATR Scaling", Group = "ATR", DefaultValue = true)]
        public bool UseAtrScaling { get; set; }

        [Parameter("ATR Period", Group = "ATR", DefaultValue = 14, MinValue = 5, MaxValue = 50)]
        public int AtrPeriod { get; set; }

        /// <summary>SlMin_eff = max(SL Min pips, ATR × this). Default 0.8.</summary>
        [Parameter("SL Min (×ATR)", Group = "ATR", DefaultValue = 0.8, MinValue = 0.0, MaxValue = 5.0)]
        public double SlMinAtrMult { get; set; }

        /// <summary>SlMax_eff = max(SL Max pips, ATR × this). Default 2.5.</summary>
        [Parameter("SL Max (×ATR)", Group = "ATR", DefaultValue = 2.5, MinValue = 0.0, MaxValue = 10.0)]
        public double SlMaxAtrMult { get; set; }

        [Parameter("SL Buffer (×ATR)", Group = "ATR", DefaultValue = 0.10, MinValue = 0.0, MaxValue = 2.0)]
        public double SlBufferAtrMult { get; set; }

        [Parameter("Base Band (×ATR)", Group = "ATR", DefaultValue = 0.15, MinValue = 0.0, MaxValue = 2.0)]
        public double BaseBandAtrMult { get; set; }

        [Parameter("Max Tol (×ATR)", Group = "ATR", DefaultValue = 0.70, MinValue = 0.0, MaxValue = 3.0)]
        public double MaxTolAtrMult { get; set; }

        [Parameter("Max Approach (×ATR)", Group = "ATR", DefaultValue = 2.00, MinValue = 0.0, MaxValue = 5.0)]
        public double MaxApproachAtrMult { get; set; }

        [Parameter("Min Range (×ATR)", Group = "ATR", DefaultValue = 0.50, MinValue = 0.0, MaxValue = 5.0)]
        public double MinRangeAtrMult { get; set; }

        [Parameter("Min Wick (×ATR)", Group = "ATR", DefaultValue = 0.15, MinValue = 0.0, MaxValue = 2.0)]
        public double MinWickAtrMult { get; set; }

        [Parameter("Max Body (×ATR)", Group = "ATR", DefaultValue = 1.20, MinValue = 0.0, MaxValue = 5.0)]
        public double MaxBodyAtrMult { get; set; }

        // ─── Take Profit (single RR) ─────────────────────────
        [Parameter("TP (R)", Group = "Take Profit", DefaultValue = 1.5, MinValue = 0.5, MaxValue = 10.0)]
        public double TpRR { get; set; }

        // ─── Break-even (start still in R; lock in strategy pips: 100 pips = 1.0 price) ───
        [Parameter("Use Break Even", Group = "Break Even", DefaultValue = true)]
        public bool UseBreakEven { get; set; }

        [Parameter("BE Start (R)", Group = "Break Even", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0)]
        public double BeAtRR { get; set; }

        /// <summary>Profit lock beyond entry after BE. Strategy pips (100=1.0). 0 = 1 tick.</summary>
        [Parameter("BE Lock (pips)", Group = "Break Even", DefaultValue = 15.0, MinValue = 0.0)]
        public double BeLockPips { get; set; }

        [Parameter("BE Add Spread", Group = "Break Even", DefaultValue = true)]
        public bool BeAddSpread { get; set; }

        // ─── Trailing (start in R; distance in strategy pips) ───
        [Parameter("Use Trailing", Group = "Trailing", DefaultValue = true)]
        public bool UseTrailing { get; set; }

        [Parameter("Trail Start (R)", Group = "Trailing", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 20.0)]
        public double TrailStartRR { get; set; }

        /// <summary>Trail SL distance behind price. Strategy pips (100=1.0). v1.1. Default 120 = 1.20.</summary>
        [Parameter("Trail Dist (pips)", Group = "Trailing", DefaultValue = 120.0, MinValue = 1.0)]
        public double TrailDistPips { get; set; }

        // ─── Pending lifecycle ──────────────────────────────
        [Parameter("Pending TTL (bars)", Group = "Pending", DefaultValue = 15, MinValue = 1, MaxValue = 100)]
        public int PendingTtlBars { get; set; }

        [Parameter("Break Buffer (pips)", Group = "Pending", DefaultValue = 40.0, MinValue = 0.0)]
        public double BreakBufferPips { get; set; }

        [Parameter("Cancel Approach Mult", Group = "Pending", DefaultValue = 1.5, MinValue = 1.0, MaxValue = 5.0)]
        public double CancelApproachMult { get; set; }

        // ─── HTF bias (block fades against higher TF) ────────
        [Parameter("Use HTF Bias", Group = "HTF Bias", DefaultValue = true)]
        public bool UseHtfBias { get; set; }

        [Parameter("HTF Timeframe", Group = "HTF Bias", DefaultValue = "Hour")]
        public TimeFrame HtfTimeframe { get; set; }

        /// <summary>Bias = sign(close[1] − close[1+N]) on HTF. Default 3 = ~3H on H1.</summary>
        [Parameter("HTF Lookback Bars", Group = "HTF Bias", DefaultValue = 3, MinValue = 1, MaxValue = 50)]
        public int HtfLookbackBars { get; set; }

        /// <summary>Min |close−ref| in strategy pips to count as non-flat. Default 50 (=0.50).</summary>
        [Parameter("HTF Min Move (pips)", Group = "HTF Bias", DefaultValue = 50.0, MinValue = 0.0)]
        public double HtfMinMovePips { get; set; }

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
        /// <summary>
        /// XAU strategy pip: 100 pips = 1.0 price (1 pip = 0.01).
        /// Not broker Symbol.PipSize (often 0.1 on gold).
        /// </summary>
        private const double StratPipPrice = 0.01;

        private CLogger _logger;
        private CRiskManager _riskManager;
        private CSessionFilter _sessionFilter;
        private CNewsFilter _newsFilter;
        private CMarketCondition _marketCondition;
        private CTrailingManager _trailingManager;
        private SignalEngine _signalEngine;
        private AverageTrueRange _atr;
        private Bars _htfBars;

        // Last bar effective geometry (price units) for arm + fill protection
        private double _effSlMin;
        private double _effSlMax;
        private double _effSlBuffer;
        private double _lastAtr;

        private int _tradesToday;
        private int _tradeDayKey;
        private DateTime _lastSignalBarTime;
        private int _rejectCountToday;
        private string _lastRejectReason;

        // Armed pending metadata (single setup)
        private bool _pendingArmed;
        private SignalSide _pendingSide;
        private double _pendingClusterLevel;
        private double _pendingClusterExtreme;
        private double _pendingTol;
        private double _pendingEntry;
        private double _pendingSl;
        private double _pendingR;
        private int _pendingArmBarIndex;
        private DateTime _pendingArmBarTime;

        // Anti-spam after trade at cluster
        private bool _hasLastTradedCluster;
        private double _lastTradedClusterLevel;
        private double _lastTradedTol;

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("ClusterWickLimit", DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);

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
            double maxSpreadPrice = StratPipsToPrice(MaxSpreadPips);
            double maxSpreadBrokerPips = PriceUtils.PriceToPips(maxSpreadPrice, Symbol);
            _marketCondition.SetSpreadCheck(true, maxSpreadBrokerPips);

            _trailingManager = new CTrailingManager();
            _trailingManager.Init(this, Symbol, BotLabel, _logger);

            _signalEngine = new SignalEngine();
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);
            _htfBars = MarketData.GetBars(HtfTimeframe, SymbolName);
            _effSlMin = StratPipsToPrice(SlMinPips);
            _effSlMax = StratPipsToPrice(SlMaxPips);
            _effSlBuffer = StratPipsToPrice(SlBufferPips);
            _lastAtr = 0;

            _tradesToday = 0;
            _tradeDayKey = -1;
            _lastSignalBarTime = DateTime.MinValue;
            _rejectCountToday = 0;
            _lastRejectReason = null;
            ClearPendingState();
            _hasLastTradedCluster = false;

            Positions.Opened += OnPositionOpened;
            Positions.Closed += OnPositionClosed;
            PendingOrders.Cancelled += OnPendingCancelled;
            PendingOrders.Filled += OnPendingFilled;

            // Cancel stray pendings from previous run
            CancelAllBotPendings("startup cleanup");

            _logger.Info(
                $"Started CWL v1.1 {SymbolName} TF={TimeFrame} risk={RiskPercent}% TpRR={TpRR} " +
                $"SL band={SlMinPips:F0}-{SlMaxPips:F0} pips (buf={SlBufferPips:F0}) ATR={UseAtrScaling} " +
                $"(min×{SlMinAtrMult:F2} max×{SlMaxAtrMult:F2}) 1pip={StratPipPrice} | " +
                $"BE={UseBreakEven}@start{BeAtRR}R lock{BeLockPips:F0}pips " +
                $"Trail={UseTrailing}@start{TrailStartRR}R dist{TrailDistPips:F0}pips " +
                $"sessions={_sessionFilter.DescribeEnabled()} maxSpread={MaxSpreadPips:F0} " +
                $"(price={maxSpreadPrice:F2}) band={BaseBandPips:F0} appr={MaxApproachPips:F0} " +
                $"bodyMax={MaxBodyPips:F0} wickBody={WickBodyMin:F1} minWick={MinWickPips:F0} " +
                $"maxTol={MaxTolPips:F0} TTL={PendingTtlBars} HTF={UseHtfBias}/{HtfTimeframe}/lb{HtfLookbackBars} debug={DebugLogging}");

            if (SlMaxPips < 150)
            {
                _logger.Warn(
                    $"SL Max={SlMaxPips:F0} pips still micro for v1.1 XAU (want ~250-600+). " +
                    $"Band price [{StratPipsToPrice(SlMinPips):F2},{StratPipsToPrice(SlMaxPips):F2}]");
            }
            if (SlMinPips > SlMaxPips)
                _logger.Error($"SL Min ({SlMinPips}) > SL Max ({SlMaxPips}) — all arms will fail");
        }

        /// <summary>Convert strategy pips → price (1 pip = <see cref="StratPipPrice"/>).</summary>
        private static double StratPipsToPrice(double pips) => pips * StratPipPrice;

        private sealed class EffectiveGeometry
        {
            public double Atr;
            public double BaseBand;
            public double MaxTol;
            public double MaxApproach;
            public double MinRange;
            public double MinWick;
            public double MaxBody;
            public double SlBuffer;
            public double SlMin;
            public double SlMax;
        }

        /// <summary>
        /// v1.1: each distance = max(fixed strategy-pips floor, ATR × mult) when Use ATR Scaling.
        /// </summary>
        private EffectiveGeometry ComputeEffectiveGeometry()
        {
            double atr = 0;
            if (_atr != null && _atr.Result.Count > 1)
                atr = _atr.Result.Last(1);
            if (atr <= 0)
                atr = StratPipsToPrice(200); // fallback ~2.0 if ATR not ready

            double Floor(double pips, double atrMult)
            {
                double fromPips = StratPipsToPrice(pips);
                if (!UseAtrScaling || atrMult <= 0)
                    return fromPips;
                return Math.Max(fromPips, atr * atrMult);
            }

            var g = new EffectiveGeometry
            {
                Atr = atr,
                BaseBand = Floor(BaseBandPips, BaseBandAtrMult),
                MaxTol = Floor(MaxTolPips, MaxTolAtrMult),
                MaxApproach = Floor(MaxApproachPips, MaxApproachAtrMult),
                MinRange = Floor(MinRangePips, MinRangeAtrMult),
                MinWick = Floor(MinWickPips, MinWickAtrMult),
                MaxBody = Floor(MaxBodyPips, MaxBodyAtrMult),
                SlBuffer = Floor(SlBufferPips, SlBufferAtrMult),
                SlMin = Floor(SlMinPips, SlMinAtrMult),
                SlMax = Floor(SlMaxPips, SlMaxAtrMult)
            };

            // Ensure max band strictly above min
            if (g.SlMax < g.SlMin + StratPipsToPrice(40))
                g.SlMax = g.SlMin + StratPipsToPrice(40);
            if (g.MaxTol < g.BaseBand)
                g.MaxTol = g.BaseBand;
            // Cluster width should be able to support SlMin (else SL always pure floor expand).
            if (g.MaxTol < g.SlMin * 0.5)
                g.MaxTol = g.SlMin * 0.5;
            if (g.MaxApproach < g.SlMin)
                g.MaxApproach = g.SlMin;
            if (g.MaxBody < g.MinWick * 2)
                g.MaxBody = g.MinWick * 2;

            return g;
        }

        /// <summary>
        /// HTF bias from closed bars: sign(close[1] − close[1+lookback]).
        /// Flat if |move| &lt; HtfMinMovePips (strategy pips).
        /// </summary>
        private SignalSide ComputeHtfBias()
        {
            if (!UseHtfBias)
                return SignalSide.None;

            if (_htfBars == null || _htfBars.Count < HtfLookbackBars + 3)
                return SignalSide.None;

            int iClose = _htfBars.Count - 2;
            int iRef = iClose - HtfLookbackBars;
            if (iRef < 0)
                return SignalSide.None;

            double c1 = _htfBars.ClosePrices[iClose];
            double cRef = _htfBars.ClosePrices[iRef];
            double move = c1 - cRef;
            double minMove = StratPipsToPrice(HtfMinMovePips);

            if (Math.Abs(move) < minMove)
                return SignalSide.None;
            return move > 0 ? SignalSide.Long : SignalSide.Short;
        }

        protected override void OnStop()
        {
            Positions.Opened -= OnPositionOpened;
            Positions.Closed -= OnPositionClosed;
            PendingOrders.Cancelled -= OnPendingCancelled;
            PendingOrders.Filled -= OnPendingFilled;
        }

        protected override void OnTick()
        {
            _riskManager.OnTick();

            if (UseBreakEven || UseTrailing)
                _trailingManager.OnTick();
        }

        protected override void OnBar()
        {
            ResetDailyCounters();

            if (Bars.Count < Math.Max(LookbackBars, RangeBars) + 5)
                return;

            // Manage existing pending first (TTL / invalidate) using latest closed bar
            ManagePendingOnBar();

            int bi = Bars.Count - 2; // closed bar
            double closedBarClose = Bars.ClosePrices[bi];

            // PRD §7: anti-spam blocks re-arm at last level until price leaves by > 2×tol.
            // Clear the lock once price has left so a later return to the same level can arm again.
            if (_hasLastTradedCluster)
            {
                double leaveTol = _lastTradedTol > 0 ? _lastTradedTol : StratPipsToPrice(BaseBandPips);
                if (Math.Abs(closedBarClose - _lastTradedClusterLevel) > 2.0 * leaveTol)
                {
                    _logger.Debug(
                        $"Anti-spam cleared: close={closedBarClose:F2} left level {_lastTradedClusterLevel:F2} (>2×tol={2.0 * leaveTol:F4})");
                    _hasLastTradedCluster = false;
                    _lastTradedClusterLevel = 0;
                    _lastTradedTol = 0;
                }
            }

            DateTime barTime = Bars.OpenTimes[bi];
            if (barTime <= _lastSignalBarTime)
                return;
            _lastSignalBarTime = barTime;

            if (HasBotPosition() || HasBotPending())
            {
                _logger.Debug("Skip arm: busy position/pending");
                return;
            }

            var bars = BuildBarSnaps();
            var geo = ComputeEffectiveGeometry();
            _effSlMin = geo.SlMin;
            _effSlMax = geo.SlMax;
            _effSlBuffer = geo.SlBuffer;
            _lastAtr = geo.Atr;

            var htfBias = ComputeHtfBias();

            var ctx = new SignalContext
            {
                Bars = bars,
                SpreadPrice = Symbol.Spread,
                TickSize = Symbol.TickSize > 0 ? Symbol.TickSize : Symbol.PipSize,
                SessionOk = _sessionFilter.IsTradingAllowed(Server.TimeInUtc),
                NewsOk = _newsFilter.IsTradingAllowed(Server.TimeInUtc),
                EquityOk = _riskManager.IsTradingAllowed(Account.Equity, Server.TimeInUtc),
                HasOpenPosition = HasBotPosition(),
                HasPending = HasBotPending(),
                TradesToday = _tradesToday,
                MaxTradesPerDay = MaxTradesPerDay,
                LookbackBars = LookbackBars,
                MaxClusterAgeBars = MaxClusterAgeBars,
                MinTouches = MinTouches,
                BaseBand = geo.BaseBand,
                TolFactor = TolFactor,
                RangeBars = RangeBars,
                MinRange = geo.MinRange,
                MaxApproach = geo.MaxApproach,
                MaxTol = geo.MaxTol,
                WickBodyMin = WickBodyMin,
                WickRangeMin = WickRangeMin,
                MinWickPrice = geo.MinWick,
                MaxBody = geo.MaxBody,
                ClosePosMax = ClosePosMax,
                ClosePosMin = ClosePosMin,
                EntryOffsetK = EntryOffsetK,
                SlBuffer = geo.SlBuffer,
                SlMin = geo.SlMin,
                SlMax = geo.SlMax,
                MaxSpreadPrice = StratPipsToPrice(MaxSpreadPips),
                HasLastTradedCluster = _hasLastTradedCluster,
                LastTradedClusterLevel = _lastTradedClusterLevel,
                LastTradedTol = _lastTradedTol,
                RequireHtfBias = UseHtfBias,
                HtfBias = htfBias
            };

            // Also respect MarketCondition spread (pips) as secondary
            if (!_marketCondition.IsTradingOK())
            {
                LogReject("REJECT:F_SPREAD_PIPS", ctx);
                return;
            }

            var result = _signalEngine.Evaluate(ctx);
            if (!result.IsValid)
            {
                LogReject(result.Reason, ctx);
                return;
            }

            _logger.Info(
                $"{result.Reason} side={result.Side} cluster={result.ClusterLevel:F2} ext={result.ClusterExtreme:F2} " +
                $"touches={result.Touches} tol={result.Tol:F4} range20={result.Range20:F4} atr={_lastAtr:F2} " +
                $"htfBias={htfBias} slBand=[{_effSlMin:F2},{_effSlMax:F2}] entry={result.Entry:F2} sl={result.StopLoss:F2} " +
                $"R={result.SlDist:F4} closePos={result.ClosePos:F2}");

            if (!EnableTrading)
            {
                _logger.Info("Dry-run (EnableTrading=false) — limit skipped");
                return;
            }

            PlaceLimitFromSignal(result, bi, barTime);
        }

        private List<BarSnap> BuildBarSnaps()
        {
            int need = Math.Max(LookbackBars, RangeBars);
            var list = new List<BarSnap>(need);
            // shift 1..need → Bars index Count-2, Count-3, ...
            for (int shift = 1; shift <= need; shift++)
            {
                int idx = Bars.Count - 1 - shift;
                if (idx < 0) break;
                list.Add(new BarSnap(
                    Bars.OpenPrices[idx],
                    Bars.HighPrices[idx],
                    Bars.LowPrices[idx],
                    Bars.ClosePrices[idx],
                    shift));
            }
            return list;
        }

        private void PlaceLimitFromSignal(SignalResult signal, int armBarIndex, DateTime armBarTime)
        {
            double entry = PriceUtils.NormalizePrice(signal.Entry, Symbol);
            double sl = PriceUtils.NormalizePrice(signal.StopLoss, Symbol);
            double r = Math.Abs(entry - sl);
            if (r <= 0)
            {
                _logger.Warn("Place aborted: R<=0");
                return;
            }

            double tpDist = r * Math.Max(0.5, TpRR);
            double tp = signal.Side == SignalSide.Long
                ? PriceUtils.NormalizePrice(entry + tpDist, Symbol)
                : PriceUtils.NormalizePrice(entry - tpDist, Symbol);

            // Limit must be on correct side of market
            if (signal.Side == SignalSide.Long && entry >= Symbol.Ask)
            {
                _logger.Warn($"BuyLimit entry {entry:F2} >= Ask {Symbol.Ask:F2} — skip (would be marketable)");
                return;
            }
            if (signal.Side == SignalSide.Short && entry <= Symbol.Bid)
            {
                _logger.Warn($"SellLimit entry {entry:F2} <= Bid {Symbol.Bid:F2} — skip (would be marketable)");
                return;
            }

            double dailyPnl = _riskManager.GetDailyPnlMoney(Account.Equity);
            double dailyRoom = _riskManager.GetRemainingDailyLossBudget(Account.Equity);
            if (_riskManager.IsDailyLossLimitEnabled && dailyRoom <= 1.0)
            {
                _logger.Warn($"Place aborted: daily loss room exhausted (pnl=${dailyPnl:F0})");
                return;
            }

            if (!TryComputeVolume(r, dailyRoom, dailyPnl, out double volume, out double expectedRisk, out string sizeNote))
            {
                _logger.Warn($"Place aborted: volume — {sizeNote}");
                return;
            }

            TradeType tradeType = signal.Side == SignalSide.Long ? TradeType.Buy : TradeType.Sell;
            var result = PlaceLimitOrder(
                tradeType,
                SymbolName,
                volume,
                entry,
                BotLabel,
                sl,
                tp,
                ProtectionType.Absolute);

            if (!result.IsSuccessful)
            {
                _logger.Error($"PlaceLimit failed: {result.Error}");
                return;
            }

            _pendingArmed = true;
            _pendingSide = signal.Side;
            _pendingClusterLevel = signal.ClusterLevel;
            _pendingClusterExtreme = signal.ClusterExtreme;
            _pendingTol = signal.Tol;
            _pendingEntry = entry;
            _pendingSl = sl;
            _pendingR = r;
            _pendingArmBarIndex = armBarIndex;
            _pendingArmBarTime = armBarTime;

            ConfigureExitsForTrade(r);

            _logger.Info(
                $"E_ARM {tradeType} Limit@{entry:F2} SL={sl:F2} TP={tp:F2} R={r:F4} TpRR={TpRR} " +
                $"vol={volume} size={sizeNote} risk≈${expectedRisk:F0} cluster={signal.ClusterLevel:F2}");
        }

        private void ManagePendingOnBar()
        {
            // Fill already opened a position — do not cancel / wipe arm metadata (race with fill).
            if (HasBotPosition())
                return;

            if (!HasBotPending())
            {
                if (_pendingArmed)
                    ClearPendingState();
                return;
            }

            if (!_pendingArmed)
            {
                var po = GetBotPending();
                if (po != null)
                {
                    _pendingArmed = true;
                    _pendingArmBarIndex = Bars.Count - 2;
                    _pendingClusterLevel = po.TargetPrice;
                    _pendingClusterExtreme = po.StopLoss ?? po.TargetPrice;
                    _pendingTol = StratPipsToPrice(BaseBandPips);
                    _pendingSide = po.TradeType == TradeType.Buy ? SignalSide.Long : SignalSide.Short;
                    _pendingEntry = po.TargetPrice;
                    _pendingR = po.StopLoss.HasValue
                        ? Math.Abs(po.TargetPrice - po.StopLoss.Value)
                        : StratPipsToPrice(SlMinPips);
                }
            }

            int bi = Bars.Count - 2;
            if (bi < 0) return;

            double close = Bars.ClosePrices[bi];
            double high = Bars.HighPrices[bi];
            double low = Bars.LowPrices[bi];

            int barsSinceArm = bi - _pendingArmBarIndex;
            if (barsSinceArm < 0)
                barsSinceArm = Bars.Count;
            if (barsSinceArm >= PendingTtlBars)
            {
                CancelAllBotPendings($"F_TTL bars={barsSinceArm}");
                return;
            }

            // Acceptance: only cancel if pending still live (not mid-fill)
            if (_pendingSide == SignalSide.Short && close > _pendingClusterExtreme + StratPipsToPrice(BreakBufferPips))
            {
                CancelAllBotPendings($"F_ACCEPT short close={close:F2}>ext+buf");
                return;
            }
            if (_pendingSide == SignalSide.Long && close < _pendingClusterExtreme - StratPipsToPrice(BreakBufferPips))
            {
                CancelAllBotPendings($"F_ACCEPT long close={close:F2}<ext-buf");
                return;
            }

            double approachLimit = StratPipsToPrice(MaxApproachPips) * CancelApproachMult;
            if (Math.Abs(close - _pendingClusterLevel) > approachLimit)
            {
                CancelAllBotPendings($"F_APPROACH_CANCEL dist={Math.Abs(close - _pendingClusterLevel):F4}");
                return;
            }

            if (Symbol.Spread > StratPipsToPrice(MaxSpreadPips))
            {
                CancelAllBotPendings($"F_SPREAD_PEND {Symbol.Spread:F4}");
                return;
            }

            if (!_newsFilter.IsTradingAllowed(Server.TimeInUtc))
            {
                CancelAllBotPendings("F_NEWS_PEND");
                return;
            }

            _logger.Debug(
                $"Pending live side={_pendingSide} entry={_pendingEntry:F2} ageBars={barsSinceArm} " +
                $"close={close:F2} hi={high:F2} lo={low:F2}");
        }

        private void ConfigureExitsForTrade(double slDistPrice)
        {
            if (slDistPrice <= 0) return;

            // TrailingManager expects broker pips (Symbol.PipSize). Strategy pips → price → broker pips.
            if (UseBreakEven)
            {
                double startPips = PriceUtils.PriceToPips(slDistPrice * BeAtRR, Symbol);
                double lockPips = StratPipsToBrokerPips(BeLockPips);
                if (lockPips <= 0)
                    lockPips = Math.Max(0.1, PriceUtils.PriceToPips(Symbol.TickSize > 0 ? Symbol.TickSize : Symbol.PipSize, Symbol));
                _trailingManager.SetBreakevenPoints(startPips, lockPips, BeAddSpread);
            }

            if (UseTrailing)
            {
                double startPips = PriceUtils.PriceToPips(slDistPrice * TrailStartRR, Symbol);
                double stepPips = StratPipsToBrokerPips(TrailDistPips);
                if (stepPips <= 0)
                    stepPips = Math.Max(1.0, PriceUtils.PriceToPips(StratPipsToPrice(40), Symbol));
                double sensPips = Math.Max(1.0, stepPips * 0.25);
                _trailingManager.SetTrailPoints(startPips, stepPips, sensPips);
            }
        }

        /// <summary>Strategy pips (100 = 1.0 price) → cTrader/broker pips for TrailingManager.</summary>
        private double StratPipsToBrokerPips(double stratPips)
        {
            if (stratPips <= 0) return 0;
            return PriceUtils.PriceToPips(StratPipsToPrice(stratPips), Symbol);
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

                expectedRisk = PriceUtils.PriceToAmount(slDist, volume, Symbol);
                if (expectedRisk <= 0)
                    expectedRisk = volume * slDist; // fallback

                if (_riskManager.IsDailyLossLimitEnabled && expectedRisk > dailyRoom)
                {
                    double scale = (dailyRoom * 0.98) / expectedRisk;
                    double reduced = _riskManager.CalculateVolume(FixedLots * scale);
                    if (reduced <= 0)
                    {
                        sizeNote = $"FixedLots exceeds daily room ${dailyRoom:F0}";
                        return false;
                    }
                    volume = reduced;
                    expectedRisk = PriceUtils.PriceToAmount(slDist, volume, Symbol);
                }

                sizeNote = $"FixedLots≈{volume / Math.Max(1e-9, Symbol.LotSize):F2}";
                return true;
            }

            double riskMoney = Account.Balance * (RiskPercent / 100.0);
            if (_riskManager.IsDailyLossLimitEnabled && riskMoney > dailyRoom)
                riskMoney = dailyRoom * 0.98;

            volume = _riskManager.CalculateVolumeFromRiskMoney(riskMoney, slDist, out expectedRisk);
            if (volume <= 0)
            {
                sizeNote = $"RiskPercent={RiskPercent}%";
                return false;
            }

            sizeNote = $"RiskPercent={RiskPercent}%";
            return true;
        }

        private bool HasBotPosition()
        {
            return Positions.FindAll(BotLabel, SymbolName).Length > 0;
        }

        private bool HasBotPending()
        {
            return PendingOrders.Any(o => o != null && o.Label == BotLabel && o.SymbolName == SymbolName);
        }

        private PendingOrder GetBotPending()
        {
            return PendingOrders.FirstOrDefault(o => o != null && o.Label == BotLabel && o.SymbolName == SymbolName);
        }

        private void CancelAllBotPendings(string reason)
        {
            // Never wipe arm state if a fill already produced a position (OnPositionOpened needs metadata).
            if (HasBotPosition())
            {
                _logger.Debug($"Skip cancel ({reason}): position open — leave state for fill handler");
                return;
            }

            var orders = PendingOrders.Where(o => o != null && o.Label == BotLabel && o.SymbolName == SymbolName).ToArray();
            if (orders.Length == 0)
            {
                // Empty pending + no position → safe clear; empty + in-flight fill handled above.
                ClearPendingState();
                return;
            }

            foreach (var o in orders)
            {
                var r = CancelPendingOrder(o);
                // EntityNotFound = already filled/cancelled — not a hard error
                if (!r.IsSuccessful)
                    _logger.Info($"Cancel pending #{o.Id}: {reason} ok=False err={r.Error}");
                else
                    _logger.Info($"Cancel pending #{o.Id}: {reason} ok=True");
            }

            // If cancel raced with fill, keep state until OnPositionOpened.
            if (!HasBotPosition() && !HasBotPending())
                ClearPendingState();
        }

        private void ClearPendingState()
        {
            _pendingArmed = false;
            _pendingSide = SignalSide.None;
            _pendingClusterLevel = 0;
            _pendingClusterExtreme = 0;
            _pendingTol = 0;
            _pendingEntry = 0;
            _pendingSl = 0;
            _pendingR = 0;
            _pendingArmBarIndex = 0;
            _pendingArmBarTime = DateTime.MinValue;
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            var pos = args.Position;
            if (pos == null || pos.Label != BotLabel || pos.SymbolName != SymbolName)
                return;

            // Limit fill can be worse than planned entry → broker drops invalid SL/TP. Re-apply from fill.
            double slDist = EnsureProtectionOnFill(pos);

            if (slDist > 0)
                ConfigureExitsForTrade(slDist);

            _tradesToday++;
            if (_pendingClusterLevel != 0)
            {
                _hasLastTradedCluster = true;
                _lastTradedClusterLevel = _pendingClusterLevel;
                _lastTradedTol = _pendingTol > 0 ? _pendingTol : StratPipsToPrice(BaseBandPips);
            }

            // Re-read after modify
            pos = Positions.Find(BotLabel, SymbolName) ?? pos;
            _logger.Info(
                $"E_FILL #{pos.Id} {pos.TradeType} entry={pos.EntryPrice:F2} SL={pos.StopLoss} TP={pos.TakeProfit} " +
                $"R={slDist:F4} tradesToday={_tradesToday}");

            ClearPendingState();
        }

        /// <summary>
        /// After limit fill, SL/TP absolute prices from the pending may be invalid
        /// (e.g. sell fill above planned SL). Rebuild SL from structure + min/max band vs fill, TP = TpRR×R.
        /// </summary>
        private double EnsureProtectionOnFill(Position pos)
        {
            double entry = pos.EntryPrice;
            // Prefer last bar effective geometry (ATR floors); fallback to pips
            double minR = _effSlMin > 0 ? _effSlMin : StratPipsToPrice(SlMinPips);
            double maxR = _effSlMax > 0 ? _effSlMax : StratPipsToPrice(SlMaxPips);
            double buf = _effSlBuffer > 0 ? _effSlBuffer : StratPipsToPrice(SlBufferPips);
            if (minR <= 0) minR = StratPipsToPrice(250);
            if (maxR < minR) maxR = minR * 2;

            double sl;
            double r;
            bool isLong = pos.TradeType == TradeType.Buy;

            if (isLong)
            {
                // Structure: below extreme − buffer
                double structSl = _pendingClusterExtreme > 0
                    ? _pendingClusterExtreme - buf
                    : (_pendingSl > 0 ? _pendingSl : entry - minR);
                sl = structSl;
                if (entry - sl < minR)
                    sl = entry - minR;
                if (entry - sl > maxR)
                    sl = entry - maxR;
                if (sl >= entry)
                    sl = entry - minR;
                r = entry - sl;
            }
            else
            {
                double structSl = _pendingClusterExtreme > 0
                    ? _pendingClusterExtreme + buf
                    : (_pendingSl > 0 ? _pendingSl : entry + minR);
                sl = structSl;
                if (sl - entry < minR)
                    sl = entry + minR;
                if (sl - entry > maxR)
                    sl = entry + maxR;
                if (sl <= entry)
                    sl = entry + minR;
                r = sl - entry;
            }

            sl = PriceUtils.NormalizePrice(sl, Symbol);
            double tp = isLong
                ? PriceUtils.NormalizePrice(entry + r * Math.Max(0.5, TpRR), Symbol)
                : PriceUtils.NormalizePrice(entry - r * Math.Max(0.5, TpRR), Symbol);

            // Always re-apply protection from actual fill (limit slip invalidates absolute SL/TP).
            // Skip if already closed (fast TP/SL in backtest → EntityNotFound).
            if (Positions.Find(BotLabel, SymbolName) == null ||
                Positions.FindAll(BotLabel, SymbolName).All(p => p.Id != pos.Id))
            {
                _logger.Debug($"EnsureProtection skip #{pos.Id}: position already closed");
                return r;
            }

            var mod = ModifyPosition(pos, sl, tp, ProtectionType.Absolute);
            if (!mod.IsSuccessful)
            {
                // EntityNotFound = closed between open event and modify (common in tick BT)
                if (mod.Error != null && mod.Error.ToString().IndexOf("EntityNotFound", StringComparison.OrdinalIgnoreCase) >= 0)
                    _logger.Debug($"EnsureProtection #{pos.Id} already gone (likely TP/SL hit)");
                else
                    _logger.Warn($"EnsureProtection failed #{pos.Id}: {mod.Error} want SL={sl:F2} TP={tp:F2}");
            }
            else
                _logger.Info($"EnsureProtection #{pos.Id} SL={sl:F2} TP={tp:F2} R={r:F4} (fill entry={entry:F2})");

            return r;
        }

        private void OnPendingFilled(PendingOrderFilledEventArgs args)
        {
            // Position.Opened handles count / BE config; log only
            if (args.PendingOrder == null || args.PendingOrder.Label != BotLabel)
                return;
            _logger.Debug($"Pending filled id={args.PendingOrder.Id}");
        }

        private void OnPendingCancelled(PendingOrderCancelledEventArgs args)
        {
            if (args.PendingOrder == null || args.PendingOrder.Label != BotLabel)
                return;
            _logger.Debug($"Pending cancelled id={args.PendingOrder.Id} reason={args.Reason}");
            if (!HasBotPending())
                ClearPendingState();
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position == null || args.Position.Label != BotLabel || args.Position.SymbolName != SymbolName)
                return;
            _logger.Info(
                $"CLOSE #{args.Position.Id} net={args.Position.GrossProfit:F2} reason={args.Reason}");
        }

        private void ResetDailyCounters()
        {
            int dayKey = Server.TimeInUtc.Year * 10000 + Server.TimeInUtc.Month * 100 + Server.TimeInUtc.Day;
            if (dayKey == _tradeDayKey)
                return;
            _tradeDayKey = dayKey;
            _tradesToday = 0;
            _rejectCountToday = 0;
            _lastRejectReason = null;
            _logger.Info($"New UTC day {dayKey} — trade/reject counters reset");
        }

        private void LogReject(string reason, SignalContext ctx)
        {
            _rejectCountToday++;
            if (reason == _lastRejectReason && !DebugLogging)
                return;
            _lastRejectReason = reason;
            if (DebugLogging)
                _logger.Debug($"{reason} (rejectsToday={_rejectCountToday})");
            else if (_rejectCountToday <= 3 || _rejectCountToday % 50 == 0)
                _logger.Info($"{reason} (rejectsToday={_rejectCountToday})");
        }
    }
}
