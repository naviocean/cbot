using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace RedWave.Common
{
    /// <summary>
    /// TradeExecutor handles all order executions (Market, Limit, Stop) and modifications (Position, PendingOrder)
    /// for cTrader cBots. It eliminates ambiguity between Absolute Price and Pips Distance, performs volume 
    /// sanitization, enforces StopLevel / MinStopDistance safety rules, and avoids duplicate API calls.
    /// </summary>
    public class TradeExecutor
    {
        private readonly Robot _robot;
        private readonly CLogger _logger;

        /// <summary>
        /// Safe minimum stop distance buffer in Pips to prevent SL/TP from being placed inside spread or broker rejection.
        /// Defaults to 1.0 Pip, but can be customized per symbol/strategy or dynamically linked to spread.
        /// Note: Unlike MT5 (SYMBOL_TRADE_STOPS_LEVEL), cTrader Automate API Symbol does not expose a native StopsLevel property.
        /// </summary>
        public double MinStopDistancePips { get; set; } = 1.0;

        /// <summary>
        /// Enable or disable auto-adjusting SL/TP when they violate MinStopDistancePips.
        /// If true, SL/TP will be pushed away to MinStopDistancePips.
        /// If false, invalid SL/TP will be rejected with a warning log.
        /// </summary>
        public bool AutoAdjustInvalidStops { get; set; } = true;

        public TradeExecutor(Robot robot, CLogger logger = null)
        {
            _robot = robot ?? throw new ArgumentNullException(nameof(robot));
            _logger = logger;
        }

        #region Market Orders

        /// <summary>
        /// Execute a Market Order using ABSOLUTE PRICE levels for SL and TP.
        /// Automatically converts price levels to Pip distances for cTrader's ExecuteMarketOrder API.
        /// </summary>
        public TradeResult ExecuteMarketByPrice(
            TradeType tradeType,
            Symbol symbol,
            double volumeUnits,
            string label,
            double? slPrice,
            double? tpPrice,
            string comment = null)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));

            double sanitizedVolume = symbol.NormalizeVolumeInUnits(volumeUnits);
            double entryPrice = tradeType == TradeType.Buy ? symbol.Ask : symbol.Bid;

            double? validSlPrice = ValidateAndAdjustStopLoss(tradeType, symbol, entryPrice, slPrice);
            double? validTpPrice = ValidateAndAdjustTakeProfit(tradeType, symbol, entryPrice, tpPrice);

            double? slPips = CalculateStopLossPips(tradeType, symbol, entryPrice, validSlPrice);
            double? tpPips = CalculateTakeProfitPips(tradeType, symbol, entryPrice, validTpPrice);

            LogExecution($"ExecuteMarketByPrice {tradeType} {symbol.Name} Vol:{sanitizedVolume} Entry:{entryPrice} SL:{validSlPrice} ({slPips} pips) TP:{validTpPrice} ({tpPips} pips)");

            return _robot.ExecuteMarketOrder(tradeType, symbol.Name, sanitizedVolume, label, stopLossPips: slPips, takeProfitPips: tpPips, comment: comment);
        }

        /// <summary>
        /// Execute a Market Order using PIPS DISTANCE for SL and TP.
        /// </summary>
        public TradeResult ExecuteMarketByPips(
            TradeType tradeType,
            Symbol symbol,
            double volumeUnits,
            string label,
            double? slPips,
            double? tpPips,
            string comment = null)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));

            double sanitizedVolume = symbol.NormalizeVolumeInUnits(volumeUnits);

            if (slPips.HasValue && slPips.Value < MinStopDistancePips) slPips = MinStopDistancePips;
            if (tpPips.HasValue && tpPips.Value < MinStopDistancePips) tpPips = MinStopDistancePips;

            LogExecution($"ExecuteMarketByPips {tradeType} {symbol.Name} Vol:{sanitizedVolume} SL:{slPips}pips TP:{tpPips}pips");

            return _robot.ExecuteMarketOrder(tradeType, symbol.Name, sanitizedVolume, label, stopLossPips: slPips, takeProfitPips: tpPips, comment: comment);
        }

        #endregion

        #region Pending Orders (Limit & Stop)

        /// <summary>
        /// Place a Limit Order using ABSOLUTE PRICE levels for SL and TP.
        /// SL and TP distances in pips are calculated relative to the target entry price.
        /// </summary>
        public TradeResult PlaceLimitByPrice(
            TradeType tradeType,
            Symbol symbol,
            double volumeUnits,
            double targetPrice,
            string label,
            double? slPrice,
            double? tpPrice,
            DateTime? expiration = null,
            string comment = null)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));

            double sanitizedVolume = symbol.NormalizeVolumeInUnits(volumeUnits);

            double? validSlPrice = ValidateAndAdjustStopLoss(tradeType, symbol, targetPrice, slPrice);
            double? validTpPrice = ValidateAndAdjustTakeProfit(tradeType, symbol, targetPrice, tpPrice);

            double? slPips = CalculateStopLossPips(tradeType, symbol, targetPrice, validSlPrice);
            double? tpPips = CalculateTakeProfitPips(tradeType, symbol, targetPrice, validTpPrice);

            LogExecution($"PlaceLimitByPrice {tradeType} {symbol.Name} Target:{targetPrice} Vol:{sanitizedVolume} SL:{validSlPrice} ({slPips} pips) TP:{validTpPrice} ({tpPips} pips)");

#pragma warning disable CS0618 // Retain cTrader SDK 1.0.19 compatibility for pips-based limit order
            return _robot.PlaceLimitOrder(tradeType, symbol.Name, sanitizedVolume, targetPrice, label, slPips, tpPips, expiration, comment);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Place a Stop Order using ABSOLUTE PRICE levels for SL and TP.
        /// SL and TP distances in pips are calculated relative to the target entry price.
        /// </summary>
        public TradeResult PlaceStopByPrice(
            TradeType tradeType,
            Symbol symbol,
            double volumeUnits,
            double targetPrice,
            string label,
            double? slPrice,
            double? tpPrice,
            DateTime? expiration = null,
            string comment = null)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));

            double sanitizedVolume = symbol.NormalizeVolumeInUnits(volumeUnits);

            double? validSlPrice = ValidateAndAdjustStopLoss(tradeType, symbol, targetPrice, slPrice);
            double? validTpPrice = ValidateAndAdjustTakeProfit(tradeType, symbol, targetPrice, tpPrice);

            double? slPips = CalculateStopLossPips(tradeType, symbol, targetPrice, validSlPrice);
            double? tpPips = CalculateTakeProfitPips(tradeType, symbol, targetPrice, validTpPrice);

            LogExecution($"PlaceStopByPrice {tradeType} {symbol.Name} Target:{targetPrice} Vol:{sanitizedVolume} SL:{validSlPrice} ({slPips} pips) TP:{validTpPrice} ({tpPips} pips)");

#pragma warning disable CS0618 // Retain cTrader SDK 1.0.19 compatibility for pips-based stop order
            return _robot.PlaceStopOrder(tradeType, symbol.Name, sanitizedVolume, targetPrice, label, slPips, tpPips, expiration, comment);
#pragma warning restore CS0618
        }

        #endregion

        #region Position Modification

        /// <summary>
        /// Modify an open Position's SL and TP using ABSOLUTE PRICE levels.
        /// Explicitly passes ProtectionType.Absolute to resolve cTrader CS0618 warnings and prevent pips ambiguity.
        /// Avoids calling cTrader API if the new levels are unchanged (within 1 TickSize).
        /// </summary>
        public TradeResult ModifyPositionByPrice(Position position, double? newSlPrice, double? newTpPrice)
        {
            if (position == null) return null;

            Symbol symbol = _robot.Symbols.GetSymbol(position.SymbolName);
            if (symbol == null) return null;

            double currentPrice = position.TradeType == TradeType.Buy ? symbol.Bid : symbol.Ask;

            if (newSlPrice.HasValue)
                newSlPrice = ValidateAndAdjustStopLoss(position.TradeType, symbol, currentPrice, newSlPrice);

            if (newTpPrice.HasValue)
                newTpPrice = ValidateAndAdjustTakeProfit(position.TradeType, symbol, currentPrice, newTpPrice);

            // Anti-Spam Check: Skip API call if change is negligible (< 1 TickSize)
            bool slChanged = HasPriceChanged(position.StopLoss, newSlPrice, symbol.TickSize);
            bool tpChanged = HasPriceChanged(position.TakeProfit, newTpPrice, symbol.TickSize);

            if (!slChanged && !tpChanged)
            {
                return null;
            }

            LogExecution($"ModifyPositionByPrice ID:{position.Id} Symbol:{position.SymbolName} NewSL:{newSlPrice} NewTP:{newTpPrice}");

            return _robot.ModifyPosition(position, newSlPrice, newTpPrice, ProtectionType.Absolute);
        }

        /// <summary>
        /// Modify a Pending Order's target price, SL, and TP using ABSOLUTE PRICE levels.
        /// </summary>
        public TradeResult ModifyPendingOrderByPrice(PendingOrder pendingOrder, double targetPrice, double? newSlPrice, double? newTpPrice, DateTime? expiration = null)
        {
            if (pendingOrder == null) return null;

            Symbol symbol = _robot.Symbols.GetSymbol(pendingOrder.SymbolName);
            if (symbol == null) return null;

            if (newSlPrice.HasValue)
                newSlPrice = ValidateAndAdjustStopLoss(pendingOrder.TradeType, symbol, targetPrice, newSlPrice);

            if (newTpPrice.HasValue)
                newTpPrice = ValidateAndAdjustTakeProfit(pendingOrder.TradeType, symbol, targetPrice, newTpPrice);

            bool targetChanged = HasPriceChanged(pendingOrder.TargetPrice, targetPrice, symbol.TickSize);
            bool slChanged = HasPriceChanged(pendingOrder.StopLoss, newSlPrice, symbol.TickSize);
            bool tpChanged = HasPriceChanged(pendingOrder.TakeProfit, newTpPrice, symbol.TickSize);
            bool expChanged = pendingOrder.ExpirationTime != expiration;

            if (!targetChanged && !slChanged && !tpChanged && !expChanged)
            {
                return null;
            }

            LogExecution($"ModifyPendingOrderByPrice ID:{pendingOrder.Id} Symbol:{pendingOrder.SymbolName} Target:{targetPrice} SL:{newSlPrice} TP:{newTpPrice}");

            return _robot.ModifyPendingOrder(pendingOrder, targetPrice, newSlPrice, newTpPrice, ProtectionType.Absolute, expiration);
        }

        /// <summary>
        /// Close an open position safely.
        /// </summary>
        public TradeResult ClosePosition(Position position, double? volumeUnits = null)
        {
            if (position == null) return null;

            if (volumeUnits.HasValue)
            {
                Symbol symbol = _robot.Symbols.GetSymbol(position.SymbolName);
                double sanitizedVolume = symbol.NormalizeVolumeInUnits(volumeUnits.Value);
                LogExecution($"ClosePosition Partial ID:{position.Id} Vol:{sanitizedVolume}");
                return _robot.ClosePosition(position, sanitizedVolume);
            }

            LogExecution($"ClosePosition Full ID:{position.Id}");
            return _robot.ClosePosition(position);
        }

        /// <summary>
        /// Cancel a pending order.
        /// </summary>
        public TradeResult CancelPendingOrder(PendingOrder pendingOrder)
        {
            if (pendingOrder == null) return null;
            LogExecution($"CancelPendingOrder ID:{pendingOrder.Id} Symbol:{pendingOrder.SymbolName}");
            return _robot.CancelPendingOrder(pendingOrder);
        }

        #endregion

        #region Helpers & Validations

        public double? ValidateAndAdjustStopLoss(TradeType tradeType, Symbol symbol, double referencePrice, double? slPrice)
        {
            if (!slPrice.HasValue || slPrice.Value <= 0) return null;

            double minDistancePips = GetEffectiveMinStopDistancePips(symbol);
            double minDistance = minDistancePips * symbol.PipSize;

            if (tradeType == TradeType.Buy)
            {
                double maxAllowedSl = referencePrice - minDistance;
                if (slPrice.Value > maxAllowedSl)
                {
                    if (AutoAdjustInvalidStops)
                    {
                        LogWarning($"Buy SL ({slPrice.Value}) too close to entry ({referencePrice}). Auto-adjusting to {maxAllowedSl}");
                        return PriceUtils.NormalizePrice(maxAllowedSl, symbol);
                    }
                    else
                    {
                        LogWarning($"Buy SL ({slPrice.Value}) violates MinStopDistance ({minDistance}) from entry ({referencePrice}).");
                        return null;
                    }
                }
            }
            else // Sell
            {
                double minAllowedSl = referencePrice + minDistance;
                if (slPrice.Value < minAllowedSl)
                {
                    if (AutoAdjustInvalidStops)
                    {
                        LogWarning($"Sell SL ({slPrice.Value}) too close to entry ({referencePrice}). Auto-adjusting to {minAllowedSl}");
                        return PriceUtils.NormalizePrice(minAllowedSl, symbol);
                    }
                    else
                    {
                        LogWarning($"Sell SL ({slPrice.Value}) violates MinStopDistance ({minDistance}) from entry ({referencePrice}).");
                        return null;
                    }
                }
            }

            return PriceUtils.NormalizePrice(slPrice.Value, symbol);
        }

        public double? ValidateAndAdjustTakeProfit(TradeType tradeType, Symbol symbol, double referencePrice, double? tpPrice)
        {
            if (!tpPrice.HasValue || tpPrice.Value <= 0) return null;

            double minDistancePips = GetEffectiveMinStopDistancePips(symbol);
            double minDistance = minDistancePips * symbol.PipSize;

            if (tradeType == TradeType.Buy)
            {
                double minAllowedTp = referencePrice + minDistance;
                if (tpPrice.Value < minAllowedTp)
                {
                    if (AutoAdjustInvalidStops)
                    {
                        LogWarning($"Buy TP ({tpPrice.Value}) too close to entry ({referencePrice}). Auto-adjusting to {minAllowedTp}");
                        return PriceUtils.NormalizePrice(minAllowedTp, symbol);
                    }
                    else
                    {
                        LogWarning($"Buy TP ({tpPrice.Value}) violates MinStopDistance ({minDistance}) from entry ({referencePrice}).");
                        return null;
                    }
                }
            }
            else // Sell
            {
                double maxAllowedTp = referencePrice - minDistance;
                if (tpPrice.Value > maxAllowedTp)
                {
                    if (AutoAdjustInvalidStops)
                    {
                        LogWarning($"Sell TP ({tpPrice.Value}) too close to entry ({referencePrice}). Auto-adjusting to {maxAllowedTp}");
                        return PriceUtils.NormalizePrice(maxAllowedTp, symbol);
                    }
                    else
                    {
                        LogWarning($"Sell TP ({tpPrice.Value}) violates MinStopDistance ({minDistance}) from entry ({referencePrice}).");
                        return null;
                    }
                }
            }

            return PriceUtils.NormalizePrice(tpPrice.Value, symbol);
        }

        /// <summary>
        /// Calculates effective minimum stop distance in Pips, considering spread dynamically.
        /// Note: Unlike MT5 (SYMBOL_TRADE_STOPS_LEVEL), cTrader Automate API Symbol does not expose a native StopsLevel property.
        /// </summary>
        public double GetEffectiveMinStopDistancePips(Symbol symbol)
        {
            if (symbol == null || symbol.PipSize <= 0) return MinStopDistancePips;
            
            // Dynamic check: Min distance should at least cover current spread
            double spreadPips = symbol.Spread / symbol.PipSize;
            return Math.Max(MinStopDistancePips, spreadPips);
        }

        public static double? CalculateStopLossPips(TradeType tradeType, Symbol symbol, double entryPrice, double? slPrice)
        {
            if (!slPrice.HasValue || slPrice.Value <= 0 || symbol == null) return null;

            double diff = tradeType == TradeType.Buy ? (entryPrice - slPrice.Value) : (slPrice.Value - entryPrice);
            if (diff <= 0) return null;

            return Math.Round(diff / symbol.PipSize, 1);
        }

        public static double? CalculateTakeProfitPips(TradeType tradeType, Symbol symbol, double entryPrice, double? tpPrice)
        {
            if (!tpPrice.HasValue || tpPrice.Value <= 0 || symbol == null) return null;

            double diff = tradeType == TradeType.Buy ? (tpPrice.Value - entryPrice) : (entryPrice - tpPrice.Value);
            if (diff <= 0) return null;

            return Math.Round(diff / symbol.PipSize, 1);
        }

        public static bool HasPriceChanged(double? current, double? target, double tickSize)
        {
            if (!current.HasValue && !target.HasValue) return false;
            if (current.HasValue != target.HasValue) return true;
            return Math.Abs(current.Value - target.Value) >= tickSize;
        }

        private void LogExecution(string message)
        {
            if (_logger != null)
                _logger.Info($"[TradeExecutor] {message}");
            else
                _robot.Print($"[TradeExecutor] {message}");
        }

        private void LogWarning(string message)
        {
            if (_logger != null)
                _logger.Warn($"[TradeExecutor] {message}");
            else
                _robot.Print($"[TradeExecutor Warning] {message}");
        }

        #endregion
    }
}
