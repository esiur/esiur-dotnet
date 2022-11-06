using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Fuzzy
{
    public class DiscreteSet : INumericalSet<double>, IEnumerable<KeyValuePair<double, double>>
    {
        Dictionary<double, double> vector = new Dictionary<double, double>();

        public double this[double index]
        {
            get => vector.ContainsKey(index) ? vector[index] : 0;
            set
            {
                if (vector.ContainsKey(index))
                    vector[index] = value;
                else
                    vector.Add(index, value);
            }
        }

        public double AlphaCut { get; set; }



        public INumericalSet<double> Intersection(INumericalSet<double> with)
        {
            return new OperationSet(Operation.Intersection, new INumericalSet<double>[] { this, with });
        }

        public INumericalSet<double> Union(INumericalSet<double> with)
        {
            return new OperationSet(Operation.Union, new INumericalSet<double>[] { this, with });
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return vector.GetEnumerator();
        }

        IEnumerator<KeyValuePair<double, double>> IEnumerable<KeyValuePair<double, double>>.GetEnumerator()
        {
            return vector.GetEnumerator();
        }

        public KeyValuePair<double, double>[] Maximas
        {
            get
            {
                var max = vector.Values.Max();
                return vector.Where(x => x.Value == max).ToArray();
            }
        }


        public double Integral(double from, double to)
        {
            return vector.Where(x => x.Key >= from && x.Key <= to).Sum(x => x.Value);
        }

        public double Centroid(double from, double to)
        {
            var r = vector.Where(x => x.Key >= from && x.Key <= to).ToArray();

            var total = r.Sum(x => x.Value);
            if (total == 0)
                return 0;
            else
                return r.Sum(x => x.Key * x.Value ) / total;
        }

        public KeyValuePair<double, double>[] Minimas
        {
            get
            {
                var min = vector.Values.Min();
                return vector.Where(x => x.Value == min).ToArray();
            }
        }

        public double[] ToArray() => vector.Values.ToArray();
    }
}
