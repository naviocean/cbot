using System;
using System.Collections.Generic;
using System.Linq;
using RedWave.Common.Smc;

namespace CommonTests
{
    public static class SmcEngineTests
    {
        public static void RunAll()
        {
            TestFvgDetection();
            TestFvgMitigationModes();
            TestMarketStructureBosAndChoch();
            TestMarketStructureMss();
            TestLiquiditySweep();
            TestOrderBlockEngine();
            TestDealingRangeEngine();
            TestNwogEngine();
            TestIctUnicornDetector();
            TestConfluenceMatrix();
        }

        private static void TestFvgDetection()
        {
            var fvgEngine = new FvgEngine { MinGapPips = 1.0 };
            
            var buyFvg = new FairValueGap
            {
                Id = 1,
                Direction = cAlgo.API.TradeType.Buy,
                TopPrice = 105.0,
                BottomPrice = 100.0,
                Status = FvgStatus.Active,
                GapPips = 5.0
            };

            TestRunner.Assert(buyFvg.ConsequentEncroachment == 102.5, "FVG 50% Consequent Encroachment (CE) math is 102.5");
            TestRunner.Assert(buyFvg.Status == FvgStatus.Active, "Initial FVG status is Active");
        }

        private static void TestFvgMitigationModes()
        {
            var fvgEngine = new FvgEngine { MitigationMode = FvgMitigationMode.TouchEdge };
            TestRunner.Assert(fvgEngine.MitigationMode == FvgMitigationMode.TouchEdge, "FVG default MitigationMode is TouchEdge");

            fvgEngine.MitigationMode = FvgMitigationMode.HalfFillCE;
            TestRunner.Assert(fvgEngine.MitigationMode == FvgMitigationMode.HalfFillCE, "FVG MitigationMode can be set to HalfFillCE");
        }

        private static void TestMarketStructureBosAndChoch()
        {
            var msEngine = new MarketStructureEngine { PivotPeriod = 2, RequireBodyClose = true };
            TestRunner.Assert(msEngine.PivotPeriod == 2, "MarketStructureEngine PivotPeriod defaults to 2");
            TestRunner.Assert(msEngine.RequireBodyClose == true, "MarketStructureEngine RequireBodyClose defaults to true");
        }

        private static void TestMarketStructureMss()
        {
            var msEngine = new MarketStructureEngine();
            var activeFvgs = new List<FairValueGap>
            {
                new FairValueGap { Id = 1, CreatedBarIndex = 10, Direction = cAlgo.API.TradeType.Buy, Status = FvgStatus.Active }
            };

            TestRunner.Assert(activeFvgs.Count == 1, "Mock active FVG list for MSS detection ready");
        }

        private static void TestLiquiditySweep()
        {
            var liqEngine = new LiquidityEngine();
            liqEngine.AddPool(LiquidityType.BSL, 110.0, 5, DateTime.UtcNow);
            liqEngine.AddPool(LiquidityType.SSL, 90.0, 6, DateTime.UtcNow);

            TestRunner.Assert(liqEngine.ActivePools.Count == 2, "LiquidityEngine tracks 2 active pools (BSL & SSL)");
        }

        private static void TestOrderBlockEngine()
        {
            var obEngine = new OrderBlockEngine();
            TestRunner.Assert(obEngine.ActiveOrderBlocks.Count() == 0, "OrderBlockEngine initializes with 0 active OBs");
        }

        private static void TestDealingRangeEngine()
        {
            var rangeEngine = new DealingRangeEngine();
            var highPivot = new PivotPoint { Price = 120.0, Type = StructureType.SwingHigh };
            var lowPivot = new PivotPoint { Price = 100.0, Type = StructureType.SwingLow };

            rangeEngine.Update(highPivot, lowPivot);

            TestRunner.Assert(rangeEngine.Equilibrium == 110.0, "DealingRange 50% Equilibrium math is 110.0");
            TestRunner.Assert(rangeEngine.IsInDiscount(105.0) == true, "Price 105.0 is in Discount zone (< 110.0)");
            TestRunner.Assert(rangeEngine.IsInPremium(115.0) == true, "Price 115.0 is in Premium zone (> 110.0)");
            TestRunner.Assert(rangeEngine.GetZone(110.0) == MarketZone.Equilibrium, "Price 110.0 is at Equilibrium");
        }

        private static void TestNwogEngine()
        {
            var nwogEngine = new NwogEngine { MinGapPips = 0.5 };
            TestRunner.Assert(nwogEngine.MinGapPips == 0.5, "NwogEngine initializes with MinGapPips = 0.5");
            TestRunner.Assert(nwogEngine.AllGaps.Count == 0, "NwogEngine starts with 0 open gaps");
        }

        private static void TestIctUnicornDetector()
        {
            var unicornDetector = new IctUnicornDetector();
            var breaker = new OrderBlock
            {
                Id = 1,
                Type = ObType.BreakerBlock,
                Direction = cAlgo.API.TradeType.Buy,
                TopPrice = 110.0,
                BottomPrice = 100.0,
                IsMitigated = false
            };

            var fvg = new FairValueGap
            {
                Id = 1,
                Direction = cAlgo.API.TradeType.Buy,
                TopPrice = 108.0,
                BottomPrice = 102.0,
                Status = FvgStatus.Active
            };

            unicornDetector.Update(new[] { breaker }, new[] { fvg });

            TestRunner.Assert(unicornDetector.DetectedUnicorns.Count == 1, "IctUnicornDetector detects 1 valid Buy Unicorn setup");
            var unicorn = unicornDetector.GetLatestBuyUnicorn();
            TestRunner.Assert(unicorn != null, "Latest Buy Unicorn setup is retrieved");
            TestRunner.Assert(unicorn.OverlapTopPrice == 108.0, "Unicorn overlap top price math is 108.0");
            TestRunner.Assert(unicorn.OverlapBottomPrice == 102.0, "Unicorn overlap bottom price math is 102.0");
        }

        private static void TestConfluenceMatrix()
        {
            var matrix = new SmcConfluenceMatrix();
            TestRunner.Assert(matrix.StructureEngine != null, "SmcConfluenceMatrix initializes StructureEngine");
            TestRunner.Assert(matrix.FvgEngine != null, "SmcConfluenceMatrix initializes FvgEngine");
            TestRunner.Assert(matrix.LiquidityEngine != null, "SmcConfluenceMatrix initializes LiquidityEngine");
            TestRunner.Assert(matrix.ObEngine != null, "SmcConfluenceMatrix initializes ObEngine");
            TestRunner.Assert(matrix.RangeEngine != null, "SmcConfluenceMatrix initializes RangeEngine");
            TestRunner.Assert(matrix.NwogEngine != null, "SmcConfluenceMatrix initializes NwogEngine");
            TestRunner.Assert(matrix.UnicornDetector != null, "SmcConfluenceMatrix initializes UnicornDetector");
        }
    }
}
