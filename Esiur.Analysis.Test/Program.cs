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

using System.Diagnostics;
using System.Formats.Asn1;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using CsvHelper;
using Esiur.Analysis.Coding;
using Esiur.Analysis.DSP;
using Esiur.Analysis.Graph;
using Esiur.Analysis.Scheduling;
using Esiur.Analysis.Signals;
using Esiur.Analysis.Signals.Codes;
using Esiur.Data;
using Esiur.Resource;
using ScottPlot.Statistics.Interpolation;
using Process = Esiur.Analysis.Scheduling.Process;
using Esiur.Analysis.Queueing;

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


            var queues = new Queue[] { new Queue() {  ServiceRate = 2, VisitRatio = 1 },
                                     new Queue() { Servers = 1 , ServiceRate = 1, VisitRatio = 0.2 },
                                     new Queue(){Servers = 1, ServiceRate = 2, VisitRatio = 0.3 },
                                     new Queue(){Servers = 1, ServiceRate = 4, VisitRatio = 0.5}};

            var mva = new MeanValueAnalysis(queues);

            mva.Process(10);

            //using (var reader = new StreamReader("c:\\cd\\s2.csv"))
            //using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            //{
            //    var records = csv.GetRecords<dynamic>();

            //    var procs = records.Select(x =>
            //    {
            //        return new Process()
            //        {
            //            Arrival = Convert.ToInt32(x.arrival),
            //            Burst = Convert.ToInt32(x.wc) * 0.5,
            //            Priority = x.label == "high" ? 0 : 1,
            //            Title = x.subject
            //        };
            //    }).ToArray();


            //    var processes = Esiur.Analysis.Scheduling.SPF.ScheduleHybrid(procs);

            //    var class0 = processes.Where(x => x.Priority == 0).ToArray();
            //    var class1 = processes.Where(x => x.Priority == 1).ToArray();

            //    var waitTime = processes.Sum(x => x.WaitTime) / processes.Length;
            //    var class0Time = class0.Sum(x => x.WaitTime) / class0.Length;
            //    var class1Time = class1.Sum(x => x.WaitTime) / class1.Length;

            //    Console.WriteLine(waitTime.ToString());
            //}





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
            Application.Run(new FGraph());
        }
    }
}