using Esiur.Tests.RPC.EsiurServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esiur.Tests.RPC.Client;


public class JsonTest
{

    public static async Task<TestResults> DoTest(string address,
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

        Console.WriteLine($"\n== JSON @ {address} ==");


        using var http = new HttpClient { BaseAddress = new Uri(address) };


        Thread.Sleep(warmupDelayMs);

        var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

        Console.WriteLine($"Handshake {ctx}/{crx}");

        await Task.Delay(postHandshakeDelayMs);

        foreach (var w in docsWorkloads)
        {
            Console.Write("Workload: " + w.Key);
            var docs = await JsonRpcCallAsync(http, "EchoDocuments", w.Value);

            await Task.Delay(sampleDelayMs);
            (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
            Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");

            rt.Docs.Add(w.Key, (ctx, crx));

        }



        //foreach (var w in dataWorkloads)
        //{
        //    Console.Write("Bytes Workload: " + w.Key);

        //    var res = await JsonRpcCallAsync(http, "EchoBytes", w.Value);


        //    //if (!w.Value.SequenceEqual(rt))
        //    //    throw new Exception("No match");


        //    await Task.Delay(sampleDelayMs);
        //    (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
        //    Console.WriteLine($", {tx}/{rx}, {ctx}/{crx}");
        //    //Console.WriteLine($"Socket {sock.BytesSent}/{sock.BytesReceived}");

        //    rt.Bytes.Add(w.Key, (ctx, crx));

        //}


        //foreach (var w in intWorkloads)
        //{
        //    Console.Write("Ints Workload: " + w.Key);

        //    var res = await JsonRpcCallAsync(http, "EchoIntArray", w.Value);

        //    //if (!w.Value.SequenceEqual(rt))
        //    //    throw new Exception("No match");


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



    // ===== JSON options =====
    static JsonSerializerOptions json = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    record JsonRpcReq(string Jsonrpc, string Method, object Params, string Id);
    record JsonRpcRes { public string Jsonrpc { get; init; } = "2.0"; public string Id { get; init; } = "1"; public object? Result { get; init; } }

    public static async Task<(JsonElement root, string raw)> JsonRpcCallAsync(HttpClient http, string method, object param, bool noId = false)
    {
        var reqObj = new JsonRpcReq("2.0", method, param, "1");
        var jsonTxt = JsonSerializer.Serialize(reqObj, json);
        var res = await http.PostAsync("/rpc", new StringContent(jsonTxt, Encoding.UTF8, "application/json"));
        var raw = await res.Content.ReadAsStringAsync();
        res.EnsureSuccessStatusCode();
        if (noId) return (default, raw);
        using var doc = JsonDocument.Parse(raw);
        return (doc.RootElement.Clone(), raw);
    }
}
