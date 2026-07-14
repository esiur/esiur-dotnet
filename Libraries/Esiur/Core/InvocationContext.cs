using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Esiur.Core
{
    public class InvocationContext
    {
        readonly uint CallbackId;
        readonly object _stateLock = new object();
        readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        readonly SemaphoreSlim _moveLock = new SemaphoreSlim(1, 1);
        TaskCompletionSource<bool> _resumeSignal = CompletedSignal();

        IAsyncEnumeratorAdapter _asyncEnumerator;
        IEnumerator _enumerator;
        bool _halted;

        internal StreamMode StreamMode { get; private set; }
        internal bool Pausable { get; private set; }
        internal IResource Resource { get; private set; }
        internal FunctionDef Function { get; private set; }

        internal volatile bool Ended;

        /// <summary>
        /// Gets a token that is cancelled when the caller terminates this execution.
        /// </summary>
        public CancellationToken CancellationToken => _cancellation.Token;

        /// <summary>
        /// Gets whether a pausable execution is currently halted.
        /// </summary>
        public bool IsHalted
        {
            get
            {
                lock (_stateLock)
                    return _halted;
            }
        }

        public void Chunk(object value)
        {
            if (Ended)
                throw new Exception("Execution has ended.");

            Connection.SendChunk(CallbackId, value);
        }

        public void Progress(uint value, uint max)
        {
            if (Ended)
                throw new Exception("Execution has ended.");

            Connection.SendProgress(CallbackId, value, max);
        }

        public void Warning(byte level, string message)
        {
            if (Ended)
                throw new Exception("Execution has ended.");

            Connection.SendWarning(CallbackId, level, message);
        }

        public EpConnection Connection { get; internal set; }

        internal InvocationContext(EpConnection connection, uint callbackId)
        {
            Connection = connection;
            CallbackId = callbackId;
        }

        internal void BindOperation(IResource resource, FunctionDef function)
        {
            Resource = resource;
            Function = function;
        }

        internal void InitializeStream(StreamMode streamMode, bool pausable)
        {
            StreamMode = streamMode;
            Pausable = pausable;
        }

        internal bool SetAsyncEnumerable(object enumerable)
        {
            var asyncEnumerableInterface = enumerable.GetType()
                .GetInterfaces()
                .Concat(new[] { enumerable.GetType() })
                .FirstOrDefault(x => x.IsGenericType &&
                                     x.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

            if (asyncEnumerableInterface == null)
                return false;

            var itemType = asyncEnumerableInterface.GetGenericArguments()[0];
            var adapterType = typeof(AsyncEnumeratorAdapter<>).MakeGenericType(itemType);
            _asyncEnumerator = (IAsyncEnumeratorAdapter)Activator.CreateInstance(
                adapterType, enumerable, _cancellation.Token);
            return true;
        }

        internal void SetEnumerable(IEnumerable enumerable)
        {
            _enumerator = enumerable.GetEnumerator();
        }

        internal async Task<(bool HasValue, object Value)> PullAsync()
        {
            if (StreamMode != StreamMode.Pull || _asyncEnumerator == null)
                throw new InvalidOperationException("Execution is not an asynchronous pull stream.");

            await _moveLock.WaitAsync(_cancellation.Token);
            try
            {
                await WaitWhileHaltedAsync();
                _cancellation.Token.ThrowIfCancellationRequested();

                var hasValue = await _asyncEnumerator.MoveNextAsync();
                return (hasValue, hasValue ? _asyncEnumerator.Current : null);
            }
            finally
            {
                _moveLock.Release();
            }
        }

        internal async Task<(bool HasValue, object Value)> MoveNextAsync()
        {
            if (_enumerator == null)
                throw new InvalidOperationException("Execution is not a synchronous stream.");

            await WaitWhileHaltedAsync();
            _cancellation.Token.ThrowIfCancellationRequested();

            var hasValue = _enumerator.MoveNext();
            return (hasValue, hasValue ? _enumerator.Current : null);
        }

        internal bool Halt()
        {
            lock (_stateLock)
            {
                if (Ended || !Pausable || _halted)
                    return false;

                _halted = true;
                _resumeSignal = NewSignal();
                return true;
            }
        }

        internal bool Resume()
        {
            TaskCompletionSource<bool> resumeSignal;

            lock (_stateLock)
            {
                if (Ended || !Pausable || !_halted)
                    return false;

                _halted = false;
                resumeSignal = _resumeSignal;
            }

            resumeSignal.TrySetResult(true);
            return true;
        }

        /// <summary>
        /// Asynchronously waits until a halted execution is resumed.
        /// </summary>
        public async Task WaitWhileHaltedAsync()
        {
            Task resumeTask;

            lock (_stateLock)
                resumeTask = _resumeSignal.Task;

            await resumeTask;
            _cancellation.Token.ThrowIfCancellationRequested();
        }

        internal async Task TerminateAsync()
        {
            TaskCompletionSource<bool> resumeSignal;

            lock (_stateLock)
            {
                if (Ended)
                    return;

                Ended = true;
                _halted = false;
                resumeSignal = _resumeSignal;
            }

            resumeSignal.TrySetResult(true);
            _cancellation.Cancel();

            if (_asyncEnumerator != null)
                await _asyncEnumerator.DisposeAsync();

            (_enumerator as IDisposable)?.Dispose();
        }

        internal async Task EndAsync()
        {
            TaskCompletionSource<bool> resumeSignal;

            lock (_stateLock)
            {
                if (Ended)
                    return;

                Ended = true;
                _halted = false;
                resumeSignal = _resumeSignal;
            }

            resumeSignal.TrySetResult(true);

            if (_asyncEnumerator != null)
                await _asyncEnumerator.DisposeAsync();

            (_enumerator as IDisposable)?.Dispose();
        }

        static TaskCompletionSource<bool> NewSignal()
            => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        static TaskCompletionSource<bool> CompletedSignal()
        {
            var signal = NewSignal();
            signal.SetResult(true);
            return signal;
        }
    }

    interface IAsyncEnumeratorAdapter
    {
        object Current { get; }
        ValueTask<bool> MoveNextAsync();
        ValueTask DisposeAsync();
    }

    sealed class AsyncEnumeratorAdapter<T> : IAsyncEnumeratorAdapter
    {
        readonly IAsyncEnumerator<T> _enumerator;

        public object Current => _enumerator.Current;

        public AsyncEnumeratorAdapter(IAsyncEnumerable<T> enumerable, CancellationToken cancellationToken)
        {
            _enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
        }

        public ValueTask<bool> MoveNextAsync() => _enumerator.MoveNextAsync();
        public ValueTask DisposeAsync() => _enumerator.DisposeAsync();
    }
}
