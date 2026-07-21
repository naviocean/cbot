using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using RedWave.Common;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    public enum LotSizeMode
    {
        RiskPercent = 0,
        FixedLots = 1
    }

    /// <summary>
    /// Confirmed ZigZag pullback → zone POC (rolling) or Fib 38.2–61.8 → market when price in zone.
    /// Spec: docs/v1.0/1-prds/PRD-zz-poc-pullback.md
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class ZigZagPocPullback : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "ZigZagPocPullback")]
        public string BotLabel { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk", DefaultValue = LotSizeMode.RiskPercent)]
        public LotSizeMode SizeMode { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.5, MinValue = 0.05, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Positions / Side", Group = "Trade & Risk", DefaultValue = 5, MinValue = 1, MaxValue = 20)]
        public int MaxPositionsPerSide { get; set; }

        [Parameter("Max Equity DD %", Group = "Trade & Risk", DefaultValue = 10.0, MinValue = 0.0)]
        public double MaxEquityDrawdownPct { get; set; }

        [Parameter("Flatten On Equity DD", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnEquityDd { get; set; }

        [Parameter("Max Daily Loss $", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyLossAmount { get; set; }

        [Parameter("Max Daily Profit $", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyProfitAmount { get; set; }

        [Parameter("Flatten On Daily Loss", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnDailyLoss { get; set; }

        [Parameter("Flatten On Daily Profit", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnDailyProfit { get; set; }

        [Parameter("Max Spread (pips)", Group = "Trade & Risk", DefaultValue = 50.0, MinValue = 0.0)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Debug Logging", Group = "Trade & Risk", DefaultValue = false)]
        public bool DebugLogging { get; set; }

        // ─── Visual ─────────────────────────────────────────
        [Parameter("Show Visuals", Group = "Visual", DefaultValue = true)]
        public bool ShowVisuals { get; set; }

        [Parameter("Show ZigZag", Group = "Visual", DefaultValue = true)]
        public bool ShowZigZag { get; set; }

        [Parameter("Show Zone", Group = "Visual", DefaultValue = true)]
        public bool ShowZone { get; set; }

        [Parameter("Show Labels", Group = "Visual", DefaultValue = true)]
        public bool ShowLabels { get; set; }

        [Parameter("ZZ Max Pivots Draw", Group = "Visual", DefaultValue = 24, MinValue = 4, MaxValue = 80)]
        public int ZzMaxPivotsDraw { get; set; }

        // ─── ZigZag ─────────────────────────────────────────
        /// <summary>cTrader Guru / MT ZigZag — same as indicator Depth (default 12).</summary>
        [Parameter("ZZ Depth", Group = "ZigZag", DefaultValue = 12, MinValue = 1, MaxValue = 100)]
        public int ZzDepth { get; set; }

        /// <summary>Deviation in points (× TickSize), same as Guru ZigZag (default 5).</summary>
        [Parameter("ZZ Deviation", Group = "ZigZag", DefaultValue = 5, MinValue = 1, MaxValue = 1000)]
        public int ZzDeviation { get; set; }

        [Parameter("ZZ BackStep", Group = "ZigZag", DefaultValue = 3, MinValue = 1, MaxValue = 50)]
        public int ZzBackstep { get; set; }

        // ─── Zone ───────────────────────────────────────────
        [Parameter("Zone Mode", Group = "Zone", DefaultValue = ZoneMode.Poc)]
        public ZoneMode ZoneMode { get; set; }

        [Parameter("POC TimeFrame", Group = "Zone", DefaultValue = "Hour")]
        public TimeFrame PocTimeFrame { get; set; }

        [Parameter("Profile Lookback Days", Group = "Zone", DefaultValue = 3, MinValue = 1, MaxValue = 15)]
        public int ProfileLookbackDays { get; set; }

        [Parameter("Buffer ATR Ratio", Group = "Zone", DefaultValue = 0.5, MinValue = 0.05, MaxValue = 3.0)]
        public double BufferAtrRatio { get; set; }

        [Parameter("VP Bin Size", Group = "Zone", DefaultValue = 0.5, MinValue = 0.01)]
        public double VpBinSize { get; set; }

        // ─── Structure ──────────────────────────────────────
        [Parameter("Use Structure Filter", Group = "Structure", DefaultValue = false)]
        public bool UseStructureFilter { get; set; }

        // ─── Exit ───────────────────────────────────────────
        [Parameter("SL ATR Ratio", Group = "Exit", DefaultValue = 1.0, MinValue = 0.2, MaxValue = 5.0)]
        public double SlAtrRatio { get; set; }

        [Parameter("TP (R)", Group = "Exit", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0)]
        public double TpRR { get; set; }

        [Parameter("ATR Period", Group = "Exit", DefaultValue = 14, MinValue = 5, MaxValue = 50)]
        public int AtrPeriod { get; set; }

        // ─── Break Even (same pattern as Fib786 / sibling bots) ───
        [Parameter("Use Break Even", Group = "Break Even", DefaultValue = true)]
        public bool UseBreakEven { get; set; }

        [Parameter("BE Start (R)", Group = "Break Even", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0)]
        public double BeAtRR { get; set; }

        [Parameter("BE Lock (pips)", Group = "Break Even", DefaultValue = 15.0, MinValue = 0.0)]
        public double BeLockPips { get; set; }

        [Parameter("BE Add Spread", Group = "Break Even", DefaultValue = true)]
        public bool BeAddSpread { get; set; }

        // ─── Trailing ───────────────────────────────────────
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

        /// <summary>XAU strategy pip: 100 pips = 1.0 price.</summary>
        private const double StratPipPrice = 0.01;

        private CLogger _logger;
        private CRiskManager _riskManager;
        private CSessionFilter _sessionFilter;
        private CMarketCondition _marketCondition;
        private CVolumeProfile _volumeProfile;
        private CTrailingManager _trailingManager;
        private SignalEngine _signalEngine;
        private AverageTrueRange _atr;
        private Bars _pocBars;

        private bool _hasLastTradedZ1;
        private long _lastTradedZ1Key;
        private double _lastEntrySlDist;

        private bool _armed;
        private SignalSide _armedSide;
        private long _armedZ1Key;
        private double _armedZ2;
        private double _armedZoneLow;
        private double _armedZoneHigh;
        private double _armedSl; // structural SL price (from z2 ± atr)
        private string _lastArmLog;
        private DateTime _lastBarTime;
        private string _lastRejectReason;
        private int _rejectCountToday;
        private int _tradeDayKey;

        private const string VisPrefix = "ZZPOC_";
        private int _visZzSegments;
        private int _visPivotMarks;
        private double _lastVisPoc;
        private string _statusText = "INIT";

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("ZigZagPocPullback", DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);

            _riskManager = new CRiskManager();
            _riskManager.Init(this, Symbol, BotLabel, _logger);
            _riskManager.SetEquityProtection(MaxEquityDrawdownPct, FlattenOnEquityDd);
            _riskManager.SetDailyLimits(MaxDailyLossAmount, MaxDailyProfitAmount, FlattenOnDailyLoss, FlattenOnDailyProfit);

            _sessionFilter = new CSessionFilter();
            _sessionFilter.Init(TradeAsia, TradeLondon, TradeNewYork, TradeOverlap, _logger);
            if (!TradeAsia && !TradeLondon && !TradeNewYork && !TradeOverlap)
                _logger.Warn("No session enabled — bot will never enter");

            _marketCondition = new CMarketCondition();
            _marketCondition.Init(Symbol, _logger);
            double maxSpreadPrice = StratPipsToPrice(MaxSpreadPips);
            double maxSpreadBrokerPips = PriceUtils.PriceToPips(maxSpreadPrice, Symbol);
            _marketCondition.SetSpreadCheck(true, maxSpreadBrokerPips);

            _trailingManager = new CTrailingManager();
            _trailingManager.Init(this, Symbol, BotLabel, _logger);

            _signalEngine = new SignalEngine();
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);

            _pocBars = MarketData.GetBars(PocTimeFrame, SymbolName);
            _volumeProfile = new CVolumeProfile();
            _volumeProfile.Init(_pocBars, Chart, precision: 100, visualize: false, logger: _logger);
            _volumeProfile.ConfigureComposite(binSize: VpBinSize, lookbackDays: ProfileLookbackDays);

            _hasLastTradedZ1 = false;
            _armed = false;
            _lastBarTime = DateTime.MinValue;
            _tradeDayKey = -1;
            _lastEntrySlDist = 0;
            _visZzSegments = 0;
            _visPivotMarks = 0;
            _lastVisPoc = 0;
            _statusText = "INIT";

            Positions.Opened += OnPositionOpened;
            Positions.Closed += OnPositionClosed;

            _logger.Info(
                $"Started ZZPOC {SymbolName} TF={TimeFrame} mode={ZoneMode} pocTF={PocTimeFrame} " +
                $"lookbackDays={ProfileLookbackDays} ZZ={ZzDepth}/{ZzDeviation}/{ZzBackstep} (ZigZagCore shared) " +
                $"bufATR={BufferAtrRatio} slATR={SlAtrRatio} TpRR={TpRR} " +
                $"BE={UseBreakEven}@{BeAtRR}R trail={UseTrailing}@{TrailStartRR}R " +
                $"risk={RiskPercent}% maxPos/side={MaxPositionsPerSide} struct={UseStructureFilter} " +
                $"vis={ShowVisuals} sessions={_sessionFilter.DescribeEnabled()} debug={DebugLogging}");
        }

        protected override void OnStop()
        {
            Positions.Opened -= OnPositionOpened;
            Positions.Closed -= OnPositionClosed;
            ClearVisuals();
        }

        protected override void OnTick()
        {
            _riskManager.OnTick();

            if (UseBreakEven || UseTrailing)
                _trailingManager.OnTick();

            if (!_armed || !EnableTrading)
                return;

            if (!_sessionFilter.IsTradingAllowed(Server.TimeInUtc))
                return;

            if (!_riskManager.IsTradingAllowed(Account.Equity, Server.TimeInUtc))
                return;

            if (!_marketCondition.IsTradingOK())
                return;

            double mid = (Symbol.Bid + Symbol.Ask) * 0.5;
            if (mid < _armedZoneLow || mid > _armedZoneHigh)
                return;

            if (_hasLastTradedZ1 && _lastTradedZ1Key == _armedZ1Key)
            {
                _armed = false;
                return;
            }

            int longN = CountSide(TradeType.Buy);
            int shortN = CountSide(TradeType.Sell);
            if (_armedSide == SignalSide.Long && longN >= MaxPositionsPerSide)
            {
                _armed = false;
                return;
            }
            if (_armedSide == SignalSide.Short && shortN >= MaxPositionsPerSide)
            {
                _armed = false;
                return;
            }

            TryExecuteArmed(mid);
        }

        protected override void OnBar()
        {
            ResetDailyCounters();

            // Same as chart ZigZag: use all closed bars available (no fake Lookback param).
            int minBars = ZzDepth + 20;
            if (Bars.Count < minBars)
                return;

            int bi = Bars.Count - 2;
            if (bi < 0)
                return;

            DateTime barTime = Bars.OpenTimes[bi];
            if (barTime <= _lastBarTime)
                return;
            _lastBarTime = barTime;

            double atr = 0;
            if (_atr != null && _atr.Result.Count > 1)
                atr = _atr.Result.Last(1);
            if (atr <= 0)
                atr = StratPipsToPrice(200);

            double poc = 0;
            bool pocOk = false;
            if (ZoneMode == ZoneMode.Poc)
            {
                var profile = BuildRollingPoc();
                pocOk = profile != null && profile.IsValid && profile.POC > 0;
                if (pocOk)
                    poc = profile.POC;
            }

            var barSnaps = BuildBarSnaps();
            var zzPoints = ExtractZigZagPoints();

            var ctx = new SignalContext
            {
                Bars = barSnaps,
                Atr = atr,
                SpreadPrice = Symbol.Spread,
                MaxSpreadPrice = StratPipsToPrice(MaxSpreadPips),
                SessionOk = _sessionFilter.IsTradingAllowed(Server.TimeInUtc),
                EquityOk = _riskManager.IsTradingAllowed(Account.Equity, Server.TimeInUtc),
                LongPositions = CountSide(TradeType.Buy),
                ShortPositions = CountSide(TradeType.Sell),
                MaxPositionsPerSide = MaxPositionsPerSide,
                ZigZagPoints = zzPoints,
                UseStructureFilter = UseStructureFilter,
                ZoneMode = ZoneMode,
                BufferAtrRatio = BufferAtrRatio,
                SlAtrRatio = SlAtrRatio,
                PocPrice = poc,
                PocValid = ZoneMode != ZoneMode.Poc || pocOk,
                LivePrice = 0,
                RequireInZone = false,
                HasLastTradedZ1 = _hasLastTradedZ1,
                LastTradedZ1Key = _lastTradedZ1Key
            };

            var setup = _signalEngine.EvaluateSetup(ctx);
            if (!setup.IsValid)
            {
                if (_armed)
                {
                    _logger.Debug($"DISARM: {setup.Reason}");
                    _armed = false;
                }
                _statusText = setup.Reason ?? "REJECT";
                LogReject(setup.Reason);
                UpdateVisuals(zzPoints, setup, poc, pocOk, armed: false);
                return;
            }

            // Re-arm if new z1 or zone moved
            bool newArm = !_armed || _armedZ1Key != setup.Z1Key ||
                          Math.Abs(_armedZoneLow - setup.ZoneLow) > atr * 0.05 ||
                          _armedSide != setup.Side;

            _armed = true;
            _armedSide = setup.Side;
            _armedZ1Key = setup.Z1Key;
            _armedZ2 = setup.Z2Price;
            _armedZoneLow = setup.ZoneLow;
            _armedZoneHigh = setup.ZoneHigh;
            _armedSl = setup.StopLoss;
            _statusText = $"ARMED {_armedSide} zone=[{_armedZoneLow:F1},{_armedZoneHigh:F1}]";

            if (newArm)
            {
                _lastArmLog =
                    $"ARM {setup.Reason} side={setup.Side} z1={setup.Z1Price:F2} z2={setup.Z2Price:F2} z3={setup.Z3Price:F2} " +
                    $"zone=[{setup.ZoneLow:F2},{setup.ZoneHigh:F2}] sl={setup.StopLoss:F2} mode={ZoneMode} poc={poc:F2}";
                _logger.Info(_lastArmLog);
            }

            UpdateVisuals(zzPoints, setup, poc, pocOk, armed: true);

            // Optional: same-bar fill if already in zone
            if (EnableTrading)
            {
                double mid = (Symbol.Bid + Symbol.Ask) * 0.5;
                if (mid >= _armedZoneLow && mid <= _armedZoneHigh)
                    TryExecuteArmed(mid);
            }
        }

        // ─── Chart visuals ──────────────────────────────────

        private void UpdateVisuals(
            List<ZzPivot> pivots,
            SignalResult setup,
            double poc,
            bool pocOk,
            bool armed)
        {
            if (!ShowVisuals)
            {
                ClearVisuals();
                return;
            }

            try
            {
                if (ShowZigZag)
                    DrawZigZag(pivots);
                else
                    ClearZigZagOnly();

                if (ShowZone && setup != null && setup.IsValid)
                    DrawZone(setup, poc, pocOk, armed);
                else if (ShowZone && ZoneMode == ZoneMode.Poc && pocOk)
                    DrawPocOnly(poc);
                else
                    ClearZoneOnly();

                if (ShowLabels)
                    DrawStatusAndPivotLabels(pivots, setup, armed);
                else
                    ClearLabelsOnly();
            }
            catch (Exception ex)
            {
                _logger?.Debug($"Visual error: {ex.Message}");
            }
        }

        private void DrawZigZag(List<ZzPivot> pivots)
        {
            if (pivots == null || pivots.Count < 2)
            {
                ClearZigZagOnly();
                return;
            }

            int max = Math.Max(4, ZzMaxPivotsDraw);
            int start = Math.Max(0, pivots.Count - max);
            int seg = 0;
            int marks = 0;

            for (int i = start; i < pivots.Count - 1; i++)
            {
                var a = pivots[i];
                var b = pivots[i + 1];
                // Confirmed segments solid; last segment (to tip) dotted
                bool isTipSeg = i == pivots.Count - 2;
                Color col = isTipSeg
                    ? Color.FromArgb(160, 149, 165, 166)
                    : Color.FromArgb(220, 241, 196, 15);
                var style = isTipSeg ? LineStyle.Dots : LineStyle.Solid;
                string name = VisPrefix + "ZZ_" + seg;
                Chart.DrawTrendLine(name, a.OpenTime, a.Price, b.OpenTime, b.Price, col, isTipSeg ? 1 : 2, style);
                seg++;
            }

            // Pivot markers — z1/z2/z3 = confirmed chain; tip = forming extreme
            for (int i = start; i < pivots.Count; i++)
            {
                var p = pivots[i];
                bool isTip = i == pivots.Count - 1;
                bool isZ1 = i == pivots.Count - 2;
                bool isZ2 = i == pivots.Count - 3;
                bool isZ3 = i == pivots.Count - 4;

                int barsAgo = BarsAgo(p.OpenTime);

                Color mc = p.IsHigh
                    ? Color.FromArgb(220, 231, 76, 60)
                    : Color.FromArgb(220, 46, 204, 113);
                if (isTip)
                    mc = Color.FromArgb(220, 149, 165, 166);
                else if (isZ1)
                    mc = Color.FromArgb(255, 52, 152, 219);
                else if (isZ2)
                    mc = Color.FromArgb(255, 155, 89, 182);
                else if (isZ3)
                    mc = Color.FromArgb(255, 26, 188, 156);

                string mark = VisPrefix + "P_" + marks;
                string label;
                if (isZ1) label = $"z1·{barsAgo}";
                else if (isZ2) label = $"z2·{barsAgo}";
                else if (isZ3) label = $"z3·{barsAgo}";
                else if (isTip) label = $"tip·{barsAgo}";
                else label = p.IsHigh ? "H" : "L";

                var t = Chart.DrawText(mark, label, p.OpenTime, p.Price, mc);
                t.VerticalAlignment = p.IsHigh ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.FontSize = isZ1 || isZ2 || isZ3 || isTip ? 11 : 8;
                marks++;
            }

            // Remove leftover segments / marks from previous larger draws
            for (int i = seg; i < _visZzSegments; i++)
                Chart.RemoveObject(VisPrefix + "ZZ_" + i);
            for (int i = marks; i < _visPivotMarks; i++)
                Chart.RemoveObject(VisPrefix + "P_" + i);

            _visZzSegments = seg;
            _visPivotMarks = marks;
        }

        private void DrawZone(SignalResult setup, double poc, bool pocOk, bool armed)
        {
            DateTime t1 = Bars.OpenTimes[Math.Max(0, Bars.Count - 40)];
            DateTime t2 = Bars.OpenTimes[Bars.Count - 1].AddMinutes(5);

            Color zoneFill = armed
                ? Color.FromArgb(45, 52, 152, 219)
                : Color.FromArgb(30, 149, 165, 166);
            Color zoneEdge = armed
                ? Color.FromArgb(200, 52, 152, 219)
                : Color.FromArgb(160, 127, 140, 141);

            var rect = Chart.DrawRectangle(
                VisPrefix + "ZONE",
                t1, setup.ZoneHigh,
                t2, setup.ZoneLow,
                zoneEdge);
            rect.IsFilled = true;
            rect.Color = zoneFill;

            Chart.DrawTrendLine(VisPrefix + "ZHI", t1, setup.ZoneHigh, t2, setup.ZoneHigh, zoneEdge, 1, LineStyle.Solid);
            Chart.DrawTrendLine(VisPrefix + "ZLO", t1, setup.ZoneLow, t2, setup.ZoneLow, zoneEdge, 1, LineStyle.Solid);

            if (ZoneMode == ZoneMode.Poc && pocOk && poc > 0)
            {
                var pocLine = Chart.DrawTrendLine(
                    VisPrefix + "POC", t1, poc, t2, poc,
                    Color.FromArgb(255, 230, 126, 34), 2, LineStyle.Solid);
                _lastVisPoc = poc;
                if (ShowLabels)
                {
                    var pt = Chart.DrawText(VisPrefix + "POC_LBL", $"POC {poc:F2}", t2, poc, Color.FromArgb(255, 230, 126, 34));
                    pt.VerticalAlignment = VerticalAlignment.Bottom;
                    pt.HorizontalAlignment = HorizontalAlignment.Right;
                    pt.FontSize = 9;
                }
            }
            else
            {
                Chart.RemoveObject(VisPrefix + "POC");
                Chart.RemoveObject(VisPrefix + "POC_LBL");
            }

            // Structural SL
            Color slCol = Color.FromArgb(220, 231, 76, 60);
            Chart.DrawTrendLine(VisPrefix + "SL", t1, setup.StopLoss, t2, setup.StopLoss, slCol, 1, LineStyle.DotsRare);
            if (ShowLabels)
            {
                var slt = Chart.DrawText(VisPrefix + "SL_LBL", $"SL {setup.StopLoss:F2}", t2, setup.StopLoss, slCol);
                slt.VerticalAlignment = VerticalAlignment.Top;
                slt.HorizontalAlignment = HorizontalAlignment.Right;
                slt.FontSize = 9;
            }
        }

        private void DrawPocOnly(double poc)
        {
            DateTime t1 = Bars.OpenTimes[Math.Max(0, Bars.Count - 40)];
            DateTime t2 = Bars.OpenTimes[Bars.Count - 1].AddMinutes(5);
            Chart.DrawTrendLine(
                VisPrefix + "POC", t1, poc, t2, poc,
                Color.FromArgb(255, 230, 126, 34), 2, LineStyle.Solid);
            _lastVisPoc = poc;
            Chart.RemoveObject(VisPrefix + "ZONE");
            Chart.RemoveObject(VisPrefix + "ZHI");
            Chart.RemoveObject(VisPrefix + "ZLO");
            Chart.RemoveObject(VisPrefix + "SL");
            Chart.RemoveObject(VisPrefix + "SL_LBL");
        }

        private void DrawStatusAndPivotLabels(List<ZzPivot> pivots, SignalResult setup, bool armed)
        {
            // Corner status (use last bar time)
            DateTime t = Bars.OpenTimes[Bars.Count - 1];
            double y = Bars.HighPrices.Last(1);
            if (_atr != null && _atr.Result.Count > 1)
                y += _atr.Result.Last(1) * 0.35;

            string mode = ZoneMode == ZoneMode.Poc ? "POC" : "FIB";
            string line1 = armed
                ? $"ZZPOC ARMED {_armedSide} | {mode}"
                : $"ZZPOC idle | {mode}";
            string line2 = _statusText ?? "";
            if (line2.Length > 64)
                line2 = line2.Substring(0, 64);

            var s1 = Chart.DrawText(VisPrefix + "STAT1", line1, t, y, armed
                ? Color.FromArgb(255, 46, 204, 113)
                : Color.FromArgb(220, 189, 195, 199));
            s1.VerticalAlignment = VerticalAlignment.Top;
            s1.HorizontalAlignment = HorizontalAlignment.Right;
            s1.FontSize = 11;

            var s2 = Chart.DrawText(VisPrefix + "STAT2", line2, t, y - (y * 0.00001 + StratPipsToPrice(80)), Color.FromArgb(200, 189, 195, 199));
            s2.VerticalAlignment = VerticalAlignment.Top;
            s2.HorizontalAlignment = HorizontalAlignment.Right;
            s2.FontSize = 9;

            if (setup != null && setup.IsValid && ShowLabels)
            {
                // Extra z1/z2/z3 price tags if pivots present
                // (markers already drawn in DrawZigZag)
            }
        }

        private void ClearZigZagOnly()
        {
            for (int i = 0; i < _visZzSegments; i++)
                Chart.RemoveObject(VisPrefix + "ZZ_" + i);
            for (int i = 0; i < _visPivotMarks; i++)
                Chart.RemoveObject(VisPrefix + "P_" + i);
            _visZzSegments = 0;
            _visPivotMarks = 0;
        }

        private void ClearZoneOnly()
        {
            Chart.RemoveObject(VisPrefix + "ZONE");
            Chart.RemoveObject(VisPrefix + "ZHI");
            Chart.RemoveObject(VisPrefix + "ZLO");
            Chart.RemoveObject(VisPrefix + "POC");
            Chart.RemoveObject(VisPrefix + "POC_LBL");
            Chart.RemoveObject(VisPrefix + "SL");
            Chart.RemoveObject(VisPrefix + "SL_LBL");
        }

        private void ClearLabelsOnly()
        {
            Chart.RemoveObject(VisPrefix + "STAT1");
            Chart.RemoveObject(VisPrefix + "STAT2");
            Chart.RemoveObject(VisPrefix + "POC_LBL");
            Chart.RemoveObject(VisPrefix + "SL_LBL");
        }

        private void ClearVisuals()
        {
            ClearZigZagOnly();
            ClearZoneOnly();
            ClearLabelsOnly();
        }

        /// <summary>How many chart bars ago this pivot open-time sits (0 = current forming).</summary>
        private int BarsAgo(DateTime pivotOpen)
        {
            // Last closed bar = shift 1
            for (int shift = 1; shift < Bars.Count; shift++)
            {
                if (Bars.OpenTimes.Last(shift) == pivotOpen)
                    return shift;
                if (Bars.OpenTimes.Last(shift) < pivotOpen)
                    return Math.Max(0, shift - 1);
            }
            return -1;
        }

        private void TryExecuteArmed(double mid)
        {
            if (!_armed)
                return;

            bool isLong = _armedSide == SignalSide.Long;
            double entry = isLong ? Symbol.Ask : Symbol.Bid;

            // SL from structure (z2 ± pad); recompute dist from live entry
            double sl = _armedSl;
            double slDist = isLong ? entry - sl : sl - entry;
            if (slDist <= 0)
            {
                // Fallback: re-pad from z2
                double atr = _atr != null && _atr.Result.Count > 1 ? _atr.Result.Last(1) : StratPipsToPrice(200);
                double pad = Math.Max(0.2, SlAtrRatio) * atr;
                sl = isLong ? _armedZ2 - pad : _armedZ2 + pad;
                slDist = isLong ? entry - sl : sl - entry;
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

            double dailyRoom = _riskManager.GetRemainingDailyLossBudget(Account.Equity);
            if (_riskManager.IsDailyLossLimitEnabled && dailyRoom <= 1.0)
            {
                _logger.Warn("Execute aborted: daily loss room exhausted");
                return;
            }

            if (!TryComputeVolume(slDist, dailyRoom, out double volume, out double expectedRisk, out string sizeNote))
            {
                _logger.Warn($"Execute aborted: volume — {sizeNote}");
                return;
            }

            TradeType tradeType = isLong ? TradeType.Buy : TradeType.Sell;
            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, sl, tp, null, false);

            if (!result.IsSuccessful)
            {
                _logger.Error($"ExecuteMarket failed: {result.Error}");
                return;
            }

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
                    _logger.Warn($"Modify SL/TP failed: {mod.Error}");
                else
                {
                    sl = absSl;
                    tp = absTp;
                    entry = fill;
                }
            }

            _hasLastTradedZ1 = true;
            _lastTradedZ1Key = _armedZ1Key;
            _armed = false;
            _lastEntrySlDist = slDist;
            ConfigureExitsForTrade(slDist);

            _logger.Info(
                $"E_MKT {tradeType} entry={entry:F2} SL={sl:F2} TP={tp:F2} R={slDist:F4} TpRR={TpRR} " +
                $"BE={UseBreakEven} trail={UseTrailing} " +
                $"vol={volume} size={sizeNote} risk≈${expectedRisk:F0} zone=[{_armedZoneLow:F2},{_armedZoneHigh:F2}] z1Key={_lastTradedZ1Key}");
        }

        /// <summary>Wire BE / trail from R distance (broker pips), same as Fib786.</summary>
        private void ConfigureExitsForTrade(double slDistPrice)
        {
            if (slDistPrice <= 0 || _trailingManager == null)
                return;

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

        private double StratPipsToBrokerPips(double stratPips)
        {
            if (stratPips <= 0)
                return 0;
            return PriceUtils.PriceToPips(StratPipsToPrice(stratPips), Symbol);
        }

        private ProfileData BuildRollingPoc()
        {
            if (_pocBars == null || _pocBars.Count < 5)
                return new ProfileData { IsValid = false };

            int lastClosed = _pocBars.Count - 2;
            if (lastClosed < 1)
                return new ProfileData { IsValid = false };

            DateTime endUtc = lastClosed + 1 < _pocBars.Count
                ? _pocBars.OpenTimes[lastClosed + 1]
                : _pocBars.OpenTimes[lastClosed].AddHours(1);

            DateTime startUtc = endUtc.AddDays(-Math.Max(1, ProfileLookbackDays));

            // Ensure volume profile uses POC TF bars
            _volumeProfile.Init(_pocBars, Chart, precision: 100, visualize: false, logger: _logger);
            return _volumeProfile.BuildRange(startUtc, endUtc, updateLastProfile: true, draw: false);
        }

        /// <summary>All closed bars (for ATR/context only — ZZ comes from indicator).</summary>
        private List<BarSnap> BuildBarSnaps()
        {
            int closed = Math.Max(0, Bars.Count - 1);
            var list = new List<BarSnap>(closed);
            for (int shift = 1; shift <= closed; shift++)
            {
                list.Add(new BarSnap(
                    Bars.OpenPrices.Last(shift),
                    Bars.HighPrices.Last(shift),
                    Bars.LowPrices.Last(shift),
                    Bars.ClosePrices.Last(shift),
                    Bars.OpenTimes.Last(shift),
                    shift));
            }
            return list;
        }

        /// <summary>
        /// ZigZagCore (same as chart indicator HighLow) → z1/z2/z3.
        /// tip = last point, z1 = second-last.
        /// </summary>
        private List<ZzPivot> ExtractZigZagPoints()
        {
            var list = new List<ZzPivot>();
            int lastClosed = Bars.Count - 2;
            if (lastClosed < ZzDepth + 2)
                return list;

            int n = lastClosed + 1; // indices 0..lastClosed
            var high = new double[n];
            var low = new double[n];
            var times = new DateTime[n];
            for (int i = 0; i < n; i++)
            {
                high[i] = Bars.HighPrices[i];
                low[i] = Bars.LowPrices[i];
                times[i] = Bars.OpenTimes[i];
            }

            double point = Symbol.TickSize > 0 ? Symbol.TickSize : Symbol.PipSize;
            double[] result = ZigZagCore.ComputeHighLow(high, low, ZzDepth, ZzDeviation, ZzBackstep, point);
            var points = ZigZagCore.ExtractPoints(result, high, low, times, lastClosed);

            foreach (var p in points)
                list.Add(new ZzPivot(p.IsHigh, p.Price, p.BarIndex, p.OpenTime));

            return list;
        }

        private int CountSide(TradeType type)
        {
            return Positions.FindAll(BotLabel, SymbolName).Count(p => p.TradeType == type);
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

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            var pos = args.Position;
            if (pos == null || pos.Label != BotLabel || pos.SymbolName != SymbolName)
                return;

            double slDist = _lastEntrySlDist;
            if (pos.StopLoss.HasValue)
                slDist = Math.Abs(pos.EntryPrice - pos.StopLoss.Value);
            if (slDist > 0)
            {
                _lastEntrySlDist = slDist;
                ConfigureExitsForTrade(slDist);
            }

            _logger.Info(
                $"E_FILL #{pos.Id} {pos.TradeType} entry={pos.EntryPrice:F2} SL={pos.StopLoss} TP={pos.TakeProfit} " +
                $"R={slDist:F4} BE={UseBreakEven} trail={UseTrailing}");
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if (pos == null || pos.Label != BotLabel || pos.SymbolName != SymbolName)
                return;

            _logger.Info($"E_CLOSE #{pos.Id} {pos.TradeType} net={pos.NetProfit:F2}");
        }

        private void ResetDailyCounters()
        {
            int dayKey = Server.TimeInUtc.Year * 1000 + Server.TimeInUtc.DayOfYear;
            if (dayKey == _tradeDayKey)
                return;
            _tradeDayKey = dayKey;
            _rejectCountToday = 0;
            _lastRejectReason = null;
        }

        private void LogReject(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return;
            _rejectCountToday++;
            if (reason == _lastRejectReason)
            {
                if (DebugLogging)
                    _logger.Debug(reason);
                return;
            }
            _lastRejectReason = reason;
            _logger.Debug($"{reason} (rejectsToday={_rejectCountToday})");
        }

        private static double StratPipsToPrice(double stratPips)
        {
            return stratPips * StratPipPrice;
        }
    }
}
