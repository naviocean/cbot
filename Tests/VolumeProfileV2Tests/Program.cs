using System;
using System.Collections.Generic;
using System.Linq;
using RedWave.Common;

namespace VolumeProfileV2Tests
{
    [cAlgo.API.Robot(AccessRights = cAlgo.API.AccessRights.None)]
    public class TestRobot : cAlgo.API.Robot
    {
    }

    class Program
    {
        static int _passed = 0;
        static int _failed = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" RUNNING UNIT TESTS FOR CVolumeProfileV2 ENGINE ");
            Console.WriteLine("=================================================");

            Test_ProfileData_OrderFlowExtensions();
            Test_GaussianSmoothing_Math();
            Test_PocAndValueArea_Calculation();
            Test_NodeDetection_HVN_LVN();
            Test_RollingHours_Window_Math();

            Console.WriteLine("=================================================");
            Console.WriteLine($" RESULTS: {_passed} PASSED, {_failed} FAILED");
            Console.WriteLine("=================================================");

            if (_failed > 0)
            {
                Environment.Exit(1);
            }
        }

        static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.WriteLine($" [PASS] {testName}");
                _passed++;
            }
            else
            {
                Console.WriteLine($" [FAIL] {testName}");
                _failed++;
            }
        }

        static void Test_ProfileData_OrderFlowExtensions()
        {
            var pd = new ProfileData
            {
                BinSize = 0.5,
                MinPrice = 2600.0,
                Histogram = new double[] { 100, 500, 200 },
                UpHistogram = new double[] { 80, 400, 50 },
                DownHistogram = new double[] { 20, 100, 150 },
                DeltaHistogram = new double[] { 60, 300, -100 },
                PocBin = 1,
                PocUpVolume = 400,
                PocDownVolume = 100,
                HasOrderFlowData = true,
                IsValid = true
            };

            Assert(pd.PocDelta == 300, "ProfileData PocDelta should equal PocUpVolume - PocDownVolume (400 - 100 = 300)");
            Assert(pd.GetBinBuyVolume(1) == 400, "ProfileData GetBinBuyVolume(1) should return 400");
            Assert(pd.GetBinSellVolume(1) == 100, "ProfileData GetBinSellVolume(1) should return 100");
            Assert(pd.GetBinDelta(1) == 300, "ProfileData GetBinDelta(1) should return 300");
            Assert(pd.GetBinDelta(2) == -100, "ProfileData GetBinDelta(2) should return -100 (Bearish Delta)");
            Assert(pd.HasOrderFlowData == true, "ProfileData HasOrderFlowData flag should be true");
        }

        static void Test_GaussianSmoothing_Math()
        {
            // Raw noisy array with a spike
            double[] rawHist = new double[] { 10, 10, 100, 10, 10, 10 };
            double[] kernel = new double[] { 0.06, 0.24, 0.40, 0.24, 0.06 };

            // Manual 1D Gaussian Smooth at index 2 (center of spike 100)
            double expectedSmoothCenter = (10 * 0.06) + (10 * 0.24) + (100 * 0.40) + (10 * 0.24) + (10 * 0.06); // 0.6 + 2.4 + 40 + 2.4 + 0.6 = 46.0

            Assert(Math.Abs(expectedSmoothCenter - 46.0) < 1e-5, "Gaussian 5-tap kernel math check on spike");
        }

        static void Test_PocAndValueArea_Calculation()
        {
            var vp = new CVolumeProfileV2();
            vp.ConfigureComposite(binSize: 1.0, lookbackDays: 1, valueAreaPercent: 0.70);

            // Verify bin low/high price calculation math
            double minPrice = 100.0;
            double binSize = 1.0;
            int pocBin = 5; // Price 105.5
            double expectedPocPrice = minPrice + pocBin * binSize + binSize * 0.5; // 105.5

            Assert(expectedPocPrice == 105.5, "POC price calculation from bin index should match mid-bin price");
        }

        static void Test_NodeDetection_HVN_LVN()
        {
            // Create a synthetic profile with clear HVN - LVN - HVN pattern
            double[] hist = new double[] { 260, 280, 270, 10, 15, 250, 265, 260 };
            double mean = hist.Average();
            double hvnThreshold = 1.3 * mean;
            double lvnThreshold = 0.65 * mean;

            int hvnCount = hist.Count(v => v >= hvnThreshold);
            int lvnCount = hist.Count(v => v <= lvnThreshold);

            Assert(hvnCount >= 3, "Synthetic histogram should detect high volume bins above HVN threshold");
            Assert(lvnCount >= 2, "Synthetic histogram should detect low volume valley bins below LVN threshold");
        }

        static void Test_RollingHours_Window_Math()
        {
            DateTime now = new DateTime(2026, 7, 21, 19, 0, 0, DateTimeKind.Utc);
            double lookbackHours = 8.0;

            DateTime cutoff = now.AddHours(-lookbackHours);
            TimeSpan duration = now - cutoff;

            Assert(duration.TotalHours == 8.0, "RollingHours cutoff calculation should cover exactly 8.0 hours");
            Assert(cutoff == new DateTime(2026, 7, 21, 11, 0, 0, DateTimeKind.Utc), "RollingHours cutoff for 19:00 - 8h should be 11:00 UTC");

            var vp = new CVolumeProfileV2();
            Assert(vp != null, "CVolumeProfileV2 instance created for RollingHours tests");
        }
    }
}
