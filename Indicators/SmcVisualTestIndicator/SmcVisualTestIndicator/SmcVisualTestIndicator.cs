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

        [Parameter("Enable Inversion FVG (iFVG)", Group = "1. Logic Engines", DefaultValue = true)]
        public bool EnableInversionFvg { get; set; }

        [Parameter("Enable Structure (BOS/MSS)", Group = "1. Logic Engines", DefaultValue = true)]
        public bool EnableStructure { get; set; }

        [Parameter("Enable Order Blocks", Group = "1. Logic Engines", DefaultValue = true)]
        public bool EnableOb { get; set; }

        [Parameter("Enable NWOG / NDOG", Group = "1. Logic Engines", DefaultValue = true)]
        public bool EnableOpenGaps { get; set; }

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
        [Parameter("Show Standard FVG Visuals", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowFvgVisuals { get; set; }

        [Parameter("Show Inversion FVG (iFVG) Visuals", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowIfvgVisuals { get; set; }

        [Parameter("Show Structure Lines", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowStructureVisuals { get; set; }

        [Parameter("Show OB Visuals", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowObVisuals { get; set; }

        [Parameter("Show Open Gap Lines", Group = "3. Visual Render", DefaultValue = true)]
        public bool ShowOpenGapVisuals { get; set; }

        protected override void Initialize()
        {
            _smcMatrix = new SmcConfluenceMatrix();
            _smcMatrix.FvgEngine.MinGapPips = MinFvgPips;
            _smcMatrix.FvgEngine.EnableInversionFvg = EnableInversionFvg;
            _smcMatrix.FvgEngine.MitigationMode = MitigationMode;
            _smcMatrix.StructureEngine.PivotPeriod = PivotPeriod;
            _smcMatrix.StructureEngine.RequireBodyClose = RequireBodyBreak;

            _renderer = new SmcChartRenderer(Chart);
        }

        public override void Calculate(int index)
        {
            if (IsLastBar)
            {
                int startBar = Math.Max(0, Bars.Count - MaxBarsToScan);
                _smcMatrix.Reset();

                for (int i = startBar; i < Bars.Count; i++)
                {
                    _smcMatrix.OnBar(Bars, i, Symbol.PipSize);
                }

                if (EnableFvg)
                {
                    foreach (var fvg in _smcMatrix.FvgEngine.AllFvgs)
                        _renderer.DrawFvg(fvg, ShowFvgVisuals, ShowIfvgVisuals, autoClean: true);
                }

                if (ShowStructureVisuals && EnableStructure)
                {
                    foreach (var evt in _smcMatrix.StructureEngine.Events)
                        _renderer.DrawStructure(evt, true);
                }

                if (ShowObVisuals && EnableOb)
                {
                    foreach (var ob in _smcMatrix.ObEngine.ActiveOrderBlocks)
                        _renderer.DrawOrderBlock(ob, true, autoClean: true);
                }

                if (ShowOpenGapVisuals && EnableOpenGaps)
                {
                    foreach (var gap in _smcMatrix.NwogEngine.ActiveGaps)
                        _renderer.DrawOpenGap(gap, true);
                }

                string zoneText = _smcMatrix.RangeEngine.GetZone(Symbol.Ask).ToString();
                Chart.DrawStaticText("SMC_PANEL", $"[SMC Indicator] Zone: {zoneText} | Active FVGs: {_smcMatrix.FvgEngine.ActiveFvgs.Count(f => !f.IsInversion)} | iFVGs: {_smcMatrix.FvgEngine.InversionFvgs.Count()} | NWOG Gaps: {_smcMatrix.NwogEngine.ActiveGaps.Count()}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.Cyan);
            }
        }
    }
}
