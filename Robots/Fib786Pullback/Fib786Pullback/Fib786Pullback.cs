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
    /// HTF filter policy.
    /// Off = no gate. Align = only with-trend (flat reject) — often hurts deep pullback longs.
    /// BlockCounter = only block clear opposite bias; flat OK (v1.2 default).
    /// </summary>
    public enum HtfFilterMode
    {
        Off = 0,
        Align = 1,
        BlockCounter = 2
    }

    /// <summary>
    /// New HH/LL swing → Fib deep pullback (default 78.6%) + closed-bar confirm → market entry.
    /// v1.2: HTF BlockCounter default (not hard Align). Spec: docs/v1.0/1-prds/PRD-fib786-pullback.md
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class Fib786Pullback : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "Fib786Pullback")]
        public string BotLabel { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk", DefaultValue = LotSizeMode.RiskPercent)]
        public LotSizeMode SizeMode { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.5, MinValue = 0.05, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Trades / Day", Group = "Trade & Risk", DefaultValue = 3, MinValue = 1, MaxValue = 50)]
        public int MaxTradesPerDay { get; set; }

        /// <summary>Max spread. Strategy pips: 100 = 1.0 XAU.</summary>
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

        // ─── Structure / Fib ────────────────────────────────
        [Parameter("Pivot Strength", Group = "Structure", DefaultValue = 5, MinValue = 2, MaxValue = 20)]
        public int PivotStrength { get; set; }

        [Parameter("Lookback Bars", Group = "Structure", DefaultValue = 120, MinValue = 40, MaxValue = 500)]
        public int LookbackBars { get; set; }

        [Parameter("Max Leg Age (bars)", Group = "Structure", DefaultValue = 80, MinValue = 10, MaxValue = 300)]
        public int MaxLegAgeBars { get; set; }

        [Parameter("Min Impulse (×ATR)", Group = "Structure", DefaultValue = 1.5, MinValue = 0.3, MaxValue = 10.0)]
        public double MinImpulseAtr { get; set; }

        [Parameter("Fib Level", Group = "Structure", DefaultValue = 0.786, MinValue = 0.5, MaxValue = 0.95)]
        public double FibLevel { get; set; }

        /// <summary>Half-width of touch zone around Fib. Strategy pips.</summary>
        [Parameter("Zone (pips)", Group = "Structure", DefaultValue = 40.0, MinValue = 1.0)]
        public double ZonePips { get; set; }

        [Parameter("Zone (×ATR)", Group = "Structure", DefaultValue = 0.08, MinValue = 0.0, MaxValue = 1.0)]
        public double ZoneAtrMult { get; set; }

        [Parameter("Cooldown Bars", Group = "Structure", DefaultValue = 10, MinValue = 0, MaxValue = 100)]
        public int CooldownBars { get; set; }

        // ─── HTF bias ───────────────────────────────────────
        /// <summary>
        /// Off / Align (hard with-trend) / BlockCounter (soft — default).
        /// Align + short HTF window made long PF worse on deep 78.6 pullbacks.
        /// </summary>
        [Parameter("HTF Mode", Group = "HTF Bias", DefaultValue = HtfFilterMode.BlockCounter)]
        public HtfFilterMode HtfMode { get; set; }

        [Parameter("HTF Timeframe", Group = "HTF Bias", DefaultValue = "Hour")]
        public TimeFrame HtfTimeframe { get; set; }

        /// <summary>Bias = sign(close[1] − close[1+N]) on HTF. Default 12 ≈ half session on H1.</summary>
        [Parameter("HTF Lookback Bars", Group = "HTF Bias", DefaultValue = 12, MinValue = 1, MaxValue = 50)]
        public int HtfLookbackBars { get; set; }

        /// <summary>Min |close−ref| strategy pips for non-flat. Default 150 (=1.50 XAU) — less noise than 50.</summary>
        [Parameter("HTF Min Move (pips)", Group = "HTF Bias", DefaultValue = 150.0, MinValue = 0.0)]
        public double HtfMinMovePips { get; set; }

        /// <summary>If false, long legs ignore HTF gate (useful when long Align hurt PF).</summary>
        [Parameter("HTF Filter Long", Group = "HTF Bias", DefaultValue = true)]
        public bool HtfFilterLong { get; set; }

        /// <summary>If false, short legs ignore HTF gate.</summary>
        [Parameter("HTF Filter Short", Group = "HTF Bias", DefaultValue = true)]
        public bool HtfFilterShort { get; set; }

        // ─── ATR ────────────────────────────────────────────
        [Parameter("ATR Period", Group = "ATR", DefaultValue = 14, MinValue = 5, MaxValue = 50)]
        public int AtrPeriod { get; set; }

        [Parameter("Use ATR SL Floors", Group = "ATR", DefaultValue = true)]
        public bool UseAtrSlFloors { get; set; }

        // ─── Stop / TP ──────────────────────────────────────
        /// <summary>Beyond impulse origin. Strategy pips (100 = 1.0).</summary>
        [Parameter("SL Buffer (pips)", Group = "Stop Loss", DefaultValue = 40.0, MinValue = 0.0)]
        public double SlBufferPips { get; set; }

        [Parameter("SL Buffer (×ATR)", Group = "Stop Loss", DefaultValue = 0.10, MinValue = 0.0, MaxValue = 2.0)]
        public double SlBufferAtrMult { get; set; }

        [Parameter("SL Min (pips)", Group = "Stop Loss", DefaultValue = 200.0, MinValue = 1.0)]
        public double SlMinPips { get; set; }

        [Parameter("SL Min (×ATR)", Group = "Stop Loss", DefaultValue = 0.7, MinValue = 0.0, MaxValue = 5.0)]
        public double SlMinAtrMult { get; set; }

        [Parameter("SL Max (pips)", Group = "Stop Loss", DefaultValue = 800.0, MinValue = 2.0)]
        public double SlMaxPips { get; set; }

        [Parameter("SL Max (×ATR)", Group = "Stop Loss", DefaultValue = 3.0, MinValue = 0.0, MaxValue = 10.0)]
        public double SlMaxAtrMult { get; set; }

        [Parameter("TP (R)", Group = "Take Profit", DefaultValue = 1.5, MinValue = 0.5, MaxValue = 10.0)]
        public double TpRR { get; set; }

        // ─── BE / Trail ─────────────────────────────────────
        [Parameter("Use Break Even", Group = "Break Even", DefaultValue = true)]
        public bool UseBreakEven { get; set; }

        [Parameter("BE Start (R)", Group = "Break Even", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0)]
        public double BeAtRR { get; set; }

        [Parameter("BE Lock (pips)", Group = "Break Even", DefaultValue = 15.0, MinValue = 0.0)]
        public double BeLockPips { get; set; }

        [Parameter("BE Add Spread", Group = "Break Even", DefaultValue = true)]
        public bool BeAddSpread { get; set; }

        [Parameter("Use Trailing", Group = "Trailing", DefaultValue = true)]
        public bool UseTrailing { get; set; }

        [Parameter("Trail Start (R)", Group = "Trailing", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 20.0)]
        public double TrailStartRR { get; set; }

        [Parameter("Trail Dist (pips)", Group = "Trailing", DefaultValue = 120.0, MinValue = 1.0)]
        public double TrailDistPips { get; set; }

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
        /// <summary>XAU strategy pip: 100 pips = 1.0 price (1 pip = 0.01).</summary>
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

        private int _tradesToday;
        private int _tradeDayKey;
        private DateTime _lastSignalBarTime;
        private int _rejectCountToday;
        private string _lastRejectReason;

        private bool _hasLastTradedLeg;
        private double _lastTradedLegHigh;
        private double _lastTradedLegLow;

        private int _cooldownUntilBarIndex;
        private double _lastEntrySlDist;

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("Fib786Pullback", DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);

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

            _tradesToday = 0;
            _tradeDayKey = -1;
            _lastSignalBarTime = DateTime.MinValue;
            _rejectCountToday = 0;
            _lastRejectReason = null;
            _hasLastTradedLeg = false;
            _cooldownUntilBarIndex = 0;
            _lastEntrySlDist = 0;

            Positions.Opened += OnPositionOpened;
            Positions.Closed += OnPositionClosed;

            _logger.Info(
                $"Started F786 v1.2 {SymbolName} TF={TimeFrame} risk={RiskPercent}% Fib={FibLevel:F3} " +
                $"pivot={PivotStrength} minImp={MinImpulseAtr:F2}×ATR TpRR={TpRR} " +
                $"SL pips={SlMinPips:F0}-{SlMaxPips:F0} buf={SlBufferPips:F0} ATR floors={UseAtrSlFloors} " +
                $"HTF={HtfMode}/{HtfTimeframe}/lb{HtfLookbackBars}/min{HtfMinMovePips:F0}p " +
                $"filterL={HtfFilterLong} filterS={HtfFilterShort} " +
                $"cooldown={CooldownBars} maxDay={MaxTradesPerDay} sessions={_sessionFilter.DescribeEnabled()} " +
                $"maxSpread={MaxSpreadPips:F0}p debug={DebugLogging}");
        }

        /// <summary>
        /// HTF bias from closed bars: sign(close[1] − close[1+lookback]).
        /// Flat if |move| &lt; HtfMinMovePips (strategy pips).
        /// </summary>
        private SignalSide ComputeHtfBias()
        {
            if (HtfMode == HtfFilterMode.Off)
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

            int minBars = Math.Max(LookbackBars, PivotStrength * 2 + 10);
            if (Bars.Count < minBars)
                return;

            int bi = Bars.Count - 2; // last closed
            if (bi < 0)
                return;

            DateTime barTime = Bars.OpenTimes[bi];
            if (barTime <= _lastSignalBarTime)
                return;
            _lastSignalBarTime = barTime;

            if (HasBotPosition())
            {
                _logger.Debug("Skip: busy position");
                return;
            }

            if (!_marketCondition.IsTradingOK())
            {
                LogReject("REJECT:F_SPREAD_PIPS");
                return;
            }

            double atr = 0;
            if (_atr != null && _atr.Result.Count > 1)
                atr = _atr.Result.Last(1);
            if (atr <= 0)
                atr = StratPipsToPrice(200);

            double slBuffer = FloorDist(SlBufferPips, SlBufferAtrMult, atr);
            double slMin = FloorDist(SlMinPips, SlMinAtrMult, atr);
            double slMax = FloorDist(SlMaxPips, SlMaxAtrMult, atr);
            if (slMax < slMin + StratPipsToPrice(40))
                slMax = slMin + StratPipsToPrice(40);

            double zoneHalf = Math.Max(StratPipsToPrice(ZonePips), atr * ZoneAtrMult);

            int cooldownLeft = 0;
            if (CooldownBars > 0 && Bars.Count - 1 < _cooldownUntilBarIndex)
                cooldownLeft = _cooldownUntilBarIndex - (Bars.Count - 1);

            var htfBias = ComputeHtfBias();

            var ctx = new SignalContext
            {
                Bars = BuildBarSnaps(),
                SpreadPrice = Symbol.Spread,
                MaxSpreadPrice = StratPipsToPrice(MaxSpreadPips),
                Atr = atr,
                SessionOk = _sessionFilter.IsTradingAllowed(Server.TimeInUtc),
                NewsOk = _newsFilter.IsTradingAllowed(Server.TimeInUtc),
                EquityOk = _riskManager.IsTradingAllowed(Account.Equity, Server.TimeInUtc),
                HasOpenPosition = HasBotPosition(),
                TradesToday = _tradesToday,
                MaxTradesPerDay = MaxTradesPerDay,
                CooldownBarsLeft = cooldownLeft,
                PivotStrength = PivotStrength,
                LookbackBars = LookbackBars,
                MaxLegAgeBars = MaxLegAgeBars,
                MinImpulseAtr = MinImpulseAtr,
                FibLevel = FibLevel,
                ZoneHalf = zoneHalf,
                SlBuffer = slBuffer,
                SlMin = slMin,
                SlMax = slMax,
                HasLastTradedLeg = _hasLastTradedLeg,
                LastTradedLegHigh = _lastTradedLegHigh,
                LastTradedLegLow = _lastTradedLegLow,
                HtfMode = (int)HtfMode,
                HtfBias = htfBias,
                HtfFilterLong = HtfFilterLong,
                HtfFilterShort = HtfFilterShort
            };

            var result = _signalEngine.Evaluate(ctx);
            if (!result.IsValid)
            {
                LogReject(result.Reason);
                return;
            }

            _logger.Info(
                $"{result.Reason} legH={result.LegHigh:F2} legL={result.LegLow:F2} L786={result.FibLevelPrice:F2} " +
                $"imp={result.ImpulseSize:F2} atr={atr:F2} htfBias={htfBias} entry≈{result.Entry:F2} sl={result.StopLoss:F2} " +
                $"R={result.SlDist:F4} extShift={result.ExtremeShift} orgShift={result.OriginShift}");

            if (!EnableTrading)
            {
                _logger.Info("Dry-run (EnableTrading=false) — market skipped");
                // Still mark leg so dry-run does not spam every bar
                _hasLastTradedLeg = true;
                _lastTradedLegHigh = result.LegHigh;
                _lastTradedLegLow = result.LegLow;
                return;
            }

            ExecuteFromSignal(result);
        }

        private void ExecuteFromSignal(SignalResult signal)
        {
            // Live entry uses current ask/bid; recompute SL from structure + live price
            bool isLong = signal.Side == SignalSide.Long;
            double entry = isLong ? Symbol.Ask : Symbol.Bid;
            double sl = signal.StopLoss;

            double slDist;
            if (isLong)
            {
                if (entry - sl < StratPipsToPrice(SlMinPips) * 0.5)
                    sl = entry - signal.SlDist;
                if (sl >= entry)
                    sl = entry - signal.SlDist;
                slDist = entry - sl;
            }
            else
            {
                if (sl - entry < StratPipsToPrice(SlMinPips) * 0.5)
                    sl = entry + signal.SlDist;
                if (sl <= entry)
                    sl = entry + signal.SlDist;
                slDist = sl - entry;
            }

            if (slDist <= 0)
            {
                _logger.Warn("Execute aborted: R<=0");
                return;
            }

            sl = PriceUtils.NormalizePrice(sl, Symbol);
            double tpDist = slDist * Math.Max(0.5, TpRR);
            double tp = isLong
                ? PriceUtils.NormalizePrice(entry + tpDist, Symbol)
                : PriceUtils.NormalizePrice(entry - tpDist, Symbol);

            // API takes SL/TP in broker pips (same pattern as other Robots — absolute price via Modify after fill).
            double slPips = PriceUtils.PriceToPips(slDist, Symbol);
            double tpPips = PriceUtils.PriceToPips(tpDist, Symbol);
            if (slPips < 0.1)
            {
                _logger.Warn($"Execute aborted: slPips={slPips:F2} too small");
                return;
            }

            double dailyPnl = _riskManager.GetDailyPnlMoney(Account.Equity);
            double dailyRoom = _riskManager.GetRemainingDailyLossBudget(Account.Equity);
            if (_riskManager.IsDailyLossLimitEnabled && dailyRoom <= 1.0)
            {
                _logger.Warn($"Execute aborted: daily loss room exhausted (pnl=${dailyPnl:F0})");
                return;
            }

            if (!TryComputeVolume(slDist, dailyRoom, out double volume, out double expectedRisk, out string sizeNote))
            {
                _logger.Warn($"Execute aborted: volume — {sizeNote}");
                return;
            }

            TradeType tradeType = isLong ? TradeType.Buy : TradeType.Sell;
            // Match HvnMagnet/PmLh/SvbsX call shape (sl/tp then comment, hasTrailingStop).
            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, sl, tp, null, false);

            if (!result.IsSuccessful)
            {
                _logger.Error($"ExecuteMarket failed: {result.Error}");
                return;
            }

            // Re-apply absolute SL/TP from structure (fill price may differ from signal close).
            if (result.Position != null)
            {
                var pos = result.Position;
                double fill = pos.EntryPrice;
                double absSl = isLong
                    ? PriceUtils.NormalizePrice(fill - slDist, Symbol)
                    : PriceUtils.NormalizePrice(fill + slDist, Symbol);
                double absTp = isLong
                    ? PriceUtils.NormalizePrice(fill + tpDist, Symbol)
                    : PriceUtils.NormalizePrice(fill - tpDist, Symbol);
                var mod = ModifyPosition(pos, absSl, absTp, ProtectionType.Absolute);
                if (!mod.IsSuccessful)
                    _logger.Warn($"Modify SL/TP failed: {mod.Error} want SL={absSl:F2} TP={absTp:F2}");
                else
                {
                    sl = absSl;
                    tp = absTp;
                    entry = fill;
                }
            }

            _lastEntrySlDist = slDist;
            _hasLastTradedLeg = true;
            _lastTradedLegHigh = signal.LegHigh;
            _lastTradedLegLow = signal.LegLow;

            if (CooldownBars > 0)
                _cooldownUntilBarIndex = Bars.Count - 1 + CooldownBars;

            ConfigureExitsForTrade(slDist);

            _logger.Info(
                $"E_MKT {tradeType} entry={entry:F2} SL={sl:F2} TP={tp:F2} R={slDist:F4} " +
                $"slPips={slPips:F1} tpPips={tpPips:F1} TpRR={TpRR} " +
                $"vol={volume} size={sizeNote} risk≈${expectedRisk:F0} L786={signal.FibLevelPrice:F2}");
        }

        private List<BarSnap> BuildBarSnaps()
        {
            int need = Math.Max(LookbackBars, PivotStrength * 2 + 10);
            var list = new List<BarSnap>(need);
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

        private double FloorDist(double pips, double atrMult, double atr)
        {
            double fromPips = StratPipsToPrice(pips);
            if (!UseAtrSlFloors || atrMult <= 0)
                return fromPips;
            return Math.Max(fromPips, atr * atrMult);
        }

        private static double StratPipsToPrice(double pips) => pips * StratPipPrice;

        private double StratPipsToBrokerPips(double stratPips)
        {
            if (stratPips <= 0) return 0;
            return PriceUtils.PriceToPips(StratPipsToPrice(stratPips), Symbol);
        }

        private void ConfigureExitsForTrade(double slDistPrice)
        {
            if (slDistPrice <= 0) return;

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

        private bool TryComputeVolume(double slDist, double dailyRoom,
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
                    expectedRisk = volume * slDist;

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

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            var pos = args.Position;
            if (pos == null || pos.Label != BotLabel || pos.SymbolName != SymbolName)
                return;

            _tradesToday++;
            double slDist = _lastEntrySlDist;
            if (pos.StopLoss.HasValue)
                slDist = Math.Abs(pos.EntryPrice - pos.StopLoss.Value);
            if (slDist > 0)
                ConfigureExitsForTrade(slDist);

            if (CooldownBars > 0)
                _cooldownUntilBarIndex = Bars.Count - 1 + CooldownBars;

            _logger.Info(
                $"E_FILL #{pos.Id} {pos.TradeType} entry={pos.EntryPrice:F2} SL={pos.StopLoss} TP={pos.TakeProfit} " +
                $"R={slDist:F4} tradesToday={_tradesToday}");
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

        private void LogReject(string reason)
        {
            _rejectCountToday++;
            if (reason == _lastRejectReason && !DebugLogging)
                return;
            _lastRejectReason = reason;
            if (DebugLogging)
                _logger.Debug($"{reason} (rejectsToday={_rejectCountToday})");
            else if (reason != null && reason.StartsWith("E_", StringComparison.Ordinal))
                _logger.Info(reason);
            // Non-debug: suppress spammy REJECT:F_* (already de-duped once)
        }
    }
}
