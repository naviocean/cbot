using System;
using cAlgo.API.Internals;

namespace RedWave.Common
{
    public static class PriceUtils
    {
        public static double GetPipSize(Symbol symbol)
        {
            // Use native PipSize to ensure accuracy across all brokers and asset types (Forex, CFDs, Gold)
            return symbol.PipSize;
        }

        // Convert pips to price distance
        public static double PipsToPrice(double pips, Symbol symbol)
        {
            return pips * GetPipSize(symbol);
        }

        // Convert price distance to pips
        public static double PriceToPips(double priceDelta, Symbol symbol)
        {
            double pipSize = GetPipSize(symbol);
            if (pipSize == 0) return 0;
            return priceDelta / pipSize;
        }

        // Normalize price to symbol digits
        public static double NormalizePrice(double price, Symbol symbol)
        {
            return Math.Round(price, symbol.Digits, MidpointRounding.AwayFromZero);
        }

        // Convert currency amount to price distance (Delta)
        // cTrader Volume is in units (e.g., 100,000 units = 1 lot)
        public static double AmountToPrice(double amount, double volumeUnits, Symbol symbol)
        {
            double absAmount = Math.Abs(amount);
            if (volumeUnits <= 0 || absAmount <= 0) return 0;

            // In cTrader, Symbol.PipValue is the value of 1 pip for 1 lot (usually 100,000 units, except some CFDs)
            // But cTrader has Symbol.GetPipValue(volumeUnits) or we can use Symbol.PipValue * (volumeUnits / Symbol.LotSize)
            // Let's use cTrader's native pip value calculation if available, or compute:
            // monetary value of 1 point = PipValue * PipSize * (volumeUnits / LotSize) ?
            // Let's look at cTrader's Symbol API:
            // Symbol.PipValue is the value of 1 Pip for 1 Lot in Account Currency.
            // Symbol.LotSize is the number of units per Lot (e.g., 100,000 for Forex).
            double lots = volumeUnits / symbol.LotSize;
            double pipValueForVolume = symbol.PipValue * lots;
            
            if (pipValueForVolume <= 0) return 0;
            
            // Total Pips = Amount / PipValueForVolume
            double pips = absAmount / pipValueForVolume;
            return PipsToPrice(pips, symbol);
        }

        // Convert price distance to currency amount
        public static double PriceToAmount(double priceDelta, double volumeUnits, Symbol symbol)
        {
            double absPriceDelta = Math.Abs(priceDelta);
            if (volumeUnits <= 0 || absPriceDelta <= 0 || symbol == null) return 0;

            // Tick-based is more reliable on XAUUSD CFDs where PipSize/PipValue can be inconsistent
            // (e.g. PipSize=1 with PipValue≈1 understates risk ~100x vs real $1/oz).
            if (symbol.TickSize > 0 && symbol.TickValue > 0 && symbol.LotSize > 0)
            {
                double ticks = absPriceDelta / symbol.TickSize;
                double lots = volumeUnits / symbol.LotSize;
                return ticks * symbol.TickValue * lots;
            }

            // Fallback: pip-based
            double pips = PriceToPips(absPriceDelta, symbol);
            if (symbol.LotSize <= 0 || symbol.PipValue <= 0) return 0;
            double lotsFb = volumeUnits / symbol.LotSize;
            return pips * symbol.PipValue * lotsFb;
        }
    }
}
