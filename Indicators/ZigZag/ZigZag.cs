/*  CTRADER GURU ZigZag — uses ZigZagCore (shared with ZigZagPocPullback cBot). */
using System;
using cAlgo.API;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class ZigZag : Indicator
    {
        public enum ModeZigZag
        {
            HighLow,
            OpenClose
        }

        public const string NAME = "ZigZag";
        public const string VERSION = "1.0.7";

        [Parameter(NAME + " " + VERSION, Group = "Identity", DefaultValue = "https://ctrader.guru/")]
        public string ProductInfo { get; set; }

        [Parameter("Mode", DefaultValue = ModeZigZag.HighLow, Group = "Params")]
        public ModeZigZag MyModeZigZag { get; set; }

        [Parameter(DefaultValue = 12, Group = "Params")]
        public int Depth { get; set; }

        [Parameter(DefaultValue = 5, Group = "Params")]
        public int Deviation { get; set; }

        [Parameter(DefaultValue = 3, Group = "Params")]
        public int BackStep { get; set; }

        [Parameter("Show", DefaultValue = true, Group = "Label")]
        public bool ShowLabel { get; set; }

        [Parameter("Color High", DefaultValue = "DodgerBlue", Group = "Label")]
        public Color ColorHigh { get; set; }

        [Parameter("Color Low", DefaultValue = "Red", Group = "Label")]
        public Color ColorLow { get; set; }

        [Output("ZigZag", LineColor = "DodgerBlue", LineStyle = LineStyle.Lines)]
        public IndicatorDataSeries Result { get; set; }

        private double _point;
        private int _lastPaintedCount = -1;

        protected override void Initialize()
        {
            Print("{0} : {1} (ZigZagCore shared)", NAME, VERSION);
            _point = Symbol.TickSize;
        }

        public override void Calculate(int index)
        {
            // OpenClose kept for compatibility — HighLow is the shared core path
            if (MyModeZigZag == ModeZigZag.OpenClose)
            {
                PerformOpenCloseLegacy(index);
                return;
            }

            // Rebuild full series from core on last bar (and first pass) so Result == bot
            if (index < Bars.Count - 1 && _lastPaintedCount == Bars.Count)
                return;

            int n = Bars.Count;
            var high = new double[n];
            var low = new double[n];
            for (int i = 0; i < n; i++)
            {
                high[i] = Bars.HighPrices[i];
                low[i] = Bars.LowPrices[i];
            }

            double[] zz = ZigZagCore.ComputeHighLow(high, low, Depth, Deviation, BackStep, _point);
            for (int i = 0; i < n && i < zz.Length; i++)
            {
                double v = zz[i];
                Result[i] = double.IsNaN(v) ? double.NaN : v;
            }

            _lastPaintedCount = Bars.Count;

            if (ShowLabel)
                PaintLabels(zz, n);
        }

        private void PaintLabels(double[] zz, int n)
        {
            int hi = 0, lo = 0;
            for (int i = 0; i < n && i < zz.Length; i++)
            {
                double v = zz[i];
                if (double.IsNaN(v) || Math.Abs(v) < 1e-12)
                    continue;

                bool isHigh = Math.Abs(Bars.HighPrices[i] - v) <= Math.Abs(Bars.LowPrices[i] - v);
                if (isHigh)
                {
                    hi++;
                    var cth = Chart.DrawText("zzh-" + hi, v.ToString("N" + Symbol.Digits), i, v, ColorHigh);
                    cth.HorizontalAlignment = HorizontalAlignment.Center;
                    cth.VerticalAlignment = VerticalAlignment.Top;
                }
                else
                {
                    lo++;
                    var ctl = Chart.DrawText("zzl-" + lo, v.ToString("N" + Symbol.Digits), i, v, ColorLow);
                    ctl.HorizontalAlignment = HorizontalAlignment.Center;
                    ctl.VerticalAlignment = VerticalAlignment.Bottom;
                }
            }
        }

        /// <summary>Legacy OpenClose path (not used by cBot).</summary>
        private void PerformOpenCloseLegacy(int index)
        {
            // Minimal stub: no shared core for OpenClose; leave empty / zero
            if (index < Depth)
                Result[index] = 0;
        }
    }
}
