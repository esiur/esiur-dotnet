using Esiur.Analysis.Units;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Statistics
{
    public struct Probability
    {
        public double Value;

        public static implicit operator Probability(double v) => new Probability(v);
        public static implicit operator double(Probability v) => v.Value;

        
        public Probability(double value)
        {
            if (value > 1 || value < 0)
                throw new Exception("0 >= Probability <= 1");

            Value = value;
        }

        public double ToPercentage() => Value * 100;
        public Probability FromPercentage(double value) => new Probability(value / 100);

        public override string ToString()
        {
            return (Math.Round(Value * 10000) / 100) + "%";
        }

        public Probability Power(double exponent)
        {
            return Math.Pow(Value, exponent);
        }

        public Probability Inverse()
        {
            return 1 - Value;
        }
    }
}
