using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Statistics
{
    public static class Distributions
    {


        public static int Factorial(this int n)
        {
            var rt = 1;
            while (n > 1)
                rt *= n--;
            return rt;
        }

        public static int Permutation(int n, int k)
        {
            var rt = 1;
            var l = n - k;
            while (n > l)
                rt *= n--;
            return n;
        }

        public static int Combination(int n, int k)
        {
            var rt = 1;
            while (n > k)
                rt *= n--;

            rt /= Factorial(n - k);

            return rt;
        }

        public static Probability Binomial(Probability p, int n, int x)
        {
            return Combination(n, x) * p.Power(x) * (1 - p.Power(n - x));
        }

        public static Probability Poisson(double l, int x)
        {
            return Math.Exp(-l) * Math.Pow(l, x) / Factorial(x);
        }

        public static Probability Geometric(Probability p, int x)
        {
            // joint probability of failures and one success
            return p.Inverse().Power(x - 1) * p;
        }
    }
}
