using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace RedWave.Common
{
    /// <summary>Snapshot of risk gates after Evaluate / OnTick.</summary>
    public sealed class RiskGateStatus
    {
        public bool AllowNewEntries { get; set; } = true;
        public bool RequestFlatten { get; set; }
        public string Reason { get; set; } = "";
    }

    /// <summary>
    /// Risk sizing + account gates (equity DD, daily loss/profit $).
    /// Call <see cref="OnTick"/> from the bot each tick — evaluate + optional flatten live here
    /// (same pattern as CTrailingManager). Bot only checks <see cref="CanOpenNewTrade"/>.
    /// </summary>
    public class CRiskManager
    {
        private Robot _robot;
        private Symbol _symbol;
        private string _label;
        private CLogger _logger;

        // Peak equity drawdown
        private double _maxEquityDrawdownPct;
        private bool _useEquityProtection;
        private bool _flattenOnEquityDd;
        private double _peakEquity;

        // Daily $ limits (0 = off)
        private double _maxDailyLossAmount;
        private double _maxDailyProfitAmount;
        private bool _flattenOnDailyLoss;
        private bool _flattenOnDailyProfit;
        /// <summary>Account equity at first sample of UTC calendar day.</summary>
        private double _dayStartEquity;
        private int _dayKey;

        private bool _equityBlockLogged;
        private bool _dailyBlockLogged;
        private bool _flattenDoneForEquity;
        private bool _flattenDoneForDaily;

        private bool _canOpenNewTrade = true;
        private RiskGateStatus _lastStatus;

        public CRiskManager()
        {
            _robot = null;
            _symbol = null;
            _label = "";
            _logger = null;
            _maxEquityDrawdownPct = 10.0;
            _useEquityProtection = false;
            _flattenOnEquityDd = false;
            _peakEquity = 0.0;
            _maxDailyLossAmount = 0;
            _maxDailyProfitAmount = 0;
            _flattenOnDailyLoss = false;
            _flattenOnDailyProfit = false;
            _dayStartEquity = 0;
            _dayKey = -1;
            _lastStatus = new RiskGateStatus();
        }

        /// <summary>Sizing-only / legacy bots (no auto-flatten).</summary>
        public bool Init(Symbol symbol, CLogger logger = null)
        {
            _logger = logger;
            _robot = null;
            _label = "";
            if (symbol == null)
            {
                _logger?.Error("RiskManager: Initialization failed. Symbol is null.");
                return false;
            }
            _symbol = symbol;
            ResetRuntimeState();
            return true;
        }

        /// <summary>
        /// Full mode: OnTick can flatten this bot's positions (label + symbol).
        /// </summary>
        public bool Init(Robot robot, Symbol symbol, string label, CLogger logger = null)
        {
            _logger = logger;
            if (robot == null || symbol == null)
            {
                _logger?.Error("RiskManager: Init(Robot) failed. Robot or Symbol is null.");
                return false;
            }
            _robot = robot;
            _symbol = symbol;
            _label = label ?? "";
            ResetRuntimeState();
            return true;
        }

        private void ResetRuntimeState()
        {
            _peakEquity = 0.0;
            _dayKey = -1;
            _dayStartEquity = 0;
            _equityBlockLogged = false;
            _dailyBlockLogged = false;
            _flattenDoneForEquity = false;
            _flattenDoneForDaily = false;
            _canOpenNewTrade = true;
            _lastStatus = new RiskGateStatus();
        }

        public void SetEquityProtection(double maxDrawdownPct, bool flattenOnBreach = false)
        {
            _useEquityProtection = maxDrawdownPct > 0;
            _maxEquityDrawdownPct = Math.Max(0, maxDrawdownPct);
            _flattenOnEquityDd = flattenOnBreach;
        }

        public void SetDailyLimits(
            double maxDailyLossAmount,
            double maxDailyProfitAmount,
            bool flattenOnDailyLoss = false,
            bool flattenOnDailyProfit = false)
        {
            _maxDailyLossAmount = Math.Max(0, maxDailyLossAmount);
            _maxDailyProfitAmount = Math.Max(0, maxDailyProfitAmount);
            _flattenOnDailyLoss = flattenOnDailyLoss;
            _flattenOnDailyProfit = flattenOnDailyProfit;
            _logger?.Debug(
                $"RiskManager daily: loss=${_maxDailyLossAmount:F0} (flat={_flattenOnDailyLoss}) " +
                $"profit=${_maxDailyProfitAmount:F0} (flat={_flattenOnDailyProfit})");
        }

        /// <summary>True if new entries are allowed (updated every OnTick / Evaluate).</summary>
        public bool CanOpenNewTrade => _canOpenNewTrade;

        public RiskGateStatus LastStatus => _lastStatus;

        /// <summary>
        /// Bot OnTick only: watch account equity, gate entries, optional flatten.
        /// Does not care about TP/SL/label PnL — only equity level vs day-start / peak.
        /// </summary>
        public void OnTick()
        {
            if (_robot == null)
                return;

            double equity = _robot.Account.Equity;
            DateTime utc = _robot.Server.TimeInUtc;
            var status = Evaluate(equity, utc);
            _lastStatus = status;
            _canOpenNewTrade = status.AllowNewEntries;

            if (!status.RequestFlatten)
                return;

            TryFlatten(status.Reason, equity);
        }

        /// <summary>
        /// Pure equity gates:
        /// - Peak equity DD % from high-water mark
        /// - Daily loss/profit $ from equity at UTC day start (floating already in Equity)
        /// Flatten only executed in OnTick when Robot is bound.
        /// </summary>
        public RiskGateStatus Evaluate(double currentEquity, DateTime serverTimeUtc)
        {
            var status = new RiskGateStatus { AllowNewEntries = true, RequestFlatten = false, Reason = "" };

            RollDayIfNeeded(currentEquity, serverTimeUtc);

            bool blockEntry = false;
            bool wantFlatten = false;
            string reason = "";

            // ── Peak equity DD (account) ──
            if (_useEquityProtection)
            {
                if (_peakEquity <= 0)
                    _peakEquity = currentEquity;
                else if (currentEquity > _peakEquity)
                {
                    _peakEquity = currentEquity;
                    _flattenDoneForEquity = false;
                }

                if (_peakEquity > 0)
                {
                    double ddPct = (_peakEquity - currentEquity) / _peakEquity * 100.0;
                    if (ddPct >= _maxEquityDrawdownPct)
                    {
                        blockEntry = true;
                        reason = $"EquityDD {ddPct:F1}%>= {_maxEquityDrawdownPct:F1}%";
                        if (_flattenOnEquityDd && !_flattenDoneForEquity)
                            wantFlatten = true;
                        if (!_equityBlockLogged)
                        {
                            _equityBlockLogged = true;
                            _logger?.Warn($"RiskManager: {reason} (eq={currentEquity:F0} peak={_peakEquity:F0})");
                        }
                    }
                    else
                        _equityBlockLogged = false;
                }
            }

            // ── Daily loss / profit $ from account equity only ──
            // dailyPnl$ = Equity_now − Equity_dayStart (includes floating of ALL positions)
            if ((_maxDailyLossAmount > 0 || _maxDailyProfitAmount > 0) && _dayStartEquity > 0)
            {
                double pnlMoney = currentEquity - _dayStartEquity;

                if (_maxDailyLossAmount > 0 && pnlMoney <= -_maxDailyLossAmount)
                {
                    blockEntry = true;
                    string r = $"DailyLoss ${pnlMoney:F0}<= -${_maxDailyLossAmount:F0}";
                    reason = string.IsNullOrEmpty(reason) ? r : reason + " | " + r;
                    if (_flattenOnDailyLoss && !_flattenDoneForDaily)
                        wantFlatten = true;
                    LogDailyOnce(r, currentEquity);
                }
                else if (_maxDailyProfitAmount > 0 && pnlMoney >= _maxDailyProfitAmount)
                {
                    blockEntry = true;
                    string r = $"DailyProfit ${pnlMoney:F0}>= ${_maxDailyProfitAmount:F0}";
                    reason = string.IsNullOrEmpty(reason) ? r : reason + " | " + r;
                    if (_flattenOnDailyProfit && !_flattenDoneForDaily)
                        wantFlatten = true;
                    LogDailyOnce(r, currentEquity);
                }
                else
                    _dailyBlockLogged = false;
            }

            status.AllowNewEntries = !blockEntry;
            status.RequestFlatten = wantFlatten;
            status.Reason = wantFlatten ? reason + " → flatten" : reason;
            _canOpenNewTrade = status.AllowNewEntries;
            return status;
        }

        private void TryFlatten(string reason, double equity)
        {
            if (_robot == null || _symbol == null)
                return;

            var positions = string.IsNullOrEmpty(_label)
                ? _robot.Positions.Where(p => p.SymbolName == _symbol.Name).ToArray()
                : _robot.Positions.FindAll(_label, _symbol.Name);

            if (positions == null || positions.Length == 0)
            {
                AcknowledgeFlatten(reason);
                return;
            }

            int closed = 0;
            foreach (var pos in positions)
            {
                var res = _robot.ClosePosition(pos);
                if (res.IsSuccessful)
                    closed++;
                else
                    _logger?.Error($"RiskManager flatten close #{pos.Id} failed: {res.Error}");
            }

            int stillOpen = string.IsNullOrEmpty(_label)
                ? _robot.Positions.Count(p => p.SymbolName == _symbol.Name)
                : _robot.Positions.FindAll(_label, _symbol.Name).Length;

            if (stillOpen == 0)
            {
                AcknowledgeFlatten(reason);
                _logger?.Warn(
                    $"RiskManager flatten: closed {closed} — {reason} eq={equity:F0} dailyPnl=${GetDailyPnlMoney(equity):F0}");
            }
            else
            {
                _logger?.Error($"RiskManager flatten incomplete: {stillOpen} still open — will retry ({reason})");
            }
        }

        private void AcknowledgeFlatten(string reasonHint)
        {
            if (string.IsNullOrEmpty(reasonHint))
            {
                _flattenDoneForDaily = true;
                _flattenDoneForEquity = true;
            }
            else if (reasonHint.IndexOf("EquityDD", StringComparison.OrdinalIgnoreCase) >= 0)
                _flattenDoneForEquity = true;
            else
                _flattenDoneForDaily = true;

            _logger?.Debug($"RiskManager: flatten acknowledged ({reasonHint})");
        }

        public bool IsTradingAllowed(double currentEquity, DateTime serverTimeUtc)
        {
            return Evaluate(currentEquity, serverTimeUtc).AllowNewEntries;
        }

        /// <summary>Legacy: peak DD only path for older bots (also rolls daily if configured).</summary>
        public bool IsTradingAllowed(double initialEquity, double currentEquity)
        {
            return Evaluate(currentEquity, DateTime.UtcNow).AllowNewEntries;
        }

        /// <summary>Equity change since UTC day start (account-level, not per-trade).</summary>
        public double GetDailyPnlMoney(double currentEquity)
        {
            if (_dayStartEquity <= 0) return 0;
            return currentEquity - _dayStartEquity;
        }

        /// <summary>
        /// How much more equity can fall today before Max Daily Loss $ is hit.
        /// room = MaxDailyLoss + (Equity − dayStartEquity).
        /// </summary>
        public double GetRemainingDailyLossBudget(double currentEquity)
        {
            if (_maxDailyLossAmount <= 0)
                return double.MaxValue;
            if (_dayStartEquity <= 0)
                return _maxDailyLossAmount;
            return _maxDailyLossAmount + (currentEquity - _dayStartEquity);
        }

        public bool IsDailyLossLimitEnabled => _maxDailyLossAmount > 0;

        public double GetDailyPnlPct(double currentEquity)
        {
            if (_dayStartEquity <= 0) return 0;
            return (currentEquity - _dayStartEquity) / _dayStartEquity * 100.0;
        }

        private void RollDayIfNeeded(double currentEquity, DateTime serverTimeUtc)
        {
            DateTime utc = DateTime.SpecifyKind(serverTimeUtc, DateTimeKind.Utc);
            int dayKey = utc.Year * 1000 + utc.DayOfYear;
            if (dayKey == _dayKey) return;

            _dayKey = dayKey;
            _dayStartEquity = currentEquity > 0 ? currentEquity : 0;
            _dailyBlockLogged = false;
            _flattenDoneForDaily = false;
            _logger?.Debug($"RiskManager new day key={_dayKey} dayStartEquity={_dayStartEquity:F2}");
        }

        private void LogDailyOnce(string reason, double equity)
        {
            if (_dailyBlockLogged) return;
            _dailyBlockLogged = true;
            _logger?.Warn($"RiskManager: {reason} (eq={equity:F0} dayStart={_dayStartEquity:F0})");
        }

        public double CalculateVolume(double lotSize)
        {
            if (_symbol == null)
            {
                _logger?.Error("RiskManager: Cannot calculate volume, Symbol is null.");
                return 0.0;
            }
            return NormalizeVolume(lotSize * _symbol.LotSize);
        }

        public double CalculateVolumeFromRisk(double balance, double riskPercent, double slPriceDistance, out double expectedRiskOut)
        {
            expectedRiskOut = 0;
            if (balance <= 0 || riskPercent <= 0)
            {
                _logger?.Warn($"RiskManager: Invalid risk inputs balance={balance}, risk%={riskPercent}");
                return 0.0;
            }
            return CalculateVolumeFromRiskMoney(balance * (riskPercent / 100.0), slPriceDistance, out expectedRiskOut);
        }

        public double CalculateVolumeFromRiskMoney(double riskMoney, double slPriceDistance, out double expectedRiskOut)
        {
            expectedRiskOut = 0;
            if (_symbol == null)
            {
                _logger?.Error("RiskManager: Cannot calculate risk volume, Symbol is null.");
                return 0.0;
            }
            if (riskMoney <= 0 || slPriceDistance <= 0)
            {
                _logger?.Warn($"RiskManager: Invalid riskMoney={riskMoney}, slDist={slPriceDistance}");
                return 0.0;
            }

            double pipSize = PriceUtils.GetPipSize(_symbol);
            if (pipSize <= 0) return 0.0;

            double slPips = slPriceDistance / pipSize;
            if (slPips < 0.1) return 0.0;

            double volumeFixed = 0;
            try { volumeFixed = _symbol.VolumeForFixedRisk(riskMoney, slPips, RoundingMode.Down); }
            catch (Exception ex) { _logger?.Warn($"RiskManager: VolumeForFixedRisk failed: {ex.Message}"); }

            double volumeTick = 0;
            double lossPerUnitTick = LossPerUnitFromTicks(slPriceDistance);
            if (lossPerUnitTick > 0)
                volumeTick = riskMoney / lossPerUnitTick;

            double volume;
            if (volumeFixed > 0 && volumeTick > 0)
            {
                if (volumeTick > volumeFixed * 3.0) volume = volumeFixed;
                else if (volumeFixed > volumeTick * 3.0) volume = Math.Min(volumeFixed, volumeTick);
                else volume = volumeFixed;
            }
            else if (volumeFixed > 0) volume = volumeFixed;
            else if (volumeTick > 0) volume = volumeTick;
            else return 0.0;

            volume = NormalizeVolume(volume);
            if (volume <= 0) return 0.0;

            // Conservative cap: if $1 price move ≈ $1 per unit (typical XAU unit=oz),
            // vol should be ≤ riskMoney / slDist. When FixedRisk oversizes, take the min.
            double volByPrice = riskMoney / slPriceDistance;
            double volByPriceN = NormalizeVolume(volByPrice);
            if (volByPriceN > 0 && volume > volByPriceN * 1.15)
            {
                _logger?.Warn(
                    $"RiskManager: FixedRisk vol {volume:F0} >> price-unit vol {volByPriceN:F0} " +
                    $"(slDist={slPriceDistance:F2}, risk$={riskMoney:F0}) — using conservative size");
                volume = volByPriceN;
            }

            expectedRiskOut = EstimateRiskMoney(volume, slPips, riskMoney, volumeFixed);
            // Prefer money estimate from price units when we used conservative size
            double priceUnitRisk = volume * slPriceDistance;
            if (priceUnitRisk > 0 && (expectedRiskOut <= 0 || Math.Abs(priceUnitRisk - expectedRiskOut) > expectedRiskOut * 0.5))
                expectedRiskOut = priceUnitRisk;

            try
            {
                double maxVol = _symbol.VolumeForFixedRisk(riskMoney * 1.5, slPips, RoundingMode.Down);
                if (maxVol > 0 && volume > maxVol)
                {
                    volume = NormalizeVolume(maxVol);
                    expectedRiskOut = volume * slPriceDistance;
                }
            }
            catch { /* ignore */ }

            if (expectedRiskOut > riskMoney * 1.5)
            {
                // Scale volume down to fit riskMoney
                double scale = riskMoney / expectedRiskOut;
                volume = NormalizeVolume(volume * scale);
                expectedRiskOut = volume * slPriceDistance;
                if (volume <= 0 || expectedRiskOut > riskMoney * 1.5)
                {
                    _logger?.Error($"RiskManager: SAFETY ABORT risk=${expectedRiskOut:F2} vol={volume}");
                    return 0.0;
                }
            }
            return volume;
        }

        public double CalculateVolumeFromRisk(double balance, double riskPercent, double slPriceDistance)
        {
            return CalculateVolumeFromRisk(balance, riskPercent, slPriceDistance, out _);
        }

        private double EstimateRiskMoney(double volume, double slPips, double targetRiskMoney, double volumeForTarget)
        {
            if (volume <= 0) return 0;
            if (volumeForTarget > 0 && targetRiskMoney > 0)
                return targetRiskMoney * (volume / volumeForTarget);
            try
            {
                double volPerUnitMoney = _symbol.VolumeForFixedRisk(1.0, slPips, RoundingMode.Up);
                if (volPerUnitMoney > 0) return volume / volPerUnitMoney;
            }
            catch { /* ignore */ }
            double lpu = LossPerUnitFromTicks(slPips * PriceUtils.GetPipSize(_symbol));
            return lpu > 0 ? lpu * volume : targetRiskMoney;
        }

        private double LossPerUnitFromTicks(double slPriceDistance)
        {
            if (_symbol == null || slPriceDistance <= 0) return 0;
            if (_symbol.TickSize <= 0 || _symbol.TickValue <= 0 || _symbol.LotSize <= 0) return 0;
            return (slPriceDistance / _symbol.TickSize) * _symbol.TickValue / _symbol.LotSize;
        }

        private double NormalizeVolume(double volume)
        {
            if (volume <= 0 || _symbol == null) return 0;
            double step = _symbol.VolumeInUnitsStep;
            if (step <= 0) step = 1;
            double normalizedVolume = Math.Floor(volume / step) * step;
            if (normalizedVolume < _symbol.VolumeInUnitsMin) return 0;
            return Math.Min(_symbol.VolumeInUnitsMax, normalizedVolume);
        }
    }
}
