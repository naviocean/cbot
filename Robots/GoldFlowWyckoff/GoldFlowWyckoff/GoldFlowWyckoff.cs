using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using RedWave.Common;

namespace cAlgo.Robots
{
    public enum GoldFlowTpMode
    {
        RiskReward = 0,
        StructureMagnet = 1,
        FixedPrice = 2
    }

    public enum GoldFlowLotSizeMode
    {
        RiskPercent = 0,
        RiskAmount = 1,
        FixedLots = 2
    }

    public enum VpLookbackMode
    {
        Daily = 0,
        RollingHours = 1
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class GoldFlowWyckoff : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "GoldFlowWyckoff")]
        public string BotLabel { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk", DefaultValue = GoldFlowLotSizeMode.RiskAmount)]
        public GoldFlowLotSizeMode SizeMode { get; set; }

        [Parameter("Risk Amount ($)", Group = "Trade & Risk", DefaultValue = 50.0, MinValue = 1.0)]
        public double RiskAmount { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.75, MinValue = 0.1, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Trades / Day", Group = "Trade & Risk", DefaultValue = 2, MinValue = 1, MaxValue = 20)]
        public int MaxTradesPerDay { get; set; }

        [Parameter("Max Spread (pips)", Group = "Trade & Risk", DefaultValue = 8.0, MinValue = 0.1)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Max Equity DD %", Group = "Trade & Risk", DefaultValue = 8.0, MinValue = 0.0)]
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

        [Parameter("Debug Logging", Group = "Trade & Risk", DefaultValue = true)]
        public bool DebugLogging { get; set; }

        // ─── Wyckoff Wave ───────────────────────────────────
        [Parameter("Pivot Distance (×ATR)", Group = "Wyckoff Wave", DefaultValue = 1.2, MinValue = 0.5)]
        public double MinPivotAtrMult { get; set; }

        [Parameter("Spring Max Penetration (Pips)", Group = "Wyckoff Wave", DefaultValue = 15.0, MinValue = 1.0)]
        public double SpringMaxPenetrationPips { get; set; }

        [Parameter("Require Higher Low / Lower High", Group = "Wyckoff Wave", DefaultValue = true)]
        public bool RequireStructureBias { get; set; }

        [Parameter("Require HTF Structure Bias", Group = "Wyckoff Wave", DefaultValue = true)]
        public bool RequireHtfTrend { get; set; }

        [Parameter("HTF Trend Timeframe", Group = "Wyckoff Wave")]
        public TimeFrame HtfTimeframe { get; set; } = TimeFrame.Hour;

        // ─── Visuals & Chart ───────────────────────────────
        [Parameter("Visualize Profile", Group = "Visuals & Chart", DefaultValue = false)]
        public bool VisualizeProfile { get; set; }

        [Parameter("Visualize Entry Markers", Group = "Visuals & Chart", DefaultValue = true)]
        public bool VisualizeEntryMarkers { get; set; }

        // ─── Volume Profile V2 ──────────────────────────────
        [Parameter("VP Mode", Group = "Volume Profile V2", DefaultValue = VpLookbackMode.RollingHours)]
        public VpLookbackMode ProfileMode { get; set; }

        [Parameter("VP Lookback (Hours)", Group = "Volume Profile V2", DefaultValue = 8.0, MinValue = 1.0, MaxValue = 168.0)]
        public double VpLookbackHours { get; set; }

        [Parameter("VP Bin Size", Group = "Volume Profile V2", DefaultValue = 0.5, MinValue = 0.01)]
        public double VpBinSize { get; set; }

        [Parameter("VP Lookback (Days)", Group = "Volume Profile V2", DefaultValue = 1, MinValue = 1, MaxValue = 30)]
        public int VpLookbackDays { get; set; }

        [Parameter("VP Value Area %", Group = "Volume Profile V2", DefaultValue = 0.70, MinValue = 0.1, MaxValue = 0.99)]
        public double VpValueAreaPercent { get; set; }

        [Parameter("Use M1 Source Bars", Group = "Volume Profile V2", DefaultValue = true)]
        public bool UseM1SourceBars { get; set; }

        [Parameter("Use Gaussian Smooth", Group = "Volume Profile V2", DefaultValue = true)]
        public bool UseGaussianSmooth { get; set; }

        [Parameter("Touch Buffer (×ATR)", Group = "Volume Profile V2", DefaultValue = 0.20, MinValue = 0.0)]
        public double TouchBufferAtrMult { get; set; }

        // ─── Order Flow Delta ───────────────────────────────
        [Parameter("Require Delta Filter", Group = "Order Flow Delta", DefaultValue = false)]
        public bool RequireDeltaFilter { get; set; }

        [Parameter("Min Delta Imbalance Ratio", Group = "Order Flow Delta", DefaultValue = 1.25, MinValue = 1.0)]
        public double MinDeltaImbalance { get; set; }

        [Parameter("Delta Window (ms)", Group = "Order Flow Delta", DefaultValue = 300000, MinValue = 10000)]
        public int DeltaWindowMs { get; set; }

        // ─── Stop Loss ──────────────────────────────────────
        [Parameter("ATR Period", Group = "Stop Loss", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        [Parameter("SL Buffer (×ATR)", Group = "Stop Loss", DefaultValue = 0.5, MinValue = 0.0)]
        public double SlAtrMult { get; set; }

        [Parameter("Min SL Distance (×ATR)", Group = "Stop Loss", DefaultValue = 0.8, MinValue = 0.1)]
        public double MinSlAtrMult { get; set; }

        [Parameter("Structure Lookback (Bars)", Group = "Stop Loss", DefaultValue = 10, MinValue = 1, MaxValue = 100)]
        public int StructureLookbackBars { get; set; }

        // ─── Take Profit ────────────────────────────────────
        [Parameter("TP Mode", Group = "Take Profit", DefaultValue = GoldFlowTpMode.RiskReward)]
        public GoldFlowTpMode TakeProfitMode { get; set; }

        [Parameter("RR Multiple", Group = "Take Profit", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0)]
        public double RrMultiple { get; set; }

        [Parameter("Fixed TP ($)", Group = "Take Profit", DefaultValue = 20.0, MinValue = 0.5)]
        public double FixedTpPrice { get; set; }

        // ─── Partial Close, BE & Trailing ────────────────────
        [Parameter("Use Partial Close", Group = "Partial TP, BE & Trailing", DefaultValue = true)]
        public bool UsePartialClose { get; set; }

        [Parameter("Partial Ratio (0.5 = 50%)", Group = "Partial TP, BE & Trailing", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 0.9)]
        public double PartialRatio { get; set; }

        [Parameter("Partial Target (R)", Group = "Partial TP, BE & Trailing", DefaultValue = 2.0, MinValue = 0.5)]
        public double PartialTargetR { get; set; }

        [Parameter("Use Break Even", Group = "Partial TP, BE & Trailing", DefaultValue = true)]
        public bool UseBreakEven { get; set; }

        [Parameter("BE Start (R)", Group = "Partial TP, BE & Trailing", DefaultValue = 1.0, MinValue = 0.1)]
        public double BeStartR { get; set; }

        [Parameter("BE Lock (R)", Group = "Partial TP, BE & Trailing", DefaultValue = 0.05, MinValue = 0.0)]
        public double BeLockR { get; set; }

        [Parameter("Use Trailing Stop", Group = "Partial TP, BE & Trailing", DefaultValue = false)]
        public bool UseTrailing { get; set; }

        [Parameter("Trail Start (R)", Group = "Partial TP, BE & Trailing", DefaultValue = 1.5, MinValue = 0.1)]
        public double TrailStartR { get; set; }

        [Parameter("Trail Step (R)", Group = "Partial TP, BE & Trailing", DefaultValue = 0.5, MinValue = 0.1)]
        public double TrailStepR { get; set; }

        // ─── Session & News ─────────────────────────────────
        [Parameter("Trade Asia", Group = "Session", DefaultValue = false)]
        public bool TradeAsia { get; set; }

        [Parameter("Trade London", Group = "Session", DefaultValue = true)]
        public bool TradeLondon { get; set; }

        [Parameter("Trade New York", Group = "Session", DefaultValue = true)]
        public bool TradeNewYork { get; set; }

        [Parameter("Trade Overlap (Lon-NY)", Group = "Session", DefaultValue = true)]
        public bool TradeOverlap { get; set; }

        [Parameter("Enable News Filter", Group = "News", DefaultValue = false)]
        public bool EnableNewsFilter { get; set; }

        [Parameter("News Blackout (min)", Group = "News", DefaultValue = 30, MinValue = 0)]
        public int NewsBlackoutMinutes { get; set; }

        [Parameter("News Schedule UTC", Group = "News", DefaultValue = "")]
        public string NewsSchedule { get; set; }

        // ─── Private Modules & State ────────────────────────
        private CLogger _logger;
        private CRiskManager _riskManager;
        private CSessionFilter _sessionFilter;
        private CNewsFilter _newsFilter;
        private CTrailingManager _trailingManager;
        private CVolumeProfileV2 _volumeProfile;
        private CTickDeltaEngine _deltaEngine;
        private CWyckoffWaveEngine _wyckoffEngine;
        private CWyckoffWaveEngine _htfWyckoffEngine;
        private AverageTrueRange _atr;
        private AverageTrueRange _htfAtr;
        private Bars _m1Bars;
        private Bars _htfBars;

        private int _tradesToday;
        private int _lastTradeDay;

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init(BotLabel, DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);
            _logger.Info($"Starting GoldFlowWyckoff v1.0 on {SymbolName}");

            _newsFilter = new CNewsFilter();
            _newsFilter.Init(EnableNewsFilter, NewsBlackoutMinutes, _logger);
            if (!string.IsNullOrWhiteSpace(NewsSchedule))
            {
                _newsFilter.LoadFromString(NewsSchedule);
            }

            _riskManager = new CRiskManager();
            _riskManager.Init(this, Symbol, BotLabel, _logger);
            _riskManager.SetEquityProtection(MaxEquityDrawdownPct, FlattenOnEquityDd);
            _riskManager.SetDailyLimits(MaxDailyLossAmount, MaxDailyProfitAmount, FlattenOnDailyLoss, FlattenOnDailyProfit);

            _sessionFilter = new CSessionFilter();
            _sessionFilter.Init(TradeAsia, TradeLondon, TradeNewYork, TradeOverlap, _logger);

            _trailingManager = new CTrailingManager();
            _trailingManager.Init(this, Symbol, BotLabel, _logger);

            _m1Bars = UseM1SourceBars ? MarketData.GetBars(TimeFrame.Minute, SymbolName) : null;
            _volumeProfile = new CVolumeProfileV2();
            _volumeProfile.Init(Bars, _m1Bars, Chart, 100, VisualizeProfile, _logger);
            _volumeProfile.ConfigureComposite(VpBinSize, VpLookbackDays, VpValueAreaPercent, useGaussianSmooth: UseGaussianSmooth);

            _deltaEngine = new CTickDeltaEngine();
            _deltaEngine.Init(50000, _logger);

            _wyckoffEngine = new CWyckoffWaveEngine();
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);

            // Historical Warmup for Wyckoff Engine (M5 Execution)
            int warmupStart = Math.Max(1, Bars.Count - 500);
            for (int i = warmupStart; i < Bars.Count - 1; i++)
            {
                double histAtr = _atr.Result[i];
                if (double.IsNaN(histAtr) || histAtr <= 0) histAtr = 2.0;
                _wyckoffEngine.Calculate(Bars, i, MinPivotAtrMult, histAtr);
            }
            _logger.Info($"[GoldFlowWyckoff] M5 Wyckoff Engine Warmed Up: {_wyckoffEngine.Pivots.Count} pivots calculated.");

            // Historical Warmup for HTF Wyckoff Engine (H1 Trend Bias)
            _htfBars = MarketData.GetBars(HtfTimeframe, SymbolName);
            _htfAtr = Indicators.AverageTrueRange(_htfBars, AtrPeriod, MovingAverageType.Simple);
            _htfWyckoffEngine = new CWyckoffWaveEngine();

            int htfWarmupStart = Math.Max(1, _htfBars.Count - 500);
            for (int i = htfWarmupStart; i < _htfBars.Count - 1; i++)
            {
                double histAtr = _htfAtr.Result[i];
                if (double.IsNaN(histAtr) || histAtr <= 0) histAtr = 2.0;
                _htfWyckoffEngine.Calculate(_htfBars, i, MinPivotAtrMult, histAtr);
            }
            _logger.Info($"[GoldFlowWyckoff] HTF ({HtfTimeframe}) Wyckoff Engine Warmed Up: {_htfWyckoffEngine.Pivots.Count} pivots calculated.");

            _tradesToday = 0;
            _lastTradeDay = Server.TimeInUtc.Day;

            Positions.Closed += OnPositionClosed;

            _logger.Info($"[GoldFlowWyckoff CONFIG] SizeMode={SizeMode}, RiskAmount=${RiskAmount:F2}, RiskPercent={RiskPercent:F2}%, FixedLots={FixedLots}");
            _logger.Info("GoldFlowWyckoff initialized successfully. Active sessions: " + _sessionFilter.DescribeEnabled());
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if (pos == null || pos.Label != BotLabel) return;
            _logger.Info($"[POSITION CLOSED] Id={pos.Id} | Side={pos.TradeType} | Reason={args.Reason} | Vol={pos.VolumeInUnits} units ({pos.VolumeInUnits / Symbol.LotSize:F2} lots) | Entry={pos.EntryPrice:F2} | SL={pos.StopLoss:F2} | TP={pos.TakeProfit:F2} | GrossPnL=${pos.GrossProfit:F2} | NetPnL=${pos.NetProfit:F2} | Swap=${pos.Swap:F2} | Comm=${pos.Commissions:F2}");
        }

        protected override void OnTick()
        {
            DateTime utc = Server.TimeInUtc;
            if (utc.Day != _lastTradeDay)
            {
                _tradesToday = 0;
                _lastTradeDay = utc.Day;
                _deltaEngine.Reset();
            }

            _deltaEngine.OnTick(Symbol.Bid, Symbol.Ask, utc);

            _riskManager.OnTick();
            _trailingManager.OnTick();

            ManagePartialAndBreakeven();
        }

        protected override void OnBar()
        {
            if (!EnableTrading) return;

            DateTime utc = Server.TimeInUtc;
            double currentAtr = _atr.Result.Last(1);
            if (double.IsNaN(currentAtr) || currentAtr <= 0)
            {
                if (DebugLogging) _logger.Debug($"[OnBar SKIPPED] Invalid ATR: {currentAtr}");
                return;
            }

            // Update Wyckoff wave engine (M5 Execution & HTF Trend Bias)
            _wyckoffEngine.Calculate(Bars, Bars.Count - 2, MinPivotAtrMult, currentAtr);

            double currentHtfAtr = _htfAtr.Result.Last(1);
            if (!double.IsNaN(currentHtfAtr) && currentHtfAtr > 0 && _htfBars.Count >= 2)
            {
                _htfWyckoffEngine.Calculate(_htfBars, _htfBars.Count - 2, MinPivotAtrMult, currentHtfAtr);
            }

            // Rebuild Volume Profile composite (Rolling Hours vs Daily)
            ProfileData profileSnapshot = ProfileMode == VpLookbackMode.RollingHours
                ? _volumeProfile.BuildRollingHours(utc, VpLookbackHours)
                : _volumeProfile.BuildComposite(utc);

            if (profileSnapshot == null || !profileSnapshot.IsValid)
            {
                if (DebugLogging) _logger.Debug($"[OnBar SKIPPED] Profile Data Invalid or Null at {utc:yyyy.MM.dd HH:mm}");
                return;
            }

            // Check Risk & Filters
            if (_tradesToday >= MaxTradesPerDay)
            {
                if (DebugLogging) _logger.Debug($"[OnBar SKIPPED] MaxTradesPerDay reached: {_tradesToday}/{MaxTradesPerDay}");
                return;
            }

            double spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
            if (spreadPips > MaxSpreadPips)
            {
                if (DebugLogging) _logger.Debug($"[OnBar SKIPPED] Spread ({spreadPips:F1} pips) exceeds max ({MaxSpreadPips:F1})");
                return;
            }

            if (!_sessionFilter.IsTradingAllowed(utc))
            {
                if (DebugLogging) _logger.Debug($"[OnBar SKIPPED] Session Filter Closed at {utc:HH:mm} UTC");
                return;
            }

            if (!_newsFilter.IsTradingAllowed(utc))
            {
                if (DebugLogging) _logger.Debug($"[OnBar SKIPPED] News Blackout Active at {utc:HH:mm} UTC");
                return;
            }

            if (!_riskManager.CanOpenNewTrade)
            {
                if (DebugLogging) _logger.Debug($"[OnBar SKIPPED] RiskManager blocked new trade");
                return;
            }

            if (Positions.Count(p => p.Label == BotLabel) > 0)
            {
                if (DebugLogging) _logger.Debug($"[OnBar SKIPPED] Position already open for label {BotLabel}");
                return;
            }

            EvaluateSignals(profileSnapshot, currentAtr);
        }

        private void EvaluateSignals(ProfileData profile, double atr)
        {
            Bar closedBar = Bars[Bars.Count - 2];
            double val = profile.VAL;
            double vah = profile.VAH;

            int startIdx = Math.Max(0, Bars.Count - 1 - StructureLookbackBars);
            double recentLow = double.MaxValue;
            double recentHigh = double.MinValue;
            for (int i = startIdx; i < Bars.Count - 1; i++)
            {
                if (Bars[i].Low < recentLow) recentLow = Bars[i].Low;
                if (Bars[i].High > recentHigh) recentHigh = Bars[i].High;
            }
            if (recentLow == double.MaxValue) recentLow = closedBar.Low;
            if (recentHigh == double.MinValue) recentHigh = closedBar.High;

            var lastLowPivot = _wyckoffEngine.GetLastPivot(WyckoffWaveDirection.Down);
            var lastHighPivot = _wyckoffEngine.GetLastPivot(WyckoffWaveDirection.Up);
            double lastWyckoffPivotLow = lastLowPivot != null ? lastLowPivot.Price : closedBar.Low;
            double lastWyckoffPivotHigh = lastHighPivot != null ? lastHighPivot.Price : closedBar.High;

            var ctx = new SignalContext
            {
                Profile = profile,
                ClosedBar = closedBar,
                Atr = atr,
                PipSize = Symbol.PipSize,
                RequireDeltaFilter = RequireDeltaFilter,
                BuyImbalance = _deltaEngine.GetImbalance(DeltaWindowMs, 10),
                SellImbalance = _deltaEngine.GetSellImbalance(DeltaWindowMs, 10),
                MinDeltaImbalance = MinDeltaImbalance,
                RequireStructureBias = RequireStructureBias,
                IsHigherLow = _wyckoffEngine.IsHigherLow(),
                IsLowerHigh = _wyckoffEngine.IsLowerHigh(),
                IsSpring = _wyckoffEngine.IsSpringPattern(closedBar, val, SpringMaxPenetrationPips, Symbol.PipSize),
                IsUpthrust = _wyckoffEngine.IsUpthrustPattern(closedBar, vah, SpringMaxPenetrationPips, Symbol.PipSize),
                RequireHtfTrend = RequireHtfTrend,
                IsHtfHigherLow = _htfWyckoffEngine != null && _htfWyckoffEngine.IsHigherLow(),
                IsHtfLowerHigh = _htfWyckoffEngine != null && _htfWyckoffEngine.IsLowerHigh(),
                RecentStructureLow = recentLow,
                RecentStructureHigh = recentHigh,
                LastWyckoffPivotLow = lastWyckoffPivotLow,
                LastWyckoffPivotHigh = lastWyckoffPivotHigh,
                TouchBuffer = TouchBufferAtrMult * atr,
                SlAtrMult = SlAtrMult,
                MinSlAtrMult = MinSlAtrMult,
                TakeProfitMode = TakeProfitMode,
                RrMultiple = RrMultiple,
                FixedTpPrice = FixedTpPrice
            };

            SignalResult result = SignalEngine.Evaluate(ctx);

            if (DebugLogging)
            {
                _logger.Debug($"[EVAL BAR {closedBar.OpenTime:yyyy.MM.dd HH:mm}] POC={profile.POC:F2} VAH={vah:F2} VAL={val:F2} BarLow={closedBar.Low:F2} BarHigh={closedBar.High:F2} | Result: {(result.IsValid ? "PASSED " + result.Side : "REJECTED -> " + result.Reason)}");
            }

            if (!result.IsValid) return;

            ExecuteTrade(result);
        }

        private void ExecuteTrade(SignalResult signal)
        {
            double entryPrice = signal.Side == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            double slPriceDist = Math.Abs(entryPrice - signal.StopLossPrice);
            double slPips = slPriceDist / Symbol.PipSize;

            double volume = 0;
            double expectedRisk = 0;

            if (SizeMode == GoldFlowLotSizeMode.FixedLots)
            {
                volume = _riskManager.CalculateVolume(FixedLots);
            }
            else if (SizeMode == GoldFlowLotSizeMode.RiskAmount)
            {
                volume = _riskManager.CalculateVolumeFromRiskMoney(RiskAmount, slPriceDist, out expectedRisk);
            }
            else
            {
                volume = _riskManager.CalculateVolumeFromRisk(Account.Balance, RiskPercent, slPriceDist, out expectedRisk);
            }

            if (volume <= 0)
            {
                _logger.Warn($"Calculated volume is 0 for SL distance {slPriceDist:F4}. Skipping trade.");
                return;
            }

            _logger.Info($"[RISK EXECUTE] Side={signal.Side} | SizeMode={SizeMode} | TargetRisk=${(SizeMode == GoldFlowLotSizeMode.RiskAmount ? RiskAmount : (Account.Balance * RiskPercent / 100.0)):F2} | SLDist={slPriceDist:F2} ({slPips:F1} pips) | Volume={volume} units ({volume / Symbol.LotSize:F2} lots) | EstLoss=${expectedRisk:F2}");

            // Set up exits in CTrailingManager
            if (UseBreakEven)
            {
                _trailingManager.SetBreakevenPoints(slPips * BeStartR, slPips * BeLockR, true);
            }

            if (UseTrailing)
            {
                _trailingManager.SetTrailPoints(slPips * TrailStartR, slPips * TrailStepR, 1.0);
            }

            double slPipsForOrder = PriceUtils.PriceToPips(Math.Abs(entryPrice - signal.StopLossPrice), Symbol);
            double tpPipsForOrder = PriceUtils.PriceToPips(Math.Abs(entryPrice - signal.TakeProfitPrice), Symbol);

            var tradeResult = ExecuteMarketOrder(signal.Side, SymbolName, volume, BotLabel, slPipsForOrder, tpPipsForOrder);
            if (tradeResult.IsSuccessful)
            {
                _tradesToday++;
                _logger.Info($"[GoldFlowWyckoff] {signal.Side} Order Executed: Reason='{signal.Reason}', Lot={volume}, SL={signal.StopLossPrice:F2}, TP={signal.TakeProfitPrice:F2}");

                if (VisualizeEntryMarkers)
                {
                    string markerId = $"GFW_ENTRY_{Server.TimeInUtc.Ticks}";
                    ChartIconType icon = signal.Side == TradeType.Buy ? ChartIconType.UpArrow : ChartIconType.DownArrow;
                    Color col = signal.Side == TradeType.Buy ? Color.Green : Color.Red;
                    Chart.DrawIcon(markerId, icon, Bars.Count - 1, entryPrice, col);
                }
            }
            else
            {
                _logger.Warn($"[GoldFlowWyckoff] Order Execution Failed: {tradeResult.Error}");
            }
        }

        private void ManagePartialAndBreakeven()
        {
            var botPositions = Positions.Where(p => p.Label == BotLabel).ToList();
            if (botPositions.Count == 0) return;

            foreach (var pos in botPositions)
            {
                if (!UsePartialClose) continue;
                if (pos.VolumeInUnits <= Symbol.VolumeInUnitsMin * 2) continue;

                double openPrice = pos.EntryPrice;
                double currentPrice = pos.TradeType == TradeType.Buy ? Symbol.Bid : Symbol.Ask;
                double profitPrice = pos.TradeType == TradeType.Buy ? (currentPrice - openPrice) : (openPrice - currentPrice);

                double slDistancePrice = pos.StopLoss.HasValue ? Math.Abs(openPrice - pos.StopLoss.Value) : 0;
                if (slDistancePrice <= 0) continue;

                if (profitPrice >= PartialTargetR * slDistancePrice)
                {
                    double closeVolume = Symbol.NormalizeVolumeInUnits(pos.VolumeInUnits * PartialRatio, RoundingMode.Down);
                    if (closeVolume >= Symbol.VolumeInUnitsMin)
                    {
                        ClosePosition(pos, closeVolume);
                        _logger.Info($"[GoldFlowWyckoff] Partial Close 50% executed: {closeVolume} units at {PartialTargetR:F1}R profit.");

                        if (UseTrailing)
                        {
                            double currentProfitPips = profitPrice / Symbol.PipSize;
                            double slPips = slDistancePrice / Symbol.PipSize;
                            _trailingManager.ArmTrailFromCurrent(pos.Id, currentProfitPips, slPips * TrailStartR, slPips * TrailStepR, 1.0);
                        }
                    }
                }
            }
        }
    }
}
