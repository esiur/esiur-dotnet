using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Signals.Codes
{
    public static class Generators
    {

        public static void SetBit(ref this int target, int index, bool value)
        {
            if (value)
                target |= 0x1 << index;
            else if ((target & (0x1 << index)) != 0)
                target ^= 0x1 << index;
        }

        public static double[] GenerateSequence(int initialValue, uint octalPolynomialCoefficients)
        {
            // convert octal to uint
            var bits = Convert.ToUInt32(octalPolynomialCoefficients.ToString(), 8);
            var taps = new List<uint>();

            // find maximum exponent
            var maxExponent = 0;
            for (var i = 31; i >= 0; i--)
            {
                var test = (uint)0x1 << i;
                if ((bits & test) != 0)
                {
                    taps.Add(test);
                    maxExponent = i;
                    break;
                }
            }

            var length = (int)(Math.Pow(2, maxExponent) - 1);

            var rt = new double[length];

            var state = initialValue;

            for(var i  = 0; i < length; i++)
            {
                //rt[i] = (state & 0x1) == 1 ? 1 : -1;

                var b = 0;
                for (var j = 0; j < taps.Count; j++)
                    b = (b + ((taps[j] & state) > 0 ? 1 : 0)) % 2;

                state >>= 1;

                state.SetBit(maxExponent, b > 0);

                rt[i] = b == 0 ? -1 : 1;
            }

            return rt;
        }

        //public double[][] GenerateMaximumLengthSequence(uint octalPolynomialCoefficients)
        //{
        //    // convert octal to uint
        //    var bits = Convert.ToUInt32(octalPolynomialCoefficients.ToString(), 8);

        //    var taps = new List<int>();

        //    // find maximum exponent
        //    var maxExponent = 0;
        //    for (var i = 31; i >= 0; i--)
        //        if ((bits & (0x1 << i)) != 0)
        //        {
        //            maxExponent = i;
        //            taps.Add(i);
        //            break;
        //        }

        //    // make taps

        //    for (var i = 0; i < maxExponent; i++)
        //        if (((bits & (0x1 << i)) != 0))
        //            taps.Add(maxExponent - i);


        //    var startState = 1 <<  | 1;

        //    startState[length - 1] = 1;

        //    while (true)
        //    {

        //    }

        //    var rt = new double[length][];

        //    for (var i = 0; i < length; i++)
        //    {
        //        rt[i] = new double[length];

        //        for (var j = 0; j < maxExponent; j++)
        //        {
        //            rt[i] = coefficients[j];
        //        }
        //    }

        //    return rt;
        //}
    }
}
