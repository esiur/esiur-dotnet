using Esiur.Analysis.Neural;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Algebra
{

    public delegate double RealFunction(double x);
    public delegate double Real2Function(double x, double y);
    public delegate double Real3Function(double x, double y, double z);

    public class MathFunction<T>
    {
        public T Function { get; internal set; }
        public MathFunction<T> Derivative { get; internal set; }
        public MathFunction<T> Integral { get; internal set; }

        public MathFunction(T function, MathFunction<T> derivative, MathFunction<T> integral)
        {
            Function = function;
            Derivative = derivative;
            Integral = integral;
        }

     }


}
