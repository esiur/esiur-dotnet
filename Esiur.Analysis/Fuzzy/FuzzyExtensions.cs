using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Analysis.Fuzzy
{
    public static class FuzzyExtensions
    {

        public static double Or(this double value, double orValue) => value > orValue ? value : orValue;
        public static double And(this double value, double orValue) => value < orValue ? value : orValue;

        public static double Is(this double value, INumericalSet<double> set) => set[value];

        // Mamdani
        public static ContinuousSet Then(this double value, ContinuousSet set) 
            => new ContinuousSet(set.Function) { AlphaCut = value };// set.AlphaCut < value ? set.AlphaCut : value };

        // TKS
        public static double Then(this double value, double constant)
            => value * constant;


        public static INumericalSet<double> FuzzyUnion(this INumericalSet<double>[] sets)
        {
            return new OperationSet(Operation.Union, sets);
        }

        public static INumericalSet<double> FuzzyIntersection(this INumericalSet<double> sets)
        {
            return new OperationSet(Operation.Intersection, sets);

        }

        public static DiscreteSet ToDiscrete(this INumericalSet<double> set, double from, double to, double step)
        {
            var rt = new DiscreteSet();
            for (var x = from; x <= to; x += step)
                rt[x] = set[x];

            return rt;
        }

        public static double[] Sample(this INumericalSet<double> set, double[] time)
        {
            var rt = new double[time.Length];
            for (var i = 0; i < time.Length; i++)
                rt[i] = set[time[i]];
            return rt;
        }

        public static double[] Sample(this INumericalSet<double> set, double from, double to, double step)
        {
            var size = (int)((to - from) / step);

            var rt = new double[size];
            var s = 0;
            for (var i = from; i < to && s < size; i+=step)
                rt[s++] = set[i];
            return rt;

        }

        public static double[] Range(double from, double to, double step)
        {
            var size = (int)((to - from) / step);

            if (size == 0)
                return new double[] { from };

            var rt = new double[size];
            var s = 0;
            for (var i = from; i < to && s < size; i += step)
                rt[s++] = i;
            return rt;

        }


    }
}
