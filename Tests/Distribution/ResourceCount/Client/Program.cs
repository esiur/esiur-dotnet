// ============================================================
// Test 2: Resource Count Scalability — CLIENT NODE
// Sequentially attaches to each resource on the server and
// records per-resource attach latency.  Reports p50/p95/p99.
//
// Also tests notification latency after all resources are ready.
//
// Usage:
//   dotnet run -- --host 127.0.0.1 --port 10901 --resources 10000 --batch 10000
//   dotnet run -- --host 127.0.0.1 --port 10901 --resources 10000 --batch 10 --rounds 10
//
// Outputs:
//   test2_attach_rounds.csv       per-round P50/P95/P99/mean/wall time
//   test2_attach_aggregate.csv    mean + sample standard deviation across rounds
//   test2_attach_latencies.csv    raw per-attach latencies, tagged by round
// ============================================================

using Esiur.Protocol;
using Esiur.Resource;
using System.Diagnostics;
using System.Globalization;
using System.Text;

var host = GetArg(args, "--host", "127.0.0.1");
var port = int.Parse(GetArg(args, "--port", "10901"));
var resourceCount = int.Parse(GetArg(args, "--resources", "10000"));
var batchSize = int.Parse(GetArg(args, "--batch", "10000"));
var rounds = int.Parse(GetArg(args, "--rounds", "1"));
var measureNotifications = bool.Parse(GetArg(args, "--notifications", rounds == 1 ? "true" : "false"));

if (rounds < 1)
    throw new ArgumentOutOfRangeException(nameof(rounds), "--rounds must be >= 1.");

Console.WriteLine($"[Client-T2] Connecting to {host}:{port}, resources={resourceCount}, batch={batchSize}, rounds={rounds}");

var roundResults = new List<RoundResult>(rounds);
var latencyRows = new List<(int Round, double LatencyMs)>(resourceCount * rounds);

for (int round = 1; round <= rounds; round++)
{
    var result = await RunRound(round);
    roundResults.Add(result);
    latencyRows.AddRange(result.Latencies.Select(l => (round, l)));

    Console.WriteLine(
        $"[Client-T2] Round {round}/{rounds}: " +
        $"wall={result.WallSeconds:F2}s " +
        $"p50={result.P50:F2}ms p99={result.P99:F2}ms mean={result.Mean:F2}ms");
}

Console.WriteLine("[Client-T2] Aggregate across rounds (sample standard deviation):");
PrintAggregate("wall_s", roundResults.Select(r => r.WallSeconds));
PrintAggregate("min_ms", roundResults.Select(r => r.Min));
PrintAggregate("p50_ms", roundResults.Select(r => r.P50));
PrintAggregate("p95_ms", roundResults.Select(r => r.P95));
PrintAggregate("p99_ms", roundResults.Select(r => r.P99));
PrintAggregate("max_ms", roundResults.Select(r => r.Max));
PrintAggregate("mean_ms", roundResults.Select(r => r.Mean));

// --- CSV output -----------------------------------------------------
var roundCsv = new StringBuilder();
roundCsv.AppendLine("round,resources,batch,wall_s,min_ms,p50_ms,p95_ms,p99_ms,max_ms,mean_ms,notifications_received");
foreach (var r in roundResults)
{
    roundCsv.AppendLine(
        $"{r.Round},{resourceCount},{batchSize},{F(r.WallSeconds)},{F(r.Min)},{F(r.P50)},{F(r.P95)},{F(r.P99)},{F(r.Max)},{F(r.Mean)},{r.NotificationsReceived}");
}

await File.WriteAllTextAsync("test2_attach_rounds.csv", roundCsv.ToString());

var aggregateCsv = new StringBuilder();
aggregateCsv.AppendLine("metric,rounds,mean,sample_stddev");
AppendAggregateCsv(aggregateCsv, "wall_s", roundResults.Select(r => r.WallSeconds));
AppendAggregateCsv(aggregateCsv, "min_ms", roundResults.Select(r => r.Min));
AppendAggregateCsv(aggregateCsv, "p50_ms", roundResults.Select(r => r.P50));
AppendAggregateCsv(aggregateCsv, "p95_ms", roundResults.Select(r => r.P95));
AppendAggregateCsv(aggregateCsv, "p99_ms", roundResults.Select(r => r.P99));
AppendAggregateCsv(aggregateCsv, "max_ms", roundResults.Select(r => r.Max));
AppendAggregateCsv(aggregateCsv, "mean_ms", roundResults.Select(r => r.Mean));
await File.WriteAllTextAsync("test2_attach_aggregate.csv", aggregateCsv.ToString());

var latencyCsv = new StringBuilder();
latencyCsv.AppendLine("round,attach_latency_ms");
foreach (var (round, latencyMs) in latencyRows)
    latencyCsv.AppendLine($"{round},{F(latencyMs)}");

await File.WriteAllTextAsync("test2_attach_latencies.csv", latencyCsv.ToString());

Console.WriteLine("[Client-T2] Round summaries written to test2_attach_rounds.csv");
Console.WriteLine("[Client-T2] Aggregate statistics written to test2_attach_aggregate.csv");
Console.WriteLine("[Client-T2] Attach latencies written to test2_attach_latencies.csv");

async Task<RoundResult> RunRound(int round)
{
    var wh = new Warehouse();
    var connection = await wh.Get<EpConnection>($"ep://{host}:{port}");

    var attachLatencies = new List<double>(resourceCount);
    var proxies = new IResource[resourceCount];

    // --- Attach in batches to avoid overwhelming the runtime -------------
    var totalSw = Stopwatch.StartNew();

    for (int batch = 0; batch < resourceCount; batch += batchSize)
    {
        int end = Math.Min(batch + batchSize, resourceCount);
        var batchTasks = new Task[end - batch];

        for (int i = batch; i < end; i++)
        {
            int capturedI = i;
            batchTasks[i - batch] = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                proxies[capturedI] = await connection.Get($"sys/sensor_{capturedI}");
                sw.Stop();

                lock (attachLatencies)
                    attachLatencies.Add(sw.Elapsed.TotalMilliseconds);
            });
        }

        await Task.WhenAll(batchTasks);

        if (batch % 1000 == 0)
            Console.WriteLine($"[Client-T2] Round {round}/{rounds}: attached {Math.Min(batch + batchSize, resourceCount)}/{resourceCount}  " +
                              $"elapsed={totalSw.Elapsed.TotalSeconds:F1}s");
    }

    totalSw.Stop();

    attachLatencies.Sort();
    int n = attachLatencies.Count;

    long received = 0;
    if (measureNotifications)
    {
        Console.WriteLine($"[Client-T2] Round {round}/{rounds}: measuring notification latency under full resource load...");

        for (int i = 0; i < resourceCount; i++)
        {
            int capturedI = i;
            var proxy = proxies[capturedI] ?? throw new InvalidOperationException($"Resource {capturedI} was not attached.");
            var instance = proxy.Instance ?? throw new InvalidOperationException($"Resource {capturedI} has no instance.");
            instance.PropertyModified += (PropertyModificationInfo data) =>
            {
                if (data.Name == "Value")
                    Interlocked.Increment(ref received);
            };
        }

        await connection.Call("UpdateValues");
        await Task.Delay(10000);
        Console.WriteLine($"[Client-T2] Round {round}/{rounds}: received {received} notifications in 10s");
    }

    await wh.Close();

    return new RoundResult(
        round,
        totalSw.Elapsed.TotalSeconds,
        attachLatencies[0],
        Quantile(attachLatencies, 0.50),
        Quantile(attachLatencies, 0.95),
        Quantile(attachLatencies, 0.99),
        attachLatencies[n - 1],
        attachLatencies.Average(),
        received,
        attachLatencies);
}

static double Quantile(IReadOnlyList<double> sorted, double p)
{
    return sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];
}

static void PrintAggregate(string metric, IEnumerable<double> values)
{
    var arr = values.ToArray();
    Console.WriteLine($"  {metric,-8} mean={Mean(arr):F3} sample_stddev={SampleStdDev(arr):F3}");
}

static void AppendAggregateCsv(StringBuilder csv, string metric, IEnumerable<double> values)
{
    var arr = values.ToArray();
    csv.AppendLine($"{metric},{arr.Length},{F(Mean(arr))},{F(SampleStdDev(arr))}");
}

static double Mean(IReadOnlyList<double> values)
{
    return values.Average();
}

static double SampleStdDev(IReadOnlyList<double> values)
{
    if (values.Count < 2)
        return double.NaN;

    var mean = Mean(values);
    var sumSquares = values.Sum(x =>
    {
        var d = x - mean;
        return d * d;
    });

    return Math.Sqrt(sumSquares / (values.Count - 1));
}

static string F(double value)
{
    return value.ToString("F3", CultureInfo.InvariantCulture);
}


static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}

record RoundResult(
    int Round,
    double WallSeconds,
    double Min,
    double P50,
    double P95,
    double P99,
    double Max,
    double Mean,
    long NotificationsReceived,
    List<double> Latencies);
