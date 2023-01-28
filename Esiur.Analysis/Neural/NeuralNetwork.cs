using Esiur.Analysis.Algebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Neural
{
    public class NeuralNetwork
    {
        NeuralLayer[] neuralLayers;

        public NeuralNetwork(int[] layers, MathFunction<RealFunction>[] activations)
        {
            neuralLayers = new NeuralLayer[layers.Length];

            for (var i = 0; i < layers.Length; i++)
                neuralLayers[i] = new NeuralLayer(layers[i], activations[i], i == 0 ? null : neuralLayers[i-1]);
        }

        public double[] FeedForward(double[] input)
        {
            for (int i = 0; i < input.Length; i++)
            {
                neuralLayers[0].Neurons[i].Value = input[i];
            }

            for (int i = 1; i < neuralLayers.Length; i++)
            {
                for (int j = 0; j < neuralLayers[i].Neurons.Length; j++)
                {
                    neuralLayers[i].Neurons[j].Forward();
                }
            }

            return neuralLayers.Last().Neurons.Select(x => x.Value).ToArray();
        }



        public void BackPropagate(double[] input, double[] target)
        {
            var output = FeedForward(input);

            // total error (square error function)
            double totalError = 0.5 * output.Zip(target, (x, y) => Math.Pow(x - y, 2)).Sum() ;


            // calculate partial derivitave of E-Total with respect to weights dE/dW = dE/dOF * dOF/dO *  dO/dW

           for(var i = 0; i < target.Length; i++)
            {
                var z = -(target[i] - output[i]) * 
            }
            //for (int i = 0; i < output.Length; i++) 
            //    totalError += (float)Math.Pow(output[i] - expected[i], 2);//calculated cost of network

            //totalError /= 2; //this value is not used in calculions, rather used to identify the performance of the network



            var gamma = neuralLayers.Select(x => x.Neurons.Select(n => n.Value).ToArray()).ToArray();

 
            int layer = layers.Length - 2;

            for (int i = 0; i < output.Length; i++) gamma[layers.Length - 1][i] = (output[i] - expected[i]) * activateDer(output[i], layer);//Gamma calculation
            for (int i = 0; i < layers[layers.Length - 1]; i++)//calculates the w' and b' for the last layer in the network
            {
                biases[layers.Length - 2][i] -= gamma[layers.Length - 1][i] * learningRate;
                for (int j = 0; j < layers[layers.Length - 2]; j++)
                {

                    weights[layers.Length - 2][i][j] -= gamma[layers.Length - 1][i] * neurons[layers.Length - 2][j] * learningRate;//*learning 
                }
            }

            for (int i = layers.Length - 2; i > 0; i--)//runs on all hidden layers
            {
                layer = i - 1;
                for (int j = 0; j < layers[i]; j++)//outputs
                {
                    gamma[i][j] = 0;
                    for (int k = 0; k < gamma[i + 1].Length; k++)
                    {
                        gamma[i][j] += gamma[i + 1][k] * weights[i][k][j];
                    }
                    gamma[i][j] *= activateDer(neurons[i][j], layer);//calculate gamma
                }
                for (int j = 0; j < layers[i]; j++)//itterate over outputs of layer
                {
                    biases[i - 1][j] -= gamma[i][j] * learningRate;//modify biases of network
                    for (int k = 0; k < layers[i - 1]; k++)//itterate over inputs to layer
                    {
                        weights[i - 1][j][k] -= gamma[i][j] * neurons[i - 1][k] * learningRate;//modify weights of network
                    }
                }
            }
        }
    }
}
