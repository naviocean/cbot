using System;
using System.Collections.Generic;
using RedWave.Common;

namespace CommonTests
{
    public static class ProfileDataTests
    {
        public static void RunAll()
        {
            Test_PriceToBin_And_BinPrices();
            Test_FindNearestLvn();
            Test_FindNearestHvnAboveBelow();
        }

        private static void Test_PriceToBin_And_BinPrices()
        {
            var pd = new ProfileData
            {
                BinSize = 1.0,
                MinPrice = 100.0,
                MaxPrice = 110.0,
                Histogram = new double[10]
            };

            int binMid = pd.PriceToBin(105.5);
            double lowPrice = pd.BinLow(5);   // 105.0
            double highPrice = pd.BinHigh(5); // 106.0
            double midPrice = pd.BinMid(5);   // 105.5

            TestRunner.Assert(binMid == 5, "ProfileData PriceToBin - Price 105.5 maps to Bin 5");
            TestRunner.Assert(lowPrice == 105.0, "ProfileData BinLow(5) equals 105.0");
            TestRunner.Assert(highPrice == 106.0, "ProfileData BinHigh(5) equals 106.0");
            TestRunner.Assert(midPrice == 105.5, "ProfileData BinMid(5) equals 105.5");
        }

        private static void Test_FindNearestLvn()
        {
            var pd = new ProfileData
            {
                Lvns = new List<VolumeNode>
                {
                    new VolumeNode { StartBin = 2, EndBin = 2, Low = 102.0, High = 103.0, Type = VolumeNodeType.LVN },
                    new VolumeNode { StartBin = 7, EndBin = 7, Low = 107.0, High = 108.0, Type = VolumeNodeType.LVN }
                }
            };

            var nearest1025 = pd.FindNearestLvn(102.5); // Inside LVN 1
            var nearest1065 = pd.FindNearestLvn(106.5); // Nearest to LVN 2 (107.0 - 108.0)

            TestRunner.Assert(nearest1025 != null && nearest1025.Low == 102.0, "ProfileData FindNearestLvn - Contains price 102.5 returns LVN 1");
            TestRunner.Assert(nearest1065 != null && nearest1065.Low == 107.0, "ProfileData FindNearestLvn - Nearest to 106.5 returns LVN 2");
        }

        private static void Test_FindNearestHvnAboveBelow()
        {
            var pd = new ProfileData
            {
                Hvns = new List<VolumeNode>
                {
                    new VolumeNode { StartBin = 1, EndBin = 3, Low = 101.0, High = 104.0, Type = VolumeNodeType.HVN },
                    new VolumeNode { StartBin = 8, EndBin = 9, Low = 108.0, High = 110.0, Type = VolumeNodeType.HVN }
                }
            };

            var hvnBelow = pd.FindNearestHvnBelow(106.0); // HVN 1 (High = 104.0 <= 106.0)
            var hvnAbove = pd.FindNearestHvnAbove(106.0); // HVN 2 (Low = 108.0 >= 106.0)

            TestRunner.Assert(hvnBelow != null && hvnBelow.High == 104.0, "ProfileData FindNearestHvnBelow(106.0) returns HVN 1");
            TestRunner.Assert(hvnAbove != null && hvnAbove.Low == 108.0, "ProfileData FindNearestHvnAbove(106.0) returns HVN 2");
        }
    }
}
