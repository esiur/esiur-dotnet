using Esiur.Data;
using Esiur.Misc;
using Esiur.Net.Sockets;
using Esiur.Resource;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Tests.RPC.Client
{
    public static class SignalRTest
    {
        public static async Task<TestResults> DoTest(string address,
            Dictionary<string, SharedModel.BusinessDocument[]> docsWorkloads,
            Dictionary<string, byte[]> dataWorkloads,
            Dictionary<string, int[]> intWorkloads)
        {

            var rt = new TestResults();

            using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
            mon.Start();

            Console.WriteLine($"\n== SignalR @ {address} ==");

            var service = new HubConnectionBuilder()
                    .WithUrl(address)
                    .WithAutomaticReconnect()
                    .Build();


            await service.StartAsync();


            Thread.Sleep(3000);

            var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

            Console.WriteLine($"Handshake {ctx}/{crx}");

            await Task.Delay(2000);

            foreach (var w in docsWorkloads)
            {
                Console.Write("Workload: " + w.Key);
                 await service.InvokeAsync("EchoDocuments", w.Value);


                //for (var i = 0; i < docs.Length; i++)
                //    if (!docs[i].Equals(w.Value[i]))
                //        throw new Exception("No match");


                await Task.Delay(3000);
                (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
                Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
                //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

                rt.Docs.Add(w.Key, (ctx, crx));

            }



            foreach (var w in dataWorkloads)
            {
                Console.Write("Bytes Workload: " + w.Key);
                 await service.InvokeAsync("EchoBytes", w.Value);


                //if (!w.Value.SequenceEqual(rt))
                //    throw new Exception("No match");


                await Task.Delay(3000);
                (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
                Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
                //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

                rt.Bytes.Add(w.Key, (ctx, crx));

            }


            foreach (var w in intWorkloads)
            {
                Console.Write("Ints Workload: " + w.Key);
                 await service.InvokeAsync("EchoIntArray", w.Value);


                //if (!w.Value.SequenceEqual(rt))
                //    throw new Exception("No match");


                await Task.Delay(3000);
                (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
                Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
                //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

                rt.Ints.Add(w.Key, (ctx, crx));

            }

            await Task.Delay(3000);

            (tx, rx) = mon.GetTotals();
            Console.WriteLine($"Transfer {tx}/{rx}");
            //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

            mon.Stop();

            return rt;
        }

        //public static async Task Do(string address)
        //{
        //    Console.WriteLine($"\n== Esiur @ {address} ==");

        //    var service = await Warehouse.Default.Get<RPC.Esiur.Service>(address);

        //    var workloads = DocGenerator.BuildWorkloads();

        //    foreach (var w in workloads)
        //    {
        //        Console.WriteLine("Workload: " + w.Item1);
        //        var rx = await service.EchoDocuments(w.Item2);

        //        for (var i = 0; i < rx.Length; i++)
        //            if (!rx[i].Equals(w.Item2[i]))
        //                throw new Exception("No match");
        //    }
        //}


    }
}
