using Avro.Generic;
using Esiur.Resource;
using FlatSharp;
using MessagePack;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PeterO.Cbor;
using ProtoBuf;
using SolTechnology.Avro;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Esiur.Tests.Serialization;

public interface ICodec
{
    string Name { get; }
    byte[]? Serialize(BusinessDocument obj); // returns null on failure
    BusinessDocument Deserialize(byte[] data);
}

public sealed class JsonCodec : ICodec
{
    static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };
    public string Name => "JSON";
    public byte[]? Serialize(BusinessDocument obj)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(obj, obj.GetType(), Opt);
        return data;
    }

    public BusinessDocument Deserialize(byte[] data)
    {
        return JsonSerializer.Deserialize<BusinessDocument>(data)!;
    }
}

public sealed class EsiurCodec : ICodec
{
    public string Name => "Esiur";

    public BusinessDocument Deserialize(byte[] data)
    {
        var (_, y) = Esiur.Data.Codec.ParseSync(data, 0, Warehouse.Default);
        return (BusinessDocument)y!;
    }

    public byte[]? Serialize(BusinessDocument obj)
    {
        var rt = Esiur.Data.Codec.Compose(obj, Warehouse.Default, null);
        return rt;
    }
}

public sealed class MessagePackCodec : ICodec
{
    public string Name => "MessagePack";

    public BusinessDocument Deserialize(byte[] data)
    {
        return MessagePackSerializer.Deserialize<BusinessDocument>(data);
    }

    public byte[]? Serialize(BusinessDocument obj)
    {
        return MessagePackSerializer.Serialize(obj.GetType(), obj);
    }
}

public sealed class ProtobufCodec : ICodec
{
    public string Name => "Protobuf";

    public BusinessDocument Deserialize(byte[] data)
    {
        var dst = Serializer.Deserialize<BusinessDocument>(new MemoryStream(data));
        return dst;
    }

    public byte[]? Serialize(BusinessDocument obj)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, obj);
        var rt = ms.ToArray();

        // Single correctness check (outside timing loops)
        var dst = Serializer.Deserialize<BusinessDocument>(new MemoryStream(rt));
        if (!obj.Equals(dst))
            throw new NotSupportedException("Protobuf roundtrip mismatch.");

        return rt;
    }
}

public sealed class FlatBuffersCodec : ICodec
{
    public string Name => "FlatBuffers";

    public BusinessDocument Deserialize(byte[] data)
    {
        var m = FlatBufferSerializer.Default.Parse<BusinessDocument>(data);
        return m;
    }

    public byte[]? Serialize(BusinessDocument obj)
    {
        var buffer = new byte[1_000_000];
        var count = FlatBufferSerializer.Default.Serialize(obj, buffer);
        var msg = buffer.AsSpan(0, count).ToArray();

        // Single correctness check (outside timing loops)
        var m = FlatBufferSerializer.Default.Parse<BusinessDocument>(msg);
        if (!m!.Equals(obj))
            throw new Exception("FlatBuffers roundtrip mismatch.");

        return msg;
    }
}

public sealed class CborCodec : ICodec
{
    public string Name => "CBOR";

    public BusinessDocument Deserialize(byte[] data)
    {
        return CBORObject.DecodeObjectFromBytes<BusinessDocument>(data)!;
    }

    public byte[]? Serialize(BusinessDocument obj)
    {
        var c = CBORObject.FromObject(obj);
        return c.EncodeToBytes();
    }
}

public sealed class BsonCodec : ICodec
{
    private static bool _init;
    private static void EnsureMaps()
    {
        if (_init) return;
        _init = true;
        // Register class maps if needed; defaults usually work for POCOs.
    }

    public string Name => "BSON";
    public byte[]? Serialize(BusinessDocument obj)
    {
        try
        {
            EnsureMaps();
            using var ms = new MemoryStream();
            using var writer = new MongoDB.Bson.IO.BsonBinaryWriter(ms);
            var context = MongoDB.Bson.Serialization.BsonSerializationContext.CreateRoot(writer);
            var args = new MongoDB.Bson.Serialization.BsonSerializationArgs(obj.GetType(), true, false);
            var serializer = BsonSerializer.LookupSerializer(obj.GetType());
            serializer.Serialize(context, args, obj);
            return ms.ToArray();
        }
        catch { return null; }
    }

    public BusinessDocument Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new MongoDB.Bson.IO.BsonBinaryReader(ms);
        var args = new MongoDB.Bson.Serialization.BsonSerializationArgs(typeof(BusinessDocument), true, false);
        var context = MongoDB.Bson.Serialization.BsonDeserializationContext.CreateRoot(reader);
        var serializer = BsonSerializer.LookupSerializer(typeof(BusinessDocument));
        return (BusinessDocument)serializer.Deserialize(context)!;
    }
}

public sealed class AvroCodec : ICodec
{
    public string Name => "Avro";

    public BusinessDocument Deserialize(byte[] data)
    {
        return AvroConvert.Deserialize<BusinessDocument>(data);
    }

    public byte[]? Serialize(BusinessDocument obj)
    {
        return AvroConvert.Serialize(obj);
    }
}

// ------------------------- Stat helpers -------------------------

public static class Stats
{
    public static double Mean(IReadOnlyList<long> xs) =>
        xs.Count == 0 ? double.NaN : xs.Average();

    public static double Median(IReadOnlyList<long> xs)
    {
        if (xs.Count == 0) return double.NaN;
        var arr = xs.OrderBy(v => v).ToArray();
        int n = arr.Length;
        return n % 2 == 1 ? arr[n / 2] : (arr[n / 2 - 1] + arr[n / 2]) / 2.0;
    }

    public static string ClassifyVsJson(double ratio)
    {
        if (double.IsNaN(ratio)) return "N/A";
        if (ratio <= 0.75) return "Smaller (≤0.75× JSON)";
        if (ratio <= 1.25) return "Similar (~0.75–1.25×)";
        return "Larger (≥1.25× JSON)";
    }
}

// ------------------------- Workload config -------------------------

public enum Workload
{
    Small,   // ~5 lines, no attachments
    Medium,  // ~20 lines, 1 small attachment
    Large,   // ~100 lines, 3 x 64KB attachments
}

public sealed class WorkItem
{
    public required string Name { get; init; }
    public required BusinessDocument Payload { get; init; }
}

// ------------------------- CPU timing helpers -------------------------

public static class CpuTimer
{
    // small warm-up to reduce JIT/first-use bias
    public static void WarmUp(Action action, int rounds = 5)
    {
        for (int i = 0; i < rounds; i++) action();
    }

    // Measures process CPU time consumed by running `action` N times.
    // Returns average microseconds per operation.
    public static double MeasureAverageMicros(Action action, int rounds)
    {
        var proc = Process.GetCurrentProcess();

        // GC before timing to reduce random interference (still CPU, but more stable)
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var start = proc.TotalProcessorTime;
        for (int i = 0; i < rounds; i++) action();
        var end = proc.TotalProcessorTime;

        var delta = end - start;
        var micros = delta.TotalMilliseconds * 1000.0;
        return micros / rounds;
    }
}

// ------------------------- Runner -------------------------

public sealed class ModelRunner
{
    private readonly ICodec[] _codecs;

    public ModelRunner()
    {
        _codecs = new ICodec[]
        {
            new JsonCodec(),
            new EsiurCodec(),
            new MessagePackCodec(),
            new ProtobufCodec(),
            new FlatBuffersCodec(),
            new CborCodec(),
            new BsonCodec(),
            new AvroCodec()
        };
    }

    record WorkloadResult
    {
        public string Name { get; init; } = "";
        public List<long> Sizes { get; init; } = new();
        public double EncodeCpuUsSum { get; set; }   // sum of per-item avg CPU us/op
        public double DecodeCpuUsSum { get; set; }
        public int Samples { get; set; }             // count of successful samples
    }

    // volatile sink to avoid aggressive JIT elimination in tight loops
    private static volatile byte[]? _sinkBytes;
    private static volatile BusinessDocument? _sinkDoc;

    public void Run()
    {
        const int Rounds = 100;

        var workloads = BuildWorkloads();

        Console.WriteLine("=== Serialization Size & CPU (process) Benchmark ===");
        Console.WriteLine($"Date (UTC): {DateTime.UtcNow:O}");
        Console.WriteLine($"Rounds per op (CPU): {Rounds}");
        Console.WriteLine();

        foreach (var (wName, items) in workloads)
        {
            Console.WriteLine($"--- Workload: {wName} ---");

            // Collect results: Codec -> result container
            var results = new Dictionary<string, WorkloadResult>();
            foreach (var c in _codecs) results[c.Name] = new WorkloadResult { Name = c.Name };

            foreach (var item in items)
            {
                foreach (var c in _codecs)
                {
                    try
                    {
                        // Single functional serialize to get bytes & verify equality ONCE (not in timed loop)
                        var bytes = c.Serialize(item.Payload);
                        if (bytes == null)
                        {
                            results[c.Name].Sizes.Add(long.MinValue);
                            continue;
                        }

                        var back = c.Deserialize(bytes);
                        if (!item.Payload.Equals(back))
                            throw new InvalidOperationException($"{c.Name} roundtrip inequality.");

                        results[c.Name].Sizes.Add(bytes.LongLength);

                        // ---- CPU timing ----

                        // Warm-up (tiny)
                        CpuTimer.WarmUp(() => { _sinkBytes = c.Serialize(item.Payload); }, 3);
                        CpuTimer.WarmUp(() => { _sinkDoc = c.Deserialize(bytes); }, 3);

                        // Measure serialize CPU (average µs/op over Rounds)
                        var encUs = CpuTimer.MeasureAverageMicros(() =>
                        {
                            _sinkBytes = c.Serialize(item.Payload);
                        }, Rounds);

                        // Measure deserialize CPU
                        var decUs = CpuTimer.MeasureAverageMicros(() =>
                        {
                            _sinkDoc = c.Deserialize(bytes);
                        }, Rounds);

                        results[c.Name].EncodeCpuUsSum += encUs;
                        results[c.Name].DecodeCpuUsSum += decUs;
                        results[c.Name].Samples += 1;
                    }
                    catch
                    {
                        // mark size failure for this sample if not already added
                        results[c.Name].Sizes.Add(long.MinValue);
                    }
                }
            }

            // Compute stats, using only successful size samples
            var jsonSizes = results["JSON"].Sizes.Where(v => v != long.MinValue).ToList();
            var jsonMean = Stats.Mean(jsonSizes);
            var jsonMed = Stats.Median(jsonSizes);

            Console.WriteLine($"JSON mean: {jsonMean:F1} B, median: {jsonMed:F1} B");
            Console.WriteLine();

            Console.WriteLine("{0,-14} {1,12} {2,12} {3,10} {4,26} {5,18} {6,18}",
                "Codec", "Mean(B)", "Median(B)", "Ratio", "Class vs JSON", "Enc CPU (µs)", "Dec CPU (µs)");
            Console.WriteLine(new string('-', 118));

            foreach (var c in _codecs)
            {
                var r = results[c.Name];
                var okSizes = r.Sizes.Where(v => v != long.MinValue).ToList();
                var mean = Stats.Mean(okSizes);
                var med = Stats.Median(okSizes);

                double ratio = double.NaN;
                if (!double.IsNaN(mean) && !double.IsNaN(jsonMean) && jsonMean > 0) ratio = mean / jsonMean;

                string cls = Stats.ClassifyVsJson(ratio);
                string meanS = double.IsNaN(mean) ? "N/A" : mean.ToString("F1");
                string medS = double.IsNaN(med) ? "N/A" : med.ToString("F1");
                string ratioS = double.IsNaN(ratio) ? "N/A" : ratio.ToString("F3");

                // average CPU µs/op across samples where serialization succeeded
                string encCpuS = (r.Samples == 0) ? "N/A" : (r.EncodeCpuUsSum / r.Samples).ToString("F1");
                string decCpuS = (r.Samples == 0) ? "N/A" : (r.DecodeCpuUsSum / r.Samples).ToString("F1");

                Console.WriteLine("{0,-14} {1,12} {2,12} {3,10} {4,26} {5,18} {6,18}",
                    c.Name, meanS, medS, ratioS, cls, encCpuS, decCpuS);
            }

            Console.WriteLine();

            Console.ReadLine();
        }
    }

    private static List<(string, List<WorkItem>)> BuildWorkloads()
    {
        var result = new List<(string, List<WorkItem>)>();

        // Small
        {
            var items = new List<WorkItem>();
            for (int i = 0; i < 16; i++)
            {
                var doc = ModelGenerator.MakeBusinessDocument(new ModelGenerator.GenOptions
                {
                    Lines = 5,
                    Payments = 3,
                    Attachments = 0,
                    IncludeV2Fields = (i % 2 == 0),
                    IncludeUnicode = true,
                    RiskScores = 500,
                    Seed = 1000 + i
                });

                items.Add(new WorkItem { Name = $"S-{i}", Payload = doc });
            }
            result.Add(("Small", items));
        }

        // Medium
        {
            var items = new List<WorkItem>();
            for (int i = 0; i < 16; i++)
            {
                var doc = ModelGenerator.MakeBusinessDocument(new ModelGenerator.GenOptions
                {
                    Lines = 20,
                    Payments = 5,
                    Attachments = 1,
                    AttachmentBytes = 8 * 1024,
                    IncludeV2Fields = (i % 3 == 0),
                    IncludeUnicode = true,
                    RiskScores = 1000,
                    Seed = 2000 + i
                });
                items.Add(new WorkItem { Name = $"M-{i}", Payload = doc });
            }
            result.Add(("Medium", items));
        }

        // Large
        {
            var items = new List<WorkItem>();
            for (int i = 0; i < 12; i++)
            {
                var doc = ModelGenerator.MakeBusinessDocument(new ModelGenerator.GenOptions
                {
                    Lines = 100,
                    Payments = 20,
                    Attachments = 3,
                    AttachmentBytes = 64 * 1024,
                    IncludeV2Fields = (i % 2 == 1),
                    IncludeUnicode = true,
                    RiskScores = 3000,
                    Seed = 3000 + i
                });
                items.Add(new WorkItem { Name = $"L-{i}", Payload = doc });
            }
            result.Add(("Large", items));
        }

        return result;
    }
}
