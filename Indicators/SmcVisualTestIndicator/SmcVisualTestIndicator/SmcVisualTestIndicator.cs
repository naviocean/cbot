using System;
using System.Linq;
using cAlgo.API;
using RedWave.Common.Smc;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SmcVisualTestIndicator : Indicator
    {
        private SmcConfluenceMatrix _smcMatrix;
        private SmcChartRenderer _renderer;

        // ==========================================
        // PARAMETERS: LOGIC ENGINE TOGGLES
        // ==========================================
        [Parameter("Enable FVG", Group = "1. Logic Engines", DefaultValue = true)]
        public bool EnableFvg { get; set; }

        [Parameter("Enable Structure (BOS/MSS)", Group = "1. Logic Engines", DefaultValue = true)]
        public bool EnableStructure { get; set; }

        [Parameter("Enable Order Blocks", Group = "1. Logic Engines", DefaultValue = true)]
        public bool EnableOb { get; set; }

        // ==========================================
        // PARAMETERS: ENGINE THRESHOLDS & BOUNDS
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
        // PARAMETERS: VISUAL TOGGLES
        // ==========================================
        [Parameter("Show FVG Visuals", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowFvgVisuals { get; set; }

        [Parameter("Show Structure Lines", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowStructureVisuals { get; set; }

        [Parameter("Show OB Visuals", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowObVisuals { get; set; }

        protected override void Initialize()
        {
            _smcMatrix = new SmcConfluenceMatrix();
            _smcMatrix.FvgEngine.MinGapPips = MinFvgPips;
            _smcMatrix.FvgEngine.MitigationMode = MitigationMode;
            _smcMatrix.StructureEngine.PivotPeriod = PivotPeriod;
            _smcMatrix.StructureEngine.RequireBodyClose = RequireBodyBreak;

            _renderer = new SmcChartRenderer(Chart);
        }

        public override void Calculate(int index)
        {
            // Limit calculation to the last MaxBarsToScan bars
            int startBarIndex = Math.Max(0, Bars.Count - MaxBarsToScan);
            if (index < startBarIndex) return;

            // 1. Process bar sequentially within MaxBarsToScan range
            _smcMatrix.OnBar(Bars, index, Symbol.PipSize);

            // 2. Render ONLY Active visual objects on the last bar
            if (IsLastBar)
            {
                if (EnableFvg)
                {
                    // Clean up mitigated or obsolete FVGs
                    foreach (var fvg in _smcMatrix.FvgEngine.AllFvgs)
                    {
                        _renderer.DrawFvg(fvg, ShowFvgVisuals, autoClean: true);
                    }
                }

                if (EnableStructure)
                {
                    foreach (var evt in _smcMatrix.StructureEngine.Events)
                        _renderer.DrawStructure(evt, ShowStructureVisuals);
                }

                if (EnableOb)
                {
                    foreach (var ob in _smcMatrix.ObEngine.ActiveOrderBlocks)
                        _renderer.DrawOrderBlock(ob, ShowObVisuals, autoClean: true);
                }

                string zoneText = _smcMatrix.RangeEngine.GetZone(Symbol.Ask).ToString();
                Chart.DrawStaticText("SMC_INFO", $"[SMC/ICT Engine] Zone: {zoneText} | Active FVGs: {_smcMatrix.FvgEngine.ActiveFvgs.Count()} | Mode: {MitigationMode}", VerticalAlignment.Top, HorizontalAlignment.Right, Color.Gold);
            }
        }
    }
}
