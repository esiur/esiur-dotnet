using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Fuzzy
{
    public interface IFuzzyEvaluator
    {

        public double[] Evaluate(double[] crispValues);
    }
}
