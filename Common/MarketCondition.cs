using System;
using cAlgo.API.Internals;

namespace RedWave.Common
{
    public class CMarketCondition
    {
        private Symbol _symbol;
        private CLogger _logger;
        private bool _enableSpreadCheck;
        private double _maxSpreadPips;

        public CMarketCondition()
        {
            _symbol = null;
            _logger = null;
            _enableSpreadCheck = false;
            _maxSpreadPips = 50.0;
        }

        public bool Init(Symbol symbol, CLogger logger = null)
        {
            _logger = logger;
            if (symbol == null)
            {
                _logger?.Error("MarketCondition: Initialization failed. Symbol is null.");
                return false;
            }
            _symbol = symbol;
            return true;
        }

        public void SetSpreadCheck(bool enable, double maxSpreadPips)
        {
            _enableSpreadCheck = enable;
            _maxSpreadPips = maxSpreadPips;
        }

        public bool IsTradingOK()
        {
            if (_symbol == null) return false;

            // Spread check (cTrader symbol.Spread is in Price unit, convert to Pips first)
            if (_enableSpreadCheck)
            {
                double currentSpread = PriceUtils.PriceToPips(_symbol.Spread, _symbol);
                if (currentSpread > _maxSpreadPips)
                {
                    // Debug only — can fire every bar in backtest
                    _logger?.Debug($"Spread high: {currentSpread:F1} > {_maxSpreadPips:F1} pips");
                    return false;
                }
            }

            return true;
        }
    }
}
