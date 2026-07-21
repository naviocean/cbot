using System;
using RedWave.Common;

namespace cAlgo.Robots
{
    public enum SvbsSide
    {
        None = 0,
        Long = 1,
        Short = 2
    }

    public enum SvbsPhase
    {
        Idle = 0,
        AcceptWait = 1
    }

    public enum SvbsAcceptMode
    {
        /// <summary>Pullback to VA edge then hold outside (strictest).</summary>
        RetestHold = 0,
        /// <summary>Two consecutive closes outside VA with HL/LH.</summary>
        Continuation = 1,
        /// <summary>Next closed bar still outside VA (no retest required) — practical default for XAU M5.</summary>
        BreakConfirm = 2
    }

    /// <summary>How position volume is computed at entry.</summary>
    public enum SvbsSizeMode
    {
        /// <summary>Volume from equity risk % and SL distance.</summary>
        RiskPercent = 0,
        /// <summary>Fixed lots (user lot size → units via RiskManager).</summary>
        FixedLots = 1
    }

    public sealed class SetupState
    {
        public SvbsPhase Phase { get; set; } = SvbsPhase.Idle;
        public SvbsSide Side { get; set; } = SvbsSide.None;
        public SvbsEntryWindow Window { get; set; } = SvbsEntryWindow.None;
        public SvbsProfileKind ProfileKind { get; set; } = SvbsProfileKind.None;
        public double VAH { get; set; }
        public double VAL { get; set; }
        public double VAWidth { get; set; }
        public int BreakBarIndex { get; set; } = -1;
        public DateTime BreakBarTime { get; set; } = DateTime.MinValue;
        /// <summary>Long: lowest low since break; Short: highest high since break.</summary>
        public double RetestExtreme { get; set; }
        public bool RetestTouched { get; set; }
        public int ContClosesOutside { get; set; }
        public double ContPrevLow { get; set; }
        public double ContPrevHigh { get; set; }
        /// <summary>Tick volume on the break bar (often the surge bar).</summary>
        public double BreakVolume { get; set; }
        public double BreakVolumeSma { get; set; }
        /// <summary>Last soft-reject while waiting (volume/POC/structure).</summary>
        public string LastSoftReject { get; set; }
    }

    public sealed class SignalContext
    {
        public int BarIndex { get; set; }
        public DateTime BarTime { get; set; }
        public DateTime UtcNow { get; set; }

        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double PrevClose { get; set; }
        public double Volume { get; set; }
        public double VolumeSma { get; set; }
        public double VolumeMedian { get; set; }

        public double Atr { get; set; }
        public ProfileData PriorProfile { get; set; }
        public double DevelopingPocNow { get; set; }
        public double DevelopingPocShift { get; set; }
        public bool DevelopingPocValid { get; set; }

        public SvbsEntryWindow EntryWindow { get; set; }
        public SvbsAcceptMode AcceptMode { get; set; }
        public int AcceptTimeoutBars { get; set; }
        public double VolumeK { get; set; }
        public bool UseVolumeMedian { get; set; }
        public double BodyMinRatio { get; set; }
        public double RetestAtrMult { get; set; }
        public double PocMidOffset { get; set; }
        public double MinVaWidth { get; set; }
        public double MaxVaWidth { get; set; }
        public bool UsePocFilter { get; set; }
        public bool UseVolumeFilter { get; set; }

        public bool SymbolOk { get; set; }
        public bool SpreadOk { get; set; }
        public bool NewsOk { get; set; }
        public bool RiskOk { get; set; }
        public bool DailyLossOk { get; set; }
        public int TradesToday { get; set; }
        public int MaxTradesPerDay { get; set; }
        public bool HasOpenPosition { get; set; }
    }

    public sealed class SignalResult
    {
        public bool IsEntry { get; set; }
        public SvbsSide Side { get; set; }
        public string Reason { get; set; } = "";
        public string RejectCode { get; set; }
        public double VAH { get; set; }
        public double VAL { get; set; }
        public double RetestExtreme { get; set; }
        public SvbsEntryWindow Window { get; set; }
        public double VolumeRatio { get; set; }
    }

    /// <summary>
    /// Pure multi-bar state machine: Idle → AcceptWait (break armed) → entry / cancel.
    /// No trade API calls.
    /// </summary>
    public sealed class SignalEngine
    {
        private readonly SetupState _state = new SetupState();

        public SetupState State => _state;

        public void Reset()
        {
            _state.Phase = SvbsPhase.Idle;
            _state.Side = SvbsSide.None;
            _state.Window = SvbsEntryWindow.None;
            _state.ProfileKind = SvbsProfileKind.None;
            _state.BreakBarIndex = -1;
            _state.BreakBarTime = DateTime.MinValue;
            _state.RetestTouched = false;
            _state.ContClosesOutside = 0;
            _state.BreakVolume = 0;
            _state.BreakVolumeSma = 0;
            _state.LastSoftReject = null;
        }

        public void Cancel(string code, out SignalResult result)
        {
            Reset();
            result = Reject(code);
        }

        public SignalResult Evaluate(SignalContext ctx)
        {
            if (ctx == null)
                return Reject("E_NULL");

            // Window ended while waiting → cancel
            if (_state.Phase == SvbsPhase.AcceptWait && ctx.EntryWindow == SvbsEntryWindow.None)
                return CancelAndReject("C_WINDOW");

            // Window flip while waiting
            if (_state.Phase == SvbsPhase.AcceptWait &&
                ctx.EntryWindow != SvbsEntryWindow.None &&
                ctx.EntryWindow != _state.Window)
                return CancelAndReject("C_WINDOW");

            if (!ctx.SymbolOk)
                return Reject("F1_SYMBOL");
            if (ctx.HasOpenPosition)
                return Reject("F8_IN_POSITION");
            if (ctx.TradesToday >= ctx.MaxTradesPerDay)
                return Reject("F6_MAX_TRADES");
            if (!ctx.DailyLossOk)
                return Reject("F7_DAILY_LOSS");
            if (!ctx.RiskOk)
                return Reject("F7_RISK");
            if (!ctx.SpreadOk)
                return Reject("F5_SPREAD");
            if (!ctx.NewsOk)
                return Reject("F9_NEWS");

            if (ctx.EntryWindow == SvbsEntryWindow.None)
                return Reject("F2_SESSION");

            var prior = ctx.PriorProfile;
            if (prior == null || !prior.IsValid)
                return Reject("F3_PROFILE");

            double vaWidth = prior.VAH - prior.VAL;
            if (vaWidth < ctx.MinVaWidth || vaWidth > ctx.MaxVaWidth)
                return Reject("F4_VA_WIDTH");

            if (ctx.Atr <= 0)
                return Reject("F10_ATR");

            // ── Accept wait path ──
            if (_state.Phase == SvbsPhase.AcceptWait)
            {
                int age = ctx.BarIndex - _state.BreakBarIndex;
                if (age > ctx.AcceptTimeoutBars)
                {
                    string detail =
                        $"C_TIMEOUT age={age}/{ctx.AcceptTimeoutBars} mode={ctx.AcceptMode} " +
                        $"retest={_state.RetestTouched} soft={_state.LastSoftReject ?? "-"} " +
                        $"side={_state.Side}";
                    Reset();
                    return new SignalResult { IsEntry = false, RejectCode = "C_TIMEOUT", Reason = detail };
                }

                // Invalidate only on full re-acceptance of VA (not mere mid pullback)
                if (_state.Side == SvbsSide.Long && ctx.Close < _state.VAL)
                    return CancelAndReject("C_REACCEPT");
                if (_state.Side == SvbsSide.Short && ctx.Close > _state.VAH)
                    return CancelAndReject("C_REACCEPT");

                // Opposite break flip
                if (_state.Side == SvbsSide.Long && IsShortBreak(ctx, prior))
                    return CancelAndReject("C_FLIP");
                if (_state.Side == SvbsSide.Short && IsLongBreak(ctx, prior))
                    return CancelAndReject("C_FLIP");

                UpdateRetestTracking(ctx);

                if (TryAccept(ctx, out var accept))
                    return accept;

                // Soft rejects while waiting (volume/POC not ready) — keep setup
                if (accept != null && !string.IsNullOrEmpty(accept.RejectCode))
                {
                    _state.LastSoftReject = accept.RejectCode;
                    return accept;
                }

                _state.LastSoftReject = "STRUCT";
                return Reject("E_ACC_WAIT");
            }

            // ── Idle: look for break ──
            if (IsLongBreak(ctx, prior))
            {
                Arm(SvbsSide.Long, ctx, prior);
                // Same bar cannot be accept for retest (no retest yet); continuation needs 2 closes
                if (ctx.AcceptMode == SvbsAcceptMode.Continuation)
                {
                    _state.ContClosesOutside = 1;
                    _state.ContPrevLow = ctx.Low;
                    _state.ContPrevHigh = ctx.High;
                }
                return Reject("E_BREAK_LONG");
            }

            if (IsShortBreak(ctx, prior))
            {
                Arm(SvbsSide.Short, ctx, prior);
                if (ctx.AcceptMode == SvbsAcceptMode.Continuation)
                {
                    _state.ContClosesOutside = 1;
                    _state.ContPrevLow = ctx.Low;
                    _state.ContPrevHigh = ctx.High;
                }
                return Reject("E_BREAK_SHORT");
            }

            return Reject("E_BREAK_NO");
        }

        private void Arm(SvbsSide side, SignalContext ctx, ProfileData prior)
        {
            _state.Phase = SvbsPhase.AcceptWait;
            _state.Side = side;
            _state.Window = ctx.EntryWindow;
            _state.ProfileKind = ctx.EntryWindow switch
            {
                SvbsEntryWindow.AsiaSession => SvbsProfileKind.Asia,
                SvbsEntryWindow.AsiaToLondon => SvbsProfileKind.Asia,
                SvbsEntryWindow.LondonToNy => SvbsProfileKind.London,
                _ => SvbsProfileKind.None
            };
            _state.VAH = prior.VAH;
            _state.VAL = prior.VAL;
            _state.VAWidth = prior.VAH - prior.VAL;
            _state.BreakBarIndex = ctx.BarIndex;
            _state.BreakBarTime = ctx.BarTime;
            _state.RetestExtreme = side == SvbsSide.Long ? ctx.Low : ctx.High;
            _state.RetestTouched = false;
            _state.ContClosesOutside = 0;
            _state.BreakVolume = ctx.Volume;
            _state.BreakVolumeSma = ctx.VolumeSma;
            _state.LastSoftReject = null;
        }

        private void UpdateRetestTracking(SignalContext ctx)
        {
            if (_state.Side == SvbsSide.Long)
            {
                if (ctx.Low < _state.RetestExtreme)
                    _state.RetestExtreme = ctx.Low;
                double zoneHi = _state.VAH + ctx.RetestAtrMult * ctx.Atr;
                if (ctx.Low <= zoneHi)
                    _state.RetestTouched = true;
            }
            else if (_state.Side == SvbsSide.Short)
            {
                if (ctx.High > _state.RetestExtreme)
                    _state.RetestExtreme = ctx.High;
                double zoneLo = _state.VAL - ctx.RetestAtrMult * ctx.Atr;
                if (ctx.High >= zoneLo)
                    _state.RetestTouched = true;
            }
        }

        private bool TryAccept(SignalContext ctx, out SignalResult result)
        {
            result = null;
            bool structureOk = ctx.AcceptMode switch
            {
                SvbsAcceptMode.RetestHold => CheckRetestHold(ctx),
                SvbsAcceptMode.Continuation => CheckContinuation(ctx),
                SvbsAcceptMode.BreakConfirm => CheckBreakConfirm(ctx),
                _ => false
            };

            if (!structureOk)
                return false;

            double volRatio = ctx.VolumeSma > 0 ? ctx.Volume / ctx.VolumeSma : 0;
            if (ctx.UseVolumeFilter)
            {
                if (!CheckVolume(ctx, out volRatio, out string volCode))
                {
                    result = Reject(volCode);
                    // volume fail does not cancel setup — wait for better bar unless timeout
                    return false;
                }
            }

            if (ctx.UsePocFilter)
            {
                if (!CheckPoc(ctx, out string pocCode))
                {
                    result = Reject(pocCode);
                    return false;
                }
            }

            // Success — consume setup
            var side = _state.Side;
            var window = _state.Window;
            double vah = _state.VAH;
            double val = _state.VAL;
            double retest = _state.RetestExtreme;
            Reset();

            result = new SignalResult
            {
                IsEntry = true,
                Side = side,
                Window = window,
                VAH = vah,
                VAL = val,
                RetestExtreme = retest,
                VolumeRatio = volRatio,
                Reason = side == SvbsSide.Long ? "PASS_LONG" : "PASS_SHORT"
            };
            return true;
        }

        private bool CheckRetestHold(SignalContext ctx)
        {
            int age = ctx.BarIndex - _state.BreakBarIndex;
            bool outside = _state.Side == SvbsSide.Long
                ? ctx.Close > _state.VAH
                : ctx.Close < _state.VAL;
            if (!outside)
                return false;

            // Classic: pullback to edge then hold outside
            if (_state.RetestTouched)
                return true;

            // Fallback: still outside after ≥2 bars (XAU often never retests within timeout)
            return age >= 2;
        }

        /// <summary>
        /// At least 1 bar after break, still closed outside VA (acceptance without retest).
        /// </summary>
        private bool CheckBreakConfirm(SignalContext ctx)
        {
            int age = ctx.BarIndex - _state.BreakBarIndex;
            if (age < 1)
                return false;

            if (_state.Side == SvbsSide.Long)
                return ctx.Close > _state.VAH;

            return ctx.Close < _state.VAL;
        }

        private bool CheckContinuation(SignalContext ctx)
        {
            bool outside = _state.Side == SvbsSide.Long
                ? ctx.Close > _state.VAH
                : ctx.Close < _state.VAL;

            if (!outside)
            {
                _state.ContClosesOutside = 0;
                return false;
            }

            if (_state.ContClosesOutside < 1)
            {
                _state.ContClosesOutside = 1;
                _state.ContPrevLow = ctx.Low;
                _state.ContPrevHigh = ctx.High;
                return false;
            }

            // Second consecutive close outside + higher low / lower high
            if (_state.Side == SvbsSide.Long)
            {
                bool hl = ctx.Low >= _state.ContPrevLow;
                _state.ContPrevLow = ctx.Low;
                if (!hl)
                    return false;
                _state.ContClosesOutside = 2;
                return true;
            }

            bool lh = ctx.High <= _state.ContPrevHigh;
            _state.ContPrevHigh = ctx.High;
            if (!lh)
                return false;
            _state.ContClosesOutside = 2;
            return true;
        }

        private bool CheckVolume(SignalContext ctx, out double ratio, out string code)
        {
            ratio = 0;
            code = null;

            // Baseline: prior-bar SMA at arm (preferred) or current prior SMA
            double sma = _state.BreakVolumeSma > 0 ? _state.BreakVolumeSma : ctx.VolumeSma;
            if (sma <= 0 && ctx.VolumeSma > 0)
                sma = ctx.VolumeSma;
            if (sma <= 0)
            {
                code = "V_SURGE";
                return false;
            }

            // Pass if break bar OR accept bar shows surge (accept bar alone is often quiet)
            double volBreak = _state.BreakVolume;
            double volNow = ctx.Volume;
            double vol = Math.Max(volBreak, volNow);
            ratio = vol / sma;

            if (vol < ctx.VolumeK * sma)
            {
                code = "V_SURGE";
                return false;
            }

            if (ctx.UseVolumeMedian && ctx.VolumeMedian > 0
                && volBreak < ctx.VolumeMedian && volNow < ctx.VolumeMedian)
            {
                code = "V_MEDIAN";
                return false;
            }

            return true;
        }

        private bool CheckPoc(SignalContext ctx, out string code)
        {
            code = null;
            if (!ctx.DevelopingPocValid)
            {
                code = "POC_SLOPE";
                return false;
            }

            double mid = (_state.VAH + _state.VAL) * 0.5;
            double offset = ctx.PocMidOffset * _state.VAWidth;

            if (_state.Side == SvbsSide.Long)
            {
                if (ctx.DevelopingPocNow <= ctx.DevelopingPocShift)
                {
                    code = "POC_SLOPE";
                    return false;
                }
                if (ctx.DevelopingPocNow < mid + offset)
                {
                    code = "POC_SIDE";
                    return false;
                }
            }
            else
            {
                if (ctx.DevelopingPocNow >= ctx.DevelopingPocShift)
                {
                    code = "POC_SLOPE";
                    return false;
                }
                if (ctx.DevelopingPocNow > mid - offset)
                {
                    code = "POC_SIDE";
                    return false;
                }
            }

            return true;
        }

        private static bool IsLongBreak(SignalContext ctx, ProfileData prior)
        {
            if (ctx.Close <= prior.VAH) return false;
            if (ctx.PrevClose > prior.VAH) return false;
            return BodyOk(ctx);
        }

        private static bool IsShortBreak(SignalContext ctx, ProfileData prior)
        {
            if (ctx.Close >= prior.VAL) return false;
            if (ctx.PrevClose < prior.VAL) return false;
            return BodyOk(ctx);
        }

        private static bool BodyOk(SignalContext ctx)
        {
            double range = ctx.High - ctx.Low;
            if (range <= 0) return false;
            double body = Math.Abs(ctx.Close - ctx.Open);
            return body >= ctx.BodyMinRatio * range;
        }

        private SignalResult CancelAndReject(string code)
        {
            Reset();
            return Reject(code);
        }

        private static SignalResult Reject(string code)
        {
            return new SignalResult
            {
                IsEntry = false,
                RejectCode = code,
                Reason = code
            };
        }
    }
}
