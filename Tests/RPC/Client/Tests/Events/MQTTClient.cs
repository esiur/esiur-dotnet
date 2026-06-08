// Benchmark MQTT client helper
// Usage: call BenchmarkClient.RunAsync("localhost", 1883, "welcome/topic/100/1024/0");
// This class is not an executable by itself to avoid duplicate entry points in the project.

using MQTTnet;
using MQTTnet.Protocol;
using RPC.Client.Tests;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class MQTTTest
{
    public static async Task DoTest(string brokerHost = "localhost", int brokerPort = 1883, string topic = "test/topic/100/100/100", CancellationToken cancellationToken = default)
    {

        using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
        mon.Start();

        var factory = new MqttClientFactory();
        using var mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerHost, brokerPort)
            .WithCleanSession()
            .Build();

        long receivedMessages = 0;
        long receivedBytes = 0;
        DateTime? firstReceivedAt = null;
        DateTime? lastReceivedAt = null;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        mqttClient.ApplicationMessageReceivedAsync += (e =>
        {
            var now = DateTime.UtcNow;

            Interlocked.Increment(ref receivedMessages);
            Interlocked.Add(ref receivedBytes, e.ApplicationMessage?.Payload.Length ?? 0);

            if (firstReceivedAt == null)
            {
                firstReceivedAt = now;
            }

            lastReceivedAt = now;

            // If expected count encoded in topic, check completion
            var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 5 && int.TryParse(segments[2], out var expected) && expected > 0)
            {
                if (Interlocked.Read(ref receivedMessages) >= expected)
                {
                    tcs.TrySetResult(true);
                }
            }

            return Task.CompletedTask;
        });

        mqttClient.ConnectedAsync += (async e =>
        {
            Console.WriteLine($"Connected to {brokerHost}:{brokerPort}");
            await mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => { f.WithTopic(topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce); })
                .Build());

            Console.WriteLine($"Subscribed to topic '{topic}'");
        });



        //mqttClient.DisconnectedAsync += (e => Console.WriteLine("Disconnected from broker"));

        await mqttClient.ConnectAsync(options, cancellationToken);

        await Task.Delay(1000);
        var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);
        Console.WriteLine($"Handshake {ctx}/{crx}");

        // If no expected count, wait until cancelled
        var segmentsCheck = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Task waitTask;
        if (segmentsCheck.Length >= 5 && int.TryParse(segmentsCheck[2], out var expectedCount) && expectedCount > 0)
        {
            // wait for expected messages or cancellation
            waitTask = Task.WhenAny(tcs.Task, Task.Run(() => { var ct = new CancellationTokenSource(); cancellationToken.Register(ct.Cancel); return Task.Delay(Timeout.Infinite, ct.Token); })).Unwrap();
            await waitTask;
        }
        else
        {
            // wait until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (TaskCanceledException) { }
        }

        // compute results
        var totalMsgs = Interlocked.Read(ref receivedMessages);
        var totalBytes = Interlocked.Read(ref receivedBytes);
        var duration = (lastReceivedAt.HasValue && firstReceivedAt.HasValue) ? (lastReceivedAt.Value - firstReceivedAt.Value) : TimeSpan.Zero;

        Console.WriteLine("--- Benchmark results ---");
        Console.WriteLine($"Messages received: {totalMsgs}");
        Console.WriteLine($"Total bytes: {totalBytes}");
        Console.WriteLine($"Duration (first->last): {duration.TotalSeconds:F3} s");
        Console.WriteLine($"Messages/sec: {(duration.TotalSeconds > 0 ? (totalMsgs / duration.TotalSeconds) : 0):F3}");
        Console.WriteLine($"Bytes/sec: {(duration.TotalSeconds > 0 ? (totalBytes / duration.TotalSeconds) : 0):F3}");


        await Task.Delay(2000);
        (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);

        Console.WriteLine($"Results {ctx}/{crx} Total: {tx}/{rx}");

        try
        {
            await mqttClient.DisconnectAsync();
        }
        catch { }
    }

}
