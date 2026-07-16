using Esiur.Core;
using Esiur.Data.Types;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Esiur.Tests.Unit;

public class AsyncReplyConcurrencyTests
{
    [Fact]
    public void CompletedRepliesKeepCallbackAndWaiterStorageLazy()
    {
        var reply = new AsyncReply<int>(42);
        var awaiter = reply.GetAwaiter();

        Assert.True(reply.Ready);
        Assert.True(awaiter.IsCompleted);
        Assert.Equal(42, awaiter.GetResult());
        Assert.Null(GetBaseField(reply, "callbacks"));
        Assert.Null(GetBaseField(reply, "errorCallbacks"));
        Assert.Null(GetBaseField(reply, "completionEvent"));
    }

    [Fact]
    public void CompletedReplyAllocationStaysWithinObjectAndLockBudget()
    {
        const int count = 4096;
        const int maximumBytesPerReply = 224;
        var replies = new AsyncReply<int>[count];

        // Warm up constructors and property access before measuring this thread.
        for (var i = 0; i < 32; i++)
            _ = new AsyncReply<int>(i).Ready;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < replies.Length; i++)
            replies[i] = new AsyncReply<int>(i);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(replies);

        Assert.True(
            allocated < count * (long)maximumBytesPerReply,
            $"Completed replies allocated {allocated / (double)count:N1} bytes each.");
    }

    [Fact]
    public void TriggerErrorWithoutAHandlerIsRetainedAndObservedByWaiters()
    {
        var reply = new AsyncReply<int>();
        var source = new InvalidOperationException("synchronous failure");

        var triggerException = Record.Exception(() => reply.TriggerError(source));

        Assert.Null(triggerException);
        Assert.False(reply.Ready);
        Assert.True(reply.Failed);
        var stored = Assert.IsType<AsyncException>(reply.Exception);
        Assert.Same(source, stored.InnerException);
        Assert.Same(stored, Assert.Throws<AsyncException>(() => reply.Wait()));
        Assert.Same(stored, Assert.Throws<AsyncException>(() => reply.Wait(10)));

        AsyncException? observed = null;
        reply.Error(error => observed = error);
        Assert.Same(stored, observed);
    }

    [Fact]
    public async Task CustomAsyncBuilderRetainsAnErrorThrownBeforeTheFirstAwait()
    {
        AsyncReply<int>? reply = null;

        var creationException = Record.Exception(() => reply = FailBeforeFirstAwait(fail: true));

        Assert.Null(creationException);
        Assert.NotNull(reply);
        Assert.True(reply!.Failed);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => _ = await reply);
    }

    [Fact]
    public async Task CustomAsyncBuilderPreservesAsyncExceptionAfterSuspension()
    {
        var gate = new AsyncReply();
        var reply = FailAfterFirstAwait(gate);
        var observation = AwaitAsTask(reply);

        gate.Trigger(null!);

        var exception = await Assert.ThrowsAsync<AsyncException>(() => observation);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task CustomAsyncBuilderPublishesSuspensionBeforeInlineContinuation()
    {
        AsyncReply<int>? reply = null;

        var creationException = Record.Exception(() => reply = FailAfterInlineCompletion());

        Assert.Null(creationException);
        var exception = await Assert.ThrowsAsync<AsyncException>(async () => _ = await reply!);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void StreamErrorsReachTheBaseTerminalStateWithoutAnErrorHandler()
    {
        static AsyncReply CompletedControl() => new AsyncReply((object)null!);

        var stream = new AsyncStreamReply<int>(
            StreamMode.Push,
            CompletedControl,
            CompletedControl,
            CompletedControl,
            CompletedControl);
        var expected = new AsyncException(ErrorType.Exception, 0, "stream failed");

        stream.TriggerError(expected);

        Assert.True(stream.Failed);
        Assert.Same(expected, stream.Exception);
        Assert.Same(expected, Assert.Throws<AsyncException>(() => stream.Wait()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalStateWakesEverySynchronousWaiter(bool fail)
    {
        const int waiterCount = 8;
        var reply = new AsyncReply<int>();
        var results = new int[waiterCount];
        var errors = new Exception?[waiterCount];
        var threads = new Thread[waiterCount];

        for (var i = 0; i < threads.Length; i++)
        {
            var index = i;
            threads[i] = new Thread(() =>
            {
                try
                {
                    results[index] = reply.Wait();
                }
                catch (Exception exception)
                {
                    errors[index] = exception;
                }
            })
            {
                IsBackground = true
            };
            threads[i].Start();
        }

        var allWaiting = SpinWait.SpinUntil(
            () => threads.All(thread =>
                (thread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0),
            millisecondsTimeout: 5000);

        if (fail)
            reply.TriggerError(new InvalidOperationException("failed"));
        else
            reply.Trigger(73);

        var allJoined = threads.Select(thread => thread.Join(2000)).ToArray();

        Assert.True(allWaiting, "Not all test threads reached the blocking wait.");
        Assert.All(allJoined, joined => Assert.True(joined, "A synchronous waiter was not released."));

        if (fail)
            Assert.All(errors, error => Assert.IsType<AsyncException>(error));
        else
        {
            Assert.All(results, value => Assert.Equal(73, value));
            Assert.All(errors, Assert.Null);
        }
    }

    [Fact]
    public void CompletionCallbacksRunOutsideTheReplyLock()
    {
        var reply = new AsyncReply<int>();
        reply.Then(_ =>
        {
            var concurrentRegistration = Task.Run(() => reply.Then(__ => { }));
            if (!concurrentRegistration.Wait(2000))
                throw new TimeoutException("Concurrent callback registration was blocked by the completion callback.");
        });

        Assert.Null(Record.Exception(() => reply.Trigger(1)));
    }

    [Fact]
    public void ErrorRegistrationRacingWithFailureInvokesExactlyOnce()
    {
        for (var iteration = 0; iteration < 500; iteration++)
        {
            var reply = new AsyncReply<int>();
            var expected = new AsyncException(ErrorType.Exception, 0, "failed");
            AsyncException? observed = null;
            var calls = 0;

            Parallel.Invoke(
                () => reply.Error(error =>
                {
                    observed = error;
                    Interlocked.Increment(ref calls);
                }),
                () => reply.TriggerError(expected));

            Assert.Equal(1, Volatile.Read(ref calls));
            Assert.Same(expected, observed);
        }
    }

    [Fact]
    public void AwaiterContinuationIsNotLostDuringCompletionRace()
    {
        for (var iteration = 0; iteration < 500; iteration++)
        {
            var reply = new AsyncReply<int>();
            var awaiter = reply.GetAwaiter();
            using var continued = new ManualResetEventSlim(false);

            Parallel.Invoke(
                () => awaiter.OnCompleted(continued.Set),
                () => reply.Trigger(iteration));

            Assert.True(continued.Wait(2000), "The awaiter continuation was lost.");
            Assert.True(awaiter.IsCompleted);
            Assert.Equal(iteration, awaiter.GetResult());
        }
    }

    private static object? GetBaseField(AsyncReply reply, string name)
        => typeof(AsyncReply)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(reply);

    private static async AsyncReply<int> FailBeforeFirstAwait(bool fail)
    {
        if (fail)
            throw new InvalidOperationException("failed before await");

        await Task.Yield();
        return 1;
    }

    private static async AsyncReply<int> FailAfterFirstAwait(AsyncReply gate)
    {
        await gate;
        throw new InvalidOperationException("failed after await");
    }

    private static async Task<int> AwaitAsTask(AsyncReply<int> reply) => await reply;

    private static async AsyncReply<int> FailAfterInlineCompletion()
    {
        await new InlineCompletion();
        throw new InvalidOperationException("failed after inline continuation");
    }

    private readonly struct InlineCompletion
    {
        public Awaiter GetAwaiter() => new Awaiter();

        public readonly struct Awaiter : INotifyCompletion
        {
            public bool IsCompleted => false;

            public void GetResult()
            {
            }

            public void OnCompleted(Action continuation) => continuation();
        }
    }
}
