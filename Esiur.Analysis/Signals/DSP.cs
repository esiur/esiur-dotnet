using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Signals
{
    public static class DSP
    {
        public static double[] ConvolveMany(params double[][] signals)
        {
            var rt = signals[0];

            for (var i = 1; i < signals.Length; i++)
                rt = rt.Convolve(signals[i]);

            return rt;
        }

        public static double[] Convolve(this double[] signal, double[] filter)
        {
            var length = signal.Length + filter.Length - 1;
            var rt = new double[length];

            //for (var i = 0; i < signal.Length; i++)
            //    for (var j = 0; j < filter.Length; j++)
            //        rt[i + j] += signal[i] * filter[j];

            for (var i = 0; i < length; i++)
            {
                for (var j = 0; j < signal.Length && i - j >= 0 && i - j < filter.Length; j++)
                {
                    rt[i] = signal[j] * filter[i - j];
                }
            }

            return rt;
        }

        public static double[] CrossCorrelate(this double[] signal, double[] filter)
        {
            var length = signal.Length + filter.Length - 1;
            var rt = new double[length];
            for (var i = 0; i < length; i++)
            {
                for (var j = 0; j < signal.Length && j + i < filter.Length; j++)
                {
                    rt[i] = signal[j] * filter[i + j];
                }
            }

            return rt;
        }

    }
}
