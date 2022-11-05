using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Schema;

namespace Esiur.Analysis.DSP
{
    public class TransferFunction
    {

        public double Step { get; set; }

        double[] inputs;
        double[] outputs;

        public double[] InputCoefficients { get; set; }
        public double[] OutputCoefficients { get; set; }

        public TransferFunction(double[] numerator, double[] denominator, double step = 0.01)
        {
            inputs = new double[numerator.Length];
            outputs = new double[denominator.Length];

            InputCoefficients = numerator.Reverse().ToArray();
            OutputCoefficients = denominator.Reverse().ToArray();

            Step = step;
        }

        public double Evaluate(double value)
        {
            var xs = new double[inputs.Length];
            xs[0] = value;// * InputCoefficients.Last();
            
            
            // diffrentiate
            for(var i = 1; i < xs.Length; i++)
                xs[i] = (xs[i - 1] - inputs[i - 1]) / Step;

            var ys = new double[outputs.Length];

            // integrate
            for (var i = outputs.Length - 2; i >= 0; i--)
            {
                var iy = outputs[i] + (Step * outputs[i + 1]);
                if (double.IsNaN(iy) || double.IsInfinity(iy))
                    ys[i] = outputs[i];
                else
                    ys[i] = iy;
            }

            var v = xs.Zip(InputCoefficients, (x, c) => x * c).Sum() - ys.Zip(OutputCoefficients, (y, c) => y * c).Sum();
            
            if (double.IsNaN(v) || double.IsInfinity(v))
                ys[ys.Length - 1] = outputs[ys.Length - 1];
            else
                ys[ys.Length - 1] = v;

            inputs = xs;
            outputs = ys;

            return ys[0];
        }

        public double[] Evaluate(double[] value)
        {
            return value.Select(x => Evaluate(x)).ToArray();
        }
    }
}
