using Esiur.Core;
using Esiur.Data.Types;
using System.Runtime.CompilerServices;

namespace Esiur.Tests.Unit;

public class AsyncStreamReplyTests
{
    [Fact]
    public async Task PullStream_RequestsOneItemPerMoveNext()
    {
        var pulls = 0;
        var stream = CreateStream<int>(StreamMode.Pull, pull: () =>
        {
            pulls++;
            return CompletedReply();
        });

        stream.TriggerStreamStarted();
        var enumerator = stream.GetAsyncEnumerator();

        var firstMove = enumerator.MoveNextAsync().AsTask();
        Assert.Equal(1, pulls);
        Assert.False(firstMove.IsCompleted);

        stream.TriggerChunk(17);
        Assert.True(await firstMove);
        Assert.Equal(17, enumerator.Current);

        var secondMove = enumerator.MoveNextAsync().AsTask();
        Assert.Equal(2, pulls);
        stream.TriggerStreamCompleted();

        Assert.False(await secondMove);
        await enumerator.DisposeAsync();
    }

    [Fact]
    public async Task DisposingStream_TerminatesRemoteExecutionOnce()
    {
        var terminations = 0;
        var stream = CreateStream<int>(StreamMode.Pull, terminate: () =>
        {
            terminations++;
            return CompletedReply();
        });

        stream.TriggerStreamStarted();
        var enumerator = stream.GetAsyncEnumerator();

        await enumerator.DisposeAsync();
        await enumerator.DisposeAsync();

        Assert.Equal(1, terminations);
        Assert.True(stream.Completed);
    }

    [Fact]
    public void LifecycleControls_AreSentThroughStreamReply()
    {
        var halts = 0;
        var resumes = 0;
        var stream = CreateStream<int>(
            StreamMode.Push,
            halt: () =>
            {
                halts++;
                return CompletedReply();
            },
            resume: () =>
            {
                resumes++;
                return CompletedReply();
            });

        stream.TriggerStreamStarted();
        stream.Halt();
        stream.Resume();

        Assert.Equal(1, halts);
        Assert.Equal(1, resumes);
        Assert.Throws<InvalidOperationException>(() => stream.Pull());
    }

    [Fact]
    public async Task InvocationContext_PullsGenericAsyncEnumerableAndCancelsIt()
    {
        var disposed = false;
        var context = new InvocationContext(null!, 1);
        context.InitializeStream(StreamMode.Pull, pausable: false);

        Assert.True(context.SetAsyncEnumerable(Values(() => disposed = true)));

        var first = await context.PullAsync();
        var second = await context.PullAsync();

        Assert.True(first.HasValue);
        Assert.Equal(1, first.Value);
        Assert.True(second.HasValue);
        Assert.Equal(2, second.Value);

        await context.TerminateAsync();

        Assert.True(context.CancellationToken.IsCancellationRequested);
        Assert.True(disposed);
    }

    [Fact]
    public async Task InvocationContext_HaltAndResumeGatePushEnumeration()
    {
        var context = new InvocationContext(null!, 1);
        context.InitializeStream(StreamMode.Push, pausable: true);
        context.SetEnumerable(new[] { 3, 4 });

        Assert.True(context.Halt());
        var move = context.MoveNextAsync();
        Assert.False(move.IsCompleted);

        Assert.True(context.Resume());
        var item = await move;

        Assert.True(item.HasValue);
        Assert.Equal(3, item.Value);
        await context.EndAsync();
    }

    static AsyncStreamReply<T> CreateStream<T>(
        StreamMode mode,
        Func<AsyncReply>? pull = null,
        Func<AsyncReply>? terminate = null,
        Func<AsyncReply>? halt = null,
        Func<AsyncReply>? resume = null)
        => new(
            mode,
            pull ?? CompletedReply,
            terminate ?? CompletedReply,
            halt ?? CompletedReply,
            resume ?? CompletedReply);

    static AsyncReply CompletedReply() => new((object)null!);

    static async IAsyncEnumerable<int> Values(
        Action onDisposed,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            yield return 1;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return 2;
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        finally
        {
            onDisposed();
        }
    }
}
