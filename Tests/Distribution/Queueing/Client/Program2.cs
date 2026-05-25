// ============================================================
// Test 4: Fork-Join Queueing Test — CLIENT NODE (REPLICATED)
//
// Extends the original single-shot client to run K independent
// replications of each (delay, α) configuration so that 95%
// confidence intervals can be reported for the metrics in
// Table III (λ, μ, R̄, δ̄, D̄, P99(D), queue length, batch B).
//
// Each replication uses an identical configuration; the server
// runs StartUpdatesLocal back-to-back, and the client snapshots
// the cumulative finished-queue length between replications so
// that each replication's evaluation sees only its own items.
//
// Usage:
//   dotnet run -- --host 127.0.0.1 --port 10901 \
//                 --trials 1000 \
//                 --delays 5:10:20:30:50:100 \
//                 --alphas 0.0:0.25:0.5:0.75:1.0 \
//                 --replications 5 \
//                 --output forkjoin_replicated.csv
// ============================================================

using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Tests.Queueing.Client;
using System.ComponentModel;
using System.Globalization;
using System.IO;

// ---------- arguments ----------
var host = GetArg(args, "--host", "127.0.0.1");
var port = int.Parse(GetArg(args, "--port", "10901"));
var trials = int.Parse(GetArg(args, "--trials", "1000"));
var replications = int.Parse(GetArg(args, "--replications", "5"));
var settleMs = int.Parse(GetArg(args, "--settle-ms", "1000"));
var outputCsv = GetArg(args, "--output", "forkjoin_replicated.csv");
var delays = GetArg(args, "--delays", "5:10:20:30:50:100")
                  .Split(':').Select(int.Parse).ToArray();
var alphas = GetArg(args, "--alphas", "0.0:0.25:0.5:0.75:1.0")
                  .Split(':').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();

Console.WriteLine($"[Client-T4-R] Connecting to {host}:{port}");
Console.WriteLine($"[Client-T4-R] trials/rep={trials}  replications={replications}  " +
                  $"settle={settleMs}ms");
Console.WriteLine($"[Client-T4-R] delays={string.Join(",", delays)}");
Console.WriteLine($"[Client-T4-R] alphas={string.Join(",", alphas.Select(a => a.ToString("F2", CultureInfo.InvariantCulture)))}");
Console.WriteLine($"[Client-T4-R] {delays.Length * alphas.Length} configurations × {replications} reps " +
                  $"= {delays.Length * alphas.Length * replications} trial runs");

// ---------- connect ----------
var wh = new Warehouse();
var serviceResource = await wh.Get<EpResource>($"ep://{host}:{port}/sys/queueing");
var service = (dynamic)serviceResource;

// ---------- replication coordinator state ----------
//
// The server's StartUpdatesLocal fires `trials` PropertyChanged events
// across a single call. We count incoming events; when `trials` arrive,
// the current replication is complete. We then slice off this rep's
// portion of the cumulative finished-queue and hand it to QueueEval.
//
// `repDone` is signaled once per replication so the orchestrator coroutine
// can drive the next call.

int eventsThisRep = 0;
TaskCompletionSource<bool> repDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
int finishedQueueBaseline = 0; // cumulative length BEFORE current rep started

serviceResource.PropertyChanged += (object? sender, PropertyChangedEventArgs e) =>
{
    int n = Interlocked.Increment(ref eventsThisRep);
    if (n == trials)
    {
        repDone.TrySetResult(true);
    }
};

// ---------- main sweep ----------
var rows = new List<ReplicatedResult>();

using var writer = new StreamWriter(outputCsv);
writer.WriteLine(ReplicatedEvalAggregator.CsvHeader);
writer.Flush();

foreach (var delay in delays)
{
    foreach (var alpha in alphas)
    {
        Console.WriteLine();
        Console.WriteLine($"[Client-T4-R] >>> delay={delay} ms  α={alpha:F2}  " +
                          $"(running {replications} replications) <<<");

        var reps = new List<EsiurQueueEval.EvalResult>(replications);

        for (int rep = 0; rep < replications; rep++)
        {
            // Reset per-rep state
            Interlocked.Exchange(ref eventsThisRep, 0);
            repDone = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // Snapshot the cumulative finished-queue length right before this rep
            // so we can slice off only this rep's portion afterwards.
            var preQueue = service.DistributedResourceConnection.GetFinishedQueue();
            finishedQueueBaseline = preQueue.Count;

            // Kick off the server-driven trial sequence (fire-and-forget;
            // completion is signalled via PropertyChanged → repDone).
            service.StartUpdatesLocal(delay, trials, alpha);

            // Wait until `trials` PropertyChanged events have been received.
            await repDone.Task;

            // The server completed `trials` events; slice off this rep's
            // portion of the cumulative finished-queue. GetFinishedQueue()
            // returns IReadOnlyList<AsyncQueueItem<T>>; we forward the
            // typed sliced subset directly to Evaluate which is generic
            // on T (the property's runtime payload type).
            var fullQueue = service.DistributedResourceConnection.GetFinishedQueue();
            var typedQueue = SliceQueue(fullQueue, finishedQueueBaseline);

            var repResult = EsiurQueueEval.Evaluate(typedQueue);
            reps.Add(repResult);

            Console.WriteLine($"  rep {rep + 1}/{replications}: " +
                              $"λ={repResult.LambdaEventsPerSecond:F1}/s  " +
                              $"R̄={repResult.Latency.ReadinessMs.Mean:F1}ms  " +
                              $"δ̄={repResult.Latency.HolMs.Mean:F1}ms  " +
                              $"D̄={repResult.Latency.EndToEndMs.Mean:F1}ms");

            // Settle period between reps to let any straggler notifications drain
            // and to keep the per-rep arrivals statistically independent of any
            // residual server state from the previous rep.
            await Task.Delay(settleMs);
        }

        var agg = ReplicatedEvalAggregator.Aggregate(delay, alpha, reps);
        rows.Add(agg);

        ReplicatedEvalAggregator.PrintSummary(agg);

        // Append to CSV immediately so partial progress is preserved
        // if the process is killed mid-sweep.
        writer.WriteLine(ReplicatedEvalAggregator.ToCsvRow(agg));
        writer.Flush();
    }
}

Console.WriteLine();
Console.WriteLine($"[Client-T4-R] Done. {rows.Count} configurations written to {outputCsv}");
Environment.Exit(0);


// ----------------------------------------------------------------
static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}

// ----------------------------------------------------------------
// Slice the cumulative finished-queue down to only the items added
// during the current replication.
//
// The queue is dynamically typed (returned from a dynamic-dispatched
// member) and its element type is AsyncQueueItem<T> where T is the
// runtime payload type of the observed property. We rely on the DLR
// to bind the LINQ Skip<T>/ToList<T> generic methods at runtime, just
// as the original code does with the Evaluate<T> call below it.
// ----------------------------------------------------------------
static dynamic SliceQueue(dynamic fullQueue, int skipCount)
{
    return System.Linq.Enumerable.ToList(
        System.Linq.Enumerable.Skip(fullQueue, skipCount));
}