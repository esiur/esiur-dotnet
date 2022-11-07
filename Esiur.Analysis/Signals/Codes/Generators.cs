using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Signals.Codes
{
    public static class Generators
    {

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
