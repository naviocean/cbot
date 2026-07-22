using System;
using System.Collections.Generic;

namespace RedWave.Common
{
    public enum ProfileShape
    {
        Neutral = 0,
        Bullish = 1,   // b-shape / bottom-heavy
        Bearish = 2,   // P-shape / top-heavy
        DShape = 3     // balanced / classic D
    }

    public enum VolumeNodeType
    {
        HVN = 0,
        LVN = 1
    }

    /// <summary>
    /// Contiguous high/low volume region on a price histogram.
    /// </summary>
    public sealed class VolumeNode
    {
        public VolumeNodeType Type { get; set; }
        public int StartBin { get; set; }
        public int EndBin { get; set; }
        public double Low { get; set; }
        public double High { get; set; }
        public double Mid => (Low + High) * 0.5;
        public double Volume { get; set; }
        public double AvgVolume { get; set; }

        /// <summary>
        /// For LVN: (shoulderAvg - lvnAvg) / shoulderAvg in [0,1].
        /// For HVN: avgVol / meanVol.
        /// </summary>
        public double Strength { get; set; }

        public bool Contains(double price)
        {
            return price >= Low && price <= High;
        }

        public int WidthBins => Math.Max(0, EndBin - StartBin + 1);
    }

    /// <summary>
    /// Immutable-style snapshot of a (composite) volume profile calculation.
    /// </summary>
    public sealed class ProfileData
    {
        public double BinSize { get; set; }
        public double MinPrice { get; set; }
        public double MaxPrice { get; set; }
        public double[] Histogram { get; set; }
        public double TotalVolume { get; set; }
        public double POC { get; set; }
        public double VAH { get; set; }
        public double VAL { get; set; }
        public int PocBin { get; set; }
        public ProfileShape Shape { get; set; }
        public double VolAbovePoc { get; set; }
        public double VolBelowPoc { get; set; }
        public double PocRelative { get; set; }
        public List<VolumeNode> Hvns { get; set; }
        public List<VolumeNode> Lvns { get; set; }
        public DateTime BuiltAt { get; set; }
        public int BarsUsed { get; set; }
        public int LookbackDays { get; set; }
        public bool IsValid { get; set; }

        // === BỔ SUNG MỚI: Order Flow Delta Extensions (v2.0) ===
        public double[] UpHistogram { get; set; }    // Buy Volume per Bin
        public double[] DownHistogram { get; set; }  // Sell Volume per Bin
        public double[] DeltaHistogram { get; set; } // Net Delta (Buy - Sell) per Bin
        public double PocUpVolume { get; set; }      // Buy Volume riêng tại POC
        public double PocDownVolume { get; set; }    // Sell Volume riêng tại POC
        public double PocDelta => PocUpVolume - PocDownVolume; // Net Delta tại POC
        public bool HasOrderFlowData { get; set; }   // True nếu được tính từ Order Flow / Source Bars

        public ProfileData()
        {
            Histogram = Array.Empty<double>();
            UpHistogram = Array.Empty<double>();
            DownHistogram = Array.Empty<double>();
            DeltaHistogram = Array.Empty<double>();
            Hvns = new List<VolumeNode>();
            Lvns = new List<VolumeNode>();
            BuiltAt = DateTime.MinValue;
            IsValid = false;
            PocUpVolume = 0;
            PocDownVolume = 0;
            HasOrderFlowData = false;
        }

        public int BinCount => Histogram?.Length ?? 0;

        public double GetBinBuyVolume(int bin) => UpHistogram != null && bin >= 0 && bin < UpHistogram.Length ? UpHistogram[bin] : 0;

        public double GetBinSellVolume(int bin) => DownHistogram != null && bin >= 0 && bin < DownHistogram.Length ? DownHistogram[bin] : 0;

        public double GetBinDelta(int bin) => DeltaHistogram != null && bin >= 0 && bin < DeltaHistogram.Length ? DeltaHistogram[bin] : 0;

        public double BinLow(int bin) => MinPrice + bin * BinSize;

        public double BinHigh(int bin) => MinPrice + (bin + 1) * BinSize;

        public double BinMid(int bin) => MinPrice + (bin + 0.5) * BinSize;

        public int PriceToBin(double price)
        {
            if (BinSize <= 0 || BinCount == 0) return 0;
            int bin = (int)Math.Floor((price - MinPrice) / BinSize);
            if (bin < 0) bin = 0;
            if (bin >= BinCount) bin = BinCount - 1;
            return bin;
        }

        public VolumeNode FindNearestLvn(double price)
        {
            if (Lvns == null || Lvns.Count == 0) return null;
            VolumeNode best = null;
            double bestDist = double.MaxValue;
            foreach (var n in Lvns)
            {
                double dist = Math.Abs(n.Mid - price);
                if (n.Contains(price))
                    return n;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = n;
                }
            }
            return best;
        }

        public VolumeNode FindNearestHvnBelow(double price)
        {
            if (Hvns == null || Hvns.Count == 0) return null;
            VolumeNode best = null;
            double bestHigh = double.MinValue;
            foreach (var n in Hvns)
            {
                if (n.High <= price + 1e-9 && n.High > bestHigh)
                {
                    bestHigh = n.High;
                    best = n;
                }
            }
            return best;
        }

        public VolumeNode FindNearestHvnAbove(double price)
        {
            if (Hvns == null || Hvns.Count == 0) return null;
            VolumeNode best = null;
            double bestLow = double.MaxValue;
            foreach (var n in Hvns)
            {
                if (n.Low >= price - 1e-9 && n.Low < bestLow)
                {
                    bestLow = n.Low;
                    best = n;
                }
            }
            return best;
        }
    }
}
