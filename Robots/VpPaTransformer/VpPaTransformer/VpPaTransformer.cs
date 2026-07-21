using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class VpPaTransformer : Robot
    {
        // ─── Parameters ────────────────────────────────────────
        [Parameter("Volume (units)", Group = "Trading", DefaultValue = 10000)]
        public int VolumeInUnits { get; set; }

        [Parameter("R-multiple (TP=SL=R*ATR)", Group = "Trading", DefaultValue = 2.0)]
        public double RRMultiple { get; set; }

        [Parameter("ATR Period", Group = "Trading", DefaultValue = 14)]
        public int ATRPeriod { get; set; }

        [Parameter("Model Threshold", Group = "Model", DefaultValue = 0.0)]
        public double Threshold { get; set; }

        [Parameter("Short Score Offset", Group = "Model", DefaultValue = -0.16)]
        public double ShortOffset { get; set; }

        [Parameter("Model Weights Path", Group = "Model", DefaultValue = "")]
        public string ModelFilePath { get; set; }

        [Parameter("Consecutive Losses Pause", Group = "Protection", DefaultValue = 1)]
        public int ConsecutiveLossesPause { get; set; }

        [Parameter("Pause Bars", Group = "Protection", DefaultValue = 8)]
        public int PauseBars { get; set; }

        // ─── Constants ─────────────────────────────────────────
        private const int N_PRICE_BINS = 24;
        private const int T_WINDOW = 96;
        private const int MAX_HOLD = 30;
        private const int ATR_PERIOD = 14;

        // ─── State ─────────────────────────────────────────────
        private double[] _inputBuffer = new double[2361];
        private int _consecutiveLosses;
        private int _pauseBarsLeft;
        private string _positionLabel = "VpPa";
        private ModelWeights _weights;
        private bool _weightsLoaded;
        private int _barCount;

        // ─── Rolling Window Buffers ────────────────────────────
        private List<double> _opens = new List<double>(T_WINDOW + 10);
        private List<double> _highs = new List<double>(T_WINDOW + 10);
        private List<double> _lows = new List<double>(T_WINDOW + 10);
        private List<double> _closes = new List<double>(T_WINDOW + 10);
        private List<long> _volumes = new List<long>(T_WINDOW + 10);

        // ─── Hardcoded test data ──────────────────────────────────
        private const int T_WARM = 96;
        private const int T_TEST = 20;
        private static double[] WARM_O = new double[] {
            4671.51,
            4669.24,
            4674.64,
            4684.61,
            4680.78,
            4676.19,
            4682.51,
            4691.02,
            4696.41,
            4703.25,
            4721.85,
            4706.50,
            4710.97,
            4708.04,
            4708.24,
            4687.85,
            4692.72,
            4693.92,
            4687.32,
            4681.70,
            4684.55,
            4689.48,
            4687.23,
            4686.98,
            4681.96,
            4673.89,
            4672.42,
            4676.88,
            4676.45,
            4682.86,
            4686.22,
            4702.26,
            4684.29,
            4697.67,
            4709.36,
            4708.70,
            4730.60,
            4715.33,
            4723.13,
            4730.70,
            4713.07,
            4719.17,
            4730.77,
            4732.04,
            4728.63,
            4718.25,
            4719.52,
            4719.26,
            4723.24,
            4715.80,
            4728.48,
            4734.89,
            4731.38,
            4736.90,
            4745.18,
            4760.48,
            4752.53,
            4743.65,
            4735.97,
            4748.12,
            4724.89,
            4731.72,
            4736.19,
            4743.49,
            4738.31,
            4744.52,
            4742.20,
            4754.02,
            4760.76,
            4764.78,
            4773.00,
            4785.55,
            4786.54,
            4774.93,
            4776.52,
            4784.24,
            4787.34,
            4788.54,
            4785.15,
            4782.04,
            4774.43,
            4771.18,
            4754.03,
            4756.81,
            4753.16,
            4759.75,
            4765.59,
            4769.52,
            4766.14,
            4768.84,
            4762.16,
            4760.02,
            4757.33,
            4759.10,
            4772.88,
            4776.38,
        };
        private static double[] WARM_H = new double[] {
            4682.15,
            4686.62,
            4695.88,
            4686.88,
            4682.58,
            4684.51,
            4695.14,
            4699.89,
            4704.91,
            4721.97,
            4722.53,
            4719.12,
            4724.21,
            4708.43,
            4708.40,
            4697.10,
            4700.59,
            4697.71,
            4690.46,
            4689.75,
            4692.75,
            4694.29,
            4688.80,
            4688.72,
            4682.09,
            4674.04,
            4677.04,
            4681.63,
            4684.87,
            4690.00,
            4703.50,
            4703.44,
            4697.54,
            4711.49,
            4712.79,
            4733.19,
            4732.62,
            4725.85,
            4747.74,
            4732.87,
            4726.42,
            4735.81,
            4740.37,
            4732.54,
            4730.23,
            4723.97,
            4722.93,
            4727.83,
            4724.51,
            4729.35,
            4736.20,
            4738.80,
            4740.42,
            4746.31,
            4762.93,
            4761.50,
            4753.74,
            4745.53,
            4752.93,
            4753.76,
            4733.63,
            4742.30,
            4749.58,
            4748.64,
            4746.97,
            4754.89,
            4756.49,
            4768.75,
            4774.89,
            4776.43,
            4786.78,
            4788.19,
            4790.75,
            4783.43,
            4784.86,
            4788.63,
            4792.96,
            4789.76,
            4786.12,
            4783.33,
            4775.29,
            4771.64,
            4766.12,
            4758.54,
            4765.32,
            4769.24,
            4778.22,
            4771.49,
            4769.76,
            4771.24,
            4763.70,
            4763.59,
            4761.18,
            4775.03,
            4781.65,
            4782.83,
        };
        private static double[] WARM_L = new double[] {
            4663.07,
            4663.15,
            4671.09,
            4661.78,
            4672.84,
            4672.14,
            4681.93,
            4688.55,
            4693.54,
            4702.54,
            4700.14,
            4704.57,
            4689.67,
            4692.60,
            4681.50,
            4686.37,
            4686.95,
            4683.50,
            4678.16,
            4678.49,
            4682.77,
            4684.43,
            4683.33,
            4679.71,
            4673.12,
            4667.10,
            4669.13,
            4672.89,
            4674.92,
            4680.53,
            4686.22,
            4683.95,
            4682.80,
            4692.71,
            4702.31,
            4708.70,
            4714.34,
            4713.13,
            4722.23,
            4709.72,
            4710.51,
            4717.15,
            4726.32,
            4726.50,
            4717.96,
            4713.99,
            4713.16,
            4719.24,
            4715.61,
            4715.36,
            4726.20,
            4728.12,
            4728.24,
            4736.48,
            4744.87,
            4752.54,
            4739.46,
            4724.58,
            4732.18,
            4724.39,
            4716.31,
            4730.83,
            4730.84,
            4733.91,
            4724.17,
            4730.61,
            4734.99,
            4752.69,
            4758.53,
            4762.49,
            4769.94,
            4782.46,
            4774.34,
            4774.62,
            4775.24,
            4781.85,
            4786.68,
            4782.48,
            4775.85,
            4773.89,
            4759.57,
            4753.33,
            4742.25,
            4751.62,
            4751.23,
            4758.70,
            4763.17,
            4758.24,
            4762.74,
            4761.05,
            4754.41,
            4756.68,
            4752.15,
            4758.90,
            4770.81,
            4775.15,
        };
        private static double[] WARM_C = new double[] {
            4668.91,
            4674.64,
            4684.62,
            4680.78,
            4676.19,
            4682.51,
            4691.02,
            4696.46,
            4703.24,
            4721.84,
            4706.54,
            4710.96,
            4708.20,
            4708.24,
            4687.80,
            4692.77,
            4694.04,
            4687.72,
            4681.70,
            4684.62,
            4689.48,
            4687.27,
            4686.99,
            4681.96,
            4673.88,
            4672.44,
            4676.85,
            4676.52,
            4682.86,
            4686.22,
            4702.16,
            4684.26,
            4697.54,
            4709.38,
            4708.61,
            4730.60,
            4715.36,
            4723.14,
            4730.70,
            4713.07,
            4719.14,
            4730.76,
            4732.04,
            4728.66,
            4718.25,
            4719.53,
            4719.21,
            4723.21,
            4715.75,
            4728.45,
            4734.88,
            4731.39,
            4736.90,
            4745.18,
            4760.50,
            4752.54,
            4743.65,
            4736.56,
            4748.07,
            4724.82,
            4731.72,
            4736.12,
            4743.46,
            4738.47,
            4744.52,
            4742.26,
            4754.02,
            4760.78,
            4764.80,
            4773.00,
            4785.56,
            4786.53,
            4774.99,
            4776.61,
            4784.24,
            4787.34,
            4788.53,
            4785.17,
            4782.04,
            4774.43,
            4771.18,
            4754.03,
            4756.61,
            4753.11,
            4759.75,
            4765.59,
            4769.50,
            4766.15,
            4768.82,
            4762.19,
            4760.03,
            4756.80,
            4759.11,
            4772.88,
            4776.39,
            4776.48,
        };
        private static double[] WARM_V = new double[] {
            4858,
            5273,
            5870,
            6675,
            4698,
            4866,
            5355,
            5799,
            7705,
            8066,
            6797,
            6804,
            10178,
            8841,
            8702,
            8050,
            7954,
            5351,
            7067,
            6478,
            5912,
            5342,
            4274,
            4406,
            3895,
            4903,
            4299,
            3785,
            4211,
            5475,
            7088,
            6463,
            7128,
            7221,
            7517,
            8498,
            8168,
            6706,
            8074,
            8996,
            8085,
            6902,
            6948,
            6063,
            6259,
            6169,
            6024,
            6140,
            5647,
            5505,
            5191,
            5726,
            5718,
            5787,
            7061,
            6752,
            6670,
            7886,
            7867,
            10619,
            9817,
            8491,
            10871,
            10396,
            10936,
            10236,
            10205,
            9580,
            9214,
            8447,
            8873,
            7790,
            8440,
            7837,
            6981,
            5483,
            6873,
            7278,
            6821,
            6173,
            8970,
            9546,
            10021,
            8867,
            8412,
            7044,
            6908,
            8705,
            4403,
            3649,
            4216,
            2774,
            2396,
            3788,
            4873,
            4007,
        };
        private static double[] TEST_O = new double[] {
            4776.48,
            4791.26,
            4794.97,
            4789.76,
            4784.12,
            4792.59,
            4789.76,
            4787.58,
            4784.61,
            4750.88,
            4697.06,
            4690.02,
            4696.77,
            4689.07,
            4689.93,
            4676.04,
            4686.94,
            4690.06,
            4691.54,
            4685.77,
        };
        private static double[] TEST_H = new double[] {
            4791.56,
            4798.95,
            4800.38,
            4789.95,
            4793.49,
            4798.71,
            4796.11,
            4791.87,
            4786.52,
            4752.82,
            4708.39,
            4711.87,
            4704.27,
            4703.13,
            4691.16,
            4689.44,
            4691.65,
            4699.37,
            4692.68,
            4687.54,
        };
        private static double[] TEST_L = new double[] {
            4773.68,
            4788.67,
            4788.45,
            4778.24,
            4775.24,
            4788.47,
            4784.57,
            4783.72,
            4724.60,
            4687.11,
            4650.69,
            4685.15,
            4678.72,
            4688.40,
            4655.49,
            4673.17,
            4677.87,
            4688.09,
            4683.49,
            4675.75,
        };
        private static double[] TEST_C = new double[] {
            4791.25,
            4794.95,
            4789.75,
            4784.15,
            4792.59,
            4789.79,
            4787.59,
            4784.62,
            4750.84,
            4696.99,
            4690.01,
            4696.76,
            4689.06,
            4689.92,
            4676.04,
            4686.94,
            4690.05,
            4691.57,
            4685.77,
            4676.25,
        };
        private static double[] TEST_V = new double[] {
            4879,
            5323,
            4711,
            5376,
            7575,
            6398,
            6633,
            6923,
            11816,
            12351,
            11919,
            10850,
            10716,
            8116,
            10649,
            8974,
            8113,
            7537,
            6385,
            5193,
        };

        private void RunTestMode()
        {
            // Load warmup bars into rolling buffers + aggregate H1
            for (int i = 0; i < T_WARM; i++)
            {
                _opens.Add(WARM_O[i]);
                _highs.Add(WARM_H[i]);
                _lows.Add(WARM_L[i]);
                _closes.Add(WARM_C[i]);
                _volumes.Add((long)WARM_V[i]);
                UpdateH1Test(i + 1);
            }
            Print("Warmup loaded: " + _closes.Count + " bars, H1: " + _h1Bars.Count + " bars");

            // Process test bars
            for (int i = 0; i < T_TEST; i++)
            {
                _opens.Add(TEST_O[i]);
                _highs.Add(TEST_H[i]);
                _lows.Add(TEST_L[i]);
                _closes.Add(TEST_C[i]);
                _volumes.Add((long)TEST_V[i]);
                UpdateH1Test(T_WARM + i + 1);

                ComputeAllFeatures();
                double longScore, shortScore;
                RunInference(out longScore, out shortScore);

                Print("BAR#" + i + " L=" + longScore.ToString("F6") + " S=" + shortScore.ToString("F6"));
                // Full feature dump for bar 0 only
                if (i == 0)
                {
                    Print("  --- BAR 0 FULL FEATURES ---");
                    // Grid first 20
                    var sb = new System.Text.StringBuilder("  GRID[0..19]:");
                    for (int g = 0; g < 20; g++)
                        sb.AppendFormat(" {0:F4}", _inputBuffer[g]);
                    Print(sb.ToString());
                    // Grid[20..39]
                    sb = new System.Text.StringBuilder("  GRID[20..39]:");
                    for (int g = 20; g < 40; g++)
                        sb.AppendFormat(" {0:F4}", _inputBuffer[g]);
                    Print(sb.ToString());
                    // Grid[2280..2303]
                    sb = new System.Text.StringBuilder("  GRID[2280..2303]:");
                    for (int g = 2280; g < 2304; g++)
                        sb.AppendFormat(" {0:F4}", _inputBuffer[g]);
                    Print(sb.ToString());

                    // OHLCV 20
                    sb = new System.Text.StringBuilder("  OHLCV[0..19]:");
                    for (int g = 2304; g < 2324; g++)
                        sb.AppendFormat(" {0:F4}", _inputBuffer[g]);
                    Print(sb.ToString());

                    // VP 9
                    sb = new System.Text.StringBuilder("  VP[0..8]:");
                    for (int g = 2324; g < 2333; g++)
                        sb.AppendFormat(" {0:F4}", _inputBuffer[g]);
                    Print(sb.ToString());

                    // PA 14
                    sb = new System.Text.StringBuilder("  PA[0..13]:");
                    for (int g = 2333; g < 2347; g++)
                        sb.AppendFormat(" {0:F4}", _inputBuffer[g]);
                    Print(sb.ToString());

                    // H1 14
                    sb = new System.Text.StringBuilder("  H1[0..13]:");
                    for (int g = 2347; g < 2361; g++)
                        sb.AppendFormat(" {0:F4}", _inputBuffer[g]);
                    Print(sb.ToString());
                    Print("  --- END FEATURES ---");
                }
            }
            Print("TEST MODE DONE");
        }

        // H1 aggregation for test mode (every 4 M15 bars = 1 H1, like resample("1h"))
        private void UpdateH1Test(int totalBars)
        {
            int i = totalBars - 1;
            if (_h1Bars.Count == 0)
            {
                _h1Bars.Add(new H1Bar { Open = _opens[i], High = _highs[i], Low = _lows[i],
                    Close = _closes[i], Volume = _volumes[i] });
                return;
            }
            var last = _h1Bars.Last();
            // New H1 every 4 M15 bars (bars 1,5,9,... start new hour)
            if (totalBars % 4 == 1)
            {
                _h1Bars.Add(new H1Bar { Open = _opens[i], High = _highs[i], Low = _lows[i],
                    Close = _closes[i], Volume = _volumes[i] });
            }
            else
            {
                last.High = Math.Max(last.High, _highs[i]);
                last.Low = Math.Min(last.Low, _lows[i]);
                last.Close = _closes[i];
                last.Volume += _volumes[i];
            }
            while (_h1Bars.Count > 48) _h1Bars.RemoveAt(0);
        }


        // Volume grid: rolling window of volume-per-price distributions

        // H1 data (from M15 aggregation)
        private List<H1Bar> _h1Bars = new List<H1Bar>();

        // ATR values
        private List<double> _atrValues = new List<double>();

        // ─── Find Model Weights Path ──────────────────────────
        private string ResolveModelPath()
        {
            string botName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;

            // 0. Explicit parameter override (highest priority)
            if (!string.IsNullOrEmpty(ModelFilePath))
                return ModelFilePath;

            // 1. Assembly.Location
            string loc = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(loc))
                return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(loc), "model_weights.bin");

            // 2. AppContext.BaseDirectory
            string baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir) && System.IO.Directory.Exists(baseDir))
                return System.IO.Path.Combine(baseDir, "model_weights.bin");

            // 3. Current directory
            try { return System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), "model_weights.bin"); }
            catch { }

            // 4. macOS: cTrader source code directory (where .cs file lives)
            try
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string srcDir = System.IO.Path.Combine(home, "cAlgo", "Sources", "Robots", botName, botName);
                if (System.IO.Directory.Exists(srcDir))
                    return System.IO.Path.Combine(srcDir, "model_weights.bin");
            }
            catch { }

            // 5. macOS: cTrader AlgoData compiled output
            try
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string algoData = System.IO.Path.Combine(home, "Library", "Application Support",
                    "cTrader", "AlgoData", "Robots", botName);
                if (System.IO.Directory.Exists(algoData))
                    return System.IO.Path.Combine(algoData, "model_weights.bin");
            }
            catch { }

            return null;
        }

        // ─── OnStart ───────────────────────────────────────────
        [Parameter("Test Mode", Group = "Test", DefaultValue = false)]
        public bool TestMode { get; set; }
        protected override void OnStart()
        {
            _weights = new ModelWeights();

            // Try embedded resource first (like MQL5 #resource)
            if (_weights.TryLoadFromResource())
            {
                _weightsLoaded = true;
                Print($"Model weights loaded from embedded resource ({_weights.TotalParams:N0} params)");
            }
            else
            {
                // Fallback: file path resolution
                string weightsPath = ResolveModelPath();
                if (weightsPath != null && System.IO.File.Exists(weightsPath))
                {
                    _weights.Load(weightsPath);
                    _weightsLoaded = true;
                    Print($"Model weights loaded from {weightsPath} ({_weights.TotalParams:N0} params)");
                }
                else
                {
                    string errMsg = weightsPath != null
                        ? $"model_weights.bin not found at {weightsPath}"
                        : "Cannot resolve cBot assembly path";
                    Print($"ERROR: {errMsg}. Model inference disabled.");
                    _weightsLoaded = false;
                }
            }

            Print($"VP+PA Transformer cBot started on {SymbolName} M15 | " +
                  $"RR={RRMultiple} ATR={ATRPeriod} thr={Threshold:F2}");

            // Subscribe to position events (override not available in this API version)
            Positions.Closed += OnPositionClosed;

            // Test mode: process hardcoded data and print
            if (TestMode)
            {
                RunTestMode();
                Print("Test mode done");
                Stop();
                return;
            }
        }

        // ─── OnBar ─────────────────────────────────────────────
        protected override void OnBar()
        {
            if (!_weightsLoaded) return;

            // 1. Collect latest bar data
            AddBar();

            // 2. Need minimum warmup
            if (_closes.Count < T_WINDOW + 10) return;

            // 3. Compute features
            ComputeAllFeatures();

            // 4. Run inference
            double longScore, shortScore;
            RunInference(out longScore, out shortScore);

            // 5. Trading logic
            ManagePositions(longScore, shortScore);

            // 6. Print first 20 bars for Python verification
            if (_barCount < 20)
            {
                Print($"BAR#{_barCount} [{Server.Time:yyyy-MM-dd HH:mm}] L={longScore:F6} S={shortScore:F6} " +
                      $"f0={_inputBuffer[0]:F6} f1={_inputBuffer[1]:F6} f2={_inputBuffer[2]:F6} " +
                      $"f2304={_inputBuffer[2304]:F6} f2305={_inputBuffer[2305]:F6}");
                _barCount++;
            }

            // 7. Log stats every 24h
            if (Server.Time.Hour == 0 && Server.Time.Minute == 0)
                Print($"State: consecLoss={_consecutiveLosses} pauseLeft={_pauseBarsLeft} " +
                      $"pos={Positions.Count}");
        }

        // ─── OnPositionClosed (event subscription — API ko có virtual method để override) ─
        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if (pos.Label != _positionLabel) return;

            if (pos.NetProfit <= 0)
            {
                _consecutiveLosses++;
                Print($"Loss #{_consecutiveLosses} — pausing {PauseBars} bars");
            }
            else
            {
                _consecutiveLosses = 0;
            }
        }

        // ─── Bar Data Collection ───────────────────────────────
        private void AddBar()
        {
            int n = Math.Min(Bars.Count, 5); // Last completed bar index offset

            // Ensure we don't add duplicate bars
            double lastClose = Bars.ClosePrices.Last(1);
            if (_closes.Count > 0 && Math.Abs(_closes.Last() - lastClose) < 0.0001)
                return; // Same bar already processed

            _opens.Add(Bars.OpenPrices.Last(1));
            _highs.Add(Bars.HighPrices.Last(1));
            _lows.Add(Bars.LowPrices.Last(1));
            _closes.Add(Bars.ClosePrices.Last(1));
            _volumes.Add((long)Bars.TickVolumes.Last(1));

            // Cap rolling buffers
            int maxKeep = T_WINDOW + 48;
            while (_closes.Count > maxKeep)
            {
                _opens.RemoveAt(0);
                _highs.RemoveAt(0);
                _lows.RemoveAt(0);
                _closes.RemoveAt(0);
                _volumes.RemoveAt(0);
            }

            // Update ATR
            UpdateATR();

            // Update volume grid

            // Update H1 bars (4 M15 bars = 1 H1)
            UpdateH1Bars();
        }

        // ─── ATR ───────────────────────────────────────────────
        private void UpdateATR()
        {
            if (_closes.Count < 2) return;
            int i = _closes.Count - 1;
            double tr = Math.Max(_highs[i] - _lows[i],
                       Math.Max(Math.Abs(_highs[i] - _closes[i - 1]),
                                Math.Abs(_lows[i] - _closes[i - 1])));
            _atrValues.Add(tr);
            while (_atrValues.Count > T_WINDOW + 48) _atrValues.RemoveAt(0);
        }

        private double GetATR(int lookback = ATR_PERIOD)
        {
            if (_atrValues.Count < lookback) return 0.001;
            double sum = 0;
            for (int i = _atrValues.Count - 1; i >= _atrValues.Count - lookback; i--)
                sum += _atrValues[i];
            return Math.Max(sum / lookback, 0.001);
        }


        private class H1Bar
        {
            public double Open, High, Low, Close;
            public long Volume;
            public DateTime Time;
        }

        private void UpdateH1Bars()
        {
            int i = _closes.Count - 1;
            if (_h1Bars.Count == 0)
            {
                _h1Bars.Add(new H1Bar
                {
                    Open = _opens[i], High = _highs[i], Low = _lows[i],
                    Close = _closes[i], Volume = _volumes[i],
                    Time = Server.Time
                });
                return;
            }

            var last = _h1Bars.Last();
            // Every 4 M15 bars → new H1 bar (approximately)
            if (_closes.Count >= 4 && i >= 3)
            {
                // Check if this closes a full hour (4 M15)
                var thisHour = new DateTime(Server.Time.Year, Server.Time.Month,
                    Server.Time.Day, Server.Time.Hour, 0, 0);
                if (last.Time.Hour != thisHour.Hour || last.Time.Day != thisHour.Day)
                {
                    // Close previous H1 and start new
                    _h1Bars.Add(new H1Bar
                    {
                        Open = _opens[i], High = _highs[i], Low = _lows[i],
                        Close = _closes[i], Volume = _volumes[i],
                        Time = thisHour
                    });
                }
                else
                {
                    // Update current H1
                    last.High = Math.Max(last.High, _highs[i]);
                    last.Low = Math.Min(last.Low, _lows[i]);
                    last.Close = _closes[i];
                    last.Volume += _volumes[i];
                }
            }

            while (_h1Bars.Count > 48) _h1Bars.RemoveAt(0);
        }

        // ─── Feature Computation (matching features.py _build_features) ─────
        private void ComputeAllFeatures()
        {
            int idx = 0;
            int n = _closes.Count;
            int ci = n - 1;
            double atr = Math.Max(GetATR(), 0.001);
            int tWin = T_WINDOW;
            int start = Math.Max(0, ci - tWin + 1);
            int lenW = ci - start + 1;
            int hourNow = Server.Time.Hour;
            int minNow = Server.Time.Minute;
            int dow = (int)Server.Time.DayOfWeek;

            // 1. Volume Grid [2304] — row-normalized rolling window, flattened row-major
            double[] gridFlat = GetGridWindowFlat();
            for (int f = 0; f < N_PRICE_BINS * T_WINDOW; f++)
                _inputBuffer[idx++] = gridFlat[f];

            // 2. OHLCV Stats [20] — matching features.py VPDataset._build_features lines 632-654
            double close = _closes[ci];

            // [0] close price
            _inputBuffer[idx++] = close;

            // [1-2] mean/std of consecutive diffs in T_WINDOW
            if (lenW > 1)
            {
                double sumDiff = 0, sumDiff2 = 0;
                for (int j = start + 1; j <= ci; j++)
                {
                    double d = _closes[j] - _closes[j - 1];
                    sumDiff += d;
                    sumDiff2 += d * d;
                }
                double m = sumDiff / (lenW - 1);
                double v = sumDiff2 / (lenW - 1) - m * m;
                _inputBuffer[idx++] = m;
                _inputBuffer[idx++] = v > 0 ? Math.Sqrt(v) : 0;
            }
            else { _inputBuffer[idx++] = 0; _inputBuffer[idx++] = 0; }

            // [3] close / mean(close_window) - 1
            double sumClose = 0;
            for (int j = start; j <= ci; j++) sumClose += _closes[j];
            double meanCw = sumClose / Math.Max(lenW, 1);
            _inputBuffer[idx++] = meanCw > 0 ? close / meanCw - 1 : 0;

            // [4] max(close_window) - min(close_window)
            double cMin = double.MaxValue, cMax = double.MinValue;
            for (int j = start; j <= ci; j++)
            {
                if (_closes[j] < cMin) cMin = _closes[j];
                if (_closes[j] > cMax) cMax = _closes[j];
            }
            _inputBuffer[idx++] = cMax - cMin;

            // [5] high - low (bar range)
            _inputBuffer[idx++] = _highs[ci] - _lows[ci];

            // [6] close - open (bar body)
            _inputBuffer[idx++] = close - _opens[ci];

            // [7] volume
            _inputBuffer[idx++] = (double)_volumes[ci];

            // [8] mean volume over last 20 bars
            double avgVol20 = 0;
            int vCount = 0;
            for (int j = Math.Max(0, ci - 20); j <= ci; j++) { avgVol20 += _volumes[j]; vCount++; }
            avgVol20 = avgVol20 / Math.Max(vCount, 1);
            _inputBuffer[idx++] = avgVol20;

            // [9] vol / max(avg_vol_20, 1)
            _inputBuffer[idx++] = _volumes[ci] / Math.Max(avgVol20, 1);

            // [10] mean(cw[-5:]) - mean(cw[:5]) — close window momentum
            double sum5last = 0, sum5first = 0;
            int c5 = 0;
            for (int j = Math.Max(start, ci - 4); j <= ci; j++) { sum5last += _closes[j]; c5++; }
            for (int j = start; j <= Math.Min(ci, start + 4); j++) sum5first += _closes[j];
            _inputBuffer[idx++] = lenW >= 10 ? (sum5last / 5 - sum5first / 5) : 0;

            // [11] (close - min(cw)) / max(ptp(cw), 1e-10)
            double ptpCw = Math.Max(cMax - cMin, 1e-10);
            _inputBuffer[idx++] = (close - cMin) / ptpCw;

            // [12] hour / 23.0
            _inputBuffer[idx++] = hourNow / 23.0;

            // [13] hour * 60 / 1439.0  (Python: .hour*60/1439 — no minute component)
            _inputBuffer[idx++] = (hourNow * 60) / 1439.0;

            // [14] sin(2*pi*hour/24)
            _inputBuffer[idx++] = Math.Sin(2 * Math.PI * hourNow / 24);

            // [15] cos(2*pi*hour/24)
            _inputBuffer[idx++] = Math.Cos(2 * Math.PI * hourNow / 24);

            // [16] dayofweek / 6.0
            _inputBuffer[idx++] = dow / 6.0;

            // [17] sin(2*pi*dow/7)
            _inputBuffer[idx++] = Math.Sin(2 * Math.PI * dow / 7);

            // [18] cos(2*pi*dow/7)
            _inputBuffer[idx++] = Math.Cos(2 * Math.PI * dow / 7);

            // [19] (close / close[ci-20] - 1) — 20-bar return
            _inputBuffer[idx++] = (ci >= 20 && _closes[ci - 20] > 0)
                ? close / _closes[ci - 20] - 1 : 0;

            // 3. VP Features [9] — matching features.py lines 658-668
            var vp = ComputeVPFeatures();
            _inputBuffer[idx++] = vp.VolumeConcentration;  // [0]
            _inputBuffer[idx++] = vp.VaWidth;              // [1]
            _inputBuffer[idx++] = vp.PocDominance;         // [2]
            _inputBuffer[idx++] = vp.VolSkew;              // [3]
            _inputBuffer[idx++] = vp.PriceInVa;            // [4]
            _inputBuffer[idx++] = vp.PocRelative;          // [5]
            _inputBuffer[idx++] = vp.VaWidth * vp.VolumeConcentration;  // [6]
            _inputBuffer[idx++] = Math.Abs(vp.PocRelative - vp.PriceInVa);  // [7]
            _inputBuffer[idx++] = Math.Abs(vp.HvnBinRel - vp.LvnBinRel);   // [8]

            // 4. PA Features [14] — matching features.py lines 672-682
            var pa = ComputePAFeatures();
            _inputBuffer[idx++] = pa.MktStruct / 2.0;          // [0]
            _inputBuffer[idx++] = pa.Bos ? 1 : 0;              // [1]
            _inputBuffer[idx++] = pa.Engulfing ? 1 : 0;        // [2]
            _inputBuffer[idx++] = pa.PinBar ? 1 : 0;           // [3]
            _inputBuffer[idx++] = pa.InsideBar ? 1 : 0;        // [4]
            _inputBuffer[idx++] = pa.BodyRatio;                // [5]
            _inputBuffer[idx++] = pa.UpperWick;                // [6]
            _inputBuffer[idx++] = pa.LowerWick;                // [7]
            _inputBuffer[idx++] = pa.SwingHigh ? 1 : 0;        // [8]
            _inputBuffer[idx++] = pa.SwingLow ? 1 : 0;         // [9]
            _inputBuffer[idx++] = double.IsNaN(pa.SwingHighDist) ? 0 : pa.SwingHighDist;  // [10]
            _inputBuffer[idx++] = double.IsNaN(pa.SwingLowDist) ? 0 : pa.SwingLowDist;    // [11]
            _inputBuffer[idx++] = double.IsNaN(pa.LiqHighDist) ? 0 : pa.LiqHighDist;       // [12]
            _inputBuffer[idx++] = double.IsNaN(pa.LiqLowDist) ? 0 : pa.LiqLowDist;         // [13]

            // 5. H1 Features [14] — matching features.py lines 555-561
            var hf = ComputeH1Features();
            for (int f = 0; f < hf.Length && idx < 2361; f++)
                _inputBuffer[idx++] = hf[f];

            // Pad
            while (idx < 2361) _inputBuffer[idx++] = 0;
        }

        // ─── Shared grid builder (matches Python _build_single_grid) ───
        private double[,] BuildRollingGrid()
        {
            int t = T_WINDOW, nb = N_PRICE_BINS;
            int n = _closes.Count;

            // 1. Rolling window high/low for each bar
            double[] winHigh = new double[n];
            double[] winLow = new double[n];
            for (int j = 0; j < n; j++)
            {
                int s = Math.Max(0, j - t + 1);
                double hh = double.MinValue, ll = double.MaxValue;
                for (int k = s; k <= j; k++)
                {
                    if (_highs[k] > hh) hh = _highs[k];
                    if (_lows[k] < ll) ll = _lows[k];
                }
                winHigh[j] = hh;
                winLow[j] = ll;
                if (winHigh[j] <= winLow[j] + 0.01) winHigh[j] = winLow[j] + 0.01;
            }

            // 2. Build SINGLE-BAR grid: each bar's volume distributed to bins
            double[,] singleGrid = new double[n, nb];
            for (int j = 0; j < n; j++)
            {
                long v = _volumes[j];
                if (v <= 0) continue;
                double lo = winLow[j], hi = winHigh[j];

                double bodyMid = (_opens[j] + _closes[j]) * 0.5;
                double bodyTop = Math.Max(_opens[j], _closes[j]);
                double bodyBot = Math.Min(_opens[j], _closes[j]);
                double upperWickMid = _highs[j] > bodyTop ? (_highs[j] + bodyTop) * 0.5 : bodyMid;
                double lowerWickMid = _lows[j] < bodyBot ? (bodyBot + _lows[j]) * 0.5 : bodyMid;

                int bodyBin = PriceToBin(bodyMid, lo, hi, nb);
                int upBin = PriceToBin(upperWickMid, lo, hi, nb);
                int lowBin = PriceToBin(lowerWickMid, lo, hi, nb);

                singleGrid[j, bodyBin] += v * 0.5;
                singleGrid[j, upBin] += v * 0.25;
                if (lowBin != bodyBin && lowBin != upBin)
                    singleGrid[j, lowBin] += v * 0.25;
                else
                    singleGrid[j, bodyBin] += v * 0.25;
            }

            // 3. Rolling sum: grid[j] = accumulate last T_WINDOW singleGrid rows
            //    Matches Python: pd.DataFrame(grid).rolling(t_window, min_periods=1).sum()
            double[,] rollingGrid = new double[n, nb];
            for (int j = 0; j < n; j++)
            {
                int rStart = Math.Max(0, j - t + 1);
                for (int k = rStart; k <= j; k++)
                    for (int b = 0; b < nb; b++)
                        rollingGrid[j, b] += singleGrid[k, b];
            }

            return rollingGrid;
        }

        // ─── Grid window extractor ──────────────────────────────
        private double[] GetGridWindowFlat()
        {
            int t = T_WINDOW, nb = N_PRICE_BINS;
            int n = _closes.Count;
            double[] result = new double[t * nb];
            double[,] rollingGrid = BuildRollingGrid();

            // Take last T_WINDOW rows of rolling-sum grid, normalize each row, flatten row-major
            int ci = n - 1;
            int startIdx = Math.Max(0, ci - t + 1);
            for (int row = 0; row < t; row++)
            {
                int ri = startIdx + row;
                if (ri > ci) break;
                double rowSum = 0;
                for (int b = 0; b < nb; b++) rowSum += rollingGrid[ri, b];
                rowSum = Math.Max(rowSum, 1e-10);
                for (int b = 0; b < nb; b++)
                    result[row * nb + b] = rollingGrid[ri, b] / rowSum;
            }

            return result;
        }

        private static int PriceToBin(double price, double lo, double hi, int nBins)
        {
            if (hi <= lo) return nBins / 2;
            int b = (int)((price - lo) / (hi - lo) * nBins);
            return Math.Max(0, Math.Min(nBins - 1, b));
        }


        // ─── VP Feature helpers ────────────────────────────
        private struct VpFeatures
        {
            public double VolumeConcentration;
            public double VaWidth;
            public double PocDominance;
            public double VolSkew;
            public double PriceInVa;
            public double PocRelative;
            public double HvnBinRel;
            public double LvnBinRel;
        }

        private VpFeatures ComputeVPFeatures()
        {
            int nb = N_PRICE_BINS;
            int ci = _closes.Count - 1;
            double[,] rollingGrid = BuildRollingGrid();

            // Current bar's rolling-sum grid row
            double[] currGrid = new double[nb];
            for (int b = 0; b < nb; b++) currGrid[b] = rollingGrid[ci, b];

            var result = new VpFeatures();

            double totalVol = 0;
            for (int b = 0; b < nb; b++) totalVol += currGrid[b];
            totalVol = Math.Max(totalVol, 1);

            // POC
            int pocBin = 0;
            double pocVal = 0;
            for (int b = 0; b < nb; b++)
                if (currGrid[b] > pocVal) { pocVal = currGrid[b]; pocBin = b; }
            result.PocRelative = pocBin / (double)(nb - 1);

            // Value Area (70%)
            var binIndices = new int[nb];
            for (int b = 0; b < nb; b++) binIndices[b] = b;
            Array.Sort(binIndices, (a, b) => -currGrid[a].CompareTo(currGrid[b]));

            int vahBin = nb / 2, valBin = nb / 2;
            double cumsum = 0;
            for (int idx = 0; idx < nb; idx++)
            {
                int b = binIndices[idx];
                cumsum += currGrid[b];
                if (cumsum <= totalVol * 0.7)
                {
                    if (idx == 0) { vahBin = b; valBin = b; }
                    else { vahBin = Math.Max(vahBin, b); valBin = Math.Min(valBin, b); }
                }
                else break;
            }
            result.VaWidth = (vahBin - valBin) / (double)(nb - 1);

            // Volume concentration (top 3 bins)
            double top3 = 0;
            for (int i = 0; i < Math.Min(3, nb); i++)
                top3 += currGrid[binIndices[i]];
            result.VolumeConcentration = top3 / totalVol;

            // POC dominance
            result.PocDominance = pocVal / totalVol;

            // Volume skew
            double aboveVol = 0, belowVol = 0;
            for (int b = 0; b < nb; b++)
            {
                if (b >= pocBin) aboveVol += currGrid[b];
                if (b <= pocBin) belowVol += currGrid[b];
            }
            result.VolSkew = (aboveVol - belowVol) / totalVol;

            // PriceInVa: position of POC within VA
            result.PriceInVa = vahBin != valBin
                ? (pocBin - valBin) / (double)(vahBin - valBin)
                : 0.5;

            // HVN/LVN
            double avgPerBin = totalVol / nb;
            double hvnSum = 0, lvnSum = 0;
            int hvnCount = 0, lvnCount = 0;
            for (int b = 0; b < nb; b++)
            {
                if (currGrid[b] > 2 * avgPerBin)
                {
                    hvnSum += b;
                    hvnCount++;
                }
                if (currGrid[b] > 0 && currGrid[b] < 0.3 * avgPerBin)
                {
                    lvnSum += b;
                    lvnCount++;
                }
            }
            double hvnMeanBin = hvnCount > 0 ? hvnSum / hvnCount : pocBin;
            double lvnMeanBin = lvnCount > 0 ? lvnSum / lvnCount : pocBin;
            result.HvnBinRel = hvnMeanBin / (nb - 1);
            result.LvnBinRel = lvnMeanBin / (nb - 1);

            return result;
        }

        // ─── PA Feature helpers ────────────────────────────
        private struct PaFeatures
        {
            public double MktStruct;
            public bool Bos;
            public bool Engulfing;
            public bool PinBar;
            public bool InsideBar;
            public double BodyRatio;
            public double UpperWick;
            public double LowerWick;
            public bool SwingHigh;
            public bool SwingLow;
            public double SwingHighDist;
            public double SwingLowDist;
            public double LiqHighDist;
            public double LiqLowDist;
        }

        private PaFeatures ComputePAFeatures()
        {
            int n = _closes.Count;
            int ci = n - 1;
            double close = _closes[ci], open = _opens[ci];
            double high = _highs[ci], low = _lows[ci];
            double range = Math.Max(high - low, 1e-10);

            var result = new PaFeatures();

            // ─── Candle Metrics ───
            double body = Math.Abs(close - open);
            result.BodyRatio = body / range;
            result.UpperWick = close >= open ? (high - close) / range : (high - open) / range;
            result.LowerWick = close >= open ? (open - low) / range : (close - low) / range;

            // ─── Engulfing ───
            if (ci >= 1)
            {
                double prevClose = _closes[ci - 1], prevOpen = _opens[ci - 1];
                bool prevBull = prevClose > prevOpen;
                bool prevBear = prevClose < prevOpen;
                bool currBull = close > open;
                bool currBear = close < open;
                result.Engulfing = (prevBull && currBear && open > prevClose && close < prevOpen)
                    || (prevBear && currBull && close > prevOpen && open < prevClose);
            }

            // ─── Pin Bar ───
            bool bullishPin = close >= open && result.LowerWick > 0.6 && result.BodyRatio < 0.3;
            bool bearishPin = close < open && result.UpperWick > 0.6 && result.BodyRatio < 0.3;
            result.PinBar = bullishPin || bearishPin;

            // ─── Inside Bar ───
            if (ci >= 1)
                result.InsideBar = high <= _highs[ci - 1] && low >= _lows[ci - 1];

            // ─── Swing Points ───
            result.SwingHigh = ci >= 2 && ci < n - 2;
            result.SwingLow = ci >= 2 && ci < n - 2;
            if (result.SwingHigh)
            {
                double swHi = _highs[ci];
                for (int k = ci - 2; k <= ci + 2; k++)
                    if (_highs[k] > swHi) { result.SwingHigh = false; break; }
            }
            if (result.SwingLow)
            {
                double swLo = _lows[ci];
                for (int k = ci - 2; k <= ci + 2; k++)
                    if (_lows[k] < swLo) { result.SwingLow = false; break; }
            }

            // ─── Last Swing High/Low Index ───
            int lastShIdx = 0, lastSlIdx = 0;
            for (int i = ci - 1; i >= 0; i--)
            {
                bool isSH = true, isSL = true;
                if (i >= 2 && i < n - 2)
                {
                    double mh = _highs[i];
                    for (int k = i - 2; k <= i + 2; k++)
                        if (_highs[k] > mh) { isSH = false; break; }
                    double ml = _lows[i];
                    for (int k = i - 2; k <= i + 2; k++)
                        if (_lows[k] < ml) { isSL = false; break; }
                }
                else { isSH = false; isSL = false; }
                if (isSH && lastShIdx == 0) lastShIdx = i;
                if (isSL && lastSlIdx == 0) lastSlIdx = i;
                if (lastShIdx > 0 && lastSlIdx > 0) break;
            }
            result.MktStruct = lastShIdx > lastSlIdx ? 1 : (lastSlIdx > lastShIdx ? 2 : 0);

            // ─── BOS (Break of Structure) ───
            result.Bos = false;
            if (ci >= 3)
            {
                for (int i = ci; i >= 3; i--)
                {
                    // Find last two swing highs before i, and last two swing lows
                    int sh1 = -1, sh2 = -1, sl1 = -1, sl2 = -1;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        bool jsH = true;
                        if (j >= 2 && j < n - 2)
                        {
                            double mh = _highs[j];
                            for (int k = j - 2; k <= j + 2; k++)
                                if (_highs[k] > mh) { jsH = false; break; }
                        }
                        else jsH = false;
                        if (jsH) { if (sh1 < 0) sh1 = j; else if (sh2 < 0) { sh2 = j; break; } }
                    }
                    for (int j = i - 1; j >= 0; j--)
                    {
                        bool jsL = true;
                        if (j >= 2 && j < n - 2)
                        {
                            double ml = _lows[j];
                            for (int k = j - 2; k <= j + 2; k++)
                                if (_lows[k] < ml) { jsL = false; break; }
                        }
                        else jsL = false;
                        if (jsL) { if (sl1 < 0) sl1 = j; else if (sl2 < 0) { sl2 = j; break; } }
                    }
                    if (sh1 >= 0 && sh2 >= 0 && sl1 >= 0 && sl2 >= 0)
                    {
                        if (close < _lows[sl2] && _highs[sh1] < _highs[sh2])
                            { result.Bos = true; break; }
                    }
                }
            }

            // ─── Swing Distance ───
            result.SwingHighDist = 0;
            result.SwingLowDist = 0;
            if (lastShIdx > 0)
                result.SwingHighDist = (_highs[lastShIdx] - close) / range;
            if (lastSlIdx > 0)
                result.SwingLowDist = (close - _lows[lastSlIdx]) / range;

            // ─── Liquidity Levels (matches Python features.py lines 316-329) ───
            result.LiqHighDist = 0;
            result.LiqLowDist = 0;
            // Look at 20 bars BEFORE current (Python: recent_highs = high[i-20:i])
            if (ci >= 10)
            {
                int hiMatches = 0, loMatches = 0;
                double lastHi = 0, lastLo = 0;
                for (int i = ci - 20; i < ci; i++)
                {
                    if (i < 0) continue;
                    double hDist = Math.Abs(_highs[i] - high) / range;
                    double lDist = Math.Abs(_lows[i] - low) / range;
                    if (hDist < 0.3) { hiMatches++; lastHi = _highs[i]; }
                    if (lDist < 0.3) { loMatches++; lastLo = _lows[i]; }
                }
                // Need at least 2 matching bars (Python: len(eq_highs) > 1)
                if (hiMatches > 1) result.LiqHighDist = Math.Abs(high - lastHi);
                if (loMatches > 1) result.LiqLowDist = Math.Abs(low - lastLo);
            }

            return result;
        }

        // ─── H1 Features (14) ──────────────────────────────
        private double[] ComputeH1Features()
        {
            var feats = new double[14];
            int h1 = _h1Bars.Count - 1;
            if (h1 < 0) return feats;

            var last = _h1Bars[h1];
            double range = Math.Max(last.High - last.Low, 1e-10);
            double body = Math.Abs(last.Close - last.Open);

            // [0] h1_ret: (close - prev_close) / h1_atr — NOT (close - open)
            double h1atr = ComputeH1ATR();
            if (h1 >= 1)
                feats[0] = (float)((last.Close - _h1Bars[h1 - 1].Close) / Math.Max(h1atr, 0.001));

            // [1] h1_range: range / avg range (24-period MA)
            double rangeMa = ComputeH1RangeMA();
            feats[1] = range / Math.Max(rangeMa, 1e-10);

            // [2] h1_vol: std of h1_ret over 24 periods
            feats[2] = ComputeH1Vol();

            // [3] h1_cpos: close position in range [0,1]
            feats[3] = (last.Close - last.Low) / range;

            // [4-6] Body ratio, upper wick, lower wick
            feats[4] = body / range;
            if (last.Close >= last.Open)
            {
                feats[5] = (last.High - last.Close) / range;
                feats[6] = (last.Open - last.Low) / range;
            }
            else
            {
                feats[5] = (last.High - last.Open) / range;
                feats[6] = (last.Close - last.Low) / range;
            }

            // [7] Engulfing on H1
            if (h1 >= 1)
            {
                var prev = _h1Bars[h1 - 1];
                bool prevBull = prev.Close > prev.Open;
                bool prevBear = prev.Close < prev.Open;
                bool currBull = last.Close > last.Open;
                bool currBear = last.Close < last.Open;
                feats[7] = (prevBull && currBear && last.Open > prev.Close && last.Close < prev.Open)
                    || (prevBear && currBull && last.Close > prev.Open && last.Open < prev.Close) ? 1 : 0;
            }

            // [8] Pin bar on H1 — binary matching Python features.py lines 517-519
            double upperWick = (last.High - Math.Max(last.Open, last.Close)) / Math.Max(range, 1e-10);
            double lowerWick = (Math.Min(last.Open, last.Close) - last.Low) / Math.Max(range, 1e-10);
            bool bullishPin = last.Close >= last.Open && lowerWick > 0.6 && feats[4] < 0.3;
            bool bearishPin = last.Close < last.Open && upperWick > 0.6 && feats[4] < 0.3;
            feats[8] = (bullishPin || bearishPin) ? 1.0f : 0.0f;

            // [9] Volume ratio (vs avg 24-period)
            double volMa = ComputeH1VolMA();
            feats[9] = last.Volume / Math.Max(volMa, 1);

            // [10] VWAP distance
            double vwap = ComputeH1VWAP();
            feats[10] = (last.Close - vwap) / Math.Max(h1atr, 0.001);

            // [11] 3-bar momentum: (close[t] / close[t-3] - 1) / (h1atr[t] / close[t])
            if (h1 >= 3)
            {
                double c3 = _h1Bars[h1 - 3].Close;
                if (c3 > 0 && h1atr > 0)
                    feats[11] = (last.Close / c3 - 1) / (h1atr / last.Close);
            }

            // [12] Direction strength: h1_ret * vol_ratio
            double volRatio = last.Volume / Math.Max(volMa, 1);
            feats[12] = feats[0] * volRatio;

            // [13] Trend consistency (matches Python lines 543-552: consecutive direction count)
            int consUp = 0, consDn = 0;
            for (int j = h1; j >= 1; j--)
            {
                if (_h1Bars[j].Close > _h1Bars[j - 1].Close)
                {
                    consUp++;
                    consDn = 0;  // Reset on direction change (Python: cons_dn[i]=0 when c[i]>c[i-1])
                }
                else if (_h1Bars[j].Close < _h1Bars[j - 1].Close)
                {
                    consDn++;
                    consUp = 0;  // Reset on direction change (Python: cons_up[i]=0 when c[i]<c[i-1])
                }
                else break;  // Equal close stops the chain
            }
            feats[13] = (consUp - consDn) / 6.0f;

            return feats;
        }

        private double ComputeH1ATR()
        {
            return ComputeH1ATRAt(_h1Bars.Count - 1);
        }

        // H1 ATR at a specific bar index (14-period, min_periods=14 like Python)
        private double ComputeH1ATRAt(int barIdx)
        {
            if (barIdx < 0 || _h1Bars.Count < 2) return 1e-5;
            int lookback = Math.Min(14, barIdx + 1);
            if (lookback < 14) return 1e-5;  // Python: min_periods=14
            int start = barIdx - lookback + 1;
            double sum = 0;
            for (int i = barIdx; i >= start; i--)
            {
                var b = _h1Bars[i];
                double prevClose = i > 0 ? _h1Bars[i - 1].Close : b.Open;
                double tr = Math.Max(b.High - b.Low,
                    Math.Max(Math.Abs(b.High - prevClose), Math.Abs(b.Low - prevClose)));
                sum += tr;
            }
            return Math.Max(sum / lookback, 1e-5);
        }

        private double ComputeH1RangeMA()
        {
            if (_h1Bars.Count < 2) return 1e-5;
            int lookback = Math.Min(24, _h1Bars.Count);
            double sum = 0;
            for (int i = _h1Bars.Count - 1; i > _h1Bars.Count - 1 - lookback; i--)
                sum += (_h1Bars[i].High - _h1Bars[i].Low);
            return sum / lookback;
        }

        private double ComputeH1Vol()
        {
            // Python: h1_atr with min_periods=14 → NaN for first 13 bars
            //         h1_vol = rolling std of valid h1_ret, NaN→0
            if (_h1Bars.Count < 15) return 0;  // Need at least 14+1 bars for valid ATR
            int lookback = Math.Min(24, _h1Bars.Count);
            int start = _h1Bars.Count - lookback;
            double sum = 0, sum2 = 0;
            int count = 0;
            for (int i = start; i < _h1Bars.Count; i++)
            {
                if (i < 14) continue;  // Skip bars without 14-period ATR (Python: min_periods=14)
                double atrI = ComputeH1ATRAt(i);
                if (atrI < 0.01) continue;  // Invalid ATR
                double ret = (_h1Bars[i].Close - _h1Bars[i - 1].Close) / atrI;
                sum += ret;
                sum2 += ret * ret;
                count++;
            }
            if (count < 2) return 0;
            double m = sum / count;
            return Math.Sqrt(Math.Max(sum2 / count - m * m, 0));
        }

        private double ComputeH1VolMA()
        {
            if (_h1Bars.Count < 2) return 1;
            int lookback = Math.Min(24, _h1Bars.Count);
            double sum = 0;
            for (int i = _h1Bars.Count - 1; i > _h1Bars.Count - 1 - lookback; i--)
                sum += _h1Bars[i].Volume;
            return Math.Max(sum / lookback, 1);
        }

        private double ComputeH1VWAP()
        {
            if (_h1Bars.Count < 2) return _h1Bars[_h1Bars.Count - 1].Close;
            int lookback = Math.Min(24, _h1Bars.Count);
            double pvSum = 0, vSum = 0;
            for (int i = _h1Bars.Count - 1; i > _h1Bars.Count - 1 - lookback; i--)
            {
                var b = _h1Bars[i];
                double tp = (b.Open + b.High + b.Low + b.Close) / 3.0;  // Python: (o+h+l+c)/3
                pvSum += tp * b.Volume;
                vSum += b.Volume;
            }
            return vSum > 0 ? pvSum / vSum : _h1Bars[_h1Bars.Count - 1].Close;
        }

        // ─── Inference ───────────────────────────────────────────
        private void RunInference(out double longScore, out double shortScore)
        {
            if (_weights == null)
            {
                longScore = 0; shortScore = 0; return;
            }

            float[] x = _inputBuffer.Select(v => (float)v).ToArray();

            // fc1: [128, 2361] @ [2361] + [128]
            float[] h1 = MatMulF32(_weights.FC1_W, x, 128, 2361);
            AddBias(h1, _weights.FC1_B);
            GELU(h1);

            // fc2: [64, 128] @ [128] + [64]
            float[] h2 = MatMulF32(_weights.FC2_W, h1, 64, 128);
            AddBias(h2, _weights.FC2_B);
            GELU(h2);

            // fc3: [32, 64] @ [64] + [32]
            float[] h3 = MatMulF32(_weights.FC3_W, h2, 32, 64);
            AddBias(h3, _weights.FC3_B);
            GELU(h3);

            // head_long intermediate: [16, 32] @ [32] + [16]
            float[] hl = MatMulF32(_weights.HL0_W, h3, 16, 32);
            AddBias(hl, _weights.HL0_B);
            GELU(hl);

            // head_long final: [1, 16] @ [16] + [1]
            float[] hlOut = MatMulF32(_weights.HL2_W, hl, 1, 16);
            AddBias(hlOut, _weights.HL2_B);

            // head_short intermediate: [16, 32] @ [32] + [16]
            float[] hs = MatMulF32(_weights.HS0_W, h3, 16, 32);
            AddBias(hs, _weights.HS0_B);
            GELU(hs);

            // head_short final: [1, 16] @ [16] + [1]
            float[] hsOut = MatMulF32(_weights.HS2_W, hs, 1, 16);
            AddBias(hsOut, _weights.HS2_B);

            longScore = hlOut[0];
            shortScore = hsOut[0];
        }

        // ─── Matrix Ops ───────────────────────────────────────
        private float[] MatMulF32(float[] W, float[] x, int rows, int cols)
        {
            var result = new float[rows];
            for (int r = 0; r < rows; r++)
            {
                double sum = 0;
                int rowOffset = r * cols;
                for (int c = 0; c < cols; c++)
                    sum += W[rowOffset + c] * x[c];
                result[r] = (float)sum;
            }
            return result;
        }

        private void AddBias(float[] vec, float[] bias)
        {
            for (int i = 0; i < vec.Length; i++)
                vec[i] += bias[i];
        }

        private void GELU(float[] vec)
        {
            double sqrt2pi = Math.Sqrt(2.0 / Math.PI);
            for (int i = 0; i < vec.Length; i++)
            {
                double x = vec[i];
                vec[i] = (float)(0.5 * x * (1.0 + Math.Tanh(sqrt2pi * (x + 0.044715 * x * x * x))));
            }
        }

        // ─── Trading Logic ────────────────────────────────────
        private void ManagePositions(double longScore, double shortScore)
        {
            // Pause check
            if (_pauseBarsLeft > 0)
            {
                _pauseBarsLeft--;
                return;
            }

            // Threshold check
            bool shouldLong = longScore > Threshold && longScore > shortScore;
            bool shouldShort = shortScore > (Threshold + ShortOffset) && shortScore > longScore;

            if (!shouldLong && !shouldShort) return;

            // Check existing positions
            var existingPositions = Positions.FindAll(_positionLabel, SymbolName);
            if (existingPositions.Length > 0) return;

            // Compute ATR-based SL/TP in pips
            double slPips, tpPips;
            double atrPrice = GetATR(ATRPeriod);
            double pipSize = Symbol.PipSize;
            if (atrPrice > 0 && pipSize > 0)
            {
                double atrPips = atrPrice / pipSize;
                slPips = RRMultiple * atrPips;
                tpPips = RRMultiple * atrPips;
            }
            else
            {
                slPips = 50;
                tpPips = 50;
            }

            try
            {
                if (shouldLong)
                {
                    var result = ExecuteMarketOrder(TradeType.Buy, SymbolName,
                        VolumeInUnits, _positionLabel, slPips, tpPips);
                    if (result.IsSuccessful)
                        Print($"LONG @ {result.Position.EntryPrice:F5} | " +
                              $"L={longScore:F4} S={shortScore:F4} " +
                              $"SL={slPips:F0} TP={tpPips:F0} thr={Threshold:F2}");
                }
                else if (shouldShort)
                {
                    var result = ExecuteMarketOrder(TradeType.Sell, SymbolName,
                        VolumeInUnits, _positionLabel, slPips, tpPips);
                    if (result.IsSuccessful)
                        Print($"SHORT @ {result.Position.EntryPrice:F5} | " +
                              $"L={longScore:F4} S={shortScore:F4} " +
                              $"SL={slPips:F0} TP={tpPips:F0} thr={Threshold:F2}");
                }
            }
            catch (Exception ex)
            {
                Print($"Trade error: {ex.Message}");
            }
        }

        private class ModelWeights
        {
            public float[] FC1_W { get; private set; }   // [128, 2361]
            public float[] FC1_B { get; private set; }   // [128]
            public float[] FC2_W { get; private set; }   // [64, 128]
            public float[] FC2_B { get; private set; }   // [64]
            public float[] FC3_W { get; private set; }   // [32, 64]
            public float[] FC3_B { get; private set; }   // [32]
            public float[] HL0_W { get; private set; }   // [16, 32]
            public float[] HL0_B { get; private set; }   // [16]
            public float[] HL2_W { get; private set; }   // [1, 16]
            public float[] HL2_B { get; private set; }   // [1]
            public float[] HS0_W { get; private set; }   // [16, 32]
            public float[] HS0_B { get; private set; }   // [16]
            public float[] HS2_W { get; private set; }   // [1, 16]
            public float[] HS2_B { get; private set; }   // [1]

            public int TotalParams { get; private set; }

            public bool TryLoadFromResource()
            {
                try
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    string[] resources = asm.GetManifestResourceNames();
                    string match = resources.FirstOrDefault(r =>
                        r.EndsWith("model_weights.bin", StringComparison.OrdinalIgnoreCase));
                    if (match == null) return false;

                    using (var stream = asm.GetManifestResourceStream(match))
                    {
                        if (stream == null) return false;
                        Load(stream);
                        return true;
                    }
                }
                catch { return false; }
            }

            public void Load(Stream stream)
            {
                using (var br = new BinaryReader(stream))
                {
                    // Magic
                    byte[] magic = br.ReadBytes(7);
                    string magicStr = System.Text.Encoding.ASCII.GetString(magic);
                    if (magicStr != "VPMODEL")
                        throw new Exception($"Invalid magic: {magicStr}");

                    int numTensors = br.ReadInt32();
                    for (int t = 0; t < numTensors; t++)
                    {
                        int nameLen = br.ReadInt32();
                        string name = System.Text.Encoding.ASCII.GetString(br.ReadBytes(nameLen));
                        int rank = br.ReadInt32();
                        int[] dims = new int[rank];
                        int totalElems = 1;
                        for (int d = 0; d < rank; d++)
                        {
                            dims[d] = br.ReadInt32();
                            totalElems *= dims[d];
                        }

                        float[] data = new float[totalElems];
                        byte[] raw = br.ReadBytes(totalElems * 4);
                        Buffer.BlockCopy(raw, 0, data, 0, raw.Length);

                        switch (name)
                        {
                            case "backbone.0.weight": FC1_W = data; break;
                            case "backbone.0.bias": FC1_B = data; break;
                            case "backbone.3.weight": FC2_W = data; break;
                            case "backbone.3.bias": FC2_B = data; break;
                            case "backbone.6.weight": FC3_W = data; break;
                            case "backbone.6.bias": FC3_B = data; break;
                            case "head_long.0.weight": HL0_W = data; break;
                            case "head_long.0.bias": HL0_B = data; break;
                            case "head_long.2.weight": HL2_W = data; break;
                            case "head_long.2.bias": HL2_B = data; break;
                            case "head_short.0.weight": HS0_W = data; break;
                            case "head_short.0.bias": HS0_B = data; break;
                            case "head_short.2.weight": HS2_W = data; break;
                            case "head_short.2.bias": HS2_B = data; break;
                        }

                        TotalParams += totalElems;
                    }
                }
            }

            public void Load(string path)
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    // Magic
                    byte[] magic = br.ReadBytes(7);
                    string magicStr = System.Text.Encoding.ASCII.GetString(magic);
                    if (magicStr != "VPMODEL")
                        throw new Exception($"Invalid magic: {magicStr}");

                    int numTensors = br.ReadInt32();

                    for (int t = 0; t < numTensors; t++)
                    {
                        int nameLen = br.ReadInt32();
                        string name = System.Text.Encoding.ASCII.GetString(br.ReadBytes(nameLen));
                        int rank = br.ReadInt32();
                        int[] dims = new int[rank];
                        int totalElems = 1;
                        for (int d = 0; d < rank; d++)
                        {
                            dims[d] = br.ReadInt32();
                            totalElems *= dims[d];
                        }

                        float[] data = new float[totalElems];
                        byte[] raw = br.ReadBytes(totalElems * 4);
                        Buffer.BlockCopy(raw, 0, data, 0, raw.Length);

                        switch (name)
                        {
                            case "backbone.0.weight": FC1_W = data; break;
                            case "backbone.0.bias": FC1_B = data; break;
                            case "backbone.3.weight": FC2_W = data; break;
                            case "backbone.3.bias": FC2_B = data; break;
                            case "backbone.6.weight": FC3_W = data; break;
                            case "backbone.6.bias": FC3_B = data; break;
                            case "head_long.0.weight": HL0_W = data; break;
                            case "head_long.0.bias": HL0_B = data; break;
                            case "head_long.2.weight": HL2_W = data; break;
                            case "head_long.2.bias": HL2_B = data; break;
                            case "head_short.0.weight": HS0_W = data; break;
                            case "head_short.0.bias": HS0_B = data; break;
                            case "head_short.2.weight": HS2_W = data; break;
                            case "head_short.2.bias": HS2_B = data; break;
                        }

                        TotalParams += totalElems;
                    }
                }
            }
        }
    }
}
