using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Handles visual rendering of SMC/ICT elements on the cTrader Chart Canvas.
    /// Supports automatic cleanup and performance toggle disabling.
    /// </summary>
    public class SmcChartRenderer
    {
        private readonly Chart _chart;

        // Standard FVG Colors: Cyan (Bullish) vs HotPink (Bearish)
        public Color BullishFvgColor { get; set; } = Color.FromArgb(60, 0, 238, 255);   // Semi-transparent Cyan
        public Color BearishFvgColor { get; set; } = Color.FromArgb(60, 255, 20, 147);  // Semi-transparent HotPink
        
        // Inversion FVG (iFVG) Colors: LimeGreen (Bullish iFVG) vs Crimson (Bearish iFVG)
        public Color BullishIfvgColor { get; set; } = Color.FromArgb(60, 50, 205, 50);   // Semi-transparent LimeGreen
        public Color BearishIfvgColor { get; set; } = Color.FromArgb(60, 220, 20, 60);   // Semi-transparent Crimson

        // OB Colors: Royal Blue (Bullish) vs Dark Purple (Bearish)
        public Color BullishObColor { get; set; } = Color.FromArgb(70, 30, 144, 255);   // Semi-transparent Royal Blue
        public Color BearishObColor { get; set; } = Color.FromArgb(70, 153, 50, 204);  // Semi-transparent Dark Purple

        // Open Gap Colors (NWOG / NDOG)
        public Color NwogColor { get; set; } = Color.Aqua;
        public Color NdogColor { get; set; } = Color.SpringGreen;

        // Structure Lines Colors
        public Color BullishBosColor { get; set; } = Color.LimeGreen;
        public Color BearishBosColor { get; set; } = Color.Crimson;
        public Color BullishChochColor { get; set; } = Color.Gold;
        public Color BearishChochColor { get; set; } = Color.DarkOrange;

        // MSS Colors (Explicitly Defined)
        public Color BullishMssColor { get; set; } = Color.Yellow;
        public Color BearishMssColor { get; set; } = Color.Magenta;

        public SmcChartRenderer(Chart chart)
        {
            _chart = chart;
        }

        public void DrawFvg(FairValueGap fvg, bool showVisual, bool autoClean = true)
        {
            if (_chart == null) return;
            string key = $"SMC_FVG_{fvg.Id}";

            if (!showVisual || (autoClean && (fvg.Status == FvgStatus.Mitigated || fvg.Status == FvgStatus.Invalidated)))
            {
                _chart.RemoveObject(key);
                _chart.RemoveObject(key + "_TXT");
                return;
            }

            Color color;
            Color textCol;
            string label;

            if (fvg.IsInversion || fvg.Status == FvgStatus.Inversion)
            {
                color = fvg.Direction == TradeType.Buy ? BullishIfvgColor : BearishIfvgColor;
                textCol = fvg.Direction == TradeType.Buy ? Color.LimeGreen : Color.Crimson;
                label = fvg.Direction == TradeType.Buy ? $"iFVG (Buy) #{fvg.Id}" : $"iFVG (Sell) #{fvg.Id}";
            }
            else
            {
                color = fvg.Direction == TradeType.Buy ? BullishFvgColor : BearishFvgColor;
                textCol = fvg.Direction == TradeType.Buy ? Color.DarkCyan : Color.DeepPink;
                label = fvg.Direction == TradeType.Buy ? $"FVG (Buy) #{fvg.Id}" : $"FVG (Sell) #{fvg.Id}";
            }

            var rect = _chart.DrawRectangle(
                key,
                fvg.CreatedBarIndex,
                fvg.TopPrice,
                _chart.LastVisibleBarIndex + 5,
                fvg.BottomPrice,
                color
            );
            rect.IsFilled = true;

            _chart.DrawText(key + "_TXT", label, fvg.CreatedBarIndex, fvg.TopPrice, textCol);
        }

        public void DrawOpenGap(OpenGapLevel gap, bool showVisual)
        {
            if (_chart == null) return;
            string key = $"SMC_GAP_{gap.Id}";

            if (!showVisual || gap.IsFilled)
            {
                _chart.RemoveObject(key);
                _chart.RemoveObject(key + "_TXT");
                return;
            }

            Color color = gap.Type == OpenGapType.NWOG ? NwogColor : NdogColor;
            string label = $"{gap.Type} #{gap.Id}";

            _chart.DrawTrendLine(
                key,
                gap.BarIndex,
                gap.MidPrice,
                _chart.LastVisibleBarIndex + 5,
                gap.MidPrice,
                color,
                2,
                LineStyle.LinesDots
            );

            _chart.DrawText(key + "_TXT", label, gap.BarIndex, gap.MidPrice, color);
        }

        public void DrawStructure(StructureEvent evt, bool showVisual)
        {
            if (_chart == null || evt?.BrokenPivot == null) return;
            string key = $"SMC_STRUCT_{evt.BrokenPivot.Time.Ticks}";

            if (!showVisual)
            {
                _chart.RemoveObject(key);
                _chart.RemoveObject(key + "_TXT");
                return;
            }

            Color color;
            if (evt.Type == BreakType.MSS)
            {
                color = evt.Direction == TradeType.Buy ? BullishMssColor : BearishMssColor;
            }
            else if (evt.Type == BreakType.ChoCH)
            {
                color = evt.Direction == TradeType.Buy ? BullishChochColor : BearishChochColor;
            }
            else // BOS
            {
                color = evt.Direction == TradeType.Buy ? BullishBosColor : BearishBosColor;
            }

            string label = $"{evt.Type} ({evt.Direction})";

            _chart.DrawTrendLine(
                key,
                evt.BrokenPivot.Index,
                evt.BrokenPivot.Price,
                evt.TriggerBarIndex,
                evt.BrokenPivot.Price,
                color,
                2,
                LineStyle.LinesDots
            );

            _chart.DrawText(key + "_TXT", label, evt.TriggerBarIndex, evt.BrokenPivot.Price, color);
        }

        public void DrawOrderBlock(OrderBlock ob, bool showVisual, bool autoClean = true)
        {
            if (_chart == null) return;
            string key = $"SMC_OB_{ob.Id}";

            if (!showVisual || (autoClean && ob.IsMitigated))
            {
                _chart.RemoveObject(key);
                _chart.RemoveObject(key + "_TXT");
                return;
            }

            Color color = ob.Direction == TradeType.Buy ? BullishObColor : BearishObColor;
            Color textCol = ob.Direction == TradeType.Buy ? Color.DodgerBlue : Color.MediumOrchid;

            var rect = _chart.DrawRectangle(
                key,
                ob.BarIndex,
                ob.TopPrice,
                _chart.LastVisibleBarIndex + 5,
                ob.BottomPrice,
                color
            );
            rect.IsFilled = true;

            string label = ob.Direction == TradeType.Buy ? $"OB (Buy) #{ob.Id}" : $"OB (Sell) #{ob.Id}";
            _chart.DrawText(key + "_TXT", label, ob.BarIndex, ob.TopPrice, textCol);
        }
    }
}
