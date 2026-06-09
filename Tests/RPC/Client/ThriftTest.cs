using Echo.ThriftModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Esiur.Tests.RPC.Client;

public class ThriftTest
{

    public static async Task<TestResults> DoTest(string host, int port,
    Dictionary<string, BusinessDocument[]> docsWorkloads,
    //Dictionary<string, byte[]> dataWorkloads,
    //Dictionary<string, int[]> intWorkloads,
    int warmupDelayMs = 3000,
    int postHandshakeDelayMs = 2000,
    int sampleDelayMs = 3000)
    {
        var rt = new TestResults();

        using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
        mon.Start();

        Console.WriteLine($"\n== Thrift @ {host} ==");


        using var socket = new Thrift.Transport.Client.TSocketTransport(host, port, new Thrift.TConfiguration());
        //await socket.OpenAsync(new CancellationToken());
        var proto = new Thrift.Protocol.TBinaryProtocol(socket);
        var service = new EchoService.Client(proto);


        Thread.Sleep(warmupDelayMs);

        var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

        Console.WriteLine($"Handshake {ctx}/{crx}");


        await Task.Delay(postHandshakeDelayMs);

        foreach (var w in docsWorkloads)
        {
            Console.Write("Workload: " + w.Key);
            var docs = await service.EchoDocuments(w.Value.ToList());


            //for (var i = 0; i < docs.Length; i++)
            //    if (!docs[i].Equals(w.Value[i]))
            //        throw new Exception("No match");


            await Task.Delay(sampleDelayMs);
            (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
            Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");

            rt.Docs.Add(w.Key, (ctx, crx));
        }



        //foreach (var w in dataWorkloads)
        //{
        //    Console.Write("Bytes Workload: " + w.Key);

        //    var res = await service.EchoBytes(w.Value);


        //    if (!w.Value.SequenceEqual(res))
        //        throw new Exception("No match");


        //    await Task.Delay(sampleDelayMs);
        //    (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
        //    Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
        //    //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

        //    rt.Bytes.Add(w.Key, (ctx, crx));

        //}


        //foreach (var w in intWorkloads)
        //{
        //    Console.Write("Ints Workload: " + w.Key);

        //    var res = await service.EchoIntArray(w.Value.ToList());


        //    if (!w.Value.SequenceEqual(res))
        //        throw new Exception("No match");


        //    await Task.Delay(sampleDelayMs);
        //    (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
        //    Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
        //    //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

        //    rt.Ints.Add(w.Key, (ctx, crx));

        //}

        await Task.Delay(sampleDelayMs);

        (tx, rx) = mon.GetTotals();
        Console.WriteLine($"Transfer {tx}/{rx}");
        //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

        mon.Stop();

        return rt;
    }
}
