using Esiur.Data;
using Esiur.Resource;
using Google.Protobuf;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using EsiurModel = Esiur.Tests.RPC.EsiurServer;
using GrpcModel = Esiur.Tests.RPC.Client.Grpc;
using SharedModel = Esiur.Tests.RPC.Client.SharedModel;

namespace Esiur.Tests.RPC.Client;

public sealed record SerializationSample(
    int Round,
    int Seed,
    string Protocol,
    string Category,
    string Workload,
    long PayloadBytes,
    double SerializeMs,
    double DeserializeMs);

public static class SerializationExperiment
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static List<SerializationSample> RunRound(
        int round,
        int seed,
        Dictionary<string, EsiurModel.BusinessDocument[]> docsWorkloads,
        Dictionary<string, byte[]> dataWorkloads,
        Dictionary<string, int[]> intWorkloads,
        int iterations)
    {
        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations));

        EsiurModel.Initialization.RegisterTypes(Warehouse.Default);

        var samples = new List<SerializationSample>();

        foreach (var workload in docsWorkloads)
        {
            var docs = workload.Value;
            var grpcDocs = docs.Select(x => x.ToGrpc()).ToArray();
            var sharedDocs = docs.Select(x => x.ToShared()).ToArray();

            samples.Add(Measure(
                round, seed, "esiur", "Docs", workload.Key, iterations,
                () => Codec.Compose(docs, Warehouse.Default, null),
                payload => GC.KeepAlive(Codec.ParseSync(payload, 0, Warehouse.Default).Item2)));

            samples.Add(Measure(
                round, seed, "grpc", "Docs", workload.Key, iterations,
                () =>
                {
                    var request = new GrpcModel.DocumentsRequest();
                    request.Docs.AddRange(grpcDocs);
                    return request.ToByteArray();
                },
                payload => GC.KeepAlive(GrpcModel.DocumentsRequest.Parser.ParseFrom(payload))));

            samples.Add(Measure(
                round, seed, "json", "Docs", workload.Key, iterations,
                () => JsonSerializer.SerializeToUtf8Bytes(docs, JsonOptions),
                payload => GC.KeepAlive(JsonSerializer.Deserialize<EsiurModel.BusinessDocument[]>(payload, JsonOptions))));

            samples.Add(Measure(
                round, seed, "signalr", "Docs", workload.Key, iterations,
                () => JsonSerializer.SerializeToUtf8Bytes(sharedDocs, JsonOptions),
                payload => GC.KeepAlive(JsonSerializer.Deserialize<SharedModel.BusinessDocument[]>(payload, JsonOptions))));
        }

        foreach (var workload in dataWorkloads)
        {
            var data = workload.Value;

            samples.Add(Measure(
                round, seed, "esiur", "Bytes", workload.Key, iterations,
                () => Codec.Compose(data, Warehouse.Default, null),
                payload => GC.KeepAlive(Codec.ParseSync(payload, 0, Warehouse.Default).Item2)));

            samples.Add(Measure(
                round, seed, "grpc", "Bytes", workload.Key, iterations,
                () => new GrpcModel.BytesRequest { Data = ByteString.CopyFrom(data) }.ToByteArray(),
                payload => GC.KeepAlive(GrpcModel.BytesRequest.Parser.ParseFrom(payload))));

            samples.Add(Measure(
                round, seed, "json", "Bytes", workload.Key, iterations,
                () => JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions),
                payload => GC.KeepAlive(JsonSerializer.Deserialize<byte[]>(payload, JsonOptions))));

            samples.Add(Measure(
                round, seed, "signalr", "Bytes", workload.Key, iterations,
                () => JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions),
                payload => GC.KeepAlive(JsonSerializer.Deserialize<byte[]>(payload, JsonOptions))));
        }

        foreach (var workload in intWorkloads)
        {
            var data = workload.Value;

            samples.Add(Measure(
                round, seed, "esiur", "Ints", workload.Key, iterations,
                () => Codec.Compose(data, Warehouse.Default, null),
                payload => GC.KeepAlive(Codec.ParseSync(payload, 0, Warehouse.Default).Item2)));

            samples.Add(Measure(
                round, seed, "grpc", "Ints", workload.Key, iterations,
                () =>
                {
                    var request = new GrpcModel.IntArrayRequest();
                    request.Array.AddRange(data);
                    return request.ToByteArray();
                },
                payload => GC.KeepAlive(GrpcModel.IntArrayRequest.Parser.ParseFrom(payload))));

            samples.Add(Measure(
                round, seed, "json", "Ints", workload.Key, iterations,
                () => JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions),
                payload => GC.KeepAlive(JsonSerializer.Deserialize<int[]>(payload, JsonOptions))));

            samples.Add(Measure(
                round, seed, "signalr", "Ints", workload.Key, iterations,
                () => JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions),
                payload => GC.KeepAlive(JsonSerializer.Deserialize<int[]>(payload, JsonOptions))));
        }

        return samples;
    }

    private static SerializationSample Measure(
        int round,
        int seed,
        string protocol,
        string category,
        string workload,
        int iterations,
        Func<byte[]> serialize,
        Action<byte[]> deserialize)
    {
        var payload = serialize();
        deserialize(payload);

        long payloadBytes = payload.Length;
        long serializeTicks = 0;
        long deserializeTicks = 0;

        for (var i = 0; i < iterations; i++)
        {
            var started = Stopwatch.GetTimestamp();
            payload = serialize();
            serializeTicks += Stopwatch.GetTimestamp() - started;
            payloadBytes = payload.Length;

            started = Stopwatch.GetTimestamp();
            deserialize(payload);
            deserializeTicks += Stopwatch.GetTimestamp() - started;
        }

        return new SerializationSample(
            round,
            seed,
            protocol,
            category,
            workload,
            payloadBytes,
            TicksToMs(serializeTicks) / iterations,
            TicksToMs(deserializeTicks) / iterations);
    }

    private static double TicksToMs(long ticks)
        => ticks * 1000.0 / Stopwatch.Frequency;
}
