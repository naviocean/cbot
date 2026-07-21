using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using RedWave.Common;

namespace cAlgo.Robots
{
    public enum TpMode
    {
        RiskReward = 0,
        Structure = 1,
        FixedPrice = 2
    }

    public enum LotSizeMode
    {
        RiskPercent = 0,
        FixedLots = 1
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class PocAbsorption : Robot
    {
        // ─── Trade & Risk ───────────────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "PocAbsorption")]
        public string BotLabel { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk", DefaultValue = LotSizeMode.RiskPercent)]
        public LotSizeMode SizeMode { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Max Trades / Day", Group = "Trade & Risk", DefaultValue = 3, MinValue = 1, MaxValue = 20)]
        public int MaxTradesPerDay { get; set; }

        [Parameter("Max Spread (pips)", Group = "Trade & Risk", DefaultValue = 50.0, MinValue = 0.1)]
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

        [Parameter("Debug Logging", Group = "Trade & Risk", DefaultValue = true)]
        public bool DebugLogging { get; set; }

        // ─── Strategy & Profile ─────────────────────────────
        [Parameter("Volume Spike Multiplier", Group = "Strategy & Profile", DefaultValue = 1.5, MinValue = 1.0)]
        public double VolumeSpikeMultiplier { get; set; }

        [Parameter("POC Proximity Pips", Group = "Strategy & Profile", DefaultValue = 15.0)]
        public double PocProximityPips { get; set; }

        [Parameter("Delta Window (Minutes)", Group = "Strategy & Profile", DefaultValue = 15, MinValue = 1, MaxValue = 60)]
        public int DeltaWindowMinutes { get; set; }

        [Parameter("Require HTF Filter", Group = "Strategy & Profile", DefaultValue = true)]
        public bool RequireHtfFilter { get; set; }

        [Parameter("HTF Timeframe", Group = "Strategy & Profile", DefaultValue = "Hour")]
        public TimeFrame HtfTimeframe { get; set; }

        [Parameter("Visualize Profile", Group = "Strategy & Profile", DefaultValue = true)]
        public bool VisualizeProfile { get; set; }

        // ─── Stop Loss ──────────────────────────────────────
        [Parameter("ATR Period", Group = "Stop Loss", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        [Parameter("Node SL Buffer (Pips)", Group = "Stop Loss", DefaultValue = 8.0, MinValue = 0.0)]
        public double NodeSlBufferPips { get; set; }

        [Parameter("Min SL distance (×ATR)", Group = "Stop Loss", DefaultValue = 0.8, MinValue = 0.1)]
        public double MinSlAtrMult { get; set; }

        // ─── Take Profit ────────────────────────────────────
        [Parameter("TP Mode", Group = "Take Profit", DefaultValue = TpMode.RiskReward)]
        public TpMode TakeProfitMode { get; set; }

        [Parameter("RR Multiple", Group = "Take Profit", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0)]
        public double RrMultiple { get; set; }

        [Parameter("Fixed TP ($)", Group = "Take Profit", DefaultValue = 20.0, MinValue = 0.5)]
        public double FixedTpPrice { get; set; }

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

        [Parameter("Trail Start (R)", Group = "Trailing", DefaultValue = 1.5, MinValue = 0.1, MaxValue = 10.0)]
        public double TrailStartR { get; set; }

        [Parameter("Trail Step (R)", Group = "Trailing", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 5.0)]
        public double TrailStepR { get; set; }

        // ─── Session Filters ─────────────────────────────────
        [Parameter("Trade London", Group = "Session", DefaultValue = true)]
        public bool TradeLondon { get; set; }

        [Parameter("Trade New York", Group = "Session", DefaultValue = true)]
        public bool TradeNewYork { get; set; }

        [Parameter("Trade Asian", Group = "Session", DefaultValue = false)]
        public bool TradeAsia { get; set; }

        [Parameter("Trade Overlap", Group = "Session", DefaultValue = true)]
        public bool TradeOverlap { get; set; }

        // ─── News Filter ────────────────────────────────────
        [Parameter("Enable News Filter", Group = "News Filter", DefaultValue = true)]
        public bool EnableNewsFilter { get; set; }

        [Parameter("News Blackout (Minutes)", Group = "News Filter", DefaultValue = 15, MinValue = 1)]
        public int NewsBlackoutMinutes { get; set; }

        [Parameter("News Schedule String", Group = "News Filter", DefaultValue = "")]
        public string NewsSchedule { get; set; }

        // ─── Component Fields ───────────────────────────────
        private CLogger _logger;
        private CNewsFilter _newsFilter;
        private CRiskManager _riskManager;
        private CSessionFilter _sessionFilter;
        private CTrailingManager _trailingManager;
        private CVolumeProfile _volumeProfile;
        private CTickDeltaEngine _tickDeltaEngine;
        private AverageTrueRange _atr;
        private Bars _htfBars;
        private int _tradesToday;
        private int _lastTradeDay;

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init(BotLabel, DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);
            _logger.Info($"Starting PocAbsorption (PADR v1.0) on {SymbolName} TF={TimeFrame}");

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

            _volumeProfile = new CVolumeProfile();
            _volumeProfile.Init(Bars, Chart, 100, VisualizeProfile, _logger);
            _volumeProfile.ConfigureComposite(0.5, 4, 0.70);

            _tickDeltaEngine = new CTickDeltaEngine();
            _tickDeltaEngine.Init(50000, _logger);

            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);
            _htfBars = MarketData.GetBars(HtfTimeframe, SymbolName);

            _tradesToday = 0;
            _lastTradeDay = Server.TimeInUtc.Day;

            _logger.Info("PocAbsorption initialized successfully. Active sessions: " + _sessionFilter.DescribeEnabled());
            UpdateHudDisplay(null, "Initialized");
        }

        protected override void OnTick()
        {
            DateTime utc = Server.TimeInUtc;
            if (utc.Day != _lastTradeDay)
            {
                _tradesToday = 0;
                _lastTradeDay = utc.Day;
                _tickDeltaEngine.Reset();
            }

            // Feed tick to engines
            _tickDeltaEngine.OnTick(Symbol.Bid, Symbol.Ask, utc);

            // Trailing & Risk Management on tick
            _riskManager.OnTick();
            _trailingManager.OnTick();
        }

        protected override void OnBar()
        {
            if (!EnableTrading) return;

            var activePositions = Positions.FindAll(BotLabel, SymbolName);
            bool hasOpenPos = activePositions.Length > 0;

            // Rebuild volume profile snapshot
            ProfileData profileSnapshot = _volumeProfile.BuildComposite(Server.TimeInUtc);
            double spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;

            long deltaMs = DeltaWindowMinutes * 60 * 1000L;
            double cvd = _tickDeltaEngine.GetCvd(deltaMs);
            double buyImb = _tickDeltaEngine.GetImbalance(deltaMs);
            double sellImb = _tickDeltaEngine.GetSellImbalance(deltaMs);

            // HTF Trend Bias Determination
            HtfBias htfTrend = HtfBias.Neutral;
            if (_htfBars != null && _htfBars.Count > 1)
            {
                double htfClose = _htfBars.ClosePrices.Last(1);
                double htfOpen = _htfBars.OpenPrices.Last(1);
                htfTrend = htfClose >= htfOpen ? HtfBias.Bullish : HtfBias.Bearish;
            }

            var ctx = new SignalContext
            {
                Profile = profileSnapshot,
                BarOpen = Bars.OpenPrices.Last(1),
                BarHigh = Bars.HighPrices.Last(1),
                BarLow = Bars.LowPrices.Last(1),
                BarClose = Bars.ClosePrices.Last(1),
                Atr = _atr.Result.Last(1),
                PipSize = Symbol.PipSize,
                RequireHtfFilter = RequireHtfFilter,
                HtfTrend = htfTrend,
                Cvd = cvd,
                BuyImbalance = buyImb,
                SellImbalance = sellImb,
                VolumeSpikeMultiplier = VolumeSpikeMultiplier,
                PocProximityPips = PocProximityPips,
                NodeSlBufferPips = NodeSlBufferPips,
                MinSlAtrMult = MinSlAtrMult,
                TakeProfitMode = TakeProfitMode,
                RrMultiple = RrMultiple,
                FixedTpPrice = FixedTpPrice,
                SessionOk = _sessionFilter.IsTradingAllowed(Server.TimeInUtc),
                NewsOk = _newsFilter.IsTradingAllowed(Server.TimeInUtc),
                SpreadOk = spreadPips <= MaxSpreadPips,
                EquityOk = _riskManager.CanOpenNewTrade,
                TradesToday = _tradesToday,
                MaxTradesPerDay = MaxTradesPerDay,
                HasOpenPosition = hasOpenPos
            };

            var result = SignalEngine.Evaluate(ctx);

            // Update Visualization & HUD
            if (VisualizeProfile && profileSnapshot != null && profileSnapshot.IsValid)
            {
                DrawProfileVisuals(profileSnapshot, result);
            }
            UpdateHudDisplay(profileSnapshot, result.Reason);

            if (!result.IsValid)
            {
                if (DebugLogging)
                {
                    _logger.Debug($"[BAR EVAL] {Bars.OpenTimes.Last(1):yyyy.MM.dd HH:mm} | POC={profileSnapshot?.POC:F2} | VolSpike={result.VolSpikeRatio:F2}x | HTF={htfTrend} | CVD({DeltaWindowMinutes}m)={cvd:F0} | Result: REJECT ({result.Reason})");
                }
                return;
            }

            // Calculate Volume (Fixed Lots vs Risk %)
            TradeType tradeType = result.Side == SignalSide.Long ? TradeType.Buy : TradeType.Sell;
            double slPriceDist = Math.Abs(ctx.BarClose - result.StopLossPrice);
            double slPips = slPriceDist / Symbol.PipSize;

            double volume = 0;
            double expectedRisk = 0;

            if (SizeMode == LotSizeMode.FixedLots)
            {
                volume = _riskManager.CalculateVolume(FixedLots);
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

            // Configure Exit points (BE & Trailing in R)
            ConfigureExitsForTrade(slPips);

            _logger.Info($"[ENTRY PASSED] {result.Reason} | Side={tradeType} | Vol={volume} | SL={result.StopLossPrice:F2} | TP={result.TakeProfitPrice:F2} | RR={result.CalculatedRr:F2}");

            // Draw entry marker on chart
            if (VisualizeProfile)
            {
                string markerId = $"PADR_ENTRY_{Server.TimeInUtc.Ticks}";
                ChartIconType icon = tradeType == TradeType.Buy ? ChartIconType.UpArrow : ChartIconType.DownArrow;
                Color col = tradeType == TradeType.Buy ? Color.Green : Color.Red;
                Chart.DrawIcon(markerId, icon, Bars.Count - 1, ctx.BarClose, col);
            }

            var tradeResult = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, result.StopLossPrice, result.TakeProfitPrice);
            if (tradeResult.IsSuccessful)
            {
                _tradesToday++;
                _logger.Info($"Order Executed Successfully: ID={tradeResult.Position.Id}");
            }
            else
            {
                _logger.Error($"Order Execution Failed: {tradeResult.Error}");
            }
        }

        private void ConfigureExitsForTrade(double slPips)
        {
            if (UseBreakEven)
            {
                double triggerPips = slPips * BeStartR;
                double lockPips = slPips * BeLockR;
                _trailingManager.SetBreakevenPoints(triggerPips, lockPips, BeAddSpread);
            }

            if (UseTrailing)
            {
                double startPips = slPips * TrailStartR;
                double stepPips = slPips * TrailStepR;
                _trailingManager.SetTrailPoints(startPips, stepPips, 1.0);
            }
        }

        private void DrawProfileVisuals(ProfileData profile, SignalResult result)
        {
            if (profile == null || !profile.IsValid) return;

            Chart.DrawHorizontalLine("PADR_POC", profile.POC, Color.Orange, 2, LineStyle.Solid);
            Chart.DrawHorizontalLine("PADR_VAH", profile.VAH, Color.DarkCyan, 1, LineStyle.Lines);
            Chart.DrawHorizontalLine("PADR_VAL", profile.VAL, Color.DarkCyan, 1, LineStyle.Lines);

            if (result.NodeTopPrice > 0 && result.NodeBottomPrice > 0)
            {
                Chart.DrawHorizontalLine("PADR_NODE_TOP", result.NodeTopPrice, Color.DimGray, 1, LineStyle.Dots);
                Chart.DrawHorizontalLine("PADR_NODE_BOT", result.NodeBottomPrice, Color.DimGray, 1, LineStyle.Dots);
            }
        }

        private void UpdateHudDisplay(ProfileData profile, string statusText)
        {
            string sessionStr = _sessionFilter.IsTradingAllowed(Server.TimeInUtc) ? "ACTIVE" : "CLOSED";
            double poc = profile != null && profile.IsValid ? profile.POC : 0;
            double vah = profile != null && profile.IsValid ? profile.VAH : 0;
            double val = profile != null && profile.IsValid ? profile.VAL : 0;
            double cvd = _tickDeltaEngine.GetCvd(DeltaWindowMinutes * 60 * 1000L);
            double spikeRatio = profile != null ? SignalEngine.CalculateVolumeSpikeRatio(profile) : 0;

            string hud = $"======================================\n" +
                        $" 🎛️ PocAbsorption (PADR v1.0) - {SymbolName}\n" +
                        $"======================================\n" +
                        $" 🌐 Session Window: {sessionStr}\n" +
                        $" 📊 POC: {poc:F2} | VAH: {vah:F2} | VAL: {val:F2}\n" +
                        $" ⚡ Vol Spike Ratio: {spikeRatio:F2}x (Req: >= {VolumeSpikeMultiplier:F2}x)\n" +
                        $" 📈 CVD ({DeltaWindowMinutes}m): {cvd:+0;-0;0}\n" +
                        $" 🛡️ Exit Controls: BE={(UseBreakEven ? $"{BeStartR:F2}R" : "off")} | Trail={(UseTrailing ? $"{TrailStartR:F2}R" : "off")}\n" +
                        $" 📅 Daily Trades: {_tradesToday} / {MaxTradesPerDay}\n" +
                        $" 🎯 Last Status: {statusText}\n" +
                        $"======================================";

            Chart.DrawStaticText("PADR_HUD", hud, VerticalAlignment.Top, HorizontalAlignment.Left, Color.Gold);
        }
    }
}
