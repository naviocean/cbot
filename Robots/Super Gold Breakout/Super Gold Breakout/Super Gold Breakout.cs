using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using RedWave.Common;

namespace cAlgo.Robots
{
    public enum LotMode 
    { 
        Fixed_Lots, 
        Risk_Percent 
    }

    public enum TpSlMode 
    { 
        Fixed_Pips, 
        ATR_Dynamic 
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class GoldBreakoutCBot : Robot
    {
        //+------------------------------------------------------------------+
        //| INPUT PARAMETERS                                                 |
        //+------------------------------------------------------------------+
        [Parameter("Restrict To Gold", Group = "Trade & Risk Management", DefaultValue = false)]
        public bool Restrict_To_Gold { get; set; }

        [Parameter("Lot Size Mode", Group = "Trade & Risk Management", DefaultValue = LotMode.Risk_Percent)]
        public LotMode Lot_Size_Mode { get; set; }

        [Parameter("Fixed Lots", Group = "Trade & Risk Management", DefaultValue = 0.01)]
        public double Lots { get; set; }

        [Parameter("Risk % of Balance", Group = "Trade & Risk Management", DefaultValue = 0.5)]
        public double RiskPercent { get; set; }

        [Parameter("Max Spread (Pips)", Group = "Trade & Risk Management", DefaultValue = 3.0)]
        public double Max_Spread_Pips { get; set; }

        [Parameter("Max Slippage (Pips)", Group = "Trade & Risk Management", DefaultValue = 5.0)]
        public double MaxSlippagePips { get; set; }

        [Parameter("Bot Label (Magic)", Group = "Trade & Risk Management", DefaultValue = "GoldBreakout")]
        public string BotLabel { get; set; }

        [Parameter("Calculation Timeframe", Group = "Strategy & Breakout Settings", DefaultValue = "Minute15")]
        public TimeFrame CalcTimeframe { get; set; }

        [Parameter("Lookback Bars (N)", Group = "Strategy & Breakout Settings", DefaultValue = 15)]
        public int BarsN { get; set; }

        [Parameter("Order Buffer Dist (Pips)", Group = "Strategy & Breakout Settings", DefaultValue = 1.0)]
        public double OrderDistPips { get; set; }

        [Parameter("Order Expiration (Hours)", Group = "Strategy & Breakout Settings", DefaultValue = 1)]
        public int ExpirationHours { get; set; }

        [Parameter("Use Trend Filter", Group = "Higher Timeframe Trend Filter", DefaultValue = true)]
        public bool Use_Trend_Filter { get; set; }

        [Parameter("Trend Timeframe", Group = "Higher Timeframe Trend Filter", DefaultValue = "Hour1")]
        public TimeFrame Trend_Timeframe { get; set; }

        [Parameter("Trend MA Period", Group = "Higher Timeframe Trend Filter", DefaultValue = 50)]
        public int Trend_MA_Period { get; set; }

        [Parameter("Trend MA Method", Group = "Higher Timeframe Trend Filter", DefaultValue = MovingAverageType.Exponential)]
        public MovingAverageType Trend_MA_Method { get; set; }

        [Parameter("TP & SL Mode", Group = "TP & SL Calculation Mode", DefaultValue = TpSlMode.ATR_Dynamic)]
        public TpSlMode TPSL_Mode { get; set; }

        [Parameter("Fixed TP (Pips)", Group = "TP & SL Calculation Mode", DefaultValue = 100.0)]
        public double TpPips { get; set; }

        [Parameter("Fixed SL (Pips)", Group = "TP & SL Calculation Mode", DefaultValue = 30.0)]
        public double SlPips { get; set; }

        [Parameter("ATR Period", Group = "TP & SL Calculation Mode", DefaultValue = 14)]
        public int ATR_Period { get; set; }

        [Parameter("ATR Multiplier for TP", Group = "TP & SL Calculation Mode", DefaultValue = 6.0)]
        public double ATR_TP_Multiplier { get; set; }

        [Parameter("ATR Multiplier for SL", Group = "TP & SL Calculation Mode", DefaultValue = 2.0)]
        public double ATR_SL_Multiplier { get; set; }

        [Parameter("TSL Trigger (Pips)", Group = "Trailing Stop Settings", DefaultValue = 1.5)]
        public double TslTriggerPips { get; set; }

        [Parameter("TSL Distance (Pips)", Group = "Trailing Stop Settings", DefaultValue = 1.0)]
        public double TslPips { get; set; }

        [Parameter("Close On Friday", Group = "Weekend Gap Protection", DefaultValue = true)]
        public bool Close_On_Friday { get; set; }

        [Parameter("Friday Close Hour", Group = "Weekend Gap Protection", DefaultValue = 22)]
        public int Friday_Close_Hour { get; set; }

        [Parameter("Friday Close Minute", Group = "Weekend Gap Protection", DefaultValue = 45)]
        public int Friday_Close_Minute { get; set; }

        //+------------------------------------------------------------------+
        //| GLOBAL VARIABLES & OBJECTS                                       |
        //+------------------------------------------------------------------+
        private AverageTrueRange _atr;
        private MovingAverage _trendMa;
        private Bars _calcBars;
        private Bars _trendBars;
        
        private CTrailingManager _trailingManager;
        
        private int _lastCloseDay = -1;
        private int _lastBarCount = -1;
        private double _lastBuyStopPrice = 0;
        private double _lastSellStopPrice = 0;
        private double _highestHigh = 0;
        private double _lowestLow = 0;

        protected override void OnStart()
        {
            // Restrict EA to XAUUSD / Gold only
            if (Restrict_To_Gold)
            {
                string sym = SymbolName.ToUpper();
                if (!sym.Contains("XAU") && !sym.Contains("GOLD"))
                {
                    Print("Initialization Failed: This cBot is exclusively optimized for Gold. Current symbol: ", SymbolName);
                    Stop();
                    return;
                }
            }

            // Setup Bars and Indicators securely with null checks
            _calcBars = MarketData.GetBars(CalcTimeframe, SymbolName);
            
            if (_calcBars == null)
            {
                Print("Initialization Failed: Could not load market data for {0}. Stopping cBot.", CalcTimeframe);
                Stop();
                return;
            }

            if (TPSL_Mode == TpSlMode.ATR_Dynamic)
            {
                _atr = Indicators.AverageTrueRange(_calcBars, ATR_Period, MovingAverageType.Simple);
            }

            if (Use_Trend_Filter)
            {
                _trendBars = MarketData.GetBars(Trend_Timeframe, SymbolName);
                
                if (_trendBars == null)
                {
                    Print("Initialization Error: Failed to load higher timeframe ({0}) data. Disabling Trend Filter to prevent crash.", Trend_Timeframe);
                    Use_Trend_Filter = false;
                }
                else
                {
                    _trendMa = Indicators.MovingAverage(_trendBars.ClosePrices, Trend_MA_Period, Trend_MA_Method);
                }
            }

            // Initialize Trailing Stop Manager from RedWave.Common
            _trailingManager = new CTrailingManager();
            _trailingManager.Init(this, Symbol, BotLabel);
            if (TslTriggerPips > 0 && TslPips > 0)
            {
                _trailingManager.SetTrailPoints(TslTriggerPips, TslPips, 0.5); // 0.5 pip sensitivity as default
            }

            // Subscribe to position opened event for Slippage Protection
            Positions.Opened += OnPositionsOpened;

            // Sync expected stop prices on startup if pending orders already exist
            var startupPendingOrders = PendingOrders.Where(o => o != null && o.Label == BotLabel && o.SymbolName == SymbolName).ToArray();
            foreach (var order in startupPendingOrders)
            {
                if (order.TradeType == TradeType.Buy && order.OrderType == PendingOrderType.Stop)
                {
                    _lastBuyStopPrice = order.TargetPrice;
                }
                else if (order.TradeType == TradeType.Sell && order.OrderType == PendingOrderType.Stop)
                {
                    _lastSellStopPrice = order.TargetPrice;
                }
            }

            // Initialize Breakout Levels immediately on start
            UpdateBreakoutLevels();
            _lastBarCount = _calcBars.Count;
        }

        protected override void OnTick()
        {
            // Indicator Data Loading Protection (Prevents NRE errors during the initial bars of a backtest)
            if (_calcBars == null || _calcBars.Count <= BarsN) return;
            if (Use_Trend_Filter && (_trendBars == null || _trendBars.Count <= Trend_MA_Period)) return;
            if (TPSL_Mode == TpSlMode.ATR_Dynamic && _calcBars.Count <= ATR_Period) return;

            // Weekend Gap Protection (Highest Priority)
            if (!IsTradingAllowed())
            {
                CloseAllTradesAndOrders();
                return; 
            }

            // 1. Manage Trailing Stop (Using CTrailingManager)
            _trailingManager.OnTick();

            // 2. Fetch Active Positions and Pending Orders once
            var activePositions = Positions.FindAll(BotLabel, SymbolName);
            var pendingOrders = PendingOrders.Where(o => o != null && o.Label == BotLabel && o.SymbolName == SymbolName).ToArray();

            bool hasBuyStop = false;
            bool hasSellStop = false;
            PendingOrder buyStopOrder = null;
            PendingOrder sellStopOrder = null;

            foreach (var order in pendingOrders)
            {
                if (order.TradeType == TradeType.Buy && order.OrderType == PendingOrderType.Stop)
                {
                    hasBuyStop = true;
                    buyStopOrder = order;
                }
                else if (order.TradeType == TradeType.Sell && order.OrderType == PendingOrderType.Stop)
                {
                    hasSellStop = true;
                    sellStopOrder = order;
                }
            }

            // OCO Logic: If we have an active position, IMMEDIATELY delete any remaining pending orders
            if (activePositions != null && activePositions.Length > 0)
            {
                if (hasBuyStop && buyStopOrder != null) CancelPendingOrder(buyStopOrder);
                if (hasSellStop && sellStopOrder != null) CancelPendingOrder(sellStopOrder);
                return; // Do not place new breakouts while in a trade
            }

            // 3. Trend Filter Logic (Run on every tick to immediately invalidate orders when trend changes)
            bool allowBuy = true;
            bool allowSell = true;

            if (Use_Trend_Filter)
            {
                if (_trendMa != null && _trendBars != null && _trendBars.Count > Trend_MA_Period)
                {
                    double maValue = _trendMa.Result.Last(1);
                    if (!double.IsNaN(maValue))
                    {
                        double currentPrice = Symbol.Bid;
                        if (currentPrice <= maValue) allowBuy = false;
                        if (currentPrice >= maValue) allowSell = false;
                    }
                }
            }

            // IMMEDIATELY delete pending orders that violate the trend filter
            if (!allowBuy && hasBuyStop && buyStopOrder != null)
            {
                CancelPendingOrder(buyStopOrder);
                hasBuyStop = false;
            }
            if (!allowSell && hasSellStop && sellStopOrder != null)
            {
                CancelPendingOrder(sellStopOrder);
                hasSellStop = false;
            }

            // 4. Update Breakout Levels on new bar
            if (_calcBars.Count > _lastBarCount)
            {
                _lastBarCount = _calcBars.Count;
                UpdateBreakoutLevels();
            }

            // 5. Execute Breakout Logic on every tick (to handle mid-bar trend and placement changes)
            ExecuteBreakoutLogic(allowBuy, allowSell, hasBuyStop, hasSellStop, buyStopOrder, sellStopOrder);
        }

        private void ExecuteBreakoutLogic(bool allowBuy, bool allowSell, bool hasBuyStop, bool hasSellStop, PendingOrder buyStopOrder, PendingOrder sellStopOrder)
        {
            // Spread Check
            double currentSpreadPips = PriceUtils.PriceToPips(Symbol.Spread, Symbol);
            if (currentSpreadPips > Max_Spread_Pips)
                return; 

            // Calculate Order Prices using cached breakout levels
            double bufferDistPrice = PriceUtils.PipsToPrice(OrderDistPips, Symbol);
            double buyPrice = PriceUtils.NormalizePrice(_highestHigh + bufferDistPrice, Symbol);
            double sellPrice = PriceUtils.NormalizePrice(_lowestLow - bufferDistPrice, Symbol);

            // Stop Level (Freeze) Check
            double minLevelDist = PriceUtils.PipsToPrice(5.0, Symbol); // standard failsafe

            bool validBuyPrice = (buyPrice >= Symbol.Ask + minLevelDist);
            bool validSellPrice = (sellPrice <= Symbol.Bid - minLevelDist);

            // Calculate TP and SL actual price distances
            double tpDistPrice = 0, slDistPrice = 0;
            GetSLTPDistances(out tpDistPrice, out slDistPrice);

            // Calculate Volume based on Risk
            double volumeInUnits = GetVolume(slDistPrice);

            // Expiration Time
            DateTime expiration = Server.Time.AddHours(ExpirationHours);

            // Strict modification threshold (Require at least 0.3 pips of change to prevent tick spam)
            double modifyThreshold = PriceUtils.PipsToPrice(0.3, Symbol);

            // Manage Buy Stop Order
            if (allowBuy && validBuyPrice)
            {
                double buyTargetSl = PriceUtils.NormalizePrice(buyPrice - slDistPrice, Symbol);
                double buyTargetTp = PriceUtils.NormalizePrice(buyPrice + tpDistPrice, Symbol);

                if (!hasBuyStop)
                {
                    TradeResult res = PlaceStopOrder(TradeType.Buy, SymbolName, volumeInUnits, buyPrice, BotLabel, buyTargetSl, buyTargetTp, ProtectionType.Absolute, expiration, null, false, StopTriggerMethod.Trade);
                    if (res != null && res.IsSuccessful)
                    {
                        _lastBuyStopPrice = buyPrice;
                    }
                    else if (res != null)
                    {
                        Print("Buy Stop Placement Failed: ", res.Error, " | Vol: ", volumeInUnits, " | Price: ", buyPrice);
                    }
                }
                else if (buyStopOrder != null)
                {
                    double currentBuyPrice = buyStopOrder.TargetPrice;
                    double currentBuySlDist = buyStopOrder.StopLoss.HasValue ? Math.Abs(currentBuyPrice - buyStopOrder.StopLoss.Value) : 0;
                    double currentBuyTpDist = buyStopOrder.TakeProfit.HasValue ? Math.Abs(buyStopOrder.TakeProfit.Value - currentBuyPrice) : 0;

                    if (Math.Abs(currentBuyPrice - buyPrice) > modifyThreshold ||
                        Math.Abs(currentBuySlDist - slDistPrice) > modifyThreshold ||
                        Math.Abs(currentBuyTpDist - tpDistPrice) > modifyThreshold)
                    {
                        // Check stop level relative to the NEW price we are trying to set
                        if (buyPrice >= Symbol.Ask + minLevelDist)
                        {
                            TradeResult res = ModifyPendingOrder(buyStopOrder, buyPrice, buyTargetSl, buyTargetTp, ProtectionType.Absolute, expiration);
                            if (res != null && res.IsSuccessful)
                            {
                                _lastBuyStopPrice = buyPrice;
                            }
                            else if (res != null)
                            {
                                Print("Buy Stop Modification Failed: ", res.Error);
                            }
                        }
                    }
                }
            }

            // Manage Sell Stop Order
            if (allowSell && validSellPrice)
            {
                double sellTargetSl = PriceUtils.NormalizePrice(sellPrice + slDistPrice, Symbol);
                double sellTargetTp = PriceUtils.NormalizePrice(sellPrice - tpDistPrice, Symbol);

                if (!hasSellStop)
                {
                    TradeResult res = PlaceStopOrder(TradeType.Sell, SymbolName, volumeInUnits, sellPrice, BotLabel, sellTargetSl, sellTargetTp, ProtectionType.Absolute, expiration, null, false, StopTriggerMethod.Trade);
                    if (res != null && res.IsSuccessful)
                    {
                        _lastSellStopPrice = sellPrice;
                    }
                    else if (res != null)
                    {
                        Print("Sell Stop Placement Failed: ", res.Error, " | Vol: ", volumeInUnits, " | Price: ", sellPrice);
                    }
                }
                else if (sellStopOrder != null)
                {
                    double currentSellPrice = sellStopOrder.TargetPrice;
                    double currentSellSlDist = sellStopOrder.StopLoss.HasValue ? Math.Abs(sellStopOrder.StopLoss.Value - currentSellPrice) : 0;
                    double currentSellTpDist = sellStopOrder.TakeProfit.HasValue ? Math.Abs(currentSellPrice - sellStopOrder.TakeProfit.Value) : 0;

                    if (Math.Abs(currentSellPrice - sellPrice) > modifyThreshold ||
                        Math.Abs(currentSellSlDist - slDistPrice) > modifyThreshold ||
                        Math.Abs(currentSellTpDist - tpDistPrice) > modifyThreshold)
                    {
                        // Check stop level relative to the NEW price we are trying to set
                        if (sellPrice <= Symbol.Bid - minLevelDist)
                        {
                            TradeResult res = ModifyPendingOrder(sellStopOrder, sellPrice, sellTargetSl, sellTargetTp, ProtectionType.Absolute, expiration);
                            if (res != null && res.IsSuccessful)
                            {
                                _lastSellStopPrice = sellPrice;
                            }
                            else if (res != null)
                            {
                                Print("Sell Stop Modification Failed: ", res.Error);
                            }
                        }
                    }
                }
            }
        }

        private void OnPositionsOpened(PositionOpenedEventArgs args)
        {
            var pos = args.Position;
            if (pos == null || pos.Label != BotLabel || pos.SymbolName != SymbolName) return;

            // Slippage Protection (Option B)
            double slippagePips = 0;
            if (pos.TradeType == TradeType.Buy && _lastBuyStopPrice > 0)
            {
                slippagePips = PriceUtils.PriceToPips(pos.EntryPrice - _lastBuyStopPrice, Symbol);
                if (slippagePips > MaxSlippagePips)
                {
                    Print("Slippage Protection Triggered (BUY): EntryPrice={0}, Expected={1}, Slippage={2:F2} Pips, Max={3:F2} Pips. Closing position.", 
                        pos.EntryPrice, _lastBuyStopPrice, slippagePips, MaxSlippagePips);
                    ClosePosition(pos);
                }
            }
            else if (pos.TradeType == TradeType.Sell && _lastSellStopPrice > 0)
            {
                slippagePips = PriceUtils.PriceToPips(_lastSellStopPrice - pos.EntryPrice, Symbol);
                if (slippagePips > MaxSlippagePips)
                {
                    Print("Slippage Protection Triggered (SELL): EntryPrice={0}, Expected={1}, Slippage={2:F2} Pips, Max={3:F2} Pips. Closing position.", 
                        pos.EntryPrice, _lastSellStopPrice, slippagePips, MaxSlippagePips);
                    ClosePosition(pos);
                }
            }
        }

        //+------------------------------------------------------------------+
        //| CALCULATE TP & SL DISTANCES (IN PRICE VALUE)                     |
        //+------------------------------------------------------------------+
        private void GetSLTPDistances(out double tpDistPrice, out double slDistPrice)
        {
            if (TPSL_Mode == TpSlMode.Fixed_Pips)
            {
                tpDistPrice = PriceUtils.PipsToPrice(TpPips, Symbol);
                slDistPrice = PriceUtils.PipsToPrice(SlPips, Symbol);
            }
            else // ATR_Dynamic
            {
                if (_atr != null && _calcBars != null && _calcBars.Count > ATR_Period) 
                {
                    double currentAtr = _atr.Result.Last(1);
                    tpDistPrice = currentAtr * ATR_TP_Multiplier;
                    slDistPrice = currentAtr * ATR_SL_Multiplier;
                }
                else
                {
                    tpDistPrice = PriceUtils.PipsToPrice(TpPips, Symbol);
                    slDistPrice = PriceUtils.PipsToPrice(SlPips, Symbol);
                }
            }
        }

        //+------------------------------------------------------------------+
        //| LOT SIZE CALCULATION (RISK MANAGEMENT)                           |
        //+------------------------------------------------------------------+
        private double GetVolume(double slDistPrice)
        {
            double volumeCalc = 0;

            if (Lot_Size_Mode == LotMode.Fixed_Lots) 
            {
                volumeCalc = Symbol.QuantityToVolumeInUnits(Lots);
            }
            else
            {
                double balance = Account.Balance;
                double riskMoney = balance * (RiskPercent / 100.0);

                double pipSize = PriceUtils.GetPipSize(Symbol);
                if (slDistPrice <= 0 || pipSize <= 0 || Symbol.PipValue <= 0)
                    return Symbol.VolumeInUnitsMin;

                double slPips = slDistPrice / pipSize;
                double pipValueForOneUnit = Symbol.PipValue / Symbol.LotSize;
                double lossPerUnit = slPips * pipValueForOneUnit;

                if (lossPerUnit <= 0) return Symbol.VolumeInUnitsMin;

                volumeCalc = riskMoney / lossPerUnit;
            }

            double normalizedVol = Symbol.NormalizeVolumeInUnits(volumeCalc, RoundingMode.Down);
            
            if (normalizedVol < Symbol.VolumeInUnitsMin) 
            {
                normalizedVol = Symbol.VolumeInUnitsMin;
            }

            return normalizedVol;
        }

        //+------------------------------------------------------------------+
        //| WEEKEND GAP PROTECTION LOGIC                                     |
        //+------------------------------------------------------------------+
        private bool IsTradingAllowed()
        {
            if (!Close_On_Friday) return true;

            DateTime dt = Server.Time;

            if (dt.DayOfWeek == DayOfWeek.Friday)
            {
                if (dt.Hour > Friday_Close_Hour || (dt.Hour == Friday_Close_Hour && dt.Minute >= Friday_Close_Minute))
                    return false;
            }
            else if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            return true;
        }

        //+------------------------------------------------------------------+
        //| CLOSE ALL TRADES AND PENDING ORDERS                              |
        //+------------------------------------------------------------------+
        private void CloseAllTradesAndOrders()
        {
            DateTime dt = Server.Time;

            if (dt.DayOfYear == _lastCloseDay) return;

            bool actionTaken = false;

            var positions = Positions.FindAll(BotLabel, SymbolName);
            if (positions != null)
            {
                foreach (var pos in positions)
                {
                    if (pos != null) ClosePosition(pos);
                    actionTaken = true;
                }
            }

            var orders = PendingOrders.Where(o => o != null && o.Label == BotLabel && o.SymbolName == SymbolName).ToArray();
            if (orders != null)
            {
                foreach (var order in orders)
                {
                    if (order != null) CancelPendingOrder(order);
                    actionTaken = true;
                }
            }

            if (actionTaken) _lastCloseDay = dt.DayOfYear;
        }

        private void UpdateBreakoutLevels()
        {
            _highestHigh = _calcBars.HighPrices.Last(1);
            _lowestLow = _calcBars.LowPrices.Last(1);

            for (int i = 1; i <= BarsN; i++)
            {
                _highestHigh = Math.Max(_highestHigh, _calcBars.HighPrices.Last(i));
                _lowestLow = Math.Min(_lowestLow, _calcBars.LowPrices.Last(i));
            }
        }
    }
}