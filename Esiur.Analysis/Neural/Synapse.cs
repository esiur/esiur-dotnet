using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Neural
{
    internal class Synapse
    {
        public double Weight { get; set; }

        public Neuron Source { get; set; }
        public Neuron Target { get; set; }
    }
}
