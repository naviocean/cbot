using System;
using cAlgo.API.Internals;

namespace RedWave.Common
{
    public static class PriceUtils
    {
        public static double GetPipSize(Symbol symbol)
        {
            return symbol.PipSize;
        }

        // Convert pips to price distance
        public static double PipsToPrice(double pips, Symbol symbol)
        {
            return pips * symbol.PipSize;
        }

        // Convert price distance to pips
        public static double PriceToPips(double priceDelta, Symbol symbol)
        {
            double pipSize = GetPipSize(symbol);
            if (pipSize <= 0) return 0;
            return priceDelta / pipSize;
        }

        // Normalize price to symbol digits
        public static double NormalizePrice(double price, Symbol symbol)
        {
            return Math.Round(price, symbol.Digits, MidpointRounding.AwayFromZero);
        }

        // Convert currency amount to price distance (Delta)
        // Note: In cTrader Automate API, Symbol.PipValue is the monetary value of 1 Pip for 1 UNIT in Account Currency.
        public static double AmountToPrice(double amount, double volumeUnits, Symbol symbol)
        {
            if (symbol == null || volumeUnits <= 0 || amount <= 0) return 0;

            double pipValueForVolume = symbol.PipValue * volumeUnits;
            if (pipValueForVolume <= 0) return 0;

            double pips = Math.Abs(amount) / pipValueForVolume;
            return PipsToPrice(pips, symbol);
        }

        // Convert price distance to currency amount
        // Note: In cTrader Automate API, Symbol.PipValue is the monetary value of 1 Pip for 1 UNIT in Account Currency.
        public static double PriceToAmount(double priceDelta, double volumeUnits, Symbol symbol)
        {
            if (symbol == null || volumeUnits <= 0 || priceDelta <= 0) return 0;

            double pips = PriceToPips(Math.Abs(priceDelta), symbol);
            return pips * symbol.PipValue * volumeUnits;
        }
    }
}
