using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Analysis.DSP;

namespace Esiur.Analysis.DSP
{
    public static class Functions
    {
        public static double[] MultipleConvolution(params double[][] signals)
        {
            var rt = signals[0];

            for (var i = 1; i < signals.Length; i++)
                rt = rt.Convolution(signals[i]);

            return rt;
        }
        public static double[] Convolution(this double[] signal, double[] filter)
        {
            var length = signal.Length + filter.Length - 1;
            var rt = new double[length];

            //for (var i = 0; i < signal.Length; i++)
            //    for (var j = 0; j < filter.Length; j++)
            //        rt[i + j] += signal[i] * filter[j];

            for (var i = 0; i < length; i++)
            {

                for (var j = 0; j < signal.Length; j++)
                {
                    if (i - j >= 0 && i - j < filter.Length)
                        rt[i] += signal[j] * filter[i - j];
                }
            }

            return rt;
        }

        public static double[] CrossCorrelation(this double[] signal, double[] filter, bool cyclic = false)
        {

            if (cyclic)
            {
                var length = signal.Length + filter.Length - 1;
                var rt = new double[length];

                for (var i = 0; i < length; i++)
                {
                    for (var j = 0; j < signal.Length; j++)
                    {
                       rt[i] += signal[j] * filter[(i + j) % filter.Length];
                    }
                }

                return rt;

            }
            else
            {
                var length = signal.Length + filter.Length - 1;
                var rt = new double[length];

                for (var i = 0; i < length; i++)
                {
                    for (var j = 0; j < signal.Length; j++)
                    {
                        if (i + j < filter.Length)
                            rt[i] += signal[j] * filter[i + j];
                    }
                }

                return rt;
            }
        }

        public static double[] AutoCorrelation(this double[] signal, bool cyclic = false)
        {
            return signal.CrossCorrelation(signal, cyclic);
        }
    }
}
