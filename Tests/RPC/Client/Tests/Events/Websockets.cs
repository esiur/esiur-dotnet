using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace RPC.Client.Tests.Events
{
    public class Websockets
    {

        public record BenchmarkResult(int? Requested, int Received, long TotalBytes, long DurationMs, double MessagesPerSec, double BytesPerSec);

        public static class WSClient
        {
            public static async Task<BenchmarkResult> DoTest(
                string url,
                int? count = 100,
                int size = 128,
                int intervalMs = 1000,
                bool binary = true,
                bool useQuery = true,
                CancellationToken cancellationToken = default)
            {

                Console.WriteLine("Starting Websockets benchmark...");
                // prepare connect url
                var connectUrl = url;
                if (useQuery)
                {
                    var qs = new QueryBuilder();
                    if (count.HasValue) qs.Add("count", count.Value.ToString());
                    qs.Add("size", size.ToString());
                    qs.Add("intervalMs", intervalMs.ToString());
                    qs.Add("binary", binary.ToString().ToLowerInvariant());
                    connectUrl += (url.Contains('?') ? "&" : "?") + qs.ToString();
                }
                await Task.Delay(2000);

                using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
                mon.Start();

                using var client = new ClientWebSocket();
                await client.ConnectAsync(new Uri(connectUrl), cancellationToken);

                // if not using query, send init JSON
                if (!useQuery)
                {
                    var init = JsonSerializer.Serialize(new { count, size, intervalMs, binary });
                    var initBytes = Encoding.UTF8.GetBytes(init);
                    await client.SendAsync(new ArraySegment<byte>(initBytes), WebSocketMessageType.Text, true, cancellationToken);
                }

                var bufferSize = Math.Max(8192, size + 4096);
                var buffer = new byte[bufferSize];

                int received = 0;
                long totalBytes = 0;
                long firstMs = 0;
                long lastMs = 0;

                await Task.Delay(2000);


                var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

                Console.WriteLine($"Handshake {ctx}/{crx}");

                long totalRxBytes = 0;

                try
                {
                    while (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                    {
                        if (count.HasValue && received >= count.Value) break;

                        // Receive a full message (handle fragmentation)
                        int messageBytes = 0;
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                            //(tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
                            //totalRxBytes += crx;

                            if (result.MessageType == WebSocketMessageType.Close) goto EndReceive;
                            messageBytes += result.Count;

                        } while (!result.EndOfMessage);

                        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                        if (received == 0) firstMs = now;
                        lastMs = now;

                        received++;
                        totalBytes += messageBytes;

                        if (count.HasValue && received >= count.Value) break;
                    }
                }
                catch (OperationCanceledException) { /* cancelled by caller */ }
                catch (WebSocketException) { /* network error */ }

            EndReceive:
                if (client.State == WebSocketState.Open || client.State == WebSocketState.CloseReceived)
                {
                    try { await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None); } catch { }
                }

                var durationMs = (firstMs != 0 && lastMs >= firstMs) ? (lastMs - firstMs) : 0;
                var msgsPerSec = durationMs > 0 ? (received * 1000.0) / durationMs : (double)received;
                var bytesPerSec = durationMs > 0 ? (totalBytes * 1000.0) / durationMs : (double)totalBytes;


                Console.WriteLine("Total RX bytes (monitor): " + totalRxBytes);

                await Task.Delay(2000);

                (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);

                Console.WriteLine($"Total Mon {tx}/{rx} {ctx}/{crx} {received}");

                mon.Stop();
                return new BenchmarkResult(count, received, totalBytes, durationMs, msgsPerSec, bytesPerSec);
            }

            // minimal QueryString builder to avoid extra deps
            private class QueryBuilder
            {
                private readonly StringBuilder _sb = new();
                private bool _first = true;
                public void Add(string name, string value)
                {
                    if (!_first) _sb.Append('&');
                    _first = false;
                    _sb.Append(Uri.EscapeDataString(name));
                    _sb.Append('=');
                    _sb.Append(Uri.EscapeDataString(value));
                }
                public override string ToString() => _sb.ToString();
            }
        }
    }
}
