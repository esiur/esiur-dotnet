using System;
using System.Collections.Generic;
using System.Drawing;
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
            {
                rt += (x[i] - X) * (y[i] - Y);
                rt += (x[i] * y[i] - Y * x[i] - X * y[i] + X * Y);//  - X) * (y[i] - Y);
            }
            
            return rt / n;
        }


        public static double RMS(this double[] x)
        {
            var r = Math.Sqrt(x.Sum(x => (float)x * (float)x) / x.Length);
            //if (double.IsNaN(r) || double.IsInfinity(r))
            //  Console.WriteLine();
            return r;
        }


        public static double Correlation(this double[] x, double[] y)
        {
            return Covariance(x, y) / (StdDiv(x) * StdDiv(y));
        }

        public static double Distance(this PointF from, PointF to)
        {
            return Math.Sqrt(Math.Pow(from.X - to.X, 2) + Math.Pow(from.Y - to.Y, 2));
        }

        class KClass
        {
            public int Id;
            public PointF Center;

            public override string ToString()
            {
                return $"C{Id} <{Center.X}, {Center.Y}>";
            }
        }

        public static Dictionary<string, PointF> KMean(PointF[] seeds, PointF[] points)
        {
            // calculate distance


            var classes = new Dictionary<KClass, List<PointF>>();
            for (var i = 0; i < seeds.Length; i++)
                classes.Add(new KClass() { Id = i+1, Center = seeds[i] }, new List<PointF>());

            while (true)
            {

                foreach (var point in points)
                {
                    var cls = classes.Keys.OrderBy(x => x.Center.Distance(point)).First(); 
                    classes[cls].Add(point);
                }

                // update center
                foreach(var kv in classes)
                {
                    kv.Key.Center = new PointF() { X = kv.Value.Average(p => p.X), Y = kv.Value.Average(p => p.Y) };
                    kv.Value.Clear();
                }

            }
        }

    }
}
