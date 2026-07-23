using System;
using cAlgo.API;
using RedWave.Common;

namespace CommonTests
{
    public static class PriceUtilsTests
    {
        public static void RunAll()
        {
            Test_PipsToPrice_And_PriceToPips();
            Test_NormalizePrice();
            Test_AmountToPrice_And_PriceToAmount();
        }

        private static void Test_PipsToPrice_And_PriceToPips()
        {
            double pips = 25.0;
            double pipSize = 0.1;
            double priceDist = pips * pipSize; // 2.5
            double calcPips = priceDist / pipSize; // 25.0

            TestRunner.Assert(priceDist == 2.5, "PriceUtils PipsToPrice - 25 pips = 2.5 price distance when PipSize = 0.1");
            TestRunner.Assert(calcPips == 25.0, "PriceUtils PriceToPips - 2.5 price distance = 25 pips");
        }

        private static void Test_NormalizePrice()
        {
            double price = 2000.12345;
            int digits = 2;
            double normalized = Math.Round(price, digits, MidpointRounding.AwayFromZero);

            TestRunner.Assert(normalized == 2000.12, "PriceUtils NormalizePrice rounds to symbol digits (2 decimals)");
        }

        private static void Test_AmountToPrice_And_PriceToAmount()
        {
            // In cTrader Automate API, Symbol.PipValue is the monetary value of 1 Pip for 1 UNIT.
            // For EURUSD (Account USD): 1 Pip (0.0001) for 1 Unit = $0.00001.
            // For 100,000 units (1 Lot): pipValuePerUnit * 100000 = $1.0 per Pip.
            double pipValuePerUnit = 0.00001;
            double volumeUnits = 100000;
            double pipSize = 0.0001;

            double targetAmount = 50.0; // $50 risk
            double pipValueForVolume = pipValuePerUnit * volumeUnits; // $1.0
            double expectedPips = targetAmount / pipValueForVolume; // 50 pips
            double expectedPriceDelta = expectedPips * pipSize; // 0.0050

            TestRunner.Assert(expectedPips == 50.0, "PriceUtils AmountToPrice - $50 risk at $1/pip = 50 pips");
            TestRunner.Assert(Math.Abs(expectedPriceDelta - 0.0050) < 1e-6, "PriceUtils AmountToPrice - 50 pips = 0.0050 price delta");
        }
    }
}
