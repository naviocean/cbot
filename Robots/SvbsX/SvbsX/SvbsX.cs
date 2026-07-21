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
    /// SVBS-X: Session VA Expansion on XAU — break prior session VA → acceptance → full-size BE+trail.
    /// Partial close is forbidden (ADR-001).
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SvbsX : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "SvbsX")]
        public string BotLabel { get; set; }

        [Parameter("Position Size Mode", Group = "Trade & Risk", DefaultValue = SvbsSizeMode.RiskPercent)]
        public SvbsSizeMode SizeMode { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.75, MinValue = 0.1, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        /// <summary>Lots when Size Mode = FixedLots (1.0 lot = Symbol.LotSize units).</summary>
        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Trades / Day", Group = "Trade & Risk", DefaultValue = 2, MinValue = 1, MaxValue = 10)]
        public int MaxTradesPerDay { get; set; }

        /// <summary>
        /// Max loss today in account currency vs equity at UTC day start. 0 = off.
        /// Blocks new entries when equity ≤ dayStart − this amount.
        /// </summary>
        [Parameter("Max Daily Loss ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyLossAmount { get; set; }

        [Parameter("Flatten On Daily Loss", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnDailyLoss { get; set; }

        /// <summary>
        /// Daily profit target in account currency. 0 = off.
        /// Blocks new entries when equity ≥ dayStart + this amount.
        /// </summary>
        [Parameter("Max Daily Profit ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyProfitAmount { get; set; }

        [Parameter("Flatten On Daily Profit", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnDailyProfit { get; set; }

        [Parameter("Max Equity DD %", Group = "Trade & Risk", DefaultValue = 12.0, MinValue = 0.0)]
        public double MaxEquityDrawdownPct { get; set; }

        [Parameter("Flatten On Equity DD", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnEquityDd { get; set; }

        [Parameter("Max Spread (pips)", Group = "Trade & Risk", DefaultValue = 80.0, MinValue = 0.1)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Debug Logging", Group = "Trade & Risk", DefaultValue = false)]
        public bool DebugLogging { get; set; }

        // ─── Session (fixed hours in SessionClock / CSessionFilter; enable only) ───
        // Asia 00–09 | London 07–16 | NY 13:30–23 | Overlap 13–16  (UTC)
        [Parameter("Trade Asia", Group = "Session", DefaultValue = false)]
        public bool TradeAsia { get; set; }

        [Parameter("Trade London", Group = "Session", DefaultValue = true)]
        public bool TradeLondon { get; set; }

        [Parameter("Trade New York", Group = "Session", DefaultValue = true)]
        public bool TradeNewYork { get; set; }

        [Parameter("Trade Overlap (Lon-NY)", Group = "Session", DefaultValue = true)]
        public bool TradeOverlap { get; set; }

        // ─── Signal ─────────────────────────────────────────
        [Parameter("Accept Mode", Group = "Signal", DefaultValue = SvbsAcceptMode.BreakConfirm)]
        public SvbsAcceptMode AcceptMode { get; set; }

        [Parameter("Accept Timeout Bars", Group = "Signal", DefaultValue = 24, MinValue = 3, MaxValue = 48)]
        public int AcceptTimeoutBars { get; set; }

        /// <summary>
        /// Require tick-volume surge on break and/or accept bar vs prior SMA.
        /// XAU M5 tick volume is noisy — keep k near 1.1–1.3; 1.6+ often yields zero entries.
        /// </summary>
        [Parameter("Use Volume Filter", Group = "Signal", DefaultValue = false)]
        public bool UseVolumeFilter { get; set; }

        [Parameter("Volume k", Group = "Signal", DefaultValue = 1.15, MinValue = 1.0, MaxValue = 5.0)]
        public double VolumeK { get; set; }

        [Parameter("Volume SMA Period", Group = "Signal", DefaultValue = 20, MinValue = 5)]
        public int VolumeSmaPeriod { get; set; }

        [Parameter("Use Volume Median", Group = "Signal", DefaultValue = false)]
        public bool UseVolumeMedian { get; set; }

        [Parameter("Use POC Filter", Group = "Signal", DefaultValue = false)]
        public bool UsePocFilter { get; set; }

        [Parameter("POC Lookback Bars", Group = "Signal", DefaultValue = 6, MinValue = 2, MaxValue = 30)]
        public int PocLookbackBars { get; set; }

        [Parameter("POC Mid Offset ×VA", Group = "Signal", DefaultValue = 0.15, MinValue = 0.0, MaxValue = 0.5)]
        public double PocMidOffset { get; set; }

        [Parameter("Body Min Ratio", Group = "Signal", DefaultValue = 0.35, MinValue = 0.1, MaxValue = 0.9)]
        public double BodyMinRatio { get; set; }

        [Parameter("Retest ATR Mult", Group = "Signal", DefaultValue = 0.3, MinValue = 0.0, MaxValue = 2.0)]
        public double RetestAtrMult { get; set; }

        [Parameter("Min VA Width $", Group = "Signal", DefaultValue = 4.0, MinValue = 0.5)]
        public double MinVaWidth { get; set; }

        /// <summary>Raise if Asia VA often ~$30–40 on XAU (old 35 blocked fat sessions).</summary>
        [Parameter("Max VA Width $", Group = "Signal", DefaultValue = 50.0, MinValue = 5.0)]
        public double MaxVaWidth { get; set; }

        // ─── Volume Profile ─────────────────────────────────
        [Parameter("Bin Size $", Group = "Volume Profile", DefaultValue = 0.5, MinValue = 0.1)]
        public double BinSize { get; set; }

        [Parameter("Value Area %", Group = "Volume Profile", DefaultValue = 70.0, MinValue = 50.0, MaxValue = 90.0)]
        public double ValueAreaPercent { get; set; }

        [Parameter("Visualize Profile", Group = "Volume Profile", DefaultValue = false)]
        public bool VisualizeProfile { get; set; }

        // ─── Stop / Exit ────────────────────────────────────
        // SL = structure (VA / retest) ± buffer; distances expressed in ×ATR so you don't guess $ on gold.
        [Parameter("ATR Period", Group = "Stop Loss", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        /// <summary>Buffer beyond VAH/retest (or VAL/retest): structureSL = anchor ± ATR×this.</summary>
        [Parameter("SL buffer (×ATR)", Group = "Stop Loss", DefaultValue = 0.35, MinValue = 0.0, MaxValue = 3.0)]
        public double SlBufferAtrMult { get; set; }

        /// <summary>
        /// Floor: minimum entry→SL distance in ATR. If structure is tighter, SL is widened to this.
        /// Example 0.8 on ATR≈$3 → min stop ~$2.4 (adapts with volatility).
        /// </summary>
        [Parameter("Min SL (×ATR)", Group = "Stop Loss", DefaultValue = 0.8, MinValue = 0.1, MaxValue = 5.0)]
        public double MinSlAtrMult { get; set; }

        /// <summary>
        /// Cap: if structure SL wider than ATR×this → skip trade (no entry).
        /// 0 = disable cap.
        /// </summary>
        [Parameter("Max SL (×ATR)", Group = "Stop Loss", DefaultValue = 3.0, MinValue = 0.0, MaxValue = 10.0)]
        public double MaxSlAtrMult { get; set; }

        /// <summary>
        /// Skip entry if price has already run more than this ×ATR beyond VAH/VAL (late chase).
        /// Prevents slDist exploding when BreakConfirm fires far from the VA edge.
        /// </summary>
        [Parameter("Max Entry Ext (×ATR)", Group = "Stop Loss", DefaultValue = 1.5, MinValue = 0.0, MaxValue = 5.0)]
        public double MaxEntryExtensionAtr { get; set; }

        /// <summary>Hard TP distance in R (× initial SL). 0 = no fixed TP (exit via BE/trail/session only).</summary>
        [Parameter("TP RR Multiple", Group = "Take Profit", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10.0)]
        public double TpRrMultiple { get; set; }

        [Parameter("Use Break Even", Group = "Break Even", DefaultValue = true)]
        public bool UseBreakEven { get; set; }

        [Parameter("BE Start (R)", Group = "Break Even", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0)]
        public double BeStartR { get; set; }

        [Parameter("BE Lock (R)", Group = "Break Even", DefaultValue = 0.05, MinValue = 0.0, MaxValue = 1.0)]
        public double BeLockR { get; set; }

        [Parameter("BE Add Spread", Group = "Break Even", DefaultValue = true)]
        public bool BeAddSpread { get; set; }

        [Parameter("Use Trailing", Group = "Trailing", DefaultValue = true)]
        public bool UseTrailing { get; set; }

        [Parameter("Trail Start (R)", Group = "Trailing", DefaultValue = 1.5, MinValue = 0.1, MaxValue = 10.0)]
        public double TrailStartR { get; set; }

        /// <summary>Trail distance behind price = this × initial SL distance (1R).</summary>
        [Parameter("Trail Step (R)", Group = "Trailing", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 5.0)]
        public double TrailStepR { get; set; }

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
        private CRiskManager _riskManager;
        private CTrailingManager _trailingManager;
        private CMarketCondition _marketCondition;
        private CNewsFilter _newsFilter;
        private SignalEngine _signalEngine;
        private SessionClock _clock;
        private CSessionFilter _sessionFilter;
        private AverageTrueRange _atr;

        private ProfileData _asiaProfile;
        private ProfileData _londonProfile;
        private ProfileData _asiaProfilePrevDay;
        private int _asiaFreezeDayKey = -1;
        private int _londonFreezeDayKey = -1;

        private int _tradesToday;
        private int _tradeDayKey = -1;
        private double _dayStartEquity;
        private DateTime _lastSignalBarTime = DateTime.MinValue;
        private int _rejectCountToday;
        private string _lastReject;

        /// <summary>PositionId → entry window + entry time + initial R price for time-stop.</summary>
        private readonly Dictionary<int, OpenTradeMeta> _openMeta = new Dictionary<int, OpenTradeMeta>();

        private sealed class OpenTradeMeta
        {
            public SvbsEntryWindow Window;
            public DateTime EntryUtc;
            public double SlDistPrice;
            public double EntryPrice;
        }

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("SvbsX", DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);

            if (!IsGoldSymbol(SymbolName))
                _logger.Warn($"Symbol {SymbolName} is not XAU/GOLD — F1 will block entries");

            _clock = new SessionClock();

            _sessionFilter = new CSessionFilter();
            _sessionFilter.Init(TradeAsia, TradeLondon, TradeNewYork, TradeOverlap, _logger);
            if (!TradeAsia && !TradeLondon && !TradeNewYork && !TradeOverlap)
                _logger.Warn("No session enabled — bot will never enter");

            _volumeProfile = new CVolumeProfile();
            _volumeProfile.Init(Bars, Chart, 100, VisualizeProfile, _logger);
            _volumeProfile.ConfigureComposite(
                BinSize,
                4,
                ValueAreaPercent / 100.0,
                0.65,
                1.5,
                0.8,
                1.25,
                1.25,
                25.0,
                true);

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

            _asiaProfile = new ProfileData { IsValid = false };
            _londonProfile = new ProfileData { IsValid = false };
            _asiaProfilePrevDay = new ProfileData { IsValid = false };
            _tradesToday = 0;
            _dayStartEquity = Account.Equity;

            Positions.Closed += OnPositionClosed;

            TryFreezeProfiles(Server.TimeInUtc);

            string sizeDesc = SizeMode == SvbsSizeMode.FixedLots
                ? $"fixedLots={FixedLots}"
                : $"risk={RiskPercent}%";
            _logger.Info(
                $"Started {SymbolName} TF={TimeFrame} size={sizeDesc} " +
                $"accept={AcceptMode} timeoutBars={AcceptTimeoutBars} " +
                $"volFilter={UseVolumeFilter} k={VolumeK} pocFilter={UsePocFilter} " +
                $"BE={UseBreakEven} Trail={UseTrailing} TP={(TpRrMultiple > 0 ? $"{TpRrMultiple:F1}R" : "off")} " +
                $"sessions={_sessionFilter.DescribeEnabled()} debug={DebugLogging}");
            _logger.Info($"Clocks(fixed UTC): {_clock.DescribeFixed()}");
            if (AcceptMode == SvbsAcceptMode.RetestHold && AcceptTimeoutBars <= 12)
                _logger.Warn(
                    "RetestHold + short timeout often → C_TIMEOUT on XAU. " +
                    "Prefer Accept Mode=BreakConfirm, Timeout≥24, Use Volume Filter=false " +
                    "(or Reset instance parameters after rebuild).");
        }

        protected override void OnTick()
        {
            _riskManager.OnTick();

            if (UseBreakEven || UseTrailing)
                _trailingManager.OnTick();

            ManageTimeAndSessionExits();
            // Daily loss/profit $ gates + optional flatten: CRiskManager.OnTick()
        }

        protected override void OnBar()
        {
            DateTime utc = Server.TimeInUtc;
            ResetDailyCounters(utc);
            TryFreezeProfiles(utc);

            if (Bars.Count < VolumeSmaPeriod + 5)
                return;

            int bi = Bars.Count - 2;
            DateTime barTime = Bars.OpenTimes[bi];
            if (barTime <= _lastSignalBarTime)
                return;
            _lastSignalBarTime = barTime;

            // Session flat also on bar (catch if no ticks)
            ManageTimeAndSessionExits();

            var window = _clock.ResolveWindow(utc, TradeAsia, TradeLondon, TradeNewYork, TradeOverlap);
            // Also require generic session OR (same toggles / fixed hours as other bots)
            if (!_sessionFilter.IsTradingAllowed(utc))
                window = SvbsEntryWindow.None;

            if (window == SvbsEntryWindow.None && _signalEngine.State.Phase == SvbsPhase.AcceptWait)
            {
                _signalEngine.Reset();
                _logger.Debug("Setup cancelled: C_WINDOW (outside entry)");
            }

            if (Positions.FindAll(BotLabel, SymbolName).Length > 0)
                return;

            var prior = GetPriorProfile(window, utc);
            double atr = _atr != null && _atr.Result.Count > 1 ? _atr.Result.Last(1) : Symbol.PipSize * 100;

            GetDevelopingPoc(utc, window, bi, out double pocNow, out double pocShift, out bool pocOk);

            // Baseline excludes current bar so a surge bar is not diluted into its own SMA
            double volSma = ComputeVolumeSmaPrior(bi, VolumeSmaPeriod);
            double volMed = ComputeVolumeMedianPrior(bi, VolumeSmaPeriod);

            var ctx = new SignalContext
            {
                BarIndex = bi,
                BarTime = barTime,
                UtcNow = utc,
                Open = Bars.OpenPrices[bi],
                High = Bars.HighPrices[bi],
                Low = Bars.LowPrices[bi],
                Close = Bars.ClosePrices[bi],
                PrevClose = bi > 0 ? Bars.ClosePrices[bi - 1] : Bars.ClosePrices[bi],
                Volume = Bars.TickVolumes[bi],
                VolumeSma = volSma,
                VolumeMedian = volMed,
                Atr = atr,
                PriorProfile = prior,
                DevelopingPocNow = pocNow,
                DevelopingPocShift = pocShift,
                DevelopingPocValid = pocOk,
                EntryWindow = window,
                AcceptMode = AcceptMode,
                AcceptTimeoutBars = AcceptTimeoutBars,
                VolumeK = VolumeK,
                UseVolumeMedian = UseVolumeMedian,
                UseVolumeFilter = UseVolumeFilter,
                UsePocFilter = UsePocFilter,
                BodyMinRatio = BodyMinRatio,
                RetestAtrMult = RetestAtrMult,
                PocMidOffset = PocMidOffset,
                MinVaWidth = MinVaWidth,
                MaxVaWidth = MaxVaWidth,
                SymbolOk = IsGoldSymbol(SymbolName),
                SpreadOk = _marketCondition.IsTradingOK(),
                NewsOk = _newsFilter.IsTradingAllowed(utc),
                // Equity DD + daily loss/profit $ (RiskManager)
                RiskOk = _riskManager.CanOpenNewTrade,
                DailyLossOk = _riskManager.CanOpenNewTrade,
                TradesToday = _tradesToday,
                MaxTradesPerDay = MaxTradesPerDay,
                HasOpenPosition = Positions.FindAll(BotLabel, SymbolName).Length > 0
            };

            var result = _signalEngine.Evaluate(ctx);
            if (!result.IsEntry)
            {
                LogReject(result);
                return;
            }

            _logger.Info(
                $"{result.Reason} win={result.Window} VAH={result.VAH:F2} VAL={result.VAL:F2} " +
                $"retest={result.RetestExtreme:F2} volRatio={result.VolumeRatio:F2}");

            if (!EnableTrading)
            {
                _logger.Info("Dry-run (EnableTrading=false) — order skipped");
                return;
            }

            ExecuteEntry(result, atr);
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnPositionClosed;
            _logger?.Info("SvbsX stopped");
        }

        // ─── Profiles ───────────────────────────────────────

        private void TryFreezeProfiles(DateTime utc)
        {
            int dayKey = utc.Year * 1000 + utc.DayOfYear;

            // Retry same day if previous freeze had 0 bars (weekend / data gap)
            bool needAsia = _clock.CanFreezeAsia(utc)
                            && (_asiaFreezeDayKey != dayKey || _asiaProfile == null || !_asiaProfile.IsValid);
            if (needAsia)
            {
                if (_asiaProfile != null && _asiaProfile.IsValid && _asiaFreezeDayKey != dayKey)
                    _asiaProfilePrevDay = _asiaProfile;

                var start = _clock.AsiaStart(utc);
                var end = _clock.AsiaEnd(utc);
                var built = _volumeProfile.BuildRange(start, end, updateLastProfile: true, draw: VisualizeProfile);
                if (built != null && built.IsValid)
                {
                    _asiaProfile = built;
                    _asiaFreezeDayKey = dayKey;
                    _logger.Info(
                        $"PROFILE Asia VAH={_asiaProfile.VAH:F2} VAL={_asiaProfile.VAL:F2} " +
                        $"POC={_asiaProfile.POC:F2} w={_asiaProfile.VAH - _asiaProfile.VAL:F2} bars={_asiaProfile.BarsUsed}");
                }
                else if (_asiaFreezeDayKey != dayKey)
                {
                    // Only mark day attempted once invalid is "final" after session should have data
                    if (utc >= end.AddHours(1))
                    {
                        _asiaFreezeDayKey = dayKey;
                        _asiaProfile = built ?? new ProfileData { IsValid = false };
                        _logger.Warn($"PROFILE Asia invalid bars={built?.BarsUsed ?? 0}");
                    }
                }
            }

            bool needLondon = _clock.CanFreezeLondon(utc)
                              && (_londonFreezeDayKey != dayKey || _londonProfile == null || !_londonProfile.IsValid);
            if (needLondon)
            {
                var start = _clock.AsiaEnd(utc);
                var end = _clock.LondonProfileEnd(utc);
                var built = _volumeProfile.BuildRange(start, end, updateLastProfile: true, draw: VisualizeProfile);
                if (built != null && built.IsValid)
                {
                    _londonProfile = built;
                    _londonFreezeDayKey = dayKey;
                    _logger.Info(
                        $"PROFILE London VAH={_londonProfile.VAH:F2} VAL={_londonProfile.VAL:F2} " +
                        $"POC={_londonProfile.POC:F2} w={_londonProfile.VAH - _londonProfile.VAL:F2} bars={_londonProfile.BarsUsed}");
                }
                else if (_londonFreezeDayKey != dayKey && utc >= end.AddHours(1))
                {
                    _londonFreezeDayKey = dayKey;
                    _londonProfile = built ?? new ProfileData { IsValid = false };
                    _logger.Warn($"PROFILE London invalid bars={built?.BarsUsed ?? 0}");
                }
            }
        }

        private ProfileData GetPriorProfile(SvbsEntryWindow window, DateTime utc)
        {
            switch (window)
            {
                case SvbsEntryWindow.AsiaToLondon:
                    // Need today's Asia freeze (after 07:00). Before freeze, invalid.
                    return _asiaProfile;

                case SvbsEntryWindow.LondonToNy:
                    return _londonProfile;

                case SvbsEntryWindow.AsiaSession:
                    // Prior completed Asia only — NOT "always PrevDay stash".
                    // Before today's 07:00 freeze: _asiaProfile still holds yesterday (D-1).
                    // After freeze: _asiaProfile = today (D), prior day is _asiaProfilePrevDay.
                    // Bug if always return PrevDay: before 07:00 that is D-2.
                    {
                        int dayKey = utc.Year * 1000 + utc.DayOfYear;
                        if (_asiaFreezeDayKey == dayKey)
                        {
                            if (_asiaProfilePrevDay != null && _asiaProfilePrevDay.IsValid)
                                return _asiaProfilePrevDay;
                        }
                        else
                        {
                            if (_asiaProfile != null && _asiaProfile.IsValid)
                                return _asiaProfile;
                        }
                        return BuildYesterdayAsia(utc);
                    }

                default:
                    return null;
            }
        }

        private ProfileData BuildYesterdayAsia(DateTime utc)
        {
            var y = utc.Date.AddDays(-1);
            var start = new DateTime(y.Year, y.Month, y.Day, SessionClock.AsiaStartHour, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(y.Year, y.Month, y.Day, SessionClock.AsiaEndHour, 0, 0, DateTimeKind.Utc);
            var p = _volumeProfile.BuildRange(start, end, updateLastProfile: false, draw: false);
            if (p == null || !p.IsValid)
                return p;

            int todayKey = utc.Year * 1000 + utc.DayOfYear;
            if (_asiaFreezeDayKey == todayKey)
            {
                // Today already frozen → yesterday belongs in prev stash
                _asiaProfilePrevDay = p;
            }
            else
            {
                // Before today's freeze → this is the live "last completed Asia"
                _asiaProfile = p;
                _asiaFreezeDayKey = y.Year * 1000 + y.DayOfYear;
            }
            return p;
        }

        private void GetDevelopingPoc(DateTime utc, SvbsEntryWindow window, int bi,
            out double pocNow, out double pocShift, out bool ok)
        {
            pocNow = 0;
            pocShift = 0;
            ok = false;

            if (window == SvbsEntryWindow.None)
                return;

            DateTime winStart = _clock.DevelopingWindowStart(utc, window);
            // BuildRange: open times in [start, end) — end exclusive via next bar open
            DateTime endExclusive = bi + 1 < Bars.Count ? Bars.OpenTimes[bi + 1] : Bars.OpenTimes[bi].AddMinutes(5);

            var nowProf = _volumeProfile.BuildRange(winStart, endExclusive, updateLastProfile: false, draw: false);
            if (nowProf == null || !nowProf.IsValid)
                return;

            pocNow = nowProf.POC;

            int shiftIdx = bi - PocLookbackBars;
            if (shiftIdx < 0)
                return;

            DateTime endShift = shiftIdx + 1 < Bars.Count
                ? Bars.OpenTimes[shiftIdx + 1]
                : Bars.OpenTimes[shiftIdx].AddMinutes(5);

            if (endShift <= winStart)
                return;

            var shiftProf = _volumeProfile.BuildRange(winStart, endShift, updateLastProfile: false, draw: false);
            if (shiftProf == null || !shiftProf.IsValid)
                return;

            pocShift = shiftProf.POC;
            ok = true;
        }

        // ─── Entry ──────────────────────────────────────────

        private void ExecuteEntry(SignalResult signal, double atr)
        {
            bool isLong = signal.Side == SvbsSide.Long;
            TradeType tradeType = isLong ? TradeType.Buy : TradeType.Sell;
            double entry = isLong ? Symbol.Ask : Symbol.Bid;

            if (atr <= 0)
            {
                _logger.Warn("Execute aborted: ATR invalid");
                return;
            }

            double buffer = Math.Max(0, atr * SlBufferAtrMult);
            double minSlDist = atr * Math.Max(0.1, MinSlAtrMult);
            double maxSlDist = MaxSlAtrMult > 0 ? atr * MaxSlAtrMult : double.MaxValue;

            // Late chase: BreakConfirm can fire when price already ran far past VA → huge SL to edge
            if (MaxEntryExtensionAtr > 0)
            {
                double maxExt = MaxEntryExtensionAtr * atr;
                if (isLong && entry > signal.VAH + maxExt)
                {
                    _logger.Warn(
                        $"Execute aborted: E_CHASE long entry={entry:F2} VAH={signal.VAH:F2} " +
                        $"ext={(entry - signal.VAH) / atr:F2}×ATR > {MaxEntryExtensionAtr:F2}");
                    return;
                }
                if (!isLong && entry < signal.VAL - maxExt)
                {
                    _logger.Warn(
                        $"Execute aborted: E_CHASE short entry={entry:F2} VAL={signal.VAL:F2} " +
                        $"ext={(signal.VAL - entry) / atr:F2}×ATR > {MaxEntryExtensionAtr:F2}");
                    return;
                }
            }

            // Structure SL at VA edge. Retest extreme only if inside [VAL, VAH].
            double structureSl;
            if (isLong)
            {
                double anchor = signal.VAH;
                if (signal.RetestExtreme < signal.VAH && signal.RetestExtreme >= signal.VAL)
                    anchor = signal.RetestExtreme;
                structureSl = anchor - buffer;
            }
            else
            {
                double anchor = signal.VAL;
                if (signal.RetestExtreme > signal.VAL && signal.RetestExtreme <= signal.VAH)
                    anchor = signal.RetestExtreme;
                structureSl = anchor + buffer;
            }

            double slDist = Math.Abs(entry - structureSl);
            if (slDist < minSlDist)
            {
                structureSl = isLong ? entry - minSlDist : entry + minSlDist;
                slDist = minSlDist;
            }

            if (slDist > maxSlDist)
            {
                _logger.Warn(
                    $"Execute aborted: X1_SL_CAP slDist={slDist:F2} ({slDist / atr:F2}×ATR) " +
                    $"> max={MaxSlAtrMult:F2}×ATR entry={entry:F2} structSL={structureSl:F2}");
                return;
            }

            structureSl = PriceUtils.NormalizePrice(structureSl, Symbol);
            slDist = Math.Abs(entry - structureSl);
            if (slDist <= 0)
            {
                _logger.Warn("Execute aborted: invalid SL distance");
                return;
            }

            double? tp = null;
            if (TpRrMultiple > 0)
            {
                double tpDist = slDist * TpRrMultiple;
                tp = PriceUtils.NormalizePrice(isLong ? entry + tpDist : entry - tpDist, Symbol);
            }

            if (!TrySizeVolume(slDist, out double volume, out double expectedRisk, out string sizeNote))
                return;

            // Comment stores entry window for restart recovery (session flat)
            string comment = signal.Window.ToString();

            // Full size only — never partial
            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, structureSl, tp, comment, false);
            if (!result.IsSuccessful || result.Position == null)
            {
                _logger.Error($"Order failed: {result.Error}");
                return;
            }

            ConfigureExitsForTrade(slDist);

            var pos = result.Position;
            double riskAtSl = EstimateRiskMoney(Math.Abs(pos.EntryPrice - structureSl), pos.VolumeInUnits);
            DateTime entryUtc = pos.EntryTime.Kind == DateTimeKind.Utc
                ? pos.EntryTime
                : pos.EntryTime.ToUniversalTime();
            _openMeta[pos.Id] = new OpenTradeMeta
            {
                Window = signal.Window,
                EntryUtc = entryUtc,
                SlDistPrice = Math.Abs(pos.EntryPrice - structureSl),
                EntryPrice = pos.EntryPrice
            };

            _tradesToday++;
            _logger.Info(
                $"OPEN {tradeType} #{pos.Id} vol={pos.VolumeInUnits} entry={pos.EntryPrice:F2} SL={structureSl:F2} " +
                $"TP={(tp.HasValue ? tp.Value.ToString("F2") : "trail")} " +
                $"slDist={slDist:F2} ({slDist / atr:F2}×ATR) size={sizeNote} " +
                $"riskEst=${expectedRisk:F0} riskAtSl=${riskAtSl:F0} " +
                $"tickSz={Symbol.TickSize} tickVal={Symbol.TickValue} lotSz={Symbol.LotSize} " +
                $"win={signal.Window} BE={(UseBreakEven ? $"{BeStartR:F1}R" : "off")} " +
                $"Trail={(UseTrailing ? $"{TrailStartR:F1}R/{TrailStepR:F1}R" : "off")} " +
                $"TP={(TpRrMultiple > 0 ? $"{TpRrMultiple:F1}R" : "off")}");
        }

        /// <summary>
        /// XAU-safe sizing (ADR VacuumHunter): many feeds report tickValue=0.01 lotSize=100 which
        /// understates risk ~100× via TickValue math → vol hundreds of units → equity wipe.
        /// Primary: VolumeForFixedRisk; always take min with oz heuristic (risk$/slDist) on gold.
        /// </summary>
        private bool TrySizeVolume(double slDist, out double volume, out double expectedRisk, out string sizeNote)
        {
            volume = 0;
            expectedRisk = 0;
            sizeNote = "";

            if (SizeMode == SvbsSizeMode.FixedLots)
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

            // B) Oz first on gold when tick meta is broken (common: tickVal=0.01 lot=100)
            double volOz = riskMoney / slDist;
            bool tickMetaOk = IsTickMetaConsistentForGold();
            bool gold = IsGoldSymbol(SymbolName);

            // A) Broker FixedRisk — skip as primary when gold meta broken (can still oversize PnL)
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

            // C) Tick formula only if metadata looks sane
            double volTick = 0;
            if (tickMetaOk && Symbol.TickSize > 0 && Symbol.TickValue > 0 && Symbol.LotSize > 0)
            {
                double lossPerUnit = (slDist / Symbol.TickSize) * Symbol.TickValue / Symbol.LotSize;
                if (lossPerUnit > 0)
                    volTick = riskMoney / lossPerUnit;
            }

            // Conservative: smallest positive candidate (gold + bad meta → Oz only effectively)
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
                _logger.Warn($"Execute aborted: vol normalize 0 raw={raw:F2} risk$={riskMoney:F0} slDist={slDist:F2}");
                return false;
            }

            // Hard ceiling: never larger than oz model (gold) or FixedRisk 1.5× target
            double maxOz = NormalizeVolumeUnits(volOz * 1.02);
            if (maxOz > 0 && volume > maxOz)
            {
                _logger.Warn($"Size cap Oz: vol {volume:F0} → {maxOz:F0}");
                volume = maxOz;
                picked += "+OzCap";
            }

            try
            {
                // Same meta gate as volFixed — do not cap with broken FixedRisk on gold
                if (slPips >= 0.1 && (!gold || tickMetaOk))
                {
                    double maxFr = Symbol.VolumeForFixedRisk(riskMoney * 1.5, slPips, RoundingMode.Down);
                    maxFr = NormalizeVolumeUnits(maxFr);
                    if (maxFr > 0 && volume > maxFr)
                    {
                        _logger.Warn($"Size cap FixedRisk1.5: vol → {maxFr:F0}");
                        volume = maxFr;
                        picked += "+FRCap";
                    }
                }
            }
            catch { /* ignore */ }

            expectedRisk = EstimateRiskMoney(slDist, volume);
            // If estimate still uses broken tick and understates, force oz estimate for log + safety
            double ozRisk = volume * slDist;
            if (ozRisk > expectedRisk * 1.5)
                expectedRisk = ozRisk;

            // Safety abort: oz-model risk >> target (would wipe account)
            if (ozRisk > riskMoney * 3.0)
            {
                _logger.Error(
                    $"Execute SAFETY ABORT: ozRisk=${ozRisk:F0} vol={volume:F0} >> target=${riskMoney:F0} " +
                    $"(tickVal={Symbol.TickValue} lotSz={Symbol.LotSize} — check symbol meta)");
                return false;
            }

            sizeNote = $"risk%={RiskPercent} via={picked}";
            _logger.Debug(
                $"Size models fixed={volFixed:F1} oz={volOz:F1} tick={volTick:F1} tickOk={tickMetaOk} → vol={volume:F0}");

            return true;
        }

        /// <summary>
        /// True when TickValue is roughly LotSize×TickSize (≈$1 per oz per $1 for standard XAU).
        /// Feed tickVal=0.01 lot=100 tickSz=0.01 is NOT consistent (understates 100×).
        /// </summary>
        private bool IsTickMetaConsistentForGold()
        {
            if (Symbol.TickSize <= 0 || Symbol.TickValue <= 0 || Symbol.LotSize <= 0)
                return false;
            if (!IsGoldSymbol(SymbolName))
                return true;

            double expect = Symbol.LotSize * Symbol.TickSize; // e.g. 100 * 0.01 = 1
            if (expect <= 0) return false;
            // Allow 0.5× … 2× band
            double ratio = Symbol.TickValue / expect;
            return ratio >= 0.5 && ratio <= 2.0;
        }

        /// <summary>Monetary risk at SL: prefer oz model on gold when tick meta is broken.</summary>
        private double EstimateRiskMoney(double slDist, double volumeUnits)
        {
            if (slDist <= 0 || volumeUnits <= 0) return 0;

            double oz = volumeUnits * slDist;
            if (!IsTickMetaConsistentForGold())
                return oz;

            double tick = PriceUtils.PriceToAmount(slDist, volumeUnits, Symbol);
            // If tick model disagrees wildly with oz on gold, trust the larger (safer reporting)
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
                // Distance only in R vs this trade's SL
                double startPips = PriceUtils.PriceToPips(slDistPrice * TrailStartR, Symbol);
                double stepPips = PriceUtils.PriceToPips(slDistPrice * TrailStepR, Symbol);
                double sensPips = Math.Max(1.0, stepPips * 0.25);
                _trailingManager.SetTrailPoints(startPips, stepPips, sensPips);
            }
        }

        // ─── Exits (full close only) ─────────────────────────

        private void ManageTimeAndSessionExits()
        {
            var positions = Positions.FindAll(BotLabel, SymbolName);
            if (positions == null || positions.Length == 0) return;

            DateTime utc = Server.TimeInUtc;

            foreach (var pos in positions)
            {
                if (!_openMeta.TryGetValue(pos.Id, out var meta))
                {
                    // Restart recovery: never use *now* for window (would remap A→L → L→NY)
                    DateTime entryUtc = pos.EntryTime.Kind == DateTimeKind.Utc
                        ? pos.EntryTime
                        : pos.EntryTime.ToUniversalTime();

                    SvbsEntryWindow window = SvbsEntryWindow.None;
                    if (!string.IsNullOrEmpty(pos.Comment)
                        && Enum.TryParse(pos.Comment, ignoreCase: true, out SvbsEntryWindow parsed))
                    {
                        window = parsed;
                    }
                    else
                    {
                        window = _clock.ResolveWindow(
                            entryUtc, TradeAsia, TradeLondon, TradeNewYork, TradeOverlap);
                    }

                    meta = new OpenTradeMeta
                    {
                        Window = window,
                        EntryUtc = entryUtc,
                        SlDistPrice = pos.StopLoss.HasValue
                            ? Math.Abs(pos.EntryPrice - pos.StopLoss.Value)
                            : 0,
                        EntryPrice = pos.EntryPrice
                    };
                    _openMeta[pos.Id] = meta;
                    _logger.Info(
                        $"Recovered open #{pos.Id} window={window} entryUtc={entryUtc:u} " +
                        $"(comment={pos.Comment ?? "-"})");
                }

                if (_clock.ShouldFlat(utc, meta.Window, meta.EntryUtc))
                    CloseFull(pos, "X4_SESSION_FLAT");
            }
        }

        private void CloseFull(Position pos, string reason)
        {
            // Full volume only — never partial
            var r = ClosePosition(pos);
            if (r.IsSuccessful)
                _logger.Info($"CLOSE #{pos.Id} {reason} net={pos.NetProfit:F2}");
            else
                _logger.Error($"CLOSE failed #{pos.Id} {reason}: {r.Error}");
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args?.Position == null) return;
            if (args.Position.Label != BotLabel || args.Position.SymbolName != SymbolName) return;

            _openMeta.Remove(args.Position.Id);
            _logger.Info($"CLOSE #{args.Position.Id} net={args.Position.NetProfit:F2} {args.Reason}");
        }

        // ─── Risk helpers ───────────────────────────────────

        private void ResetDailyCounters(DateTime utc)
        {
            int dayKey = utc.Year * 1000 + utc.DayOfYear;
            if (dayKey == _tradeDayKey) return;

            if (_tradeDayKey > 0)
                _logger.Info($"Day summary: trades={_tradesToday} rejects={_rejectCountToday} last={_lastReject}");

            _tradeDayKey = dayKey;
            _tradesToday = 0;
            _rejectCountToday = 0;
            _lastReject = null;
            _dayStartEquity = Account.Equity;
            _signalEngine.Reset();
            // Profiles re-freeze on new day keys via TryFreezeProfiles
            // Daily $ day-start equity is rolled inside CRiskManager
        }

        private void LogReject(SignalResult result)
        {
            if (result?.RejectCode == null) return;
            string code = result.RejectCode;

            // Armed break — always visible (proves pipeline sees VA breaks)
            if (code is "E_BREAK_LONG" or "E_BREAK_SHORT")
            {
                _logger.Info(
                    $"{code} VAH={_signalEngine.State.VAH:F2} VAL={_signalEngine.State.VAL:F2} " +
                    $"win={_signalEngine.State.Window} timeout={AcceptTimeoutBars}");
                _lastReject = code;
                return;
            }

            // Soft rejects (volume/POC/struct wait) — see C_TIMEOUT soft=… when setup dies
            if (code is "E_BREAK_NO" or "E_ACC_WAIT" or "F2_SESSION" or "F8_IN_POSITION"
                or "V_SURGE" or "V_MEDIAN" or "POC_SLOPE" or "POC_SIDE")
            {
                _logger.Debug(code);
                return;
            }

            _lastReject = code;
            if (code is "C_TIMEOUT" or "C_REACCEPT" or "C_WINDOW" or "C_FLIP" or "F4_VA_WIDTH" or "X1_SL_CAP")
            {
                _rejectCountToday++;
                _logger.Info($"REJECT {result.Reason}");
            }
            else if (code == "F3_PROFILE")
            {
                // Weekend/gap: count once in summary, do not spam Info
                _rejectCountToday++;
                _logger.Debug($"REJECT {code}");
            }
            else
            {
                _rejectCountToday++;
                _logger.Debug($"REJECT {code}");
            }
        }

        private static bool IsGoldSymbol(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string u = name.ToUpperInvariant();
            return u.Contains("XAU") || u.Contains("GOLD");
        }

        /// <summary>SMA of tick volume on bars strictly before <paramref name="bi"/>.</summary>
        private double ComputeVolumeSmaPrior(int bi, int period)
        {
            if (bi < period) return 0;
            double sum = 0;
            for (int i = 1; i <= period; i++)
                sum += Bars.TickVolumes[bi - i];
            return sum / period;
        }

        private double ComputeVolumeMedianPrior(int bi, int period)
        {
            if (bi < period) return 0;
            var arr = new double[period];
            for (int i = 0; i < period; i++)
                arr[i] = Bars.TickVolumes[bi - 1 - i];
            Array.Sort(arr);
            int mid = period / 2;
            return period % 2 == 0 ? (arr[mid - 1] + arr[mid]) * 0.5 : arr[mid];
        }
    }
}
