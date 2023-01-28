using Esiur.Analysis.Neural;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Algebra
{

    public static class Functions
    {
        private static MathFunction<RealFunction> SigmoidDerivative
            = new MathFunction<RealFunction>(new RealFunction(x => x * (1 - x)), null, null);

        public static MathFunction<RealFunction> Sigmoid = new MathFunction<RealFunction>(new RealFunction(x =>
        {
            double k = Math.Exp(x);
            return k / (1.0 + k);
        }), SigmoidDerivative, null);


        private static MathFunction<RealFunction> TanhDerivative = new MathFunction<RealFunction>(
                new RealFunction(x => 1 - (x * x)), null, null);

        public static MathFunction<RealFunction> Tanh = new MathFunction<RealFunction>(
            new RealFunction(x => Math.Tanh(x)), TanhDerivative, null);


        private static MathFunction<RealFunction> ReLUDerivative = new MathFunction<RealFunction>(
        new RealFunction(x => (0 >= x) ? 0 : 1), null, null);

        public static MathFunction<RealFunction> ReLU = new MathFunction<RealFunction>(
            new RealFunction(x => (0 >= x) ? 0 : x), ReLUDerivative, null);


        private static MathFunction<RealFunction> LeakyReLUDerivative = new MathFunction<RealFunction>(
            new RealFunction(x => (0 >= x) ? 0.01 : 1), null, null);

        public static MathFunction<RealFunction> LeakyReLU = new MathFunction<RealFunction>(
            new RealFunction(x => (0 >= x) ? 0.01 * x : x), LeakyReLUDerivative, null);

    }
}