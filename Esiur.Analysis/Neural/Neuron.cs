using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Neural
{
    internal class Neuron
    {
        public double Value { get; set; }
        public NeuralLayer Layer { get; set; }

        public List<Synapse> Synapses { get; set; } = new List<Synapse>();

        public void Forward()
        {
            var sum = Synapses.Sum(x => x.Weight * x.Source.Value);
            Value = Layer.Activation.Function(sum + Layer.PreviousLayer?.Bias ?? 0);
        }
    }
}
