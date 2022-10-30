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
        MeanOfMaxima,
    }
    public class MamdaniDefuzzifier
    {

        public static double Evaluate(INumericalSet<double>[] sets, MamdaniDefuzzifierMethod method, double from, double to, double step)
        {

            var union = sets.FuzzyUnion();
            var output = union.ToDiscrete(from, to, step);

            if (method == MamdaniDefuzzifierMethod.CenterOfGravity)
                return output.Centroid(from, to);
            else if (method == MamdaniDefuzzifierMethod.FirstMaxima)
                return output.Maximas.First().Key;
            else if (method == MamdaniDefuzzifierMethod.LastMaxima)
                return output.Maximas.Last().Key;
            else if (method == MamdaniDefuzzifierMethod.MeanOfMaxima)
            {
                var max = output.Maximas;
                return max.First().Key + ((max.Last().Key - max.First().Key) / 2);
            }
            else
                throw new Exception("Unknown method");
         }

    }
}
