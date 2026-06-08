using System;
using System.Collections.Generic;
using System.Text;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace RPC.Client.Tests.Events;



// Simple SSE client helper for benchmarking the /sse endpoint.
// Usage:
// var opts = new SseOptions { MessagesPerBurst = 10, MessageSize = 512, IntervalMs = 10, BurstIntervalMs = 1000 };
// await SseClient.ConnectAsync("http://localhost:5000/sse", opts, CancellationToken.None,
//     onMessage: data => Console.WriteLine("msg:" + data.Length), onOpen: () => Console.WriteLine("open"), onError: ex => Console.WriteLine(ex));
public static class SseClient
{
    public static async Task DoTest(string baseUrl, SseOptions options, CancellationToken ct,
        Action<string>? onMessage = null, Action? onOpen = null, Action<Exception>? onError = null)
    {

        Console.WriteLine("Starting SSE client..." );

        options ??= new SseOptions();

        var url = BuildUrl(baseUrl, options);

        using var http = new HttpClient() { Timeout = Timeout.InfiniteTimeSpan };
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Clear();
        req.Headers.Accept.ParseAdd("text/event-stream");

        using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
        mon.Start();
        long tx = 0, rx = 0, ctx = 0, crx = 0;// = mon.GetDiff(0, 0);

        try
        {


            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            await Task.Delay(1000);

            (tx, rx, ctx, crx) = mon.GetDiff(0, 0);
            Console.WriteLine($"Handshake {ctx}/{crx}");

            onOpen?.Invoke();

            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);

            var dataBuilder = new StringBuilder();



            long totalRxBytes = 0;

            while (!ct.IsCancellationRequested)
            {
                var readTask = reader.ReadLineAsync();
                var completed = await Task.WhenAny(readTask, Task.Delay(Timeout.InfiniteTimeSpan, ct)).ConfigureAwait(false);
                if (completed != readTask)
                    break; // cancelled

                var line = await readTask!; // safe because completed
                if (line is null)
                    break; // stream ended

                if (line.Length == 0)
                {
                    // dispatch event
                    if (dataBuilder.Length > 0)
                    {
                        // Remove trailing newline added while parsing
                        if (dataBuilder.Length > 0 && dataBuilder[dataBuilder.Length - 1] == '\n')
                            dataBuilder.Length--;

                        var data = dataBuilder.ToString();
                        onMessage?.Invoke(data);
                        dataBuilder.Clear();
                    }
                    continue;
                }

                // comments start with ':' -> ignore
                if (line[0] == ':')
                    continue;

                // data: lines (may be multi-line)
                if (line.StartsWith("data:"))
                {
                    var payload = line.Length > 5 ? line.Substring(5) : string.Empty;
                    // If the payload starts with a single space per SSE spec, trim one leading space
                    if (payload.Length > 0 && payload[0] == ' ') payload = payload.Substring(1);
                    dataBuilder.Append(payload);
                    dataBuilder.Append('\n');
                }
                // other fields (event:, id:, retry:) are ignored by this simple client
            }
        }
        catch (OperationCanceledException)
        {
            // cancellation requested by caller
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }

        await Task.Delay(2000);
        (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);

        Console.WriteLine($"Results {ctx}/{crx} Total: {tx}/{rx}");

    }

    static string BuildUrl(string baseUrl, SseOptions opts)
    {
        var sb = new StringBuilder();
        sb.Append(baseUrl);
        var sep = baseUrl.Contains('?') ? '&' : '?';
        sb.Append(sep);
        sb.Append("messagesPerBurst=").Append(opts.MessagesPerBurst);
        sb.Append("&messageSize=").Append(opts.MessageSize);
        sb.Append("&intervalMs=").Append(opts.IntervalMs);
        sb.Append("&burstIntervalMs=").Append(opts.BurstIntervalMs);
        return sb.ToString();
    }
}

public class SseOptions
{
    public int MessagesPerBurst { get; set; } = 1;
    public int MessageSize { get; set; } = 100;
    public int IntervalMs { get; set; } = 0;
    public int BurstIntervalMs { get; set; } = 1000;
}
