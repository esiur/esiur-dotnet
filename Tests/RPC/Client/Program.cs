using Esiur.Tests.RPC.Client;

var rounds = ReadIntArg(args, "--rounds", 10);
var baseSeed = ReadIntArg(args, "--seed", 1000);
var serializationIterations = ReadIntArg(args, "--serialization-iterations", 3);
var warmupDelayMs = ReadIntArg(args, "--warmup-ms", 3000);
var postHandshakeDelayMs = ReadIntArg(args, "--post-handshake-ms", 2000);
var sampleDelayMs = ReadIntArg(args, "--sample-ms", 3000);
var protocolTimeoutMs = ReadIntArg(args, "--protocol-timeout-ms", 120000);
var runRpc = !HasArg(args, "--skip-rpc");
var runSerialization = false;// !HasArg(args, "--skip-serialization");
var outputDirectory = Path.GetFullPath(ReadStringArg(
    args,
    "--output",
    Path.Combine("Tests", "RPC", "Results", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"))));

var results = new Dictionary<string, List<TestResults>>
{
    ["esiur"] = new(),
    ["grpc"] = new(),
    ["thrift"] = new(),
    ["json"] = new(),
    ["signalr"] = new()
};

var serializationSamples = new List<SerializationSample>();

Console.WriteLine("RPC supplementary experiment");
Console.WriteLine($"Rounds={rounds}, seed={baseSeed}, serialization iterations={serializationIterations}");
Console.WriteLine($"Output={outputDirectory}");

for (var i = 0; i < rounds; i++)
{
    var round = i + 1;
    var seed = baseSeed + (i * 1000);

    Console.WriteLine($"\n# Round {round}/{rounds}, seed {seed}");

    //var docsWorkloads = DocGenerator.BuildWorkloads(seed);
    var docsWorkloads = ModelGenerator.BuildWorkloads();

    if (!runRpc)
        continue;

    var esiurWorkload = docsWorkloads.ToDictionary(x => x.Item1, v => v.Item2.Select(x => x.Payload).ToArray());
    var thriftDocs = docsWorkloads.ToDictionary(x => x.Item1, v => v.Item2.Select(x => x.Payload.ToThrift()).ToArray());
    var signalRDocs = docsWorkloads.ToDictionary(x => x.Item1, v => v.Item2.Select(x => x.Payload.ToShared()).ToArray());
    var grpcDocs = docsWorkloads.ToDictionary(x => x.Item1, v => v.Item2.Select(x => x.Payload.ToGrpc()).ToArray());

    if (await RunProtocol("esiur", () => EsiurTest.DoTest(
        "ep://localhost:5005/sys/service",
        esiurWorkload,
        //dataWorkLoads,
        //intWorkloads,
        warmupDelayMs,
        postHandshakeDelayMs,
        sampleDelayMs),
        protocolTimeoutMs) is { } esiurResults)
    {
        results["esiur"].Add(esiurResults);
    }

    if (await RunProtocol("thrift", () => ThriftTest.DoTest(
        "127.0.0.1",
        5400,
        thriftDocs,
        //dataWorkLoads,
        //intWorkloads,
        warmupDelayMs,
        postHandshakeDelayMs,
        sampleDelayMs),
        protocolTimeoutMs) is { } thriftResults)
    {
        results["thrift"].Add(thriftResults);
    }

    if (await RunProtocol("signalr", () => SignalRTest.DoTest(
        "http://127.0.0.1:5200/hub/echo",
        signalRDocs,
        //dataWorkLoads,
        //intWorkloads,
        warmupDelayMs,
        postHandshakeDelayMs,
        sampleDelayMs),
        protocolTimeoutMs) is { } signalRResults)
    {
        results["signalr"].Add(signalRResults);
    }

    if (await RunProtocol("json", () => JsonTest.DoTest(
        "http://127.0.0.1:5100",
        esiurWorkload,
        //dataWorkLoads,
        //intWorkloads,
        warmupDelayMs,
        postHandshakeDelayMs,
        sampleDelayMs),
        protocolTimeoutMs) is { } jsonResults)
    {
        results["json"].Add(jsonResults);
    }

    if (await RunProtocol("grpc", () => GrpcTest.DoTest(
        "http://127.0.0.1:5300",
        grpcDocs,
        //dataWorkLoads,
        //intWorkloads,
        warmupDelayMs,
        postHandshakeDelayMs,
        sampleDelayMs),
        protocolTimeoutMs) is { } grpcResults)
    {
        results["grpc"].Add(grpcResults);
    }
}

if (runRpc)
    PrintTransferStats(results);

var reportPath = ExperimentResultWriter.WriteAll(
    outputDirectory,
    new ExperimentRunSettings(
        rounds,
        baseSeed,
        serializationIterations,
        warmupDelayMs,
        postHandshakeDelayMs,
        sampleDelayMs,
        protocolTimeoutMs,
        runRpc,
        runSerialization),
    results,
    serializationSamples);

Console.WriteLine($"\nReport written to {reportPath}");

static void PrintTransferStats(Dictionary<string, List<TestResults>> results)
{
    foreach (var transport in results.Keys)
    {
        Console.WriteLine($"\n== Stats for {transport} ==");

        var rounds = results[transport];
        if (rounds.Count == 0)
        {
            Console.WriteLine("No results.");
            continue;
        }

        var categories = new Dictionary<string, Func<TestResults, Dictionary<string, (long, long)>>>
        {
            { "Docs", tr => tr.Docs },
            { "Bytes", tr => tr.Bytes },
            { "Ints", tr => tr.Ints }
        };

        foreach (var cat in categories)
        {
            Console.WriteLine($"-- {cat.Key} --");

            var allKeys = new HashSet<string>();
            foreach (var r in rounds)
            {
                foreach (var k in cat.Value(r).Keys)
                    allKeys.Add(k);
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
}

static (double avg, long min, long max, double median) StatsLongs(List<long> xs)
{
    if (xs == null || xs.Count == 0)
        return (double.NaN, 0, 0, double.NaN);

    xs.Sort();
    double avg = xs.Average(x => (double)x);
    long min = xs.First();
    long max = xs.Last();
    double median = xs.Count % 2 == 1 ? xs[xs.Count / 2] : 0.5 * (xs[xs.Count / 2 - 1] + xs[xs.Count / 2]);
    return (avg, min, max, median);
}

static async Task<TestResults?> RunProtocol(string protocol, Func<Task<TestResults>> action, int timeoutMs)
{
    try
    {
        var task = action();

        if (timeoutMs > 0)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task)
            {
                Console.WriteLine($"{protocol} failed: timed out after {timeoutMs} ms");
                return null;
            }
        }

        return await task;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{protocol} failed: {ex.GetType().Name}: {ex.Message}");
        return null;
    }
}

static int ReadIntArg(string[] args, string name, int defaultValue)
{
    var raw = TryReadStringArg(args, name);
    return raw == null ? defaultValue : int.Parse(raw);
}

static string ReadStringArg(string[] args, string name, string defaultValue)
    => TryReadStringArg(args, name) ?? defaultValue;

static string? TryReadStringArg(string[] args, string name)
{
    var prefix = name + "=";
    var inline = args.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (inline != null)
        return inline[prefix.Length..];

    var index = Array.FindIndex(args, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    if (index >= 0 && index + 1 < args.Length)
        return args[index + 1];

    return null;
}

static bool HasArg(string[] args, string name)
    => args.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
