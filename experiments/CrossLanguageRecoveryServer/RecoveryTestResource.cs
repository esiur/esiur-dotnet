using System.Text.Json;
using Esiur.Resource;

namespace Esiur.Experiments.CrossLanguageRecoveryServer;

[Resource]
[Annotation("experiment", "Cross-Language TypeDef Discovery and Reattachment Recovery")]
public partial class RecoveryTestResource
{
    readonly object sync = new();
    readonly string logPath;
    CancellationTokenSource? updatesCts;
    volatile bool updatesPaused;

    [Export]
    [Annotation("semantic", "monotonic counter incremented by the C# server")]
    int counter;

    [Export]
    [Annotation("semantic", "client-visible service status")]
    string status = "starting";

    [Export]
    [Annotation("semantic", "UTC tick timestamp of the last counter update")]
    long lastUpdateTicks;

    [Export]
    [Annotation("semantic", "raised whenever Counter is incremented")]
    public event ResourceEventHandler<int>? CounterChanged;

    public RecoveryTestResource(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        logPath = Path.Combine(outputDirectory, "server_log.jsonl");
        File.WriteAllText(logPath, "");
    }

    [Export]
    [Annotation("semantic", "returns a + b; used to verify dynamic function invocation")]
    public int Add(int a, int b) => a + b;

    [Export]
    [Annotation("semantic", "sets Status and returns true")]
    public bool SetStatus(string value)
    {
        Status = value;
        return true;
    }

    [Export]
    [Annotation("semantic", "pauses or resumes periodic server updates")]
    public bool SetUpdatesPaused(bool paused)
    {
        updatesPaused = paused;
        return true;
    }

    [Export]
    [Annotation("semantic", "authoritative C# state snapshot encoded as JSON")]
    public string GetAuthoritativeStateJson() => JsonSerializer.Serialize(CreateSnapshot());

    public void StartPeriodicUpdates(int updatePeriodMs, CancellationToken shutdown)
    {
        updatesCts = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var token = updatesCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(updatePeriodMs, token).ConfigureAwait(false);
                if (updatesPaused)
                    continue;

                int value;
                lock (sync)
                {
                    Counter = Counter + 1;
                    LastUpdateTicks = DateTime.UtcNow.Ticks;
                    value = Counter;
                }

                CounterChanged?.Invoke(value);
                AppendLog("tick");
            }
        }, token);
    }

    public void StopPeriodicUpdates()
    {
        updatesCts?.Cancel();
    }

    public RecoveryStateSnapshot CreateSnapshot()
    {
        lock (sync)
        {
            return new RecoveryStateSnapshot
            {
                Counter = Counter,
                Status = Status,
                LastUpdateTicks = LastUpdateTicks,
                Age = Instance?.Age ?? 0,
                TimestampUtc = DateTimeOffset.UtcNow,
                UpdatesPaused = updatesPaused
            };
        }
    }

    public void AppendLog(string eventName)
    {
        var payload = new
        {
            event_name = eventName,
            state = CreateSnapshot()
        };
        File.AppendAllText(logPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
    }
}

public sealed class RecoveryStateSnapshot
{
    public int Counter { get; set; }
    public string Status { get; set; } = "";
    public long LastUpdateTicks { get; set; }
    public ulong Age { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public bool UpdatesPaused { get; set; }
}
