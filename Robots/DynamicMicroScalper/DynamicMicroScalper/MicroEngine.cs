using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using RedWave.Common;

namespace RedWave.MicroScalper
{
    public class CMicroEngine
    {
        private Robot _robot;
        private Symbol _symbol;
        private string _label;
        private CLogger _logger;
        private CTrailingManager _trailing;

        private double _lastAnchorPrice;
        private bool _isRefreshing;
        private bool _useOCO;
        private bool _cancelOnTrailing;

        public CMicroEngine()
        {
            _robot = null;
            _symbol = null;
            _label = "";
            _logger = null;
            _trailing = null;
            _lastAnchorPrice = 0;
            _isRefreshing = false;
        }

        public double LastAnchorPrice
        {
            get { return _lastAnchorPrice; }
        }

        public bool Init(Robot robot, Symbol symbol, string label, CTrailingManager trailing, bool useOCO, bool cancelOnTrailing, CLogger logger = null)
        {
            _robot = robot;
            _symbol = symbol;
            _label = label;
            _trailing = trailing;
            _useOCO = useOCO;
            _cancelOnTrailing = cancelOnTrailing;
            _isRefreshing = false;

            _lastAnchorPrice = _symbol.Bid;

            _logger?.Info($"MicroEngine initialized for {_symbol.Name}. Label: {_label}");
            return true;
        }

        public void Deinit()
        {
        }

        // Check and place missing stop orders
        public void CheckOrders(double distancePips, double slPips, double tpPips, double volumeUnits)
        {
            if (_symbol.Bid <= 0 || _symbol.Ask <= 0) return;
            if (_isRefreshing) return;
            if (ShouldSuppressOrders()) return;

            _isRefreshing = true;

            bool hasBuySide = OrderExists(PendingOrderType.Limit, TradeType.Buy) || // Limit/Stop in cTrader are split or general? PendingOrder.PendingOrderType is Limit or Stop
                              OrderExists(PendingOrderType.Stop, TradeType.Buy) ||
                              PositionExists(TradeType.Buy);

            bool hasSellSide = OrderExists(PendingOrderType.Limit, TradeType.Sell) ||
                               OrderExists(PendingOrderType.Stop, TradeType.Sell) ||
                               PositionExists(TradeType.Sell);

            double distance = PriceUtils.PipsToPrice(distancePips, _symbol);
            double slDist = PriceUtils.PipsToPrice(slPips, _symbol);
            double tpDist = PriceUtils.PipsToPrice(tpPips, _symbol);

            double stopsLevel = PriceUtils.PipsToPrice(_symbol.Spread, _symbol); // cTrader stops level can be approximated as Spread size

            if (!hasBuySide)
            {
                double entryPrice = _symbol.Ask + distance;
                if (entryPrice < _symbol.Ask + stopsLevel)
                    entryPrice = _symbol.Ask + stopsLevel;

                entryPrice = PriceUtils.NormalizePrice(entryPrice, _symbol);
                double? sl = (slPips > 0) ? (double?)PriceUtils.NormalizePrice(entryPrice - slDist, _symbol) : null;
                double? tp = (tpPips > 0) ? (double?)PriceUtils.NormalizePrice(entryPrice + tpDist, _symbol) : null;

                _logger?.Info($"MicroEngine: Placing Buy Stop Order at {entryPrice}. SL: {sl}, TP: {tp}");
                var result = _robot.PlaceStopOrder(TradeType.Buy, _symbol.Name, volumeUnits, entryPrice, _label, sl, tp, ProtectionType.Absolute);
                if (result.IsSuccessful)
                {
                    _lastAnchorPrice = _symbol.Ask;
                }
            }

            if (!hasSellSide)
            {
                double entryPrice = _symbol.Bid - distance;
                if (entryPrice > _symbol.Bid - stopsLevel)
                    entryPrice = _symbol.Bid - stopsLevel;

                entryPrice = PriceUtils.NormalizePrice(entryPrice, _symbol);
                double? sl = (slPips > 0) ? (double?)PriceUtils.NormalizePrice(entryPrice + slDist, _symbol) : null;
                double? tp = (tpPips > 0) ? (double?)PriceUtils.NormalizePrice(entryPrice - tpDist, _symbol) : null;

                _logger?.Info($"MicroEngine: Placing Sell Stop Order at {entryPrice}. SL: {sl}, TP: {tp}");
                var result = _robot.PlaceStopOrder(TradeType.Sell, _symbol.Name, volumeUnits, entryPrice, _label, sl, tp, ProtectionType.Absolute);
                if (result.IsSuccessful)
                {
                    _lastAnchorPrice = _symbol.Bid;
                }
            }

            _isRefreshing = false;
        }

        // Refresh anchor price and modify pending orders if price drifts
        public void RefreshAnchor(double distancePips, double slPips, double tpPips, double minMovePips)
        {
            if (_isRefreshing) return;
            if (ShouldSuppressOrders()) return;

            double currentPrice = (_symbol.Bid + _symbol.Ask) / 2.0;
            double minMove = PriceUtils.PipsToPrice(minMovePips, _symbol);

            if (Math.Abs(currentPrice - _lastAnchorPrice) < minMove)
            {
                return;
            }

            _isRefreshing = true;

            double distance = PriceUtils.PipsToPrice(distancePips, _symbol);
            double slDist = PriceUtils.PipsToPrice(slPips, _symbol);
            double tpDist = PriceUtils.PipsToPrice(tpPips, _symbol);

            var pendingOrders = _robot.PendingOrders.Where(o => o.Label == _label && o.SymbolName == _symbol.Name).ToList();

            foreach (var order in pendingOrders)
            {
                if (order.OrderType != PendingOrderType.Stop) continue;

                // Safety check: If price is too close to the pending order target price, do not modify it (let it fill).
                // This prevents cTrader's cancel-and-replace modification from failing and deleting the order.
                double priceDistance = 0;
                if (order.TradeType == TradeType.Buy)
                {
                    priceDistance = order.TargetPrice - _symbol.Ask;
                }
                else if (order.TradeType == TradeType.Sell)
                {
                    priceDistance = _symbol.Bid - order.TargetPrice;
                }

                double safetyPips = Math.Max(_symbol.Spread * 3.0, 5.0);
                double safetyLimit = PriceUtils.PipsToPrice(safetyPips, _symbol);
                
                // Also check broker's minimum stop loss / take profit distance limits
                double minBrokerDistance = Math.Max(_symbol.MinStopLossDistance, _symbol.MinTakeProfitDistance) * _symbol.PipSize;
                double finalSafetyLimit = Math.Max(safetyLimit, minBrokerDistance);

                if (priceDistance <= finalSafetyLimit)
                {
                    _logger?.Info($"MicroEngine: Price is close to Pending Order #{order.Id} (Dist: {PriceUtils.PriceToPips(priceDistance, _symbol):F1} pips, Safety Limit: {PriceUtils.PriceToPips(finalSafetyLimit, _symbol):F1} pips). Skipping modification to allow fill.");
                    continue;
                }

                double newPrice = 0;
                double? sl = null;
                double? tp = null;

                if (order.TradeType == TradeType.Buy)
                {
                    newPrice = PriceUtils.NormalizePrice(_symbol.Ask + distance, _symbol);
                    sl = (slPips > 0) ? (double?)PriceUtils.NormalizePrice(newPrice - slDist, _symbol) : null;
                    tp = (tpPips > 0) ? (double?)PriceUtils.NormalizePrice(newPrice + tpDist, _symbol) : null;
                }
                else if (order.TradeType == TradeType.Sell)
                {
                    newPrice = PriceUtils.NormalizePrice(_symbol.Bid - distance, _symbol);
                    sl = (slPips > 0) ? (double?)PriceUtils.NormalizePrice(newPrice + slDist, _symbol) : null;
                    tp = (tpPips > 0) ? (double?)PriceUtils.NormalizePrice(newPrice - tpDist, _symbol) : null;
                }

                if (newPrice > 0 && newPrice != order.TargetPrice)
                {
                    _logger?.Debug($"MicroEngine: Modifying Pending Order #{order.Id}. New Price: {newPrice}, SL: {sl}, TP: {tp}");
                    _robot.ModifyPendingOrder(order, newPrice, sl, tp, ProtectionType.Absolute);
                }
            }

            _lastAnchorPrice = currentPrice;
            _isRefreshing = false;
        }

        // Sync open position SL/TP (slippage correction)
        public void SyncPosition(Position position, double slPips, double tpPips)
        {
            if (slPips <= 0) return;

            double openPrice = position.EntryPrice;
            double? currentSL = position.StopLoss;
            double? currentTP = position.TakeProfit;

            double slDist = PriceUtils.PipsToPrice(slPips, _symbol);
            double tpDist = PriceUtils.PipsToPrice(tpPips, _symbol);

            double targetSL = 0;
            double? targetTP = null;

            if (position.TradeType == TradeType.Buy)
            {
                targetSL = PriceUtils.NormalizePrice(openPrice - slDist, _symbol);
                targetTP = (tpPips > 0) ? (double?)PriceUtils.NormalizePrice(openPrice + tpDist, _symbol) : null;

                if (currentSL == null || currentSL < targetSL - _symbol.TickSize)
                {
                    _robot.ModifyPosition(position, targetSL, currentTP ?? targetTP, ProtectionType.Absolute);
                }
            }
            else if (position.TradeType == TradeType.Sell)
            {
                targetSL = PriceUtils.NormalizePrice(openPrice + slDist, _symbol);
                targetTP = (tpPips > 0) ? (double?)PriceUtils.NormalizePrice(openPrice - tpDist, _symbol) : null;

                if (currentSL == null || currentSL > targetSL + _symbol.TickSize)
                {
                    _robot.ModifyPosition(position, targetSL, currentTP ?? targetTP, ProtectionType.Absolute);
                }
            }
        }

        private bool OrderExists(PendingOrderType type, TradeType tradeType)
        {
            return _robot.PendingOrders.Any(o => o.Label == _label && 
                                                o.SymbolName == _symbol.Name && 
                                                o.OrderType == type && 
                                                o.TradeType == tradeType);
        }

        private bool PositionExists(TradeType tradeType)
        {
            return _robot.Positions.Any(p => p.Label == _label && 
                                            p.SymbolName == _symbol.Name && 
                                            p.TradeType == tradeType);
        }

        private bool ShouldSuppressOrders()
        {
            bool hasPos = _robot.Positions.Any(p => p.Label == _label && p.SymbolName == _symbol.Name);
            if (hasPos)
            {
                if (_useOCO) return true;
                if (_cancelOnTrailing && _trailing != null && _trailing.IsAnyTrailingStarted()) return true;
            }
            return false;
        }

        public void ForceResetAnchor()
        {
            _lastAnchorPrice = _symbol.Bid;
            _logger?.Info("MicroEngine: Anchor manually reset to current Bid.");
        }
    }
}
