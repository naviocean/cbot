using System;
using System.Collections.Generic;
using cAlgo.API;

namespace RedWave.Common.Smc
{
    /// <summary>
    /// Handles visual rendering of SMC/ICT elements on the cTrader Chart Canvas.
    /// Positions labels dynamically at the start of zones so they are always visible right on the pattern.
    /// </summary>
    public class SmcChartRenderer
    {
        private readonly Chart _chart;
        private readonly HashSet<string> _renderedKeys = new HashSet<string>();
        private readonly HashSet<string> _currentFrameKeys = new HashSet<string>();

        // Standard FVG Colors: Cyan (Bullish) vs HotPink (Bearish)
        public Color BullishFvgColor { get; set; } = Color.FromArgb(60, 0, 238, 255);   // Semi-transparent Cyan
        public Color BearishFvgColor { get; set; } = Color.FromArgb(60, 255, 20, 147);  // Semi-transparent HotPink
        
        // Inversion FVG (iFVG) Colors: LimeGreen (Bullish iFVG) vs Crimson (Bearish iFVG)
        public Color BullishIfvgColor { get; set; } = Color.FromArgb(60, 50, 205, 50);   // Semi-transparent LimeGreen
        public Color BearishIfvgColor { get; set; } = Color.FromArgb(60, 220, 20, 60);   // Semi-transparent Crimson

        // OB Colors: Royal Blue (Bullish) vs Dark Purple (Bearish)
        public Color BullishObColor { get; set; } = Color.FromArgb(70, 30, 144, 255);   // Semi-transparent Royal Blue
        public Color BearishObColor { get; set; } = Color.FromArgb(70, 153, 50, 204);  // Semi-transparent Dark Purple

        // Breaker Block Colors: Teal (Bullish Breaker) vs OrangeRed (Bearish Breaker)
        public Color BullishBreakerColor { get; set; } = Color.FromArgb(70, 0, 201, 167);  // Semi-transparent Teal
        public Color BearishBreakerColor { get; set; } = Color.FromArgb(70, 255, 69, 0);   // Semi-transparent OrangeRed

        // ICT Unicorn Setup Colors: Gold (Bullish Unicorn) vs HotPink/Violet (Bearish Unicorn)
        public Color BullishUnicornColor { get; set; } = Color.FromArgb(90, 255, 215, 0);  // High-contrast Gold
        public Color BearishUnicornColor { get; set; } = Color.FromArgb(90, 255, 105, 180); // High-contrast HotPink

        // Open Gap Colors (NWOG / NDOG)
        public Color NwogColor { get; set; } = Color.Aqua;
        public Color NdogColor { get; set; } = Color.SpringGreen;

        // Structure Lines Colors
        public Color BullishBosColor { get; set; } = Color.LimeGreen;
        public Color BearishBosColor { get; set; } = Color.Crimson;
        public Color BullishChochColor { get; set; } = Color.Gold;
        public Color BearishChochColor { get; set; } = Color.DarkOrange;

        // MSS Colors
        public Color BullishMssColor { get; set; } = Color.Yellow;
        public Color BearishMssColor { get; set; } = Color.Magenta;

        public SmcChartRenderer(Chart chart)
        {
            _chart = chart;
        }

        public void BeginFrame()
        {
            _currentFrameKeys.Clear();
        }

        public void EndFrame()
        {
            if (_chart == null) return;

            var staleKeys = new List<string>();
            foreach (var key in _renderedKeys)
            {
                if (!_currentFrameKeys.Contains(key))
                {
                    staleKeys.Add(key);
                }
            }

            foreach (var staleKey in staleKeys)
            {
                _chart.RemoveObject(staleKey);
                _renderedKeys.Remove(staleKey);
            }
        }

        private void TrackKey(string key)
        {
            _currentFrameKeys.Add(key);
            _renderedKeys.Add(key);
        }

        public void DrawFvg(FairValueGap fvg, bool showVisual, bool showIfvgVisual = true, bool autoClean = true)
        {
            if (_chart == null || fvg == null) return;
            string key = $"SMC_FVG_{fvg.CreatedBarIndex}_{(int)fvg.Direction}";

            bool isIfvg = fvg.IsInversion || fvg.Status == FvgStatus.Inversion;

            if (!showVisual || (isIfvg && !showIfvgVisual) || (autoClean && (fvg.Status == FvgStatus.Mitigated || fvg.Status == FvgStatus.Invalidated)))
            {
                return;
            }

            TrackKey(key);
            TrackKey(key + "_TXT");

            Color color;
            Color textCol;
            string label;

            if (isIfvg)
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

        public void DrawUnicorn(UnicornSetup unicorn, bool showVisual)
        {
            if (_chart == null || unicorn == null) return;
            string key = $"SMC_UNICORN_{unicorn.BreakerBlock.BarIndex}_{unicorn.Fvg.CreatedBarIndex}";

            if (!showVisual || unicorn.BreakerBlock.IsMitigated || unicorn.Fvg.Status == FvgStatus.Mitigated)
            {
                return;
            }

            TrackKey(key);
            TrackKey(key + "_TXT");

            Color color = unicorn.Direction == TradeType.Buy ? BullishUnicornColor : BearishUnicornColor;
            Color textCol = unicorn.Direction == TradeType.Buy ? Color.Yellow : Color.Magenta;

            int startBar = Math.Min(unicorn.BreakerBlock.BarIndex, unicorn.Fvg.CreatedBarIndex);

            var rect = _chart.DrawRectangle(
                key,
                startBar,
                unicorn.OverlapTopPrice,
                _chart.LastVisibleBarIndex + 5,
                unicorn.OverlapBottomPrice,
                color
            );
            rect.IsFilled = true;

            string label = unicorn.Direction == TradeType.Buy ? $"🦄 Unicorn (Buy) #{unicorn.Id}" : $"🦄 Unicorn (Sell) #{unicorn.Id}";
            
            // Draw text label at startBar at the bottom price for Buy (or top price for Sell) so it never collides with OB label
            double textY = unicorn.Direction == TradeType.Buy ? unicorn.OverlapBottomPrice : unicorn.OverlapTopPrice;
            _chart.DrawText(key + "_TXT", label, startBar, textY, textCol);
        }

        public void DrawOpenGap(OpenGapLevel gap, bool showVisual)
        {
            if (_chart == null || gap == null) return;
            string key = $"SMC_GAP_{gap.BarIndex}_{(int)gap.Type}";

            if (!showVisual || gap.IsFilled)
            {
                return;
            }

            TrackKey(key);
            TrackKey(key + "_TXT");

            Color color = gap.Type == OpenGapType.NWOG ? NwogColor : NdogColor;
            string label = $"{gap.Type} #{gap.Id}";

            var gapRect = _chart.DrawRectangle(
                key,
                gap.BarIndex,
                gap.TopPrice,
                _chart.LastVisibleBarIndex + 5,
                gap.BottomPrice,
                color
            );
            gapRect.IsFilled = true;

            _chart.DrawText(key + "_TXT", label, gap.BarIndex, gap.TopPrice, color);
        }

        public void DrawStructure(StructureEvent evt, bool showVisual)
        {
            if (_chart == null || evt?.BrokenPivot == null) return;
            string key = $"SMC_STRUCT_{evt.BrokenPivot.Time.Ticks}";

            if (!showVisual)
            {
                return;
            }

            TrackKey(key);
            TrackKey(key + "_TXT");

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
            if (_chart == null || ob == null) return;
            string key = $"SMC_OB_{ob.Id}";

            if (!showVisual || (autoClean && ob.IsMitigated))
            {
                return;
            }

            TrackKey(key);
            TrackKey(key + "_TXT");

            Color color = ob.Type == ObType.BreakerBlock
                ? (ob.Direction == TradeType.Buy ? BullishBreakerColor : BearishBreakerColor)
                : (ob.Direction == TradeType.Buy ? BullishObColor : BearishObColor);

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

            string obTypeLabel = ob.Type == ObType.BreakerBlock ? "Breaker" : "OB";
            string label = $"{obTypeLabel} ({ob.Direction}) #{ob.Id}";
            _chart.DrawText(key + "_TXT", label, ob.BarIndex, ob.TopPrice, textCol);
        }
    }
}
