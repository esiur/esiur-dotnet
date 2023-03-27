using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Esiur.Analysis.Coding;
using Esiur.Analysis.DSP;
using Esiur.Analysis.Signals;
/*
 
Copyright (c) 2022 Ahmed Kh. Zamil

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

using Esiur.Analysis.Signals.Codes;
using Esiur.Data;
using Esiur.Resource;

namespace Esiur.Analysis.Test
{
    internal static class Program
    {
        private const int V = -1;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            var msg = Encoding.ASCII.GetBytes("A_DEAD_DAD_CEDED_A_BAD_BABE_A_BEADED_ABACA_BED").Select(x => CodeWord<Base2>.FromByte(x)).ToArray();// <Base2>());

            // convert msg to codewords
            var codec = new Huffman<Base2>(msg, 0, (uint)msg.Length);

            var enc = codec.Encode(msg, 0, (uint) msg.Length);

            var dec = codec.Decode(enc, 0, (uint)enc.Length);

            //var code = codec.Encode();
            //var ds = codec.DecisionTree.Decide(new bool[] { true, true, true, true }, 0);
            
            Console.WriteLine();

            var f = Esiur.Analysis.Algebra.Functions.Sigmoid;

            var signalA = new double[] { V,1, V, 1 , V, V, V };
            var signalB = new double[] { V, V, 1, V, V, 1, V };
             var cor = signalA.CrossCorrelation(signalB, true);
            Debug.WriteLine(cor);

            var seq = Generators.GenerateSequence(1, 7);

            var res = Capacity.ComputeOutage(1, new Capacity.CSI[]
   {
                new Capacity.CSI(0.1, 0.1),
                new Capacity.CSI(1, 0.2),
                new Capacity.CSI(3.16, 0.3),
                new Capacity.CSI(10, 0.4),
   });
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new FSoft());
        }
    }
}