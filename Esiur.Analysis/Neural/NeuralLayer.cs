using Esiur.Analysis.Algebra;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Neural
{
    internal class NeuralLayer
    {


        public Neuron[] Neurons { get; internal set; }

        public MathFunction<RealFunction> Activation { get; internal set; }

        public NeuralLayer? PreviousLayer { get; internal set; }
        public double Bias { get; set; }

        public NeuralLayer(int nodes, MathFunction<RealFunction> activation, NeuralLayer? previousLayer)
        {

            PreviousLayer = previousLayer;
            Neurons = new Neuron[nodes];
            Activation = activation;

            for(var i = 0; i < nodes; i++)
            {
                var synapses = new List<Synapse>();

                var neuron = new Neuron()
                {
                    Layer = this,
                };


                if (previousLayer != null)
                {
                    for(var j = 0; j < previousLayer.Neurons.Length; j++)
                    {
                        synapses.Add(new Synapse() { Source = previousLayer.Neurons[j] , Target = neuron, Weight = 0});
                    }
                }

                Neurons[i] = neuron;   
            }

        }
    }
}
