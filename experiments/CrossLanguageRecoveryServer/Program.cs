using System.Text.Json;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Stores;
using Esiur.Experiments.CrossLanguageRecoveryServer;

var host = GetArg(args, "--host", "127.0.0.1");
var port = int.Parse(GetArg(args, "--port", "10901"));
var updatePeriodMs = int.Parse(GetArg(args, "--update-period", "100"));
var outputDirectory = Path.GetFullPath(GetArg(args, "--output", Path.Combine("results", "cross-language-recovery")));
var waitForStdin = !HasFlag(args, "--no-stdin");

Directory.CreateDirectory(outputDirectory);

var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

var wh = new Warehouse();
await wh.Put("sys", new MemoryStore());
var epServer = new EpServer
{
    Port = (ushort)port,
    AllowUnauthorizedAccess = true
};

if (!string.IsNullOrWhiteSpace(host) && host != "0.0.0.0")
    epServer.IP = host;

var server = await wh.Put("sys/server", epServer);

var resource = await wh.Put("sys/recovery", new RecoveryTestResource(outputDirectory));
await wh.Open();
resource.SetStatus("ready");
resource.StartPeriodicUpdates(updatePeriodMs, shutdown.Token);
resource.AppendLog("server_started");

var ready = new
{
    host,
    port,
    url = $"ep://{host}:{port}",
    websocket_url = $"ws://{host}:{port}",
    resource_path = "sys/recovery",
    update_period_ms = updatePeriodMs,
    output_directory = outputDirectory,
    started_utc = DateTimeOffset.UtcNow
};

var readyPath = Path.Combine(outputDirectory, "server-ready.json");
await File.WriteAllTextAsync(readyPath, JsonSerializer.Serialize(ready, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"[CrossLanguageRecoveryServer] listening ep://{host}:{port}/sys/recovery");
Console.WriteLine($"[CrossLanguageRecoveryServer] updatePeriodMs={updatePeriodMs}");
Console.WriteLine($"[CrossLanguageRecoveryServer] output={outputDirectory}");
Console.WriteLine(waitForStdin
    ? "[CrossLanguageRecoveryServer] Press ENTER or Ctrl+C to stop."
    : "[CrossLanguageRecoveryServer] Running until the process is stopped.");

_ = Task.Run(async () =>
{
    try
    {
        while (!shutdown.IsCancellationRequested)
        {
            await Task.Delay(1000, shutdown.Token).ConfigureAwait(false);
            var state = resource.CreateSnapshot();
            Console.WriteLine($"[CrossLanguageRecoveryServer] counter={state.Counter} status={state.Status} age={state.Age} clients={server.Connections.Count}");
        }
    }
    catch (TaskCanceledException)
    {
    }
});

if (waitForStdin)
{
    _ = Task.Run(() =>
    {
        Console.ReadLine();
        shutdown.Cancel();
    });
}

try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
}
catch (TaskCanceledException)
{
}
finally
{
    resource.AppendLog("server_stopping");
    resource.StopPeriodicUpdates();
    await wh.Close();
}

static string GetArg(string[] args, string key, string defaultValue)
{
    var i = Array.IndexOf(args, key);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : defaultValue;
}

static bool HasFlag(string[] args, string key) => args.Contains(key);
