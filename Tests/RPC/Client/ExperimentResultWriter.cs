using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Esiur.Tests.RPC.Client;

public sealed record ExperimentRunSettings(
    int Rounds,
    int BaseSeed,
    int SerializationIterations,
    int WarmupDelayMs,
    int PostHandshakeDelayMs,
    int SampleDelayMs,
    int ProtocolTimeoutMs,
    bool RanRpc,
    bool RanSerialization);

public sealed record TransferSample(
    int Round,
    string Protocol,
    string Category,
    string Workload,
    long TxBytes,
    long RxBytes);

public static class ExperimentResultWriter
{
    public static string WriteAll(
        string outputDirectory,
        ExperimentRunSettings settings,
        IReadOnlyDictionary<string, List<TestResults>> transferResults,
        IReadOnlyList<SerializationSample> serializationSamples)
    {
        Directory.CreateDirectory(outputDirectory);

        var transferSamples = FlattenTransferResults(transferResults).ToList();
        var transferSummary = SummarizeTransfer(transferSamples).ToList();
        var serializationSummary = SummarizeSerialization(serializationSamples).ToList();

        WriteTransferDetail(Path.Combine(outputDirectory, "transfer-detail.csv"), transferSamples);
        WriteTransferSummary(Path.Combine(outputDirectory, "transfer-summary.csv"), transferSummary);
        WriteSerializationDetail(Path.Combine(outputDirectory, "serialization-detail.csv"), serializationSamples);
        WriteSerializationSummary(Path.Combine(outputDirectory, "serialization-summary.csv"), serializationSummary);

        var reportPath = Path.Combine(outputDirectory, "report.md");
        WriteReport(reportPath, settings, transferSummary, serializationSummary);
        return reportPath;
    }

    private static IEnumerable<TransferSample> FlattenTransferResults(IReadOnlyDictionary<string, List<TestResults>> results)
    {
        foreach (var protocol in results.OrderBy(x => x.Key))
        {
            for (var round = 0; round < protocol.Value.Count; round++)
            {
                foreach (var sample in FlattenCategory(protocol.Value[round].Docs, round + 1, protocol.Key, "Docs"))
                    yield return sample;
                foreach (var sample in FlattenCategory(protocol.Value[round].Bytes, round + 1, protocol.Key, "Bytes"))
                    yield return sample;
                foreach (var sample in FlattenCategory(protocol.Value[round].Ints, round + 1, protocol.Key, "Ints"))
                    yield return sample;
            }
        }
    }

    private static IEnumerable<TransferSample> FlattenCategory(
        Dictionary<string, (long txBytes, long rxBytes)> category,
        int round,
        string protocol,
        string categoryName)
    {
        foreach (var sample in category.OrderBy(x => x.Key))
            yield return new TransferSample(round, protocol, categoryName, sample.Key, sample.Value.txBytes, sample.Value.rxBytes);
    }

    private static IEnumerable<(string Protocol, string Category, string Workload, int Count, NumberStats Tx, NumberStats Rx)> SummarizeTransfer(
        IReadOnlyList<TransferSample> samples)
    {
        return samples
            .GroupBy(x => new { x.Protocol, x.Category, x.Workload })
            .OrderBy(x => x.Key.Protocol)
            .ThenBy(x => x.Key.Category)
            .ThenBy(x => x.Key.Workload)
            .Select(x => (
                x.Key.Protocol,
                x.Key.Category,
                x.Key.Workload,
                x.Count(),
                NumberStats.From(x.Select(v => (double)v.TxBytes)),
                NumberStats.From(x.Select(v => (double)v.RxBytes))));
    }

    private static IEnumerable<(string Protocol, string Category, string Workload, int Count, NumberStats Payload, NumberStats Serialize, NumberStats Deserialize)> SummarizeSerialization(
        IReadOnlyList<SerializationSample> samples)
    {
        return samples
            .GroupBy(x => new { x.Protocol, x.Category, x.Workload })
            .OrderBy(x => x.Key.Protocol)
            .ThenBy(x => x.Key.Category)
            .ThenBy(x => x.Key.Workload)
            .Select(x => (
                x.Key.Protocol,
                x.Key.Category,
                x.Key.Workload,
                x.Count(),
                NumberStats.From(x.Select(v => (double)v.PayloadBytes)),
                NumberStats.From(x.Select(v => v.SerializeMs)),
                NumberStats.From(x.Select(v => v.DeserializeMs))));
    }

    private static void WriteTransferDetail(string path, IReadOnlyList<TransferSample> samples)
    {
        var csv = new StringBuilder();
        csv.AppendLine("round,protocol,category,workload,tx_bytes,rx_bytes");
        foreach (var x in samples)
            csv.AppendLine(string.Join(",", x.Round, Csv(x.Protocol), Csv(x.Category), Csv(x.Workload), x.TxBytes, x.RxBytes));
        File.WriteAllText(path, csv.ToString());
    }

    private static void WriteTransferSummary(
        string path,
        IReadOnlyList<(string Protocol, string Category, string Workload, int Count, NumberStats Tx, NumberStats Rx)> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine("protocol,category,workload,samples,tx_avg_bytes,tx_stddev_bytes,tx_min_bytes,tx_max_bytes,tx_median_bytes,rx_avg_bytes,rx_stddev_bytes,rx_min_bytes,rx_max_bytes,rx_median_bytes");
        foreach (var x in rows)
        {
            csv.AppendLine(string.Join(",",
                Csv(x.Protocol), Csv(x.Category), Csv(x.Workload), x.Count,
                D(x.Tx.Average), D(x.Tx.StandardDeviation), D(x.Tx.Minimum), D(x.Tx.Maximum), D(x.Tx.Median),
                D(x.Rx.Average), D(x.Rx.StandardDeviation), D(x.Rx.Minimum), D(x.Rx.Maximum), D(x.Rx.Median)));
        }
        File.WriteAllText(path, csv.ToString());
    }

    private static void WriteSerializationDetail(string path, IReadOnlyList<SerializationSample> samples)
    {
        var csv = new StringBuilder();
        csv.AppendLine("round,seed,protocol,category,workload,payload_bytes,serialize_ms,deserialize_ms");
        foreach (var x in samples)
        {
            csv.AppendLine(string.Join(",",
                x.Round, x.Seed, Csv(x.Protocol), Csv(x.Category), Csv(x.Workload),
                x.PayloadBytes, D(x.SerializeMs), D(x.DeserializeMs)));
        }
        File.WriteAllText(path, csv.ToString());
    }

    private static void WriteSerializationSummary(
        string path,
        IReadOnlyList<(string Protocol, string Category, string Workload, int Count, NumberStats Payload, NumberStats Serialize, NumberStats Deserialize)> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine("protocol,category,workload,samples,payload_avg_bytes,payload_stddev_bytes,payload_min_bytes,payload_max_bytes,payload_median_bytes,serialize_avg_ms,serialize_stddev_ms,serialize_min_ms,serialize_max_ms,serialize_median_ms,deserialize_avg_ms,deserialize_stddev_ms,deserialize_min_ms,deserialize_max_ms,deserialize_median_ms");
        foreach (var x in rows)
        {
            csv.AppendLine(string.Join(",",
                Csv(x.Protocol), Csv(x.Category), Csv(x.Workload), x.Count,
                D(x.Payload.Average), D(x.Payload.StandardDeviation), D(x.Payload.Minimum), D(x.Payload.Maximum), D(x.Payload.Median),
                D(x.Serialize.Average), D(x.Serialize.StandardDeviation), D(x.Serialize.Minimum), D(x.Serialize.Maximum), D(x.Serialize.Median),
                D(x.Deserialize.Average), D(x.Deserialize.StandardDeviation), D(x.Deserialize.Minimum), D(x.Deserialize.Maximum), D(x.Deserialize.Median)));
        }
        File.WriteAllText(path, csv.ToString());
    }

    private static void WriteReport(
        string path,
        ExperimentRunSettings settings,
        IReadOnlyList<(string Protocol, string Category, string Workload, int Count, NumberStats Tx, NumberStats Rx)> transferRows,
        IReadOnlyList<(string Protocol, string Category, string Workload, int Count, NumberStats Payload, NumberStats Serialize, NumberStats Deserialize)> serializationRows)
    {
        var md = new StringBuilder();
        md.AppendLine("# RPC Serialization Supplementary Experiment");
        md.AppendLine();
        md.AppendLine("## Run Configuration");
        md.AppendLine();
        md.AppendLine($"- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine($"- Rounds: {settings.Rounds}");
        md.AppendLine($"- Base seed: {settings.BaseSeed}");
        md.AppendLine($"- Serialization iterations per workload sample: {settings.SerializationIterations}");
        md.AppendLine($"- RPC warmup/post-handshake/sample delays: {settings.WarmupDelayMs}/{settings.PostHandshakeDelayMs}/{settings.SampleDelayMs} ms");
        md.AppendLine($"- Per-protocol RPC timeout: {settings.ProtocolTimeoutMs} ms");
        md.AppendLine($"- Runtime: {RuntimeInformation.FrameworkDescription}");
        md.AppendLine($"- OS: {RuntimeInformation.OSDescription}");
        md.AppendLine($"- Architecture: {RuntimeInformation.ProcessArchitecture}");
        md.AppendLine($"- Logical processors: {Environment.ProcessorCount}");
        md.AppendLine();
        md.AppendLine("The document workloads are synthetic but deterministic. Each round uses `baseSeed + (round - 1) * 1000`; workloads cover small, medium, and large business documents with nested records, enums, nullable values, maps, Unicode text, integer arrays, and attachments.");
        md.AppendLine();
        md.AppendLine("Serialization measurements are local codec payload measurements. They include Esiur, gRPC, JSON, and SignalR JSON model payloads. Thrift is included in RPC transfer measurements; local Thrift codec timing is not emitted because the current Thrift package does not expose a stable in-memory transport in this test project.");
        md.AppendLine();
        md.AppendLine("RPC transfer counters use Windows ETW kernel network tracing. If the client is not run with Administrator rights, the RPC calls still execute but transfer counters are recorded as zero. Protocol failures or timeouts are printed to the console and omitted from the transfer summary.");
        md.AppendLine();

        md.AppendLine("## Serialization Payload Summary");
        md.AppendLine();
        md.AppendLine("| Protocol | Category | Workload | Samples | Payload avg bytes | Encode avg ms | Decode avg ms |");
        md.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        foreach (var row in serializationRows)
        {
            md.AppendLine($"| {row.Protocol} | {row.Category} | {row.Workload} | {row.Count} | {D(row.Payload.Average)} | {D(row.Serialize.Average)} | {D(row.Deserialize.Average)} |");
        }
        md.AppendLine();

        md.AppendLine("## RPC Transfer Summary");
        md.AppendLine();
        md.AppendLine("| Protocol | Category | Workload | Samples | TX avg bytes | RX avg bytes |");
        md.AppendLine("|---|---|---:|---:|---:|---:|");
        foreach (var row in transferRows)
        {
            md.AppendLine($"| {row.Protocol} | {row.Category} | {row.Workload} | {row.Count} | {D(row.Tx.Average)} | {D(row.Rx.Average)} |");
        }
        md.AppendLine();
        md.AppendLine("## Output Files");
        md.AppendLine();
        md.AppendLine("- `serialization-detail.csv`: per-round codec payload bytes and encode/decode timing.");
        md.AppendLine("- `serialization-summary.csv`: aggregate codec payload and timing statistics.");
        md.AppendLine("- `transfer-detail.csv`: per-round process network TX/RX deltas from the RPC client.");
        md.AppendLine("- `transfer-summary.csv`: aggregate RPC transfer statistics.");

        File.WriteAllText(path, md.ToString());
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }

    private static string D(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    public readonly record struct NumberStats(double Average, double Minimum, double Maximum, double Median, double StandardDeviation)
    {
        public static NumberStats From(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            if (sorted.Length == 0)
                return new NumberStats(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

            var avg = sorted.Average();
            var median = sorted.Length % 2 == 1
                ? sorted[sorted.Length / 2]
                : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2.0;

            // Calculate standard deviation
            var variance = sorted.Aggregate(0.0, (sum, x) => sum + Math.Pow(x - avg, 2)) / sorted.Length;
            var stdDev = Math.Sqrt(variance);

            return new NumberStats(avg, sorted[0], sorted[^1], median, stdDev);
        }
    }
}
