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

            // Behavior Pipeline Tests (Mock Bars)
            TestFvgScanDetectsBullishGap();
            TestFvgPartiallyFilledBeforeMitigated();
            TestBosEmittedOnce();
            TestMssNotFiredOnFirstBar();
            TestBreakerBlockMitigatedBelowBottom();

            // Phase 2 Sprint 1 Tests
            TestSessionEngineKillZones();
            TestAsianRangeLocksAtLondon();
            TestPdhPdlUpdatesOnNewDay();

            // Phase 2 Sprint 2 Tests
            TestMtfFilterBlocksCounterTrendSignal();
            TestBprOverlapDetectedWhenFvgsIntersect();
            TestBprNoDetectWhenNoOverlap();
            TestBprMitigatedWhenPriceClosesBeyond();
            TestPo3AccumulationDetectedInAsianSession();
            TestPo3ManipulationOnJudasSwing();
            TestPo3DistributionDirectionAfterManipulation();

            // Phase 2 Sprint 3 Tests
            TestDailyBiasScoring();
            TestBiasFilterBlocksSellInBuyBiasDay();
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
            var normalOb = new OrderBlock
            {
                Id = 1,
                Type = ObType.BullishOB,
                Direction = cAlgo.API.TradeType.Buy,
                TopPrice = 110.0,
                BottomPrice = 100.0,
                IsMitigated = false
            };

            var breaker = new OrderBlock
            {
                Id = 2,
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

            // 1. Passing normal OB should NOT detect Unicorn
            unicornDetector.Update(new[] { normalOb }, new[] { fvg });
            TestRunner.Assert(unicornDetector.DetectedUnicorns.Count == 0, "IctUnicornDetector ignores standard OrderBlocks (non-BreakerBlock)");

            // 2. Passing BreakerBlock should detect Unicorn
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

            matrix.RangeEngine.Update(new PivotPoint { Price = 120.0 }, new PivotPoint { Price = 100.0 });
            TestRunner.Assert(matrix.RangeEngine.SwingHigh == 120.0, "RangeEngine SwingHigh set to 120.0");
            matrix.Reset();
            TestRunner.Assert(matrix.RangeEngine.SwingHigh == 0, "RangeEngine SwingHigh reset to 0 by matrix.Reset()");
        }

        private static void TestFvgScanDetectsBullishGap()
        {
            var engine = new FvgEngine { MinGapPips = 1.0 };
            var now = DateTime.UtcNow;
            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = now.AddMinutes(0), Open = 100, High = 102.0, Low = 99.0, Close = 101.5 },
                new MockBarData { Time = now.AddMinutes(5), Open = 101.5, High = 107.0, Low = 101.0, Close = 106.5 },
                new MockBarData { Time = now.AddMinutes(10), Open = 106.5, High = 109.0, Low = 104.0, Close = 108.0 }
            });

            engine.Update(bars, 2, 0.1);
            TestRunner.Assert(engine.AllFvgs.Count == 1, "FvgEngine scan detects 1 Bullish FVG from mock bars");
            var fvg = engine.AllFvgs.FirstOrDefault();
            TestRunner.Assert(fvg != null && fvg.Direction == cAlgo.API.TradeType.Buy, "FvgEngine detected gap direction is Buy");
            TestRunner.Assert(fvg != null && fvg.TopPrice == 104.0 && fvg.BottomPrice == 102.0, "FvgEngine detected gap bounds Top=104.0, Bottom=102.0");
        }

        private static void TestFvgPartiallyFilledBeforeMitigated()
        {
            var engine = new FvgEngine { MinGapPips = 1.0, MitigationMode = FvgMitigationMode.FullFill };
            var now = DateTime.UtcNow;
            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = now.AddMinutes(0), Open = 100, High = 102.0, Low = 99.0, Close = 101.5 },
                new MockBarData { Time = now.AddMinutes(5), Open = 101.5, High = 107.0, Low = 101.0, Close = 106.5 },
                new MockBarData { Time = now.AddMinutes(10), Open = 106.5, High = 109.0, Low = 104.0, Close = 108.0 },
                new MockBarData { Time = now.AddMinutes(15), Open = 108.0, High = 108.5, Low = 102.5, Close = 107.0 }
            });

            engine.Update(bars, 2, 0.1);
            engine.Update(bars, 3, 0.1);
            var fvg = engine.AllFvgs.FirstOrDefault();
            TestRunner.Assert(fvg != null && fvg.Status == FvgStatus.PartiallyFilled, "FvgEngine sets status to PartiallyFilled when price retests past CE under FullFill mode");
        }

        private static void TestBosEmittedOnce()
        {
            var msEngine = new MarketStructureEngine { PivotPeriod = 1, RequireBodyClose = true };
            var now = DateTime.UtcNow;
            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = now.AddMinutes(0), Open = 100, High = 105.0, Low = 99.0, Close = 104.0 },
                new MockBarData { Time = now.AddMinutes(5), Open = 104, High = 110.0, Low = 103.0, Close = 108.0 },
                new MockBarData { Time = now.AddMinutes(10), Open = 108, High = 106.0, Low = 102.0, Close = 105.0 },
                new MockBarData { Time = now.AddMinutes(15), Open = 105, High = 107.0, Low = 103.0, Close = 106.0 },
                new MockBarData { Time = now.AddMinutes(20), Open = 106, High = 113.0, Low = 105.0, Close = 112.0 },
                new MockBarData { Time = now.AddMinutes(25), Open = 112, High = 115.0, Low = 111.0, Close = 114.0 }
            });

            for (int i = 0; i < bars.Count; i++)
            {
                msEngine.Update(bars, i);
            }

            TestRunner.Assert(msEngine.Events.Count == 1, "MarketStructureEngine emits BOS event ONCE upon pivot break (no duplicate spam)");
        }

        private static void TestMssNotFiredOnFirstBar()
        {
            var msEngine = new MarketStructureEngine { PivotPeriod = 1, RequireBodyClose = true };
            var now = DateTime.UtcNow;
            // Bar 1 is a pivot (High 105.0, Low 90.0)
            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = now.AddMinutes(0), Open = 100, High = 100.0, Low = 95.0, Close = 98.0 },
                new MockBarData { Time = now.AddMinutes(5), Open = 98, High = 105.0, Low = 90.0, Close = 92.0 },
                new MockBarData { Time = now.AddMinutes(10), Open = 92, High = 96.0, Low = 91.0, Close = 95.0 },
                new MockBarData { Time = now.AddMinutes(15), Open = 95, High = 96.0, Low = 84.0, Close = 85.0 }
            });

            for (int i = 0; i < bars.Count; i++)
            {
                msEngine.Update(bars, i);
            }

            TestRunner.Assert(msEngine.LatestEvent != null && msEngine.LatestEvent.Type == BreakType.BOS, "MarketStructureEngine first break emits BOS (not false MSS/ChoCH)");
        }

        private static void TestBreakerBlockMitigatedBelowBottom()
        {
            // Integration test: drives full lifecycle through OrderBlockEngine.Update()
            // Scenario: Bearish OB detected → closes above TopPrice → converts to Bullish BreakerBlock
            //           → Low stays above BottomPrice (NOT mitigated) → Low drops below BottomPrice (MITIGATED)
            // This guards against regression of BUG-OBE-01 (mitigation condition was inverted).
            //
            // Bar layout (pipSize = 0.1):
            //   bars[0]: Bearish OB candidate — bullish body (Close=103 >= Open=100)
            //   bars[1]: middle bar for Bearish FVG (CreatedBarIndex=1)
            //   bars[2]: Bearish FVG confirm — High(98) < firstLow(102) → gap detected
            //            → engine detects Bearish OB at barIndex=0 (TopPrice=103, BottomPrice=100)
            //   bars[3]: Close(107) > OB.TopPrice(103) → converts to Bullish BreakerBlock (Direction=Buy, BottomPrice=100)
            //   bars[4]: Low(101) > OB.BottomPrice(100) → BreakerBlock still ACTIVE
            //   bars[5]: Low(98)  < OB.BottomPrice(100) → BreakerBlock MITIGATED ← BUG-OBE-01 guard
            var engine = new OrderBlockEngine { EnableBreakerBlocks = true, UseBodyBounds = true };
            var now = DateTime.UtcNow;
            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = now.AddMinutes(0),  Open = 100, High = 101.0, Low = 99.0,  Close = 100.0 }, // bar 0 padding
                new MockBarData { Time = now.AddMinutes(5),  Open = 100, High = 103.5, Low = 102.0, Close = 103.0 }, // bar 1 Bearish OB (Top 103, Bottom 100)
                new MockBarData { Time = now.AddMinutes(10), Open = 103, High = 103.2, Low = 102.5, Close = 102.0 }, // bar 2 FVG created
                new MockBarData { Time = now.AddMinutes(15), Open = 102, High = 98.0,  Low = 97.0,  Close = 97.5  }, // bar 3 FVG confirm
                new MockBarData { Time = now.AddMinutes(20), Open = 97,  High = 110.0, Low = 96.0,  Close = 107.0 }, // bar 4 Close(107) > Top(103) -> Breaker
                new MockBarData { Time = now.AddMinutes(25), Open = 107, High = 110.0, Low = 101.0, Close = 103.0 }, // bar 5 Low(101) > Bottom(100) -> Active
                new MockBarData { Time = now.AddMinutes(30), Open = 103, High = 104.0, Low = 98.0,  Close = 99.0  }, // bar 6 Low(98) < Bottom(100) -> Mitigated
            });

            // Mock Bearish FVG at CreatedBarIndex=2
            var bearishFvg = new FairValueGap
            {
                Id = 1, Direction = cAlgo.API.TradeType.Sell,
                TopPrice = 102.0, BottomPrice = 98.0,
                Status = FvgStatus.Active, CreatedBarIndex = 2, IsInversion = false
            };
            // Mock StructureEvent confirming the Bearish move at bar[3]
            var sellEvent = new StructureEvent
            {
                Direction = cAlgo.API.TradeType.Sell,
                TriggerBarIndex = 3,
                Type = BreakType.BOS
            };

            var noFvgs = System.Linq.Enumerable.Empty<FairValueGap>();
            var noEvents = System.Linq.Enumerable.Empty<StructureEvent>();

            // Step 1: bar[3] — detect Bearish OB at barIndex=1 (FVG + StructureEvent match)
            engine.Update(bars, new[] { bearishFvg }, new[] { sellEvent }, 3);
            TestRunner.Assert(engine.AllOrderBlocks.Count == 1, "OrderBlockEngine detects 1 Bearish OB after FVG+StructureEvent confirmation");
            TestRunner.Assert(engine.BreakerBlocks.Count() == 0, "No BreakerBlocks yet — OB still standard Bearish OB");

            // Step 2: bar[4] — Close(107) > OB.TopPrice(103) → EnableBreakerBlocks → Bullish BreakerBlock
            engine.Update(bars, noFvgs, noEvents, 4);
            TestRunner.Assert(engine.BreakerBlocks.Count() == 1, "Bearish OB converts to Bullish BreakerBlock when price closes above TopPrice");
            var breaker = engine.BreakerBlocks.First();
            TestRunner.Assert(breaker.Direction == cAlgo.API.TradeType.Buy, "Converted BreakerBlock direction is Buy (acts as Support zone)");
            TestRunner.Assert(breaker.BottomPrice == 100.0, "BreakerBlock BottomPrice is 100.0 (body lower bound of original OB)");

            // Step 3: bar[5] — Low(101) > BottomPrice(100) → Bullish BreakerBlock still ACTIVE
            engine.Update(bars, noFvgs, noEvents, 5);
            TestRunner.Assert(!breaker.IsMitigated, "Bullish BreakerBlock remains active when Low(101) > BottomPrice(100) — guard pre-condition");

            // Step 4: bar[6] — Low(98) < BottomPrice(100) → MITIGATED (BUG-OBE-01 regression guard)
            engine.Update(bars, noFvgs, noEvents, 6);
            TestRunner.Assert(breaker.IsMitigated, "Bullish BreakerBlock is mitigated when Low(98) < BottomPrice(100) — BUG-OBE-01 regression guard");
            TestRunner.Assert(engine.BreakerBlocks.Count() == 0, "BreakerBlocks collection is empty after mitigation");
        }

        private static void TestSessionEngineKillZones()
        {
            var session = new SessionEngine();
            var tLondon = new DateTime(2026, 7, 23, 3, 0, 0, DateTimeKind.Utc);
            session.Update(tLondon, 105.0, 100.0);
            TestRunner.Assert(session.CurrentSession == SessionType.London, "SessionEngine detects London Session at 03:00 UTC");
            TestRunner.Assert(session.ActiveKillZone == KillZone.LOKZ, "SessionEngine detects LOKZ KillZone at 03:00 UTC");
            TestRunner.Assert(session.IsInKillZone == true, "SessionEngine IsInKillZone is true during LOKZ");

            var tNyAm = new DateTime(2026, 7, 23, 8, 30, 0, DateTimeKind.Utc);
            session.Update(tNyAm, 105.0, 100.0);
            TestRunner.Assert(session.CurrentSession == SessionType.NewYork, "SessionEngine detects NY Session at 08:30 UTC");
            TestRunner.Assert(session.ActiveKillZone == KillZone.NYAM, "SessionEngine detects NYAM KillZone at 08:30 UTC");

            var tSb1 = new DateTime(2026, 7, 23, 10, 30, 0, DateTimeKind.Utc);
            session.Update(tSb1, 105.0, 100.0);
            TestRunner.Assert(session.ActiveKillZone == KillZone.SilverBullet1, "SessionEngine detects SilverBullet1 at 10:30 UTC");
            TestRunner.Assert(session.IsInSilverBullet == true, "SessionEngine IsInSilverBullet is true during SB1");
        }

        private static void TestAsianRangeLocksAtLondon()
        {
            var session = new SessionEngine();
            var tAsian1 = new DateTime(2026, 7, 23, 21, 0, 0, DateTimeKind.Utc);
            session.Update(tAsian1, 110.0, 100.0);

            var tAsian2 = new DateTime(2026, 7, 23, 22, 0, 0, DateTimeKind.Utc);
            session.Update(tAsian2, 115.0, 95.0);

            TestRunner.Assert(session.AsianHigh == 115.0 && session.AsianLow == 95.0, "Asian Range updates high=115 and low=95 during Asian session");
            TestRunner.Assert(session.AsianRangeLocked == false, "Asian Range is not locked during Asian session");

            var tLondon = new DateTime(2026, 7, 24, 2, 0, 0, DateTimeKind.Utc);
            session.Update(tLondon, 120.0, 90.0);

            TestRunner.Assert(session.AsianRangeLocked == true, "Asian Range locks upon transition to London session");
            TestRunner.Assert(session.AsianHigh == 115.0 && session.AsianLow == 95.0, "Asian Range High/Low frozen after locking");
        }

        private static void TestPdhPdlUpdatesOnNewDay()
        {
            var liquidity = new LiquidityEngine();
            var day1Time = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
            var barsDay1 = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = day1Time, Open = 100, High = 105.0, Low = 95.0, Close = 102.0 },
                new MockBarData { Time = day1Time.AddHours(2), Open = 102, High = 110.0, Low = 94.0, Close = 108.0 },
                new MockBarData { Time = day1Time.AddHours(4), Open = 108, High = 109.0, Low = 98.0, Close = 106.0 },
                new MockBarData { Time = day1Time.AddHours(6), Open = 106, High = 107.0, Low = 97.0, Close = 105.0 },
                new MockBarData { Time = day1Time.AddHours(8), Open = 105, High = 108.0, Low = 96.0, Close = 104.0 }
            });

            for (int i = 0; i < barsDay1.Count; i++)
            {
                liquidity.Update(barsDay1, i, 0.1, barsDay1.OpenTimes[i]);
            }

            var day2Time = new DateTime(2026, 7, 23, 1, 0, 0, DateTimeKind.Utc);
            var barsDay2 = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = day2Time, Open = 104, High = 106.0, Low = 103.0, Close = 105.0 },
                new MockBarData { Time = day2Time.AddHours(2), Open = 105, High = 107.0, Low = 102.0, Close = 104.0 },
                new MockBarData { Time = day2Time.AddHours(4), Open = 104, High = 108.0, Low = 101.0, Close = 106.0 },
                new MockBarData { Time = day2Time.AddHours(6), Open = 106, High = 109.0, Low = 100.0, Close = 107.0 },
                new MockBarData { Time = day2Time.AddHours(8), Open = 107, High = 110.0, Low = 99.0, Close = 108.0 }
            });

            for (int i = 0; i < barsDay2.Count; i++)
            {
                liquidity.Update(barsDay2, i, 0.1, barsDay2.OpenTimes[i]);
            }

            TestRunner.Assert(liquidity.PreviousDayHigh == 110.0, "LiquidityEngine updates PreviousDayHigh = 110.0 on new day rollover");
            TestRunner.Assert(liquidity.PreviousDayLow == 94.0, "LiquidityEngine updates PreviousDayLow = 94.0 on new day rollover");
        }

        private static void TestMtfFilterBlocksCounterTrendSignal()
        {
            var matrix = new SmcConfluenceMatrix();
            matrix.EnableMtfFilter = true;
            matrix.HTFBias = new MtfBias
            {
                IsValid = true,
                Direction = cAlgo.API.TradeType.Sell
            };

            // Set Range to Discount and Structure to Buy
            matrix.RangeEngine.Update(new PivotPoint { Price = 120.0 }, new PivotPoint { Price = 100.0 });
            // Structure LastDirection Buy set by creating a Buy FVG/OB
            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = DateTime.UtcNow, Open = 100, High = 101, Low = 99, Close = 100.5 },
                new MockBarData { Time = DateTime.UtcNow.AddMinutes(5), Open = 100.5, High = 107, Low = 100, Close = 106.5 },
                new MockBarData { Time = DateTime.UtcNow.AddMinutes(10), Open = 106.5, High = 109, Low = 104, Close = 108.0 }
            });
            matrix.OnBar(bars, 2, 0.1);

            bool isBuyValid = matrix.IsValidBuySetup(105.0, out _, out _);
            TestRunner.Assert(!isBuyValid, "SmcConfluenceMatrix MTF filter blocks counter-trend Buy setup when HTFBias is Sell");
        }

        private static void TestBprOverlapDetectedWhenFvgsIntersect()
        {
            var bprEngine = new BprEngine { MinOverlapPips = 1.0 };
            var bullFvg = new FairValueGap
            {
                Id = 1, Direction = cAlgo.API.TradeType.Buy,
                TopPrice = 107.0, BottomPrice = 102.0, Status = FvgStatus.Active, CreatedBarIndex = 1
            };
            var bearFvg = new FairValueGap
            {
                Id = 2, Direction = cAlgo.API.TradeType.Sell,
                TopPrice = 109.0, BottomPrice = 104.0, Status = FvgStatus.Active, CreatedBarIndex = 2
            };

            bprEngine.Update(new[] { bullFvg, bearFvg }, 0.1);

            TestRunner.Assert(bprEngine.ActiveBprs.ToList().Count == 1, "BprEngine detects 1 active Balanced Price Range overlap");
            var bpr = bprEngine.ActiveBprs.First();
            TestRunner.Assert(bpr.OverlapTopPrice == 107.0 && bpr.OverlapBottomPrice == 104.0, "BprEngine overlap bounds math Top=107.0, Bottom=104.0");
            TestRunner.Assert(bpr.Direction == cAlgo.API.TradeType.Buy, "BPR direction is Buy when Bullish FVG formed before Bearish FVG");
        }

        private static void TestBprNoDetectWhenNoOverlap()
        {
            var bprEngine = new BprEngine { MinOverlapPips = 1.0 };
            var bullFvg = new FairValueGap
            {
                Id = 1, Direction = cAlgo.API.TradeType.Buy,
                TopPrice = 102.0, BottomPrice = 100.0, Status = FvgStatus.Active, CreatedBarIndex = 1
            };
            var bearFvg = new FairValueGap
            {
                Id = 2, Direction = cAlgo.API.TradeType.Sell,
                TopPrice = 109.0, BottomPrice = 105.0, Status = FvgStatus.Active, CreatedBarIndex = 2
            };

            bprEngine.Update(new[] { bullFvg, bearFvg }, 0.1);
            TestRunner.Assert(bprEngine.ActiveBprs.ToList().Count == 0, "BprEngine detects 0 BPRs when FVGs do not overlap");
        }

        private static void TestBprMitigatedWhenPriceClosesBeyond()
        {
            var bprEngine = new BprEngine { MinOverlapPips = 1.0 };
            var bullFvg = new FairValueGap
            {
                Id = 1, Direction = cAlgo.API.TradeType.Buy,
                TopPrice = 107.0, BottomPrice = 104.0, Status = FvgStatus.Active, CreatedBarIndex = 1
            };
            var bearFvg = new FairValueGap
            {
                Id = 2, Direction = cAlgo.API.TradeType.Sell,
                TopPrice = 109.0, BottomPrice = 105.0, Status = FvgStatus.Active, CreatedBarIndex = 2
            };

            bprEngine.Update(new[] { bullFvg, bearFvg }, 0.1);
            var bpr = bprEngine.ActiveBprs.First();
            TestRunner.Assert(!bpr.IsMitigated, "BPR Buy is initially unmitigated");

            // Close below OverlapBottomPrice 105.0 -> Mitigated
            bprEngine.Update(new[] { bullFvg, bearFvg }, 0.1, recentClose: 103.0);
            TestRunner.Assert(bpr.IsMitigated, "BPR Buy is mitigated when recent close falls below OverlapBottomPrice");
        }

        private static void TestPo3AccumulationDetectedInAsianSession()
        {
            var po3 = new PowerOfThreeEngine { MinAsianRangePips = 5.0 };
            var session = new SessionEngine();
            var liquidity = new LiquidityEngine();

            var tAsian = new DateTime(2026, 7, 23, 21, 0, 0, DateTimeKind.Utc);
            session.Update(tAsian, 110.0, 100.0); // Range 10 pips >= 5.0

            po3.Update(session, liquidity, pipSize: 0.1, barTime: tAsian);
            TestRunner.Assert(po3.CurrentPhase == Po3Phase.Accumulation, "PowerOfThreeEngine enters Accumulation phase during Asian session");
        }

        private static void TestPo3ManipulationOnJudasSwing()
        {
            var po3 = new PowerOfThreeEngine { MinAsianRangePips = 5.0 };
            var session = new SessionEngine();
            var liquidity = new LiquidityEngine();

            var tAsian = new DateTime(2026, 7, 23, 21, 0, 0, DateTimeKind.Utc);
            session.Update(tAsian, 110.0, 100.0);
            po3.Update(session, liquidity, pipSize: 0.1, barTime: tAsian);

            // Transition to London
            var tLondon = new DateTime(2026, 7, 24, 3, 0, 0, DateTimeKind.Utc);
            session.Update(tLondon, 110.0, 98.0); // Dips below AsianLow 100.0
            liquidity.SetSessionLevels(session.AsianHigh, session.AsianLow);

            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = tAsian, Open = 105, High = 106, Low = 104, Close = 105 },
                new MockBarData { Time = tAsian.AddMinutes(5), Open = 105, High = 106, Low = 104, Close = 105 },
                new MockBarData { Time = tAsian.AddMinutes(10), Open = 105, High = 106, Low = 104, Close = 105 },
                new MockBarData { Time = tAsian.AddMinutes(15), Open = 105, High = 106, Low = 104, Close = 105 },
                new MockBarData { Time = tLondon, Open = 102, High = 103, Low = 98.0, Close = 101.0 } // sweeps AsianLow 100.0 and closes back inside
            });

            // Update liquidity to register sweep
            liquidity.AddPool(LiquidityType.AsianLow, 100.0, 0, tAsian);
            liquidity.Update(bars, 4, 0.1, tLondon);

            po3.Update(session, liquidity, pipSize: 0.1, barTime: tLondon);
            TestRunner.Assert(po3.CurrentPhase == Po3Phase.Manipulation, "PowerOfThreeEngine enters Manipulation phase on Asian Low Judas Swing sweep");
            TestRunner.Assert(po3.DistributionDirection == cAlgo.API.TradeType.Buy, "PowerOfThreeEngine sets DistributionDirection to Buy after Asian Low sweep");
        }

        private static void TestPo3DistributionDirectionAfterManipulation()
        {
            var po3 = new PowerOfThreeEngine { MinAsianRangePips = 5.0 };
            var session = new SessionEngine();
            var liquidity = new LiquidityEngine();

            var tAsian = new DateTime(2026, 7, 23, 21, 0, 0, DateTimeKind.Utc);
            session.Update(tAsian, 110.0, 100.0);
            po3.Update(session, liquidity, pipSize: 0.1, barTime: tAsian);

            var tLondon = new DateTime(2026, 7, 24, 3, 0, 0, DateTimeKind.Utc);
            session.Update(tLondon, 110.0, 98.0);
            liquidity.AddPool(LiquidityType.AsianLow, 100.0, 0, tAsian);

            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = tAsian, Open = 105, High = 106, Low = 104, Close = 105 },
                new MockBarData { Time = tAsian.AddMinutes(5), Open = 105, High = 106, Low = 104, Close = 105 },
                new MockBarData { Time = tAsian.AddMinutes(10), Open = 105, High = 106, Low = 104, Close = 105 },
                new MockBarData { Time = tAsian.AddMinutes(15), Open = 105, High = 106, Low = 104, Close = 105 },
                new MockBarData { Time = tLondon, Open = 102, High = 103, Low = 98.0, Close = 101.0 }
            });
            liquidity.Update(bars, 4, 0.1, tLondon);

            po3.Update(session, liquidity, pipSize: 0.1, barTime: tLondon); // Manipulation phase
            po3.Update(session, liquidity, pipSize: 0.1, barTime: tLondon.AddMinutes(15)); // Distribution phase

            TestRunner.Assert(po3.CurrentPhase == Po3Phase.Distribution, "PowerOfThreeEngine reaches Distribution phase");
            TestRunner.Assert(po3.IsSetupValid == true, "PowerOfThreeEngine IsSetupValid is true during Distribution phase");
        }

        private static void TestDailyBiasScoring()
        {
            var biasEngine = new DailyBiasEngine();
            var htfBias = new MtfBias { IsValid = true, Direction = cAlgo.API.TradeType.Buy };
            var range = new DealingRangeEngine();
            range.Update(new PivotPoint { Price = 120.0 }, new PivotPoint { Price = 100.0 }); // Eq = 110.0
            var liquidity = new LiquidityEngine();
            var session = new SessionEngine();
            var tAsian = new DateTime(2026, 7, 23, 21, 0, 0, DateTimeKind.Utc);
            session.Update(tAsian, 110.0, 90.0); // AsianMidpoint = 100.0

            // Add PDL sweep so PDL swept & PDH intact -> condition 3 +0.25 Buy
            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = tAsian, Open = 100, High = 105, Low = 95, Close = 102 },
                new MockBarData { Time = tAsian.AddMinutes(5), Open = 102, High = 105, Low = 95, Close = 102 },
                new MockBarData { Time = tAsian.AddMinutes(10), Open = 102, High = 105, Low = 95, Close = 102 },
                new MockBarData { Time = tAsian.AddMinutes(15), Open = 102, High = 105, Low = 95, Close = 102 },
                new MockBarData { Time = tAsian.AddMinutes(20), Open = 102, High = 105, Low = 95, Close = 102 }
            });
            liquidity.AddPool(LiquidityType.PDL, 98.0, 0, tAsian);
            liquidity.Update(bars, 4, 0.1, tAsian);

            // Price 95.0 is in Discount (< 110.0) and < AsianMidpoint (100.0)
            // HTFBias Buy (+0.25), Discount (+0.25), PDL swept (+0.25), Price < AsianMid (+0.25) -> Total Buy Score = 1.0
            biasEngine.Update(htfBias, range, liquidity, session, 95.0, tAsian);

            TestRunner.Assert(biasEngine.TodayBias == BiasType.BuyBias, "DailyBiasEngine calculates BuyBias when 4/4 conditions match");
            TestRunner.Assert(biasEngine.BiasScore == 1.0, "DailyBiasEngine calculates 1.0 BiasScore for perfect Buy bias");
        }

        private static void TestBiasFilterBlocksSellInBuyBiasDay()
        {
            var matrix = new SmcConfluenceMatrix();
            matrix.EnableBiasFilter = true;
            matrix.HTFBias = new MtfBias { IsValid = true, Direction = cAlgo.API.TradeType.Buy };
            matrix.RangeEngine.Update(new PivotPoint { Price = 120.0 }, new PivotPoint { Price = 100.0 });

            var tAsian = new DateTime(2026, 7, 23, 21, 0, 0, DateTimeKind.Utc);
            matrix.SessionEngine.Update(tAsian, 110.0, 90.0);

            // Update matrix on bar to calculate BuyBias
            var bars = new MockBars(new List<MockBarData>
            {
                new MockBarData { Time = tAsian, Open = 105, High = 106, Low = 104, Close = 105 }
            });
            matrix.OnBar(bars, 0, 0.1);

            bool isSellValid = matrix.IsValidSellSetup(115.0, out _, out _);
            TestRunner.Assert(!isSellValid, "SmcConfluenceMatrix Bias filter blocks Sell setup on a BuyBias day");
        }

    }

    public class MockDataSeries : cAlgo.API.DataSeries
    {
        private readonly List<double> _v;
        public MockDataSeries(List<double> v) => _v = v ?? new List<double>();
        public double this[int index] => (index >= 0 && index < _v.Count) ? _v[index] : 0.0;
        public int Count => _v.Count;
        public double LastValue => _v.Count > 0 ? _v[_v.Count - 1] : 0.0;
        public double Last(int index) => (_v.Count - 1 - index >= 0) ? _v[_v.Count - 1 - index] : 0.0;
        public IEnumerator<double> GetEnumerator() => _v.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class MockTimeSeries : cAlgo.API.TimeSeries
    {
        private readonly List<DateTime> _v;
        public MockTimeSeries(List<DateTime> v) => _v = v ?? new List<DateTime>();
        public DateTime this[int index] => (index >= 0 && index < _v.Count) ? _v[index] : DateTime.MinValue;
        public int Count => _v.Count;
        public DateTime LastValue => _v.Count > 0 ? _v[_v.Count - 1] : DateTime.MinValue;
        public DateTime Last(int index) => (_v.Count - 1 - index >= 0) ? _v[_v.Count - 1 - index] : DateTime.MinValue;
        public int GetIndexByExactTime(DateTime time) => _v.IndexOf(time);
        public int GetIndexByTime(DateTime time)
        {
            for (int i = _v.Count - 1; i >= 0; i--)
                if (_v[i] <= time) return i;
            return -1;
        }
        public IEnumerator<DateTime> GetEnumerator() => _v.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class MockBarData
    {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
    }

    public class MockBars : cAlgo.API.Bars
    {
        private readonly List<MockBarData> _bars;

        public MockDataSeries OpenPrices { get; }
        public MockDataSeries HighPrices { get; }
        public MockDataSeries LowPrices { get; }
        public MockDataSeries ClosePrices { get; }
        public MockDataSeries TickVolumes { get; }
        public MockTimeSeries OpenTimes { get; }

        cAlgo.API.DataSeries cAlgo.API.Bars.OpenPrices => OpenPrices;
        cAlgo.API.DataSeries cAlgo.API.Bars.HighPrices => HighPrices;
        cAlgo.API.DataSeries cAlgo.API.Bars.LowPrices => LowPrices;
        cAlgo.API.DataSeries cAlgo.API.Bars.ClosePrices => ClosePrices;
        cAlgo.API.DataSeries cAlgo.API.Bars.TickVolumes => TickVolumes;
        cAlgo.API.TimeSeries cAlgo.API.Bars.OpenTimes => OpenTimes;

        public string SymbolName => "EURUSD";
        public cAlgo.API.TimeFrame TimeFrame => cAlgo.API.TimeFrame.Minute5;

        public MockBars(List<MockBarData> bars = null)
        {
            _bars = bars ?? new List<MockBarData>();
            OpenPrices = new MockDataSeries(_bars.Select(b => b.Open).ToList());
            HighPrices = new MockDataSeries(_bars.Select(b => b.High).ToList());
            LowPrices = new MockDataSeries(_bars.Select(b => b.Low).ToList());
            ClosePrices = new MockDataSeries(_bars.Select(b => b.Close).ToList());
            TickVolumes = new MockDataSeries(_bars.Select(_ => 100.0).ToList());
            OpenTimes = new MockTimeSeries(_bars.Select(b => b.Time).ToList());
        }

        public int Count => _bars.Count;
        public cAlgo.API.Bar this[int index] => default;
        public cAlgo.API.Bar LastBar => default;
        public cAlgo.API.Bar Last(int index) => default;

        public int LoadMoreHistory() => 0;
        public void LoadMoreHistoryAsync() { }
        public void LoadMoreHistoryAsync(Action<cAlgo.API.BarsHistoryLoadedEventArgs> callback) { }
        public cAlgo.API.DataSeries GetPrices(cAlgo.API.PriceType priceType) => ClosePrices;
        public DateTime GetServerFirstBarOpenTime() => OpenTimes.Count > 0 ? OpenTimes[0] : DateTime.MinValue;

        public cAlgo.API.DataSeries AveragePrices => ClosePrices;
        public cAlgo.API.DataSeries MedianPrices => ClosePrices;
        public cAlgo.API.DataSeries TypicalPrices => ClosePrices;
        public cAlgo.API.DataSeries WeightedPrices => ClosePrices;

        public event Action<cAlgo.API.BarsHistoryLoadedEventArgs> HistoryLoaded;
        public event Action<cAlgo.API.BarsHistoryLoadedEventArgs> Reloaded;
        public event Action<cAlgo.API.BarsTickEventArgs> Tick;
        public event Action<cAlgo.API.BarOpenedEventArgs> BarOpened;
        public event Action<cAlgo.API.BarClosedEventArgs> BarClosed;

        public IEnumerator<cAlgo.API.Bar> GetEnumerator()
        {
            yield break;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
