/*
 
Copyright (c) 2017-2026 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esiur.Data;
using Esiur.Data.Types;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Esiur.Core;

/// <summary>
/// Represents a remotely executing stream and exposes its lifecycle controls.
/// </summary>
public class AsyncStreamReply : AsyncReply
{
    readonly Func<AsyncReply> _pull;
    readonly Func<AsyncReply> _terminate;
    readonly Func<AsyncReply> _halt;
    readonly Func<AsyncReply> _resume;

    readonly object _streamLock = new object();
    readonly TaskCompletionSource<bool> _started = NewCompletionSource();

    Exception _streamException;
    bool _streamStarted;
    bool _streamCompleted;
    bool _terminationSent;

    /// <summary>
    /// Gets the delivery mode declared by the remote function.
    /// </summary>
    public StreamMode StreamMode { get; }

    /// <summary>
    /// Gets whether the peer acknowledged that the stream has started.
    /// </summary>
    public bool Started
    {
        get
        {
            lock (_streamLock)
                return _streamStarted;
        }
    }

    /// <summary>
    /// Gets whether the stream completed or was terminated.
    /// </summary>
    public bool Completed
    {
        get
        {
            lock (_streamLock)
                return _streamCompleted;
        }
    }

    internal AsyncStreamReply(
        StreamMode streamMode,
        Func<AsyncReply> pull,
        Func<AsyncReply> terminate,
        Func<AsyncReply> halt,
        Func<AsyncReply> resume)
    {
        StreamMode = streamMode;
        _pull = pull;
        _terminate = terminate;
        _halt = halt;
        _resume = resume;
    }

    /// <summary>
    /// Requests the next item of a pull stream.
    /// </summary>
    public AsyncReply Pull()
    {
        if (StreamMode != StreamMode.Pull)
            throw new InvalidOperationException("Pull is only valid for a pull stream.");

        lock (_streamLock)
        {
            if (_streamCompleted)
                return new AsyncReply(null);
        }

        return _pull();
    }

    /// <summary>
    /// Terminates the remote stream execution and releases its enumerator.
    /// </summary>
    public AsyncReply Terminate()
    {
        lock (_streamLock)
        {
            if (_terminationSent || _streamCompleted)
                return new AsyncReply(null);

            _terminationSent = true;
            _streamCompleted = true;
            _started.TrySetResult(true);
        }

        OnStreamCompleted();

        var reply = _terminate();
        reply.Error(TriggerStreamError);
        return reply;
    }

    /// <summary>
    /// Halts a pausable remote stream execution.
    /// </summary>
    public AsyncReply Halt()
    {
        lock (_streamLock)
        {
            if (_streamCompleted)
                return new AsyncReply(null);
        }

        var reply = _halt();
        reply.Error(TriggerStreamError);
        return reply;
    }

    /// <summary>
    /// Resumes a halted remote stream execution.
    /// </summary>
    public AsyncReply Resume()
    {
        lock (_streamLock)
        {
            if (_streamCompleted)
                return new AsyncReply(null);
        }

        var reply = _resume();
        reply.Error(TriggerStreamError);
        return reply;
    }

    internal void TriggerStreamStarted()
    {
        lock (_streamLock)
        {
            if (_streamCompleted || _streamException != null)
                return;

            _streamStarted = true;
            _started.TrySetResult(true);
        }

        OnStreamStarted();
    }

    internal void TriggerStreamCompleted()
    {
        lock (_streamLock)
        {
            if (_streamCompleted)
                return;

            _streamCompleted = true;
            _started.TrySetResult(true);
        }

        OnStreamCompleted();
    }

    internal void TriggerStreamError(AsyncException exception)
    {
        lock (_streamLock)
        {
            if (_streamCompleted || _streamException != null)
                return;

            _streamException = exception;
            _streamCompleted = true;
            _started.TrySetException(exception);
        }

        OnStreamError(exception);

        if (errorCallbacks != null)
            base.TriggerError(exception);
    }

    /// <inheritdoc />
    public override void TriggerError(Exception exception)
    {
        TriggerStreamError(exception as AsyncException ?? new AsyncException(exception));
    }

    internal Task WaitUntilStartedAsync() => _started.Task;

    internal Exception GetStreamException()
    {
        lock (_streamLock)
            return _streamException;
    }

    internal bool IsStreamCompleted()
    {
        lock (_streamLock)
            return _streamCompleted;
    }

    protected virtual void OnStreamStarted() { }
    protected virtual void OnStreamCompleted() { }
    protected virtual void OnStreamError(Exception exception) { }

    protected static TaskCompletionSource<bool> NewCompletionSource()
        => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// A typed remote stream that can be consumed with <c>await foreach</c>.
/// Pull streams request exactly one remote item for each <c>MoveNextAsync</c> call.
/// </summary>
public sealed class AsyncStreamReply<T> : AsyncStreamReply, IAsyncEnumerable<T>
{
    readonly object _itemsLock = new object();
    readonly Queue<T> _items = new Queue<T>();

    TaskCompletionSource<bool> _itemAvailable;
    bool _enumeratorCreated;
    bool _movePending;

    internal AsyncStreamReply(
        StreamMode streamMode,
        Func<AsyncReply> pull,
        Func<AsyncReply> terminate,
        Func<AsyncReply> halt,
        Func<AsyncReply> resume)
        : base(streamMode, pull, terminate, halt, resume)
    {
        Chunk(value => ReceiveItem((T)RuntimeCaster.Cast(value, typeof(T))));
    }

    /// <inheritdoc />
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        lock (_itemsLock)
        {
            if (_enumeratorCreated)
                throw new InvalidOperationException("A remote stream can only be enumerated once.");

            _enumeratorCreated = true;
        }

        return new Enumerator(this, cancellationToken);
    }

    void ReceiveItem(T item)
    {
        TaskCompletionSource<bool> itemAvailable;

        lock (_itemsLock)
        {
            if (IsStreamCompleted() || GetStreamException() != null)
                return;

            _items.Enqueue(item);
            itemAvailable = _itemAvailable;
            _itemAvailable = null;
        }

        itemAvailable?.TrySetResult(true);
    }

    protected override void OnStreamCompleted()
    {
        TaskCompletionSource<bool> itemAvailable;

        lock (_itemsLock)
        {
            itemAvailable = _itemAvailable;
            _itemAvailable = null;
        }

        itemAvailable?.TrySetResult(true);
    }

    protected override void OnStreamError(Exception exception)
    {
        TaskCompletionSource<bool> itemAvailable;

        lock (_itemsLock)
        {
            itemAvailable = _itemAvailable;
            _itemAvailable = null;
        }

        itemAvailable?.TrySetException(exception);
    }

    async Task<(bool HasValue, T Value)> MoveNextAsync(CancellationToken cancellationToken)
    {
        lock (_itemsLock)
        {
            if (_movePending)
                throw new InvalidOperationException("Concurrent MoveNextAsync calls are not supported.");

            _movePending = true;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WaitUntilStartedAsync();

            while (true)
            {
                TaskCompletionSource<bool> itemAvailable;

                lock (_itemsLock)
                {
                    var exception = GetStreamException();
                    if (exception != null)
                        throw exception;

                    if (_items.Count > 0)
                        return (true, _items.Dequeue());

                    if (IsStreamCompleted())
                        return (false, default);

                    _itemAvailable = NewCompletionSource();
                    itemAvailable = _itemAvailable;
                }

                if (StreamMode == StreamMode.Pull)
                {
                    var pull = Pull();
                    _ = pull.Error(TriggerStreamError);
                }

                using (cancellationToken.Register(() =>
                {
                    itemAvailable.TrySetCanceled();
                    _ = Terminate();
                }))
                {
                    await itemAvailable.Task;
                }
            }
        }
        finally
        {
            lock (_itemsLock)
                _movePending = false;
        }
    }

    async Task DisposeAsync()
    {
        if (IsStreamCompleted())
            return;

        await Terminate();
    }

    sealed class Enumerator : IAsyncEnumerator<T>
    {
        readonly AsyncStreamReply<T> _owner;
        readonly CancellationToken _cancellationToken;

        public T Current { get; private set; }

        internal Enumerator(AsyncStreamReply<T> owner, CancellationToken cancellationToken)
        {
            _owner = owner;
            _cancellationToken = cancellationToken;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            var result = await _owner.MoveNextAsync(_cancellationToken);
            Current = result.Value;
            return result.HasValue;
        }

        public ValueTask DisposeAsync() => new ValueTask(_owner.DisposeAsync());
    }
}
