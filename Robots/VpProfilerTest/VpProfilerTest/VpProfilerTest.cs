using System;
using cAlgo.API;
using cAlgo.API.Internals;
using RedWave.Common;

namespace cTrader.Bots
{
    [Robot(AccessRights = AccessRights.None)]
    public class VpProfilerTest : Robot
    {
        [Parameter("Profiler Window (ms)", DefaultValue = 10000, MinValue = 1000)]
        public int ProfilerWindowMs { get; set; }

        [Parameter("Query Window (ms)", DefaultValue = 10000, MinValue = 1000)]
        public int QueryWindowMs { get; set; }

        [Parameter("VP Lookback (bars)", Group = "Volume Profile", DefaultValue = 100, MinValue = 10)]
        public int VpLookbackBars { get; set; }

        [Parameter("VP Precision", Group = "Volume Profile", DefaultValue = 100, MinValue = 10)]
        public int VpPrecision { get; set; }

        private CTickVolumeProfiler _profiler;
        private CVolumeProfile _volumeProfile;
        private CLogger _logger;
        private DateTime _lastLogTime;

        // UI Controls
        private TextBlock _ticksText;
        private TextBlock _avgTicksText;
        private TextBlock _deltaText;
        private TextBlock _spreadText;
        private TextBlock _pocText;
        private TextBlock _vaText;

        protected override void OnStart()
        {
            _logger = new CLogger();
            _logger.Init("VpProfilerTest", LogLevel.Info, Print);

            _profiler = new CTickVolumeProfiler();
            _profiler.Init(Symbol.Name, ProfilerWindowMs, _logger);

            _volumeProfile = new CVolumeProfile();
            _volumeProfile.Init(Bars, Chart, VpPrecision, true, _logger);

            _logger.Info($"Profiler initialized with window {ProfilerWindowMs}ms");
            _lastLogTime = DateTime.MinValue;

            BuildChartUI();
            
            // Calculate initial volume profile
            OnBar();
        }

        protected override void OnTick()
        {
            DateTime tickTime = Server.TimeInUtc;
            _profiler.OnTick(Symbol.Bid, Symbol.Ask, tickTime);

            int ticks = _profiler.GetTicksInWindow(QueryWindowMs);
            double avgTicks = _profiler.GetAverageTicksPerWindow(QueryWindowMs);
            double priceDelta = _profiler.GetPriceDelta(QueryWindowMs);
            double deltaPips = PriceUtils.PriceToPips(priceDelta, Symbol);

            UpdateChartUI(ticks, avgTicks, deltaPips);

            // Log every 5 seconds to avoid flooding the log
            if ((tickTime - _lastLogTime).TotalSeconds >= 5.0)
            {
                _logger.Info($"Stats (last {QueryWindowMs}ms) -> Ticks: {ticks}, Avg Ticks: {avgTicks:F1}, Price Delta: {deltaPips:F1} pips");
                _lastLogTime = tickTime;
            }
        }

        protected override void OnBar()
        {
            _volumeProfile.OnBar(VpLookbackBars);
            
            if (_pocText != null)
            {
                _pocText.Text = $"POC: {_volumeProfile.POC:F5}";
                _vaText.Text = $"VAH: {_volumeProfile.VAH:F5} | VAL: {_volumeProfile.VAL:F5}";
            }
        }

        protected override void OnStop()
        {
            _profiler.Deinit();
            _volumeProfile.ClearVisuals();
            _logger.Info("Profiler stopped");
        }

        private void BuildChartUI()
        {
            // StackPanel container (Glassmorphic dark design)
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                BackgroundColor = Color.FromHex("#121214"),
                Opacity = 0.92,
                Width = 240,
                Margin = new Thickness(0, 50, 10, 0)
            };

            // Border decoration
            var border = new Border
            {
                BorderColor = Color.FromHex("#2B2B30"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Child = panel
            };

            // Header Title
            panel.AddChild(new TextBlock
            {
                Text = "RedWave Tick Profiler HUD",
                ForegroundColor = Color.White,
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            // Separator Line
            panel.AddChild(new Border
            {
                Height = 1,
                BackgroundColor = Color.FromHex("#2B2B30"),
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Ticks Display
            _ticksText = new TextBlock
            {
                Text = "Ticks in Window: 0",
                ForegroundColor = Color.FromHex("#3498DB"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.AddChild(_ticksText);

            // Avg Ticks Display
            _avgTicksText = new TextBlock
            {
                Text = "Avg Ticks: 0.00",
                ForegroundColor = Color.FromHex("#2ECC71"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.AddChild(_avgTicksText);

            // Price Delta Display
            _deltaText = new TextBlock
            {
                Text = "Price Delta: 0.0 pips",
                ForegroundColor = Color.FromHex("#E67E22"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.AddChild(_deltaText);

            // Real-time Spread Display
            _spreadText = new TextBlock
            {
                Text = "Spread: 0.0 pips",
                ForegroundColor = Color.LightGray,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.AddChild(_spreadText);

            // Volume Profile POC Display
            _pocText = new TextBlock
            {
                Text = "POC: 0.00000",
                ForegroundColor = Color.Orange,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.AddChild(_pocText);
            
            // Volume Profile VA Display
            _vaText = new TextBlock
            {
                Text = "VAH: 0.00000 | VAL: 0.00000",
                ForegroundColor = Color.DarkCyan,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.AddChild(_vaText);

            Chart.AddControl(border);
        }

        private void UpdateChartUI(int ticks, double avgTicks, double deltaPips)
        {
            if (_ticksText == null) return;

            _ticksText.Text = $"Ticks (last {QueryWindowMs / 1000}s): {ticks}";
            _avgTicksText.Text = $"Avg Ticks: {avgTicks:F2}";
            _deltaText.Text = $"Price Delta: {deltaPips:F1} pips";
            double currentSpreadPips = PriceUtils.PriceToPips(Symbol.Spread, Symbol);
            _spreadText.Text = $"Current Spread: {currentSpreadPips:F1} pips";
        }
    }
}
