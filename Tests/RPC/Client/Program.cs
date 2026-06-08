using MQTTnet;
using RPC.Client.Tests;
using RPC.Client.Tests.Docs;
using RPC.Client.Tests.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RPC.Client.Tests.Events.Websockets;


var results = new Dictionary<string, List<TestResults>>();

results.Add("esiur", new List<TestResults>());
results.Add("grpc", new List<TestResults>());
results.Add("thrift", new List<TestResults>());
results.Add("json", new List<TestResults>());
results.Add("signalr", new List<TestResults>());

for (var i = 0; i < 10; i++)
{
    var seed = 1000 + (i * 1000);

    var docsWorkloads = new Dictionary<string, RPC.EsiurTest.BusinessDocument[]>();// RPC.Client.Tests.DocGenerator.BuildWorkloads(seed);
    var dataWorkLoads = Shared.BuildBytesWorkLoads(seed);
    var intWorkloads = Shared.BuildIntWorkloads(seed);

    results["esiur"].Add(
        await EsiurTest.DoTest("iip://localhost:5005/sys/service", docsWorkloads, dataWorkLoads, intWorkloads)
        );

    results["thrift"].Add(
        await ThriftTest.DoTest("127.0.0.1", 5400,
        docsWorkloads.ToDictionary(x => x.Key, v => v.Value.Select(x => x.ToThrift()).ToArray()),
        dataWorkLoads,
        intWorkloads
        )
        );

    results["signalr"].Add(await SignalRTest.DoTest("http://127.0.0.1:5200/hub/echo",
        docsWorkloads.ToDictionary(x => x.Key, v => v.Value.Select(x => x.ToShared()).ToArray()),
        dataWorkLoads,
        intWorkloads
        ));

    results["json"].Add( await JsonTest.DoTest("http://127.0.0.1:5100",
        docsWorkloads,
        dataWorkLoads,
        intWorkloads
        ) );

    results["grpc"].Add(await GrpcTest.DoTest("http://127.0.0.1:5300",
        docsWorkloads.ToDictionary(x => x.Key, v => v.Value.Select(x => x.ToGrpc()).ToArray()),
        dataWorkLoads,
        intWorkloads
        ));
}

// Compute statistics: average, min, max, median for tx/rx per transport and workload
static (double avg, long min, long max, double median) StatsLongs(List<long> xs)
{
    if (xs == null || xs.Count == 0) return (double.NaN, 0, 0, double.NaN);
    xs.Sort();
    double avg = xs.Average(x => (double)x);
    long min = xs.First();
    long max = xs.Last();
    double median = xs.Count % 2 == 1 ? xs[xs.Count / 2] : 0.5 * (xs[xs.Count / 2 - 1] + xs[xs.Count / 2]);
    return (avg, min, max, median);
}

foreach (var transport in results.Keys)
{
    Console.WriteLine($"\n== Stats for {transport} ==");

    var rounds = results[transport];
    if (rounds.Count == 0)
    {
        Console.WriteLine("No results.");
        continue;
    }

    // categories: Docs, Bytes, Ints
    var categories = new Dictionary<string, Func<TestResults, Dictionary<string, (long, long)>>>()
    {
        { "Docs", tr => tr.Docs },
        { "Bytes", tr => tr.Bytes },
        { "Ints", tr => tr.Ints }
    };

    foreach (var cat in categories)
    {
        Console.WriteLine($"-- {cat.Key} --");

        // collect all workload keys seen in any round
        var allKeys = new HashSet<string>();
        foreach (var r in rounds)
        {
            foreach (var k in cat.Value(r).Keys) allKeys.Add(k);
        }

        foreach (var key in allKeys.OrderBy(k => k))
        {
            var txList = new List<long>();
            var rxList = new List<long>();
            foreach (var r in rounds)
            {
                if (cat.Value(r).TryGetValue(key, out var tup))
                {
                    txList.Add(tup.Item1);
                    rxList.Add(tup.Item2);
                }
            }

            if (txList.Count == 0)
            {
                Console.WriteLine($"{key}: no samples");
                continue;
            }

            var sTx = StatsLongs(txList);
            var sRx = StatsLongs(rxList);

            Console.WriteLine($"{key}: TX avg={sTx.avg:0.##}, min={sTx.min}, max={sTx.max}, med={sTx.median:0.##} | RX avg={sRx.avg:0.##}, min={sRx.min}, max={sRx.max}, med={sRx.median:0.##}");
        }
    }
}

