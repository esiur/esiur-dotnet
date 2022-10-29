using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Fuzzy
{
    public class FuzzyRule
    {
        public string Name { get; set; }

        public FuzzySet InputSet { get; set; }
        public FuzzySet OutputSet { get; set; }

        public FuzzyRule(string name, FuzzySet input, FuzzySet output)
        {
            Name = name;
            InputSet = input;
            OutputSet = output;
        }

        public FuzzySet Evaluate(double input)
        {
            var val = InputSet[input];
            var results = new FuzzySet(OutputSet.Function) { AlphaCut = OutputSet.AlphaCut < val ? OutputSet.AlphaCut : val };
            return results;
        }
    }
}
