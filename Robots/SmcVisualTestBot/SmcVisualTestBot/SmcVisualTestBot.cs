using System;
using System.Linq;
using cAlgo.API;
using RedWave.Common.Smc;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SmcVisualTestBot : Robot
    {
        private SmcConfluenceMatrix _smcMatrix;
        private SmcChartRenderer _renderer;

        // ==========================================
        // PARAMETERS: MODULE TOGGLES
        // ==========================================
        [Parameter("Enable FVG Logic", Group = "1. Logic Switches", DefaultValue = true)]
        public bool EnableFvgLogic { get; set; }

        [Parameter("Enable Structure Logic", Group = "1. Logic Switches", DefaultValue = true)]
        public bool EnableStructureLogic { get; set; }

        [Parameter("Enable OB Logic", Group = "1. Logic Switches", DefaultValue = true)]
        public bool EnableObLogic { get; set; }

        // ==========================================
        // PARAMETERS: ENGINE THRESHOLDS
        // ==========================================
        [Parameter("Max Bars to Scan", Group = "2. Engine Settings", DefaultValue = 500, MinValue = 50, MaxValue = 5000)]
        public int MaxBarsToScan { get; set; }

        [Parameter("FVG Mitigation Mode", Group = "2. Engine Settings", DefaultValue = FvgMitigationMode.TouchEdge)]
        public FvgMitigationMode MitigationMode { get; set; }

        [Parameter("Min FVG Pips", Group = "2. Engine Settings", DefaultValue = 1.0)]
        public double MinFvgPips { get; set; }

        [Parameter("Pivot Period", Group = "2. Engine Settings", DefaultValue = 2)]
        public int PivotPeriod { get; set; }

        [Parameter("Require Body Break", Group = "2. Engine Settings", DefaultValue = true)]
        public bool RequireBodyBreak { get; set; }

        // ==========================================
        // PARAMETERS: VISUAL RENDER SWITCHES
        // ==========================================
        [Parameter("Show FVG Boxes", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowFvgVisuals { get; set; }

        [Parameter("Show Structure Lines", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowStructureVisuals { get; set; }

        [Parameter("Show OB Boxes", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowObVisuals { get; set; }

        // ==========================================
        // PARAMETERS: TRADE EXECUTION
        // ==========================================
        [Parameter("Place Trade Orders", Group = "4. Trading Settings", DefaultValue = false)]
        public bool EnableTrading { get; set; }

        [Parameter("Risk Lot Size", Group = "4. Trading Settings", DefaultValue = 0.01)]
        public double FixedLots { get; set; }

        protected override void OnStart()
        {
            _smcMatrix = new SmcConfluenceMatrix();
            _smcMatrix.FvgEngine.MinGapPips = MinFvgPips;
            _smcMatrix.FvgEngine.MitigationMode = MitigationMode;
            _smcMatrix.StructureEngine.PivotPeriod = PivotPeriod;
            _smcMatrix.StructureEngine.RequireBodyClose = RequireBodyBreak;

            _renderer = new SmcChartRenderer(Chart);

            // Pre-fill historical bars on startup so bot has full SMC context & drawings immediately
            int startBar = Math.Max(0, Bars.Count - MaxBarsToScan);
            for (int i = startBar; i < Bars.Count; i++)
            {
                _smcMatrix.OnBar(Bars, i, Symbol.PipSize);
            }

            RenderVisuals();
        }

        protected override void OnBar()
        {
            // 1. Process newest bar
            _smcMatrix.OnBar(Bars, Bars.Count - 1, Symbol.PipSize);

            // 2. Render active visual shapes
            RenderVisuals();

            // 3. Trade Execution Logic Test
            if (EnableTrading)
            {
                if (_smcMatrix.IsValidBuySetup(Symbol.Ask, out var targetFvg, out var targetOb))
                {
                    double entry = targetFvg?.TopPrice ?? targetOb?.TopPrice ?? Symbol.Ask;
                    double sl = targetFvg?.BottomPrice ?? targetOb?.BottomPrice ?? (entry - 20 * Symbol.PipSize);

                    if (Positions.Count(p => p.Label == "SMC_TEST") == 0 && PendingOrders.Count(p => p.Label == "SMC_TEST") == 0)
                    {
                        PlaceLimitOrder(TradeType.Buy, SymbolName, Symbol.QuantityToVolumeInUnits(FixedLots), entry, "SMC_TEST", sl, null);
                    }
                }
            }
        }

        private void RenderVisuals()
        {
            if (ShowFvgVisuals && EnableFvgLogic)
            {
                foreach (var fvg in _smcMatrix.FvgEngine.AllFvgs)
                    _renderer.DrawFvg(fvg, true, autoClean: true);
            }

            if (ShowStructureVisuals && EnableStructureLogic)
            {
                foreach (var evt in _smcMatrix.StructureEngine.Events)
                    _renderer.DrawStructure(evt, true);
            }

            if (ShowObVisuals && EnableObLogic)
            {
                foreach (var ob in _smcMatrix.ObEngine.ActiveOrderBlocks)
                    _renderer.DrawOrderBlock(ob, true, autoClean: true);
            }

            string zoneText = _smcMatrix.RangeEngine.GetZone(Symbol.Ask).ToString();
            Chart.DrawStaticText("SMC_BOT_PANEL", $"[SMC Bot] Zone: {zoneText} | Active FVGs: {_smcMatrix.FvgEngine.ActiveFvgs.Count()}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.Gold);
        }
    }
}
