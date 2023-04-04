using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Coding
{
    public static class Functions
    {
        public static double Entropy(int[] frequencies)
        {
            double total = frequencies.Sum();

            return frequencies.Sum(x => ((double)x / total * -Log2(x)));
        }

        public static double AverageLength<T>(this CodeWord<T>[] words)
        {
            return words.Sum(x => x.Length) / (double)words.Length;
        }

        public static double Log2(double value) => Math.Log10(value) / Math.Log10(2);
    }
}
