using System;
using System.Collections;
using System.Collections.Generic;
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
            get {
                var max = vector.Values.Max();
                return vector.Where(x => x.Value == max).ToArray();
            }
        }

        public KeyValuePair<double, double>[] Minimas
        {
            get
            {
                var min = vector.Values.Min();
                return vector.Where(x => x.Value == min).ToArray();
            }
        }

    }
}
