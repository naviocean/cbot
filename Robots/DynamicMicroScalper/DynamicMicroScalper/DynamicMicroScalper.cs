using System;
using System.Diagnostics;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using RedWave.Common;
using RedWave.MicroScalper;

namespace cTrader.Bots
{
    [Robot(AccessRights = AccessRights.None)]
    public class DynamicMicroScalper : Robot
    {
        // === General Settings ===
        [Parameter("Magic Number", DefaultValue = 20260406001, Group = "General Settings")]
        public double InpMagic { get; set; }

        [Parameter("Lot Size (Fixed)", DefaultValue = 0.68, Group = "General Settings")]
        public double InpLotSize { get; set; }

        // === Entry Logic ===
        [Parameter("Base Distance (Pips)", DefaultValue = 140.0, Group = "Entry Logic")]
        public double InpDistancePips { get; set; }

        [Parameter("Min Move to Refresh (Pips)", DefaultValue = 70.0, Group = "Entry Logic")]
        public double InpMinMovePips { get; set; }

        [Parameter("Refresh Interval (ms)", DefaultValue = 1000, Group = "Entry Logic")]
        public int InpTimerMs { get; set; }

        // === Order Management ===
        [Parameter("Use OCO (One Cancels Other)", DefaultValue = false, Group = "Order Management")]
        public bool InpUseOCO { get; set; }

        [Parameter("Cancel Pending on Trailing", DefaultValue = true, Group = "Order Management")]
        public bool InpCancelOnTrailing { get; set; }

        // === Trade Management ===
        [Parameter("Stop Loss (Pips)", DefaultValue = 150.0, Group = "Trade Management")]
        public double InpStopLossPips { get; set; }

        [Parameter("Take Profit (Pips)", DefaultValue = 500.0, Group = "Trade Management")]
        public double InpTakeProfitPips { get; set; }

        // === Trailing Stop ===
        [Parameter("Use Trailing", DefaultValue = true, Group = "Trailing Stop")]
        public bool InpUseTrailing { get; set; }

        [Parameter("Trailing Start (Pips)", DefaultValue = 35.0, Group = "Trailing Stop")]
        public double InpTrailingStartPips { get; set; }

        [Parameter("Trailing Step (Pips)", DefaultValue = 35.0, Group = "Trailing Stop")]
        public double InpTrailingStepPips { get; set; }

        [Parameter("Trailing Sensitivity (Pips)", DefaultValue = 0.5, Group = "Trailing Stop")]
        public double InpTrailingSensitivityPips { get; set; }

        // === Break Even ===
        [Parameter("Use Break Even", DefaultValue = false, Group = "Break Even")]
        public bool InpUseBreakeven { get; set; }

        [Parameter("BE Trigger (Pips)", DefaultValue = 54.0, Group = "Break Even")]
        public double InpBE_TriggerPips { get; set; }

        [Parameter("BE Lock Profit (Pips)", DefaultValue = 16.0, Group = "Break Even")]
        public double InpBE_LockPips { get; set; }

        [Parameter("BE Add Spread", DefaultValue = true, Group = "Break Even")]
        public bool InpBE_AddSpread { get; set; }

        // === Session Filter ===
        [Parameter("Use Session Filter", DefaultValue = false, Group = "Session Filter")]
        public bool InpUseSessionFilter { get; set; }

        [Parameter("Trade Asian Session", DefaultValue = true, Group = "Session Filter")]
        public bool InpTradeAsian { get; set; }

        [Parameter("Trade London Session", DefaultValue = true, Group = "Session Filter")]
        public bool InpTradeLondon { get; set; }

        [Parameter("Trade NY Session", DefaultValue = true, Group = "Session Filter")]
        public bool InpTradeNewYork { get; set; }

        [Parameter("Trade EU-US Overlap", DefaultValue = true, Group = "Session Filter")]
        public bool InpTradeOverlap { get; set; }

        // === Time Filter ===
        [Parameter("Use Time Filter", DefaultValue = false, Group = "Time Filter")]
        public bool InpUseTimeFilter { get; set; }

        [Parameter("Mon Enabled", DefaultValue = true, Group = "Time Filter")]
        public bool InpMonEnabled { get; set; }

        [Parameter("Mon Hours", DefaultValue = "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23", Group = "Time Filter")]
        public string InpMonHours { get; set; }

        [Parameter("Tue Enabled", DefaultValue = true, Group = "Time Filter")]
        public bool InpTueEnabled { get; set; }

        [Parameter("Tue Hours", DefaultValue = "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23", Group = "Time Filter")]
        public string InpTueHours { get; set; }

        [Parameter("Wed Enabled", DefaultValue = true, Group = "Time Filter")]
        public bool InpWedEnabled { get; set; }

        [Parameter("Wed Hours", DefaultValue = "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23", Group = "Time Filter")]
        public string InpWedHours { get; set; }

        [Parameter("Thu Enabled", DefaultValue = true, Group = "Time Filter")]
        public bool InpThuEnabled { get; set; }

        [Parameter("Thu Hours", DefaultValue = "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23", Group = "Time Filter")]
        public string InpThuHours { get; set; }

        [Parameter("Fri Enabled", DefaultValue = true, Group = "Time Filter")]
        public bool InpFriEnabled { get; set; }

        [Parameter("Fri Hours", DefaultValue = "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23", Group = "Time Filter")]
        public string InpFriHours { get; set; }

        [Parameter("Sat Enabled", DefaultValue = false, Group = "Time Filter")]
        public bool InpSatEnabled { get; set; }

        [Parameter("Sat Hours", DefaultValue = "", Group = "Time Filter")]
        public string InpSatHours { get; set; }

        [Parameter("Sun Enabled", DefaultValue = false, Group = "Time Filter")]
        public bool InpSunEnabled { get; set; }

        [Parameter("Sun Hours", DefaultValue = "", Group = "Time Filter")]
        public string InpSunHours { get; set; }

        // === Market Condition ===
        [Parameter("Enable Spread Check", DefaultValue = false, Group = "Market Condition")]
        public bool InpEnableSpreadCheck { get; set; }

        [Parameter("Max Spread (Pips)", DefaultValue = 50.0, Group = "Market Condition")]
        public double InpMaxSpreadPips { get; set; }

        // === Logging ===
        [Parameter("Log Level", DefaultValue = LogLevel.Debug, Group = "Logging")]
        public LogLevel InpLogLevel { get; set; }

        // Global Objects
        private CLogger _logger;
        private CRiskManager _riskManager;
        private CTrailingManager _trailingManager;
        private CSessionFilter _sessionFilter;
        private CTimeFilter _timeFilter;
        private CMarketCondition _marketCondition;
        private CMicroEngine _microEngine;

        // State & Timing
        private DateTime _lastExecutionTime;
        private bool _botEnabled = true;
        private double _initialEquity;
        private string _botLabel;

        // UI Controls
        private TextBlock _statusText;
        private TextBlock _spreadText;
        private TextBlock _anchorText;
        private Button _toggleButton;

        protected override void OnStart()
        {
            _botLabel = InpMagic.ToString("F0");
            _initialEquity = Account.Equity;

            // Initialize Logger
            _logger = new CLogger();
            _logger.Init("MicroScalper", InpLogLevel, Print);

            _logger.Info("Starting Dynamic Micro-Breakout Scalper (v1.3 Hybrid Port)...");

            // Initialize Risk Manager
            _riskManager = new CRiskManager();
            _riskManager.Init(Symbol, _logger);

            // Log Symbol Specifications
            _logger.Info($"Symbol Specifications for {Symbol.Name}: Digits={Symbol.Digits}, TickSize={Symbol.TickSize}, PipSize={Symbol.PipSize}, PipValue={Symbol.PipValue}, LotSize={Symbol.LotSize}, MinSLDistance={Symbol.MinStopLossDistance}, MinTPDistance={Symbol.MinTakeProfitDistance}");

            // Initialize Trailing Stop Manager
            _trailingManager = new CTrailingManager();
            _trailingManager.Init(this, Symbol, _botLabel, _logger);
            if (InpUseTrailing)
            {
                _trailingManager.SetTrailPoints(InpTrailingStartPips, InpTrailingStepPips, InpTrailingSensitivityPips);
            }
            if (InpUseBreakeven)
            {
                _trailingManager.SetBreakevenPoints(InpBE_TriggerPips, InpBE_LockPips, InpBE_AddSpread);
            }

            // Initialize Filters
            _sessionFilter = new CSessionFilter();
            _sessionFilter.Init(InpTradeAsian, InpTradeLondon, InpTradeNewYork, InpTradeOverlap, _logger);

            _timeFilter = new CTimeFilter();
            _timeFilter.Init(_logger);
            _timeFilter.SetDayHours(1, InpMonEnabled, InpMonHours); // Monday
            _timeFilter.SetDayHours(2, InpTueEnabled, InpTueHours); // Tuesday
            _timeFilter.SetDayHours(3, InpWedEnabled, InpWedHours); // Wednesday
            _timeFilter.SetDayHours(4, InpThuEnabled, InpThuHours); // Thursday
            _timeFilter.SetDayHours(5, InpFriEnabled, InpFriHours); // Friday
            _timeFilter.SetDayHours(6, InpSatEnabled, InpSatHours); // Saturday
            _timeFilter.SetDayHours(0, InpSunEnabled, InpSunHours); // Sunday

            _marketCondition = new CMarketCondition();
            _marketCondition.Init(Symbol, _logger);
            _marketCondition.SetSpreadCheck(InpEnableSpreadCheck, InpMaxSpreadPips);

            // Initialize MicroEngine
            _microEngine = new CMicroEngine();
            _microEngine.Init(this, Symbol, _botLabel, _trailingManager, InpUseOCO, InpCancelOnTrailing, _logger);

            // Initialize last execution time to trigger immediately on first tick
            _lastExecutionTime = Server.Time.AddMilliseconds(-InpTimerMs);

            // Set up chart drawing & controls
            BuildChartUI();

            // Event Handlers for cTrader events
            Positions.Opened += OnPositionOpened;
            PendingOrders.Cancelled += OnPendingOrderCancelled;
            PendingOrders.Filled += OnPendingOrderFilled;

            _logger.Info("Bot successfully started.");
        }

        protected override void OnTick()
        {
            // Always update trailing stops & OCO checks immediately on every tick
            SyncTrailingAndOCO();
            _trailingManager.OnTick();

            // Update UI values
            UpdateChartUI();

            // If bot is disabled manually via UI, skip entries
            if (!_botEnabled) return;

            // Check trading limits and rules
            if (!IsTradingAllowed())
            {
                RemoveChartLines();
                return;
            }

            // Hybrid simulated time check for entries & anchor drift
            if ((Server.Time - _lastExecutionTime).TotalMilliseconds >= InpTimerMs)
            {
                double volumeUnits = _riskManager.CalculateVolume(InpLotSize);
                
                // Core execution
                _microEngine.CheckOrders(InpDistancePips, InpStopLossPips, InpTakeProfitPips, volumeUnits);
                _microEngine.RefreshAnchor(InpDistancePips, InpStopLossPips, InpTakeProfitPips, InpMinMovePips);

                // Draw levels on chart
                DrawPriceLines();

                _lastExecutionTime = Server.Time;
            }
        }

        protected override void OnStop()
        {
            RemoveChartLines();
            _logger.Info("Bot stopped.");
        }

        private bool IsTradingAllowed()
        {
            if (!_riskManager.IsTradingAllowed(_initialEquity, Account.Equity)) return false;
            if (InpUseSessionFilter && !_sessionFilter.IsTradingAllowed(Server.Time)) return false;
            if (InpUseTimeFilter && !_timeFilter.IsTradingAllowed(Server.Time)) return false;
            if (!_marketCondition.IsTradingOK()) return false;

            return true;
        }

        private void SyncTrailingAndOCO()
        {
            var activePositions = Positions.FindAll(_botLabel, Symbol.Name);
            int posCount = activePositions.Length;

            // OCO: If we have an active position, cancel all opposite pending orders
            if (posCount > 0 && InpUseOCO)
            {
                var pendingOrders = PendingOrders.Where(o => o.Label == _botLabel && o.SymbolName == Symbol.Name).ToList();
                if (pendingOrders.Count > 0)
                {
                    foreach (var order in pendingOrders)
                    {
                        CancelPendingOrder(order);
                    }
                    _logger.Info("OCO triggered: Position filled, cancelled opposite pending orders.");
                }
            }

            // Cancel on Trailing: If trailing stop has started, cancel pending orders
            if (InpCancelOnTrailing && _trailingManager.IsAnyTrailingStarted())
            {
                var pendingOrders = PendingOrders.Where(o => o.Label == _botLabel && o.SymbolName == Symbol.Name).ToList();
                if (pendingOrders.Count > 0)
                {
                    foreach (var order in pendingOrders)
                    {
                        CancelPendingOrder(order);
                    }
                    _logger.Info("Trailing triggered: Cancelled pending orders.");
                }
            }
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            var position = args.Position;
            if (position.Label == _botLabel && position.SymbolName == Symbol.Name)
            {
                _logger.Info($"Position Opened: #{position.Id} {position.TradeType} at {position.EntryPrice}");
                
                // Perform slippage correction immediately after fill
                _microEngine.SyncPosition(position, InpStopLossPips, InpTakeProfitPips);
            }
        }
        private void OnPendingOrderCancelled(PendingOrderCancelledEventArgs args)
        {
            var order = args.PendingOrder;
            if (order.Label == _botLabel && order.SymbolName == Symbol.Name)
            {
                _logger.Warn($"Pending Order #{order.Id} ({order.OrderType} {order.TradeType}) CANCELLED by server. Reason: {args.Reason}");
            }
        }

        private void OnPendingOrderFilled(PendingOrderFilledEventArgs args)
        {
            var order = args.PendingOrder;
            var position = args.Position;
            if (order.Label == _botLabel && order.SymbolName == Symbol.Name)
            {
                _logger.Info($"Pending Order #{order.Id} FILLED. Position #{position.Id} opened.");
            }
        }

        // === UI Drawing & Chart HUD ===
        private void BuildChartUI()
        {
            // 1. Control Panel Container (Glassmorphic look)
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                BackgroundColor = Color.FromHex("#121214"),
                Opacity = 0.95,
                Width = 220,
                Margin = new Thickness(0, 50, 10, 0)
            };

            // 2. Control Border
            var border = new Border
            {
                BorderColor = Color.FromHex("#2B2B30"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Child = panel
            };

            // Title
            panel.AddChild(new TextBlock
            {
                Text = "RedWave MicroScalper v1.3",
                ForegroundColor = Color.White,
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Status Row
            _statusText = new TextBlock
            {
                Text = "Status: ACTIVE",
                ForegroundColor = Color.Green,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.AddChild(_statusText);

            // Spread Row
            _spreadText = new TextBlock
            {
                Text = "Spread: 0.0 pips",
                ForegroundColor = Color.LightGray,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.AddChild(_spreadText);

            // Anchor Row
            _anchorText = new TextBlock
            {
                Text = "Anchor: 0.00000",
                ForegroundColor = Color.LightGray,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.AddChild(_anchorText);

            // Buttons
            _toggleButton = new Button
            {
                Text = "PAUSE TRADING",
                ForegroundColor = Color.White,
                BackgroundColor = Color.FromHex("#E67E22"),
                Margin = new Thickness(0, 0, 0, 5),
                Height = 24
            };
            _toggleButton.Click += OnToggleButtonClick;
            panel.AddChild(_toggleButton);

            var resetBtn = new Button
            {
                Text = "RESET ANCHOR",
                ForegroundColor = Color.White,
                BackgroundColor = Color.FromHex("#3498DB"),
                Margin = new Thickness(0, 0, 0, 5),
                Height = 24
            };
            resetBtn.Click += OnResetButtonClick;
            panel.AddChild(resetBtn);

            var closeAllBtn = new Button
            {
                Text = "CLOSE ALL",
                ForegroundColor = Color.White,
                BackgroundColor = Color.FromHex("#E74C3C"),
                Height = 24
            };
            closeAllBtn.Click += OnCloseAllButtonClick;
            panel.AddChild(closeAllBtn);

            Chart.AddControl(border);
        }

        private void UpdateChartUI()
        {
            if (_statusText == null) return;

            // Update status text
            if (!_botEnabled)
            {
                _statusText.Text = "Status: PAUSED";
                _statusText.ForegroundColor = Color.Red;
            }
            else if (!IsTradingAllowed())
            {
                _statusText.Text = "Status: INACTIVE (Filters)";
                _statusText.ForegroundColor = Color.Orange;
            }
            else
            {
                _statusText.Text = "Status: ACTIVE";
                _statusText.ForegroundColor = Color.Green;
            }

            // Update spread
            double currentSpreadPips = PriceUtils.PriceToPips(Symbol.Spread, Symbol);
            _spreadText.Text = $"Spread: {currentSpreadPips:F1} pips";

            // Update anchor price
            _anchorText.Text = $"Anchor: {_microEngine.LastAnchorPrice.ToString("F" + Symbol.Digits)}";
        }

        private void DrawPriceLines()
        {
            double anchor = _microEngine.LastAnchorPrice;
            if (anchor <= 0) return;

            double distance = PriceUtils.PipsToPrice(InpDistancePips, Symbol);

            // 1. Draw Anchor Price Line
            Chart.DrawHorizontalLine("MS_Anchor", anchor, Color.FromHex("#3498DB"), 1, LineStyle.Solid);

            // 2. Draw Buy Stop target
            Chart.DrawHorizontalLine("MS_BuyStop", anchor + distance, Color.FromHex("#2ECC71"), 1, LineStyle.Lines);

            // 3. Draw Sell Stop target
            Chart.DrawHorizontalLine("MS_SellStop", anchor - distance, Color.FromHex("#E74C3C"), 1, LineStyle.Lines);
        }

        private void RemoveChartLines()
        {
            Chart.RemoveObject("MS_Anchor");
            Chart.RemoveObject("MS_BuyStop");
            Chart.RemoveObject("MS_SellStop");
        }

        private void OnToggleButtonClick(ButtonClickEventArgs args)
        {
            _botEnabled = !_botEnabled;
            if (_botEnabled)
            {
                _toggleButton.Text = "PAUSE TRADING";
                _toggleButton.BackgroundColor = Color.FromHex("#E67E22");
                _logger.Info("Bot manual trading enabled.");
            }
            else
            {
                _toggleButton.Text = "RESUME TRADING";
                _toggleButton.BackgroundColor = Color.FromHex("#2ECC71");
                _logger.Info("Bot manual trading paused. Pending orders remain untouched, but no new orders will be placed.");
            }
            UpdateChartUI();
        }

        private void OnResetButtonClick(ButtonClickEventArgs args)
        {
            _microEngine.ForceResetAnchor();
            DrawPriceLines();
            UpdateChartUI();
            _logger.Info("Bot Anchor manually reset.");
        }

        private void OnCloseAllButtonClick(ButtonClickEventArgs args)
        {
            _logger.Warn("PANIC BUTTON: Closing all open positions for this bot...");
            var activePositions = Positions.FindAll(_botLabel, Symbol.Name);
            foreach (var pos in activePositions)
            {
                ClosePosition(pos);
            }

            var pendingOrders = PendingOrders.Where(o => o.Label == _botLabel && o.SymbolName == Symbol.Name).ToList();
            foreach (var order in pendingOrders)
            {
                CancelPendingOrder(order);
            }
        }
    }
}
