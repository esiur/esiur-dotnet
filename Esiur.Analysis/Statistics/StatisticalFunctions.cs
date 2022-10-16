using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Statistics
{
    public static class Statistics
    {
        public static double Mean(this double[] values) => values.Sum() / values.Length;

        public static double Variance(this double[] x)
        {
            var mean = x.Mean();
            return x.Sum(x => Math.Pow(x - mean, 2)) / x.Length;
        }


        public static double StdDiv(this double[] x) => Math.Sqrt(x.Variance());

        public static double Covariance(this double[] x, double[] y)
        {
            var n = x.Length < y.Length ? x.Length : y.Length;
            var X = x.Mean();
            var Y = y.Mean();

            double rt = 0;

            for (var i = 0; i < n; i++)
                rt += (x[i] - X) * (y[i] - Y);

            return rt / n;
        }


        public static double RMS(this double[] x) => Math.Sqrt(x.Sum(x => x * x) / x.Length);


        public static double Correlation(this double[] x, double[] y)
        {
            return x.Covariance(y) / (x.StdDiv() * y.StdDiv());
        }
    }
}
