using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using RedWave.Common;

namespace cAlgo.Robots
{
    public enum LotSizeMode
    {
        RiskPercent = 0,
        RiskAmount = 1,
        FixedLots = 2
    }

    public enum MomentumMode
    {
        /// <summary>Simple price delta over window (Legacy v2.0).</summary>
        SimpleDelta = 0,

        /// <summary>Price delta + Tick Frequency (requires N ticks + M pips in window).</summary>
        TickFrequency = 1,

        /// <summary>Price delta + Order Flow Imbalance (requires Buy/Sell ratio + M pips).</summary>
        OrderFlowDelta = 2,

        /// <summary>Combo: Price delta + Tick Frequency + Order Flow Imbalance.</summary>
        Combined = 3,

        /// <summary>Test ONLY Tick Frequency in Time Window (Ignores Price Delta!).</summary>
        TickFrequencyOnly = 4,

        /// <summary>Test ONLY Order Flow Imbalance in Time Window (Ignores Price Delta!).</summary>
        OrderFlowOnly = 5
    }

    public struct AnchorTickSample
    {
        public DateTime Time;
        public double AnchorPrice;

        public AnchorTickSample(DateTime time, double anchorPrice)
        {
            Time = time;
            AnchorPrice = anchorPrice;
        }
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class GoldReversingScalper : Robot
    {
        // ─── Trade & Risk Parameters ────────────────────────
        [Parameter("Enable Trading", Group = "Trade & Risk", DefaultValue = true)]
        public bool EnableTrading { get; set; }

        [Parameter("Bot Label", Group = "Trade & Risk", DefaultValue = "GRS-01")]
        public string BotLabel { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk", DefaultValue = LotSizeMode.FixedLots)]
        public LotSizeMode SizeMode { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk", DefaultValue = 0.01, MinValue = 0.01)]
        public double FixedLots { get; set; }

        [Parameter("Risk %", Group = "Trade & Risk", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0)]
        public double RiskPercent { get; set; }

        [Parameter("Risk Amount ($)", Group = "Trade & Risk", DefaultValue = 50.0, MinValue = 1.0)]
        public double RiskAmount { get; set; }

        [Parameter("Max Spread (pips)", Group = "Trade & Risk", DefaultValue = 5.0, MinValue = 0.1)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Max Equity DD %", Group = "Trade & Risk", DefaultValue = 10.0, MinValue = 0.0)]
        public double MaxEquityDrawdownPct { get; set; }

        [Parameter("Flatten On Equity DD", Group = "Trade & Risk", DefaultValue = false)]
        public bool FlattenOnEquityDd { get; set; }

        [Parameter("Max Daily Loss ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyLossAmount { get; set; }

        [Parameter("Max Daily Profit ($)", Group = "Trade & Risk", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxDailyProfitAmount { get; set; }

        [Parameter("Debug Logging", Group = "Trade & Risk", DefaultValue = true)]
        public bool DebugLogging { get; set; }

        // ─── UI & Dashboard Parameters ──────────────────────
        [Parameter("Show Dashboard", Group = "UI & Visuals", DefaultValue = true)]
        public bool ShowDashboard { get; set; }

        // ─── Momentum Straddle Parameters ───────────────────
        [Parameter("Momentum Mode", Group = "Momentum Straddle", DefaultValue = MomentumMode.Combined)]
        public MomentumMode Mode { get; set; }

        [Parameter("Distance (pips)", Group = "Momentum Straddle", DefaultValue = 100.0, MinValue = 1.0)]
        public double DistancePips { get; set; }

        [Parameter("Momentum Window (ms)", Group = "Momentum Straddle", DefaultValue = 200, MinValue = 10)]
        public int MomentumWindowMs { get; set; }

        [Parameter("Use Min Move Check", Group = "Momentum Straddle", DefaultValue = true)]
        public bool UseMinMoveCheck { get; set; }

        [Parameter("Momentum Min Move (pips)", Group = "Momentum Straddle", DefaultValue = 50.0, MinValue = 0.0)]
        public double MomentumMinMovePips { get; set; }

        [Parameter("Min Ticks In Window", Group = "Momentum Straddle", DefaultValue = 8, MinValue = 1)]
        public int MinTicksInWindow { get; set; }

        [Parameter("Min Delta Imbalance", Group = "Momentum Straddle", DefaultValue = 1.5, MinValue = 1.0)]
        public double MinDeltaImbalance { get; set; }

        [Parameter("Wait New Bar Re-entry", Group = "Momentum Straddle", DefaultValue = true)]
        public bool WaitNewBarForReEntry { get; set; }

        // ─── Position Management Parameters ─────────────────
        [Parameter("Stop Loss (pips)", Group = "Position Management", DefaultValue = 200.0, MinValue = 0.0)]
        public double StopLossPips { get; set; }

        [Parameter("Take Profit (pips)", Group = "Position Management", DefaultValue = 400.0, MinValue = 0.0)]
        public double TakeProfitPips { get; set; }

        [Parameter("Use Trailing", Group = "Position Management", DefaultValue = true)]
        public bool UseTrailing { get; set; }

        [Parameter("Trail Start (pips)", Group = "Position Management", DefaultValue = 100.0, MinValue = 1.0)]
        public double TrailStartPips { get; set; }

        [Parameter("Trail Step (pips)", Group = "Position Management", DefaultValue = 20.0, MinValue = 0.5)]
        public double TrailStepPips { get; set; }

        // ─── Session Filter ──────────────────────────────────
        [Parameter("Trade Asia", Group = "Session", DefaultValue = false)]
        public bool TradeAsia { get; set; }

        [Parameter("Trade London", Group = "Session", DefaultValue = true)]
        public bool TradeLondon { get; set; }

        [Parameter("Trade New York", Group = "Session", DefaultValue = true)]
        public bool TradeNewYork { get; set; }

        [Parameter("Trade Overlap (Lon-NY)", Group = "Session", DefaultValue = false)]
        public bool TradeOverlap { get; set; }

        // ─── RedWave.Common & Internals ──────────────────────
        private CLogger _logger;
        private TradeExecutor _tradeExecutor;
        private CRiskManager _riskManager;
        private CSessionFilter _sessionFilter;
        private CMarketCondition _marketCondition;
        private CTrailingManager _trailingManager;
        private CTickDeltaEngine _deltaEngine;

        private readonly Queue<AnchorTickSample> _tickHistory = new Queue<AnchorTickSample>();
        private double _lastTickAnchor = 0.0;
        private bool _awaitingNewBarForStraddle = false;

        // Dashboard UI Controls
        private TextBlock _txtMode;
        private TextBlock _txtAnchor;
        private TextBlock _txtPriceDelta;
        private TextBlock _txtTicksInWindow;
        private TextBlock _txtBuyImbalance;
        private TextBlock _txtSellImbalance;
        private TextBlock _txtSpikeStatus;

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("GoldReversingScalper", DebugLogging ? LogLevel.Debug : LogLevel.Info, Print);

            _tradeExecutor = new TradeExecutor(this, _logger);

            _riskManager = new CRiskManager();
            _riskManager.Init(this, Symbol, BotLabel, _logger);
            _riskManager.SetEquityProtection(MaxEquityDrawdownPct, FlattenOnEquityDd);
            _riskManager.SetDailyLimits(MaxDailyLossAmount, MaxDailyProfitAmount, false, false);

            _sessionFilter = new CSessionFilter();
            _sessionFilter.Init(TradeAsia, TradeLondon, TradeNewYork, TradeOverlap, _logger);

            _marketCondition = new CMarketCondition();
            _marketCondition.Init(Symbol, _logger);
            _marketCondition.SetSpreadCheck(true, MaxSpreadPips);

            _trailingManager = new CTrailingManager();
            _trailingManager.Init(this, Symbol, BotLabel, _logger);
            if (UseTrailing)
            {
                double sensPips = Math.Max(1.0, TrailStepPips * 0.25);
                _trailingManager.SetTrailPoints(TrailStartPips, TrailStepPips, sensPips);
            }

            _deltaEngine = new CTickDeltaEngine();
            _deltaEngine.Init(50000, _logger);

            Positions.Closed += OnPositionClosed;
            Positions.Opened += OnPositionOpened;

            _awaitingNewBarForStraddle = false;
            _lastTickAnchor = 0.0;

            CreateDashboardUI();

            _logger.Info($"[GRS-01 v2.0] Started on {SymbolName} ({TimeFrame}). Mode={Mode}, Distance={DistancePips}pips, Window={MomentumWindowMs}ms, UseMinMove={UseMinMoveCheck}, MinMove={MomentumMinMovePips}pips, MinTicks={MinTicksInWindow}, MinDelta={MinDeltaImbalance}, Dashboard={ShowDashboard}");
        }

        protected override void OnBar()
        {
            _logger.Debug($"[OnBar] New Bar Opened at {Bars.OpenTimes.LastValue:yyyy-MM-dd HH:mm:ss} UTC (Open: {Bars.OpenPrices.LastValue:F2})");

            if (_awaitingNewBarForStraddle)
            {
                _awaitingNewBarForStraddle = false;
                _logger.Info($"[OnBar] Awaiting flag cleared at {Bars.OpenTimes.LastValue:HH:mm:ss}. Re-arming straddle creation.");
            }
        }

        protected override void OnTick()
        {
            // 1. Update Order Flow Tick Delta Engine
            _deltaEngine.OnTick(Symbol.Bid, Symbol.Ask, Server.TimeInUtc);

            // 2. Risk Protection Check (Equity DD & Daily Limits)
            _riskManager.OnTick();

            if (!EnableTrading)
                return;

            // 3. Trailing Manager OnTick for open positions
            if (UseTrailing)
                _trailingManager.OnTick();

            DateTime now = Server.TimeInUtc;
            double anchor = (Symbol.Ask + Symbol.Bid) / 2.0;

            // Maintain Tick History STRICTLY within MomentumWindowMs (purges ticks older than 200ms)
            _tickHistory.Enqueue(new AnchorTickSample(now, anchor));
            while (_tickHistory.Count > 0 && (now - _tickHistory.Peek().Time).TotalMilliseconds > MomentumWindowMs)
            {
                _tickHistory.Dequeue();
            }

            int tickCountInWindow = _tickHistory.Count;

            // Delta Price Calculation: Use oldest tick in 200ms window if multiple ticks exist, otherwise use previous tick anchor
            double referenceAnchor = _tickHistory.Count > 1 ? _tickHistory.Peek().AnchorPrice : (_lastTickAnchor > 0 ? _lastTickAnchor : anchor);
            double deltaPrice = Math.Abs(anchor - referenceAnchor);
            double deltaPips = PriceUtils.PriceToPips(deltaPrice, Symbol);
            _lastTickAnchor = anchor;

            long orderFlowWindowMs = Math.Max(MomentumWindowMs, 3000);
            double buyImbalance = _deltaEngine.GetImbalance(orderFlowWindowMs, 2);
            double sellImbalance = _deltaEngine.GetSellImbalance(orderFlowWindowMs, 2);

            bool isSpike = IsMomentumSpike(deltaPips, tickCountInWindow, buyImbalance, sellImbalance);

            // ALWAYS UPDATE DASHBOARD UI ON EVERY SINGLE TICK
            UpdateDashboardUI(anchor, deltaPips, tickCountInWindow, buyImbalance, sellImbalance, isSpike);

            var activePositions = Positions.FindAll(BotLabel, SymbolName);
            var pendingOrders = PendingOrders.Where(o => o.Label == BotLabel && o.SymbolName == SymbolName).ToList();

            // 4. Evaluate States
            if (activePositions.Length == 0)
            {
                ManageIdleState(anchor, pendingOrders, deltaPips, tickCountInWindow, isSpike);
            }
            else
            {
                // In Trade: Ensure any lingering pending orders are canceled
                if (pendingOrders.Count > 0)
                {
                    CancelAllPendingOrders(pendingOrders);
                }
            }
        }

        /// <summary>
        /// Idle State: Floating Straddle Engine.
        /// When isSpike == false (sideway / normal slow move), BUY STOP & SELL STOP trail/float after Anchor.
        /// When isSpike == true (momentum spike), BUY STOP & SELL STOP freeze (hold static) to let price hit and fill!
        /// </summary>
        private void ManageIdleState(double currentAnchor, List<PendingOrder> pendingOrders, double deltaPips, int tickCountInWindow, bool isSpike)
        {
            if (!_sessionFilter.IsTradingAllowed(Server.TimeInUtc))
            {
                _logger.Debug($"[Idle-Blocked] Trading session not allowed at {Server.TimeInUtc:HH:mm:ss} UTC");
                CancelAllPendingOrders(pendingOrders);
                return;
            }

            if (!_marketCondition.IsTradingOK())
            {
                _logger.Debug($"[Idle-Blocked] Market condition check failed (Spread={PriceUtils.PriceToPips(Symbol.Spread, Symbol):F1} > {MaxSpreadPips:F1} pips)");
                CancelAllPendingOrders(pendingOrders);
                return;
            }

            var buyStop = pendingOrders.FirstOrDefault(o => o.OrderType == PendingOrderType.Stop && o.TradeType == TradeType.Buy);
            var sellStop = pendingOrders.FirstOrDefault(o => o.OrderType == PendingOrderType.Stop && o.TradeType == TradeType.Sell);

            // Initial Straddle Placement (if no pending orders exist yet)
            if (buyStop == null || sellStop == null)
            {
                if (WaitNewBarForReEntry && _awaitingNewBarForStraddle)
                {
                    _logger.Debug($"[Idle-WaitBar] Awaiting new bar open before creating straddle (OpenTime: {Bars.OpenTimes.LastValue:HH:mm:ss})");
                    return;
                }

                double volume = CalculateVolume(StopLossPips > 0 ? StopLossPips : DistancePips);

                double distancePrice = PriceUtils.PipsToPrice(DistancePips, Symbol);
                double buyTarget = currentAnchor + distancePrice;
                double sellTarget = currentAnchor - distancePrice;

                double? buySlPrice = StopLossPips > 0 ? buyTarget - PriceUtils.PipsToPrice(StopLossPips, Symbol) : (double?)null;
                double? buyTpPrice = TakeProfitPips > 0 ? buyTarget + PriceUtils.PipsToPrice(TakeProfitPips, Symbol) : (double?)null;

                double? sellSlPrice = StopLossPips > 0 ? sellTarget - PriceUtils.PipsToPrice(StopLossPips, Symbol) : (double?)null;
                double? sellTpPrice = TakeProfitPips > 0 ? sellTarget - PriceUtils.PipsToPrice(TakeProfitPips, Symbol) : (double?)null;

                if (buyStop == null)
                {
                    _logger.Info($"[Idle-Create] Placing BUY STOP @ {buyTarget:F2} (SL: {buySlPrice:F2}, TP: {buyTpPrice:F2}, Vol: {volume})");
                    _tradeExecutor.PlaceStopByPrice(TradeType.Buy, Symbol, volume, buyTarget, BotLabel, buySlPrice, buyTpPrice);
                }

                if (sellStop == null)
                {
                    _logger.Info($"[Idle-Create] Placing SELL STOP @ {sellTarget:F2} (SL: {sellSlPrice:F2}, TP: {sellTpPrice:F2}, Vol: {volume})");
                    _tradeExecutor.PlaceStopByPrice(TradeType.Sell, Symbol, volume, sellTarget, BotLabel, sellSlPrice, sellTpPrice);
                }

                _logger.Info($"[Idle-Created] Initial Momentum Straddle created around Anchor {currentAnchor:F2} (BUY STOP: {buyTarget:F2}, SELL STOP: {sellTarget:F2})");
                return;
            }

            // CORE FLOATING STRADDLE LOGIC:
            // When NOT in Momentum Spike (isSpike == false, sideway / normal slow move):
            // DỜI (FLOAT) BUY STOP & SELL STOP THEO ANCHOR GIỮ KHOẢNG CÁCH 100 PIPS!
            if (!isSpike)
            {
                double distancePrice = PriceUtils.PipsToPrice(DistancePips, Symbol);
                double buyTarget = currentAnchor + distancePrice;
                double sellTarget = currentAnchor - distancePrice;

                double? buySlPrice = StopLossPips > 0 ? buyTarget - PriceUtils.PipsToPrice(StopLossPips, Symbol) : (double?)null;
                double? buyTpPrice = TakeProfitPips > 0 ? buyTarget + PriceUtils.PipsToPrice(TakeProfitPips, Symbol) : (double?)null;

                double? sellSlPrice = StopLossPips > 0 ? sellTarget + PriceUtils.PipsToPrice(StopLossPips, Symbol) : (double?)null;
                double? sellTpPrice = TakeProfitPips > 0 ? sellTarget - PriceUtils.PipsToPrice(TakeProfitPips, Symbol) : (double?)null;

                // Anti-Spam: Only modify if anchor moved by at least 1 TickSize
                bool buyNeedsUpdate = Math.Abs(buyStop.TargetPrice - buyTarget) >= Symbol.TickSize;
                bool sellNeedsUpdate = Math.Abs(sellStop.TargetPrice - sellTarget) >= Symbol.TickSize;

                if (buyNeedsUpdate || sellNeedsUpdate)
                {
                    _logger.Debug($"[Idle-Float] Floating Straddle to Anchor {currentAnchor:F2} | Delta: {deltaPips:F1}pips | Ticks: {tickCountInWindow} | BuyStop: {buyTarget:F2} | SellStop: {sellTarget:F2}");

                    if (buyNeedsUpdate)
                        _tradeExecutor.ModifyPendingOrderByPrice(buyStop, buyTarget, buySlPrice, buyTpPrice);

                    if (sellNeedsUpdate)
                        _tradeExecutor.ModifyPendingOrderByPrice(sellStop, sellTarget, sellSlPrice, sellTpPrice);
                }
            }
            else
            {
                // Momentum Spike DETECTED (isSpike == true):
                // KHÓA CỐ ĐỊNH LỆNH STOP ĐỂ GIÁ VỌT TỚI KHỚP LỆNH!
                _logger.Info($"[Idle-Spike] Momentum Spike DETECTED! Mode: {Mode} | Delta: {deltaPips:F1}pips | Ticks: {tickCountInWindow} in {MomentumWindowMs}ms. Holding pending orders STATIC to allow filling!");
            }
        }

        private void CancelAllPendingOrders(List<PendingOrder> pendingOrders)
        {
            if (pendingOrders == null || pendingOrders.Count == 0) return;
            foreach (var order in pendingOrders)
            {
                _logger.Debug($"[CancelPending] Canceling order #{order.Id} ({order.TradeType} @ {order.TargetPrice:F2})");
                CancelPendingOrder(order);
            }
        }

        /// <summary>
        /// Evaluates whether current market conditions constitute a Momentum Spike based on Mode.
        /// </summary>
        private bool IsMomentumSpike(double deltaPips, int tickCountInWindow, double buyImbalance, double sellImbalance)
        {
            bool priceSpike = !UseMinMoveCheck || MomentumMinMovePips <= 0 || deltaPips >= MomentumMinMovePips;
            bool frequencySpike = tickCountInWindow >= MinTicksInWindow;
            bool orderFlowSurge = buyImbalance >= MinDeltaImbalance || sellImbalance >= MinDeltaImbalance;

            bool spikeResult = Mode switch
            {
                MomentumMode.TickFrequency => priceSpike && frequencySpike,
                MomentumMode.OrderFlowDelta => priceSpike && orderFlowSurge,
                MomentumMode.Combined => priceSpike && frequencySpike && orderFlowSurge,
                MomentumMode.TickFrequencyOnly => frequencySpike,
                MomentumMode.OrderFlowOnly => orderFlowSurge,
                _ => priceSpike // SimpleDelta
            };

            _logger.Debug($"[MomentumCheck] Mode={Mode} | UseMinMove={UseMinMoveCheck} | Delta={deltaPips:F1}/{MomentumMinMovePips:F1}pips (PriceSpike={priceSpike}) | Ticks={tickCountInWindow}/{MinTicksInWindow} (FreqSpike={frequencySpike}) | BuyImbal={buyImbalance:F2}, SellImbal={sellImbalance:F2} vs Min={MinDeltaImbalance:F2} (OrderFlowSurge={orderFlowSurge}) => SpikeResult={spikeResult}");

            return spikeResult;
        }

        /// <summary>
        /// Creates the Chart Dashboard Overlay UI.
        /// </summary>
        private void CreateDashboardUI()
        {
            if (!ShowDashboard)
                return;

            var mainPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                BackgroundColor = Color.FromArgb(230, 20, 24, 32),
                Margin = "10 10 10 10",
                Width = 250
            };

            var title = new TextBlock
            {
                Text = "⚡ GRS-01 v2.0 MOMENTUM",
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                ForegroundColor = Color.Goldenrod,
                Margin = "10 8 10 6",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.AddChild(title);

            _txtMode = AddDashboardRow(mainPanel, "Mode:", Mode.ToString());
            _txtAnchor = AddDashboardRow(mainPanel, "Anchor Price:", "0.00");
            _txtPriceDelta = AddDashboardRow(mainPanel, "Price Delta:", "0.0 / 0.0 pips");
            _txtTicksInWindow = AddDashboardRow(mainPanel, "Ticks Window:", "0 / 0");
            _txtBuyImbalance = AddDashboardRow(mainPanel, "Buy Imbalance:", "1.00x");
            _txtSellImbalance = AddDashboardRow(mainPanel, "Sell Imbalance:", "1.00x");
            _txtSpikeStatus = AddDashboardRow(mainPanel, "Status:", "FLOATING", Color.LightGray);

            var container = new Border
            {
                BorderColor = Color.FromArgb(255, 60, 70, 90),
                BorderThickness = 1,
                CornerRadius = 6,
                Child = mainPanel
            };

            Chart.AddControl(container);
        }

        private TextBlock AddDashboardRow(StackPanel parent, string label, string defaultValue, Color valColor = default)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = "10 2 10 2"
            };

            var lblText = new TextBlock
            {
                Text = label,
                FontSize = 10,
                ForegroundColor = Color.Gray,
                Width = 120
            };

            Color textColor = valColor == default ? Color.White : valColor;

            var valText = new TextBlock
            {
                Text = defaultValue,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                ForegroundColor = textColor
            };

            row.AddChild(lblText);
            row.AddChild(valText);
            parent.AddChild(row);

            return valText;
        }

        private void UpdateDashboardUI(double currentAnchor, double deltaPips, int tickCount, double buyImbal, double sellImbal, bool isSpike)
        {
            if (!ShowDashboard || _txtAnchor == null)
                return;

            _txtMode.Text = Mode.ToString();
            _txtAnchor.Text = $"{currentAnchor:F2}";

            _txtPriceDelta.Text = UseMinMoveCheck ? $"{deltaPips:F1} / {MomentumMinMovePips:F1} pips" : $"{deltaPips:F1} pips (OFF)";
            _txtPriceDelta.ForegroundColor = (!UseMinMoveCheck || deltaPips >= MomentumMinMovePips) ? Color.LimeGreen : Color.White;

            _txtTicksInWindow.Text = $"{tickCount} / {MinTicksInWindow}";
            _txtTicksInWindow.ForegroundColor = tickCount >= MinTicksInWindow ? Color.LimeGreen : Color.White;

            _txtBuyImbalance.Text = $"{buyImbal:F2}x";
            _txtBuyImbalance.ForegroundColor = buyImbal >= MinDeltaImbalance ? Color.LimeGreen : Color.White;

            _txtSellImbalance.Text = $"{sellImbal:F2}x";
            _txtSellImbalance.ForegroundColor = sellImbal >= MinDeltaImbalance ? Color.Crimson : Color.White;

            if (isSpike)
            {
                _txtSpikeStatus.Text = "🔥 HOLD STATIC (Spike)";
                _txtSpikeStatus.ForegroundColor = Color.Gold;
            }
            else
            {
                _txtSpikeStatus.Text = "FLOATING (Trailing)";
                _txtSpikeStatus.ForegroundColor = Color.LightGray;
            }
        }

        /// <summary>
        /// Position Opened Event:
        /// When a pending order fills, immediately cancel the opposite pending order.
        /// </summary>
        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            Position newPos = args.Position;
            if (newPos.Label != BotLabel || newPos.SymbolName != SymbolName)
                return;

            _logger.Info($"[PositionOpened] Position #{newPos.Id} ({newPos.TradeType}) opened at {newPos.EntryPrice:F2} | SL: {newPos.StopLoss:F2} | TP: {newPos.TakeProfit:F2} | Volume: {newPos.VolumeInUnits} units ({newPos.Quantity} lots)");

            // Cancel any remaining pending orders
            var pendingOrders = PendingOrders.Where(o => o.Label == BotLabel && o.SymbolName == SymbolName).ToList();
            CancelAllPendingOrders(pendingOrders);
        }

        /// <summary>
        /// Position Closed Event:
        /// When a position is closed (SL/TP/Trailing), flag awaiting new bar for re-entry straddle.
        /// </summary>
        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            Position closedPos = args.Position;
            if (closedPos.Label != BotLabel || closedPos.SymbolName != SymbolName)
                return;

            _logger.Info($"[PositionClosed] Position #{closedPos.Id} ({closedPos.TradeType}) closed at {closedPos.GrossProfit:F2} USD (Net: {closedPos.NetProfit:F2} USD, Swap: {closedPos.Swap:F2}, Comm: {closedPos.Commissions:F2}, Reason: {args.Reason})");

            if (WaitNewBarForReEntry)
            {
                _awaitingNewBarForStraddle = true;
                _logger.Info($"[PositionClosed] Awaiting new bar open before creating next straddle.");
            }
        }

        /// <summary>
        /// Volume Calculation helper based on LotSizeMode.
        /// </summary>
        private double CalculateVolume(double slPips)
        {
            double slPriceDistance = PriceUtils.PipsToPrice(slPips, Symbol);
            double units = SizeMode switch
            {
                LotSizeMode.RiskPercent => _riskManager.CalculateVolumeFromRisk(Account.Balance, RiskPercent, slPriceDistance),
                LotSizeMode.RiskAmount => _riskManager.CalculateVolumeFromRiskMoney(RiskAmount, slPriceDistance, out _),
                _ => _riskManager.CalculateVolume(FixedLots)
            };

            if (units <= 0)
                units = Symbol.QuantityToVolumeInUnits(FixedLots);

            double normalizedUnits = Symbol.NormalizeVolumeInUnits(units);
            _logger.Debug($"[VolumeCalc] SizeMode={SizeMode} | Balance={Account.Balance:F2} | Risk%={RiskPercent} | Risk$={RiskAmount} | SLPips={slPips:F1} => Vol={normalizedUnits} units ({normalizedUnits / Symbol.LotSize:F2} lots)");

            return normalizedUnits;
        }
    }
}
