using Esiur.Net.Sockets;
using Esiur.Resource;
using Esiur.Tests.RPC.Client.Grpc;
using Google.Protobuf;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Esiur.Tests.RPC.Client;

public class GrpcTest
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
        mon.Start();

        Console.WriteLine($"\n== Grpc @ {address} ==");

        using var channel = GrpcChannel.ForAddress(address);
        var service = new Client.Grpc.EchoService.EchoServiceClient(channel);


        Thread.Sleep(warmupDelayMs);

        var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

        Console.WriteLine($"Handshake {ctx}/{crx}");

        await Task.Delay(postHandshakeDelayMs);

        foreach (var w in docsWorkloads)
        {
            Console.Write("Workload: " + w.Key);
            var rd = new DocumentsRequest();
            rd.Docs.AddRange(w.Value);
            var docs = await service.EchoDocumentsAsync(rd);


            //for (var i = 0; i < docs.Length; i++)
            //    if (!docs[i].Equals(w.Value[i]))
            //        throw new Exception("No match");


            await Task.Delay(sampleDelayMs);
            (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
            Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");

            rt.Docs.Add(w.Key, (ctx, crx));

        }



        foreach (var w in dataWorkloads)
        {
            Console.Write("Bytes Workload: " + w.Key);

            var br = new BytesRequest() { Data = ByteString.CopyFrom(w.Value) };
            var res = await service.EchoBytesAsync(br);


            //if (!w.Value.SequenceEqual(rt))
            //    throw new Exception("No match");


            await Task.Delay(sampleDelayMs);
            (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
            Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
            //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

            rt.Bytes.Add(w.Key, (ctx, crx));

        }


        foreach (var w in intWorkloads)
        {
            Console.Write("Ints Workload: " + w.Key);

            var ir = new IntArrayRequest();
            ir.Array.AddRange(w.Value);

            var res = await service.EchoIntArrayAsync(ir);


            //if (!w.Value.SequenceEqual(rt))
            //    throw new Exception("No match");


            await Task.Delay(sampleDelayMs);
            (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
            Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
            //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

            rt.Ints.Add(w.Key, (ctx, crx));

        }

        await Task.Delay(sampleDelayMs);

        (tx, rx) = mon.GetTotals();
        Console.WriteLine($"Transfer {tx}/{rx}");
        //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

        mon.Stop();

        return rt;
    }
}
