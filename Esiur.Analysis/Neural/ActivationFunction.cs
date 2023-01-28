using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Neural
{
    public delegate double ActivationFunction(double value);

    public static class ActivationFunctions
    {

        public static (ActivationFunction, ActivationFunction) Sigmoid()
        {
            return (new ActivationFunction(x =>
            {
                float k = (float)Math.Exp(x);
                return k / (1.0 + k);
            }),
              new ActivationFunction(x =>
             {
                 return x * (1 - x);
             }));
        }

        public static ActivationFunction Tanh()
        {
            return new ActivationFunction(x =>
            {
                return Math.Tanh(x);
            });
        }


        public static ActivationFunction ReLU()
        {
            return new ActivationFunction(x =>
            {
                return (0 >= x) ? 0 : x;
            });
        }

        public static ActivationFunction LeakyReLU()
        {
            return new ActivationFunction(x =>
            {
                return (0 >= x) ? 0.01 * x : x;
            });
        }

        public static ActivationFunction SigmoidDer()
        {
            return new ActivationFunction(x =>
            {
                return x * (1 - x);
            });
        }

        public static ActivationFunction TanhDer()
        {
            return new ActivationFunction(x =>
            {
                return 1 - (x * x);
            });
        }

        public static ActivationFunction ReLUDer()
        {
            return new ActivationFunction(x =>
            {
                return (0 >= x) ? 0 : 1;
            });
        }

        public static ActivationFunction LeakyReLUDer()
        {
            return new ActivationFunction(x =>
            {
                return (0 >= x) ? 0.01f : 1;
            });
        }



        
    }

}
