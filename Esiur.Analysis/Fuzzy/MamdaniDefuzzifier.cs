using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Fuzzy
{
    public enum MamdaniDefuzzifierMethod
    {
        CenterOfGravity,
        FirstMaxima,
        LastMaxima,
        Bisector,
        MeanOfMaxima,
    }
    public class MamdaniDefuzzifier
    {

        public static double Evaluate(INumericalSet<double>[] sets, MamdaniDefuzzifierMethod method, double from, double to, double step)
        {

            var union = sets.FuzzyUnion();
            var output = union.ToDiscrete(from, to, step);
            var max = output.Maximas;
            return max[0].Key;

         }

    }
}
