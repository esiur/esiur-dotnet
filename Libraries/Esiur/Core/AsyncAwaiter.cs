using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Esiur.Core;

public class AsyncAwaiter : INotifyCompletion
{
    private static readonly Action CompletedSentinel = () => { };

    private Action continuation;
    private AsyncException exception;
    private object result;
    private readonly AsyncReply reply;

    public AsyncAwaiter(AsyncReply reply)
    {
        this.reply = reply;
        reply.Then(Complete).Error(Fail);
    }

    public object GetResult()
    {
        if (exception != null)
            throw reply.GetExceptionForAwait();

        return result;
    }

    public bool IsCompleted
        => ReferenceEquals(Volatile.Read(ref continuation), CompletedSentinel);

    public void OnCompleted(Action continuation)
    {
        if (continuation == null)
            throw new ArgumentNullException(nameof(continuation));

        var previous = Interlocked.CompareExchange(ref this.continuation, continuation, null);
        if (ReferenceEquals(previous, CompletedSentinel))
        {
            continuation();
        }
        else if (previous != null)
        {
            throw new InvalidOperationException("The awaiter already has a continuation.");
        }
    }

    private void Complete(object value)
    {
        result = value;
        InvokeContinuation();
    }

    private void Fail(AsyncException value)
    {
        exception = value;
        InvokeContinuation();
    }

    private void InvokeContinuation()
    {
        var registeredContinuation = Interlocked.Exchange(ref continuation, CompletedSentinel);
        if (registeredContinuation != null && !ReferenceEquals(registeredContinuation, CompletedSentinel))
            registeredContinuation();
    }
}
