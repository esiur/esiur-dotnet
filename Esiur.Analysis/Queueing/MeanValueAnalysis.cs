/*
 
Copyright (c) 2023 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Esiur.Analysis.Queueing
{
    public class MeanValueAnalysis
    {

        public Queue[] Nodes { get; set; }

        public MeanValueAnalysis(Queue[] queues)
        {
            Nodes = queues;
        }

        public double NormalizationConstant { get; set; }

        public class MVAResult
        {
            public Queue Node { get; set; }

            public double MeanNumberOfCustomers { get; set; }

            public double MeanResponseTime { get; set; }

            public double Throughput { get; set; }

            public double NormalizationConstant { get; set; }
        }
        
        public MVAResult[] Process(int customers)
        {

            var R = new double[Nodes.Length];
            var L = new double[Nodes.Length];
            var X = 0.0;
            var G = 1.0;

            // initialize 

            for (var k = 1; k <= customers; k++)
            {
                X = 0;

                // Compute response time R
                for (var i = 0; i < Nodes.Length; i++)
                {
                    if (k == 1 || Nodes[i].Servers == 0)
                        R[i] = (1 / Nodes[i].ServiceRate);
                    else if (Nodes[i].Servers == 1)
                        R[i] = (1 / Nodes[i].ServiceRate) * (1 + L[i]);

                    // Compute throughput X
                    X += R[i] * Nodes[i].VisitRatio;

                }

                X = k / X;

                // Compute normalization factor G

                G = G / X;

                // Compute mean number of customers L
                for (var i = 0; i < Nodes.Length; i++)
                    L[i] = X * Nodes[i].VisitRatio * R[i];


                //Debug.Print(String.Join(" ", R));

            }

            var rt = new MVAResult[Nodes.Length];
            for (var i = 0; i < Nodes.Length; i++)
                rt[i] = new MVAResult() { MeanNumberOfCustomers = L[i], MeanResponseTime = R[i], Node = Nodes[i], NormalizationConstant = G, Throughput = L[i] / R[i] };
            //Console.WriteLine(X);

            return rt;
        }
    }
}