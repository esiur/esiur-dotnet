using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;

namespace Esiur.Tests.Unit.Integration;

[Collection("Integration")]
public class AsyncStreamIntegrationTests
{
    [Fact]
    public async Task AsyncEnumerable_IsPulledAcrossProtocol()
    {
        await using var cluster = await StartCluster().WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await Task.Run(async () =>
            (EpResource)await cluster.Connection.Get("sys/stream"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        var function = remote.Instance.Definition.GetFunctionDefByName(nameof(StreamResource.Numbers));
        var stream = remote._InvokeStream<int>(
            function.Index,
            new Map<byte, object> { [0] = 4 });

        var values = new List<int>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var value in stream.WithCancellation(timeout.Token))
            values.Add(value);

        Assert.Equal(new[] { 0, 1, 2, 3 }, values);
        Assert.True(stream.Completed);
    }

    [Fact]
    public async Task DisposingAsyncEnumerable_TerminatesRemoteExecution()
    {
        await using var cluster = await StartCluster().WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await Task.Run(async () =>
            (EpResource)await cluster.Connection.Get("sys/stream"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        var function = remote.Instance.Definition.GetFunctionDefByName(nameof(StreamResource.Infinite));
        var stream = remote._InvokeStream<int>(function.Index, Array.Empty<object>());
        var enumerator = stream.GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(0, enumerator.Current);

        await enumerator.DisposeAsync();
        Assert.True(stream.Completed);
    }

    [Fact]
    public async Task PausablePushStream_HaltsAndResumesAcrossProtocol()
    {
        await using var cluster = await StartCluster().WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await Task.Run(async () =>
            (EpResource)await cluster.Connection.Get("sys/stream"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        var function = remote.Instance.Definition.GetFunctionDefByName(nameof(StreamResource.Pausable));
        var stream = remote._InvokeStream<int>(function.Index, Array.Empty<object>());
        var enumerator = stream.GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(0, enumerator.Current);

        await stream.Halt();
        var haltedMove = enumerator.MoveNextAsync().AsTask();
        await Task.Delay(200);
        Assert.False(haltedMove.IsCompleted);

        await stream.Resume();
        Assert.True(await haltedMove.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, enumerator.Current);

        await enumerator.DisposeAsync();
    }

    static Task<IntegrationCluster> StartCluster()
        => IntegrationCluster.StartAsync(async warehouse =>
        {
            await warehouse.Put("sys/stream", new StreamResource());
        });
}
