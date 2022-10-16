using Esiur.Analysis.Statistics;
using Esiur.Analysis.Units;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Esiur.Analysis.Signals
{
    public static class Capacity
    {
        public struct CSI
        {
            public double SNR;
            public Probability Probability;

            public CSI(double snr, double probability)
            {
                SNR = snr;
                Probability = probability;
            }
        }

        public struct OutageCapacity
        {
            public BitRate Capacity;
            public double MinSNR;
            public Probability Outage;
            public double Bandwidth;

            public OutageCapacity(Probability outage, double minSNR, double bandwidth)
            {
                MinSNR = minSNR;
                Outage = outage;
                Bandwidth = bandwidth;
                Capacity = (1 - Outage) * bandwidth * Math.Log(1 + minSNR, 2);
            }

            public override string ToString() => $" {MinSNR.ToString("F")} <{Outage}> => {Capacity}";
        }

        public static double Compute(double bandwidth, double snr)
        => bandwidth * Math.Log(1 + snr, 2);


        public static double ComputeErgodic(double bandwidth, CSI[] receiverCSI)
        {
            return bandwidth * receiverCSI.Sum(x => Math.Log(1 + x.SNR, 2) * x.Probability);
        }

        public static OutageCapacity[] ComputeOutage(double bandwidth, CSI[] receiverCSI)
        {
            var sorted = receiverCSI.OrderBy(x => x.SNR);
            var rt = sorted.Select(x => {
                var pOut = receiverCSI.Where(csi => csi.SNR < x.SNR).Sum(x => x.Probability);
                return new OutageCapacity()
                {
                    Outage = pOut,
                    MinSNR = x.SNR,
                    Capacity = (1 - pOut) * bandwidth * Math.Log(1 + x.SNR, 2)
                };
            });

            return rt.ToArray();
        }
    }
}