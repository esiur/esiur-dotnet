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
            Dictionary<string, int[]> intWorkloads,
            int warmupDelayMs = 3000,
            int postHandshakeDelayMs = 2000,
            int sampleDelayMs = 3000)
        {

            var rt = new TestResults();

            using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
            //mon.Start();

            Console.WriteLine($"\n== Esiur @ {address} ==");

            // register proxy types
            Initialization.RegisterTypes(Warehouse.Default);

            var service = await Warehouse.Default.Get<Service>(address);

            var sock = service.ResourceConnection.Socket as TcpSocket;


            Thread.Sleep(warmupDelayMs);

            var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

            Console.WriteLine($"Handshake {ctx}/{crx}");

            await Task.Delay(postHandshakeDelayMs);

            foreach (var w in docsWorkloads)
            {
                Console.Write("Workload: " + w.Key);
                var docs = await service.EchoDocuments(w.Value);


                for (var i = 0; i < docs.Length; i++)
                    if (!docs[i].Equals(w.Value[i]))
                        throw new Exception("No match");


                await Task.Delay(sampleDelayMs);
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


                await Task.Delay(sampleDelayMs);
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


                await Task.Delay(sampleDelayMs);
                (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
                Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
                Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");


                rt.Ints.Add(w.Key, (ctx, crx));

            }

            await Task.Delay(sampleDelayMs);

            (tx, rx) = mon.GetTotals();
            Console.WriteLine($"Transfer {tx}/{rx}");
            Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

            mon.Stop();

            return rt;
        }

  

    }
}
