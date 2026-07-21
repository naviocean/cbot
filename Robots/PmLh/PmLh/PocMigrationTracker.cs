using System;

namespace cAlgo.Robots
{
    public enum MigrationDir
    {
        Flat = 0,
        Bull = 1,
        Bear = -1
    }

    /// <summary>
    /// Rolling POC series + migration score M = (POC_t − POC_{t−K}) / ATR.
    /// Pure series logic — caller supplies each closed-bar POC.
    /// </summary>
    public sealed class PocMigrationTracker
    {
        private double[] _poc;
        private double[] _oneBarDelta;
        private int _count;
        private int _capacity;

        private int _n;
        private int _k;
        private double _mMin;
        private int _minMoveBins;
        private double _binSize;
        private bool _useStreak;
        private int _streakNeed;
        private int _streakOf;
        private double _strongMBypass;

        public bool IsWarm { get; private set; }
        public double PocNow { get; private set; }
        public double Delta { get; private set; }
        public double M { get; private set; }
        public MigrationDir Direction { get; private set; }
        public bool StreakOk { get; private set; }
        public string FailCode { get; private set; }
        /// <summary>Non-zero same-sign 1-bar steps in streak window (diagnostics).</summary>
        public int StreakSame { get; private set; }
        /// <summary>Non-zero opposite-sign 1-bar steps in streak window.</summary>
        public int StreakOpp { get; private set; }
        /// <summary>Flat / sub-bin 1-bar steps ignored by streak.</summary>
        public int StreakFlat { get; private set; }

        public void Configure(
            int pocWindowBars,
            int migrateLookback,
            double minMigrationM,
            int minPocMoveBins,
            double binSize,
            bool useStreak,
            int streakNeed,
            int streakOf,
            double strongMBypass = 1.0)
        {
            _n = Math.Max(2, pocWindowBars);
            _k = Math.Max(1, migrateLookback);
            _mMin = Math.Max(0, minMigrationM);
            _minMoveBins = Math.Max(0, minPocMoveBins);
            _binSize = Math.Max(1e-9, binSize);
            _useStreak = useStreak;
            _streakNeed = Math.Max(1, streakNeed);
            _streakOf = Math.Max(_streakNeed, streakOf);
            _strongMBypass = Math.Max(0, strongMBypass);

            _capacity = Math.Max(_n, _k + _streakOf + 4) + 8;
            _poc = new double[_capacity];
            _oneBarDelta = new double[_capacity];
            Reset();
        }

        public void Reset()
        {
            _count = 0;
            IsWarm = false;
            PocNow = 0;
            Delta = 0;
            M = 0;
            Direction = MigrationDir.Flat;
            StreakOk = false;
            FailCode = "E_POC_INVALID";
            StreakSame = 0;
            StreakOpp = 0;
            StreakFlat = 0;
            if (_poc != null)
            {
                Array.Clear(_poc, 0, _poc.Length);
                Array.Clear(_oneBarDelta, 0, _oneBarDelta.Length);
            }
        }

        /// <summary>Push POC for the latest closed bar. Returns true if series is warm enough to score.</summary>
        public bool Push(double poc, double atr)
        {
            if (double.IsNaN(poc) || double.IsInfinity(poc) || poc <= 0)
            {
                FailCode = "E_POC_INVALID";
                IsWarm = false;
                Direction = MigrationDir.Flat;
                return false;
            }

            if (_count >= _capacity)
                ShiftLeft();

            if (_count > 0)
                _oneBarDelta[_count] = poc - _poc[_count - 1];

            _poc[_count] = poc;
            _count++;
            PocNow = poc;

            int need = _k + 1;
            if (_useStreak)
                need = Math.Max(need, _streakOf + 1);

            IsWarm = _count >= need;
            if (!IsWarm)
            {
                FailCode = "E_POC_INVALID";
                Direction = MigrationDir.Flat;
                M = 0;
                Delta = 0;
                StreakOk = false;
                return false;
            }

            if (atr <= 1e-12 || double.IsNaN(atr))
            {
                FailCode = "E_POC_INVALID";
                Direction = MigrationDir.Flat;
                return false;
            }

            double pocThen = _poc[_count - 1 - _k];
            Delta = poc - pocThen;
            M = Delta / atr;

            double minMove = _binSize * _minMoveBins;
            if (_minMoveBins > 0 && Math.Abs(Delta) < minMove)
            {
                Direction = MigrationDir.Flat;
                StreakOk = false;
                FailCode = "E_POC_TINY";
                CountStreakStats(MigrationDir.Flat);
                return true;
            }

            if (Math.Abs(M) < _mMin)
            {
                Direction = MigrationDir.Flat;
                StreakOk = false;
                FailCode = "E_POC_FLAT";
                CountStreakStats(MigrationDir.Flat);
                return true;
            }

            MigrationDir dir = M > 0 ? MigrationDir.Bull : MigrationDir.Bear;
            CountStreakStats(dir);

            // Strong net migration: skip micro-path noise filter (stepped POC common on VP)
            bool strong = _strongMBypass > 0 && Math.Abs(M) >= _strongMBypass;
            StreakOk = !_useStreak || strong || CheckStreak(dir);
            if (!StreakOk)
            {
                Direction = MigrationDir.Flat;
                FailCode = "E_POC_NOISE";
                return true;
            }

            Direction = dir;
            FailCode = null;
            return true;
        }

        /// <summary>
        /// Streak on <b>non-zero</b> POC steps only. Rolling VP POC often plateaus many bars
        /// then jumps one bin — counting zeros made 4/6 impossible and killed real |M|≫0.
        /// Pass if: enough same-sign steps, or same &gt; opp with ≥1 same, or no steps (plateau after net Δ).
        /// </summary>
        private bool CheckStreak(MigrationDir dir)
        {
            if (dir == MigrationDir.Flat)
                return false;

            int nonzero = StreakSame + StreakOpp;
            if (nonzero == 0)
                return true;

            if (StreakSame >= _streakNeed)
                return true;

            // Majority of actual moves agree with net M; allow 1 opposing flick
            if (StreakSame > StreakOpp && StreakSame >= 1 && StreakOpp <= 1)
                return true;

            return false;
        }

        private void CountStreakStats(MigrationDir dir)
        {
            int sign = dir == MigrationDir.Bull ? 1 : dir == MigrationDir.Bear ? -1 : 0;
            int from = _count - _streakOf;
            if (from < 1) from = 1;

            // Sub-bin wiggle = flat (POC sticky / quantize)
            double eps = Math.Max(_binSize * 0.25, 1e-9);
            int same = 0, opp = 0, flat = 0;

            for (int i = from; i < _count; i++)
            {
                double d = _oneBarDelta[i];
                if (Math.Abs(d) < eps)
                {
                    flat++;
                    continue;
                }

                if (sign == 0)
                {
                    flat++;
                    continue;
                }

                if (d * sign > 0) same++;
                else opp++;
            }

            StreakSame = same;
            StreakOpp = opp;
            StreakFlat = flat;
        }

        private void ShiftLeft()
        {
            int drop = _capacity / 4;
            if (drop < 1) drop = 1;
            int keep = _count - drop;
            if (keep < 0) keep = 0;
            Array.Copy(_poc, drop, _poc, 0, keep);
            Array.Copy(_oneBarDelta, drop, _oneBarDelta, 0, keep);
            _count = keep;
            Array.Clear(_poc, _count, _capacity - _count);
            Array.Clear(_oneBarDelta, _count, _capacity - _count);
        }
    }
}
