using Esiur.Net.Sockets;
using Esiur.Resource;
using Esiur.Tests.RPC.EsiurServer;
using System.Diagnostics;

namespace Esiur.Tests.RPC.Client
{
    public static class EsiurTest
    {
        public static async Task<TestResults> DoTest(string address, 
            Dictionary<string, BusinessDocument[]> docsWorkloads, 
            Dictionary<string, byte[]> dataWorkloads, 
            Dictionary<string, int[]> intWorkloads )
        {

            var rt = new TestResults();

            using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
            mon.Start();

            Console.WriteLine($"\n== Esiur @ {address} ==");

            var service = await Warehouse.Default.Get<Service>(address);

            var sock = service.ResourceConnection.Socket as TcpSocket;


            Thread.Sleep(3000);

            var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

            Console.WriteLine($"Handshake {ctx}/{crx}");

            await Task.Delay(2000);

            foreach (var w in docsWorkloads)
            {
                Console.Write("Workload: " + w.Key);
                var docs = await service.EchoDocuments(w.Value);


                for (var i = 0; i < docs.Length; i++)
                    if (!docs[i].Equals(w.Value[i]))
                        throw new Exception("No match");


                await Task.Delay(3000);
                (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
                Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
                Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

                rt.Docs.Add(w.Key, (ctx, crx));
            }



            foreach (var w in dataWorkloads)
            {
                Console.Write("Bytes Workload: " + w.Key);
                var res = await service.EchoBytes(w.Value);


                if (!w.Value.SequenceEqual(res))
                    throw new Exception("No match");


                await Task.Delay(3000);
                (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
                Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
                Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

                rt.Bytes.Add(w.Key, (ctx, crx));

            }


            foreach (var w in intWorkloads)
            {
                Console.Write("Ints Workload: " + w.Key);
                var res = await service.EchoIntArray(w.Value);


                if (!w.Value.SequenceEqual(res))
                    throw new Exception("No match");


                await Task.Delay(3000);
                (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
                Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
                Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");


                rt.Ints.Add(w.Key, (ctx, crx));

            }

            await Task.Delay(3000);

            (tx, rx) = mon.GetTotals();
            Console.WriteLine($"Transfer {tx}/{rx}");
            Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

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
