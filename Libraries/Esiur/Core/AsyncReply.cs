/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

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

using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Esiur.Core;

[AsyncMethodBuilder(typeof(AsyncReplyBuilder))]
public class AsyncReply
{
    public DateTime ReadyTime;

    // These lists are intentionally lazy. Completed replies are common and should not
    // allocate callback storage or a kernel-backed wait primitive unless it is needed.
    protected List<Action<object>> callbacks;
    protected List<Action<AsyncException>> errorCallbacks;
    protected List<Action<ProgressType, uint, uint>> progressCallbacks;
    protected List<Action<object>> chunkCallbacks;
    protected List<Action<object>> propagationCallbacks;
    protected List<Action<byte, string>> warningCallbacks;

    protected volatile object result;
    protected volatile bool resultReady;

    private readonly object asyncLock = new object();
    private volatile AsyncException exception;
    private Exception observedException;
    private ManualResetEventSlim completionEvent;

    public static int MaxId;
    public int Id;

    protected string codePath, codeMethod;
    protected int codeLine;

    public bool Ready => resultReady;

    public bool Failed => exception != null;

    public Exception Exception => exception;

    public object Result => result;

    public static AsyncReply<T> FromResult<T>(T result) => new AsyncReply<T>(result);

    public object Wait()
    {
        ManualResetEventSlim waiter;

        lock (asyncLock)
        {
            if (resultReady)
                return result;
            if (exception != null)
                throw observedException ?? exception;

            waiter = completionEvent ?? (completionEvent = new ManualResetEventSlim(false));
        }

        waiter.Wait();
        return GetWaitResult();
    }

    public object Wait(int millisecondsTimeout)
    {
        ManualResetEventSlim waiter;

        lock (asyncLock)
        {
            if (resultReady)
                return result;
            if (exception != null)
                throw observedException ?? exception;

            waiter = completionEvent ?? (completionEvent = new ManualResetEventSlim(false));
        }

        if (!waiter.Wait(millisecondsTimeout))
        {
            var timeoutException = new Exception("AsyncReply timeout");

            // If completion won the timeout race, return that terminal state. Otherwise
            // retain the timeout on the reply and preserve Wait's historical exception.
            if (TrySetException(timeoutException))
                throw timeoutException;
        }

        return GetWaitResult();
    }

    public void Timeout(int milliseconds, Action callback = null)
    {
        _ = Task.Delay(milliseconds).ContinueWith(_ =>
        {
            var timeoutException = new AsyncException(
                ErrorType.Management,
                (ushort)ExceptionCode.Timeout,
                "Execution timeout expired.");

            if (TrySetException(timeoutException))
                callback?.Invoke();
        });
    }

    public AsyncReply Then(
        Action<object> callback,
        [CallerMemberName] string methodName = null,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        object completedResult = null;
        var invokeImmediately = false;

        lock (asyncLock)
        {
            if (codeLine == 0)
            {
                codeLine = lineNumber;
                codeMethod = methodName;
                codePath = filePath;
            }

            if (resultReady)
            {
                completedResult = result;
                invokeImmediately = true;
            }
            else if (exception == null)
            {
                (callbacks ?? (callbacks = new List<Action<object>>())).Add(callback);
            }
        }

        if (invokeImmediately)
            callback(completedResult);

        return this;
    }

    public AsyncReply Error(Action<AsyncException> callback)
    {
        AsyncException completedException = null;

        lock (asyncLock)
        {
            if (exception != null)
            {
                completedException = exception;
            }
            else if (!resultReady)
            {
                (errorCallbacks ?? (errorCallbacks = new List<Action<AsyncException>>())).Add(callback);
            }
        }

        if (completedException != null)
            callback(completedException);

        return this;
    }

    public AsyncReply Progress(Action<ProgressType, uint, uint> callback)
    {
        lock (asyncLock)
            (progressCallbacks ?? (progressCallbacks = new List<Action<ProgressType, uint, uint>>())).Add(callback);

        return this;
    }

    public AsyncReply Warning(Action<byte, string> callback)
    {
        lock (asyncLock)
            (warningCallbacks ?? (warningCallbacks = new List<Action<byte, string>>())).Add(callback);

        return this;
    }

    public AsyncReply Chunk(Action<object> callback)
    {
        lock (asyncLock)
            (chunkCallbacks ?? (chunkCallbacks = new List<Action<object>>())).Add(callback);

        return this;
    }

    public AsyncReply Propagation(Action<object> callback)
    {
        lock (asyncLock)
            (propagationCallbacks ?? (propagationCallbacks = new List<Action<object>>())).Add(callback);

        return this;
    }

    public void Trigger(object result)
    {
        Action<object> singleCallback = null;
        Action<object>[] registeredCallbacks = null;
        var callbackCount = 0;
        ManualResetEventSlim waiter;

        lock (asyncLock)
        {
            if (exception != null || resultReady)
                return;

            ReadyTime = DateTime.Now;
            this.result = result;
            resultReady = true;

            // AsyncQueue deliberately resets resultReady between deliveries. Snapshot a
            // multi-callback list before unlocking, while keeping its usual one-callback
            // delivery path allocation-free.
            if (callbacks != null)
            {
                callbackCount = callbacks.Count;
                if (callbackCount == 1)
                    singleCallback = callbacks[0];
                else if (callbackCount > 1)
                    registeredCallbacks = callbacks.ToArray();
            }

            waiter = completionEvent;
        }

        waiter?.Set();

        if (callbackCount == 1)
            singleCallback(result);
        else if (registeredCallbacks != null)
            foreach (var callback in registeredCallbacks)
                callback(result);
    }

    public virtual void TriggerError(Exception exception)
    {
        TrySetException(exception);
    }

    internal void TriggerErrorFromBuilder(Exception exception, bool preserveSourceForAwait)
    {
        TrySetException(exception, preserveSourceForAwait);
    }

    internal Exception GetExceptionForAwait()
    {
        lock (asyncLock)
            return observedException ?? exception;
    }

    public void TriggerProgress(ProgressType type, uint value, uint max)
    {
        Action<ProgressType, uint, uint>[] registeredCallbacks;

        lock (asyncLock)
            registeredCallbacks = progressCallbacks?.ToArray();

        if (registeredCallbacks != null)
            foreach (var callback in registeredCallbacks)
                callback(type, value, max);
    }

    public void TriggerWarning(byte level, string message)
    {
        Action<byte, string>[] registeredCallbacks;

        lock (asyncLock)
            registeredCallbacks = warningCallbacks?.ToArray();

        if (registeredCallbacks != null)
            foreach (var callback in registeredCallbacks)
                callback(level, message);
    }

    public void TriggerPropagation(object value)
    {
        Action<object>[] registeredCallbacks;

        lock (asyncLock)
            registeredCallbacks = propagationCallbacks?.ToArray();

        if (registeredCallbacks != null)
            foreach (var callback in registeredCallbacks)
                callback(value);
    }

    public void TriggerChunk(object value)
    {
        Action<object>[] registeredCallbacks;

        lock (asyncLock)
            registeredCallbacks = chunkCallbacks?.ToArray();

        if (registeredCallbacks != null)
            foreach (var callback in registeredCallbacks)
                callback(value);
    }

    public AsyncAwaiter GetAwaiter() => new AsyncAwaiter(this);

    public AsyncReply()
    {
        Id = Interlocked.Increment(ref MaxId) - 1;
    }

    public AsyncReply(object result)
    {
        ReadyTime = DateTime.Now;
        resultReady = true;
        this.result = result;
        Id = Interlocked.Increment(ref MaxId) - 1;
    }

    private object GetWaitResult()
    {
        lock (asyncLock)
        {
            if (exception != null)
                throw observedException ?? exception;

            return result;
        }
    }

    private bool TrySetException(Exception sourceException, bool preserveSourceForAwait = false)
    {
        AsyncException asyncException;
        Action<AsyncException> singleCallback = null;
        Action<AsyncException>[] registeredCallbacks = null;
        var callbackCount = 0;
        ManualResetEventSlim waiter;

        lock (asyncLock)
        {
            if (resultReady || exception != null)
                return false;

            asyncException = sourceException as AsyncException ?? new AsyncException(sourceException);
            observedException = preserveSourceForAwait ? sourceException : asyncException;
            exception = asyncException;

            if (errorCallbacks != null)
            {
                callbackCount = errorCallbacks.Count;
                if (callbackCount == 1)
                    singleCallback = errorCallbacks[0];
                else if (callbackCount > 1)
                    registeredCallbacks = errorCallbacks.ToArray();
            }

            waiter = completionEvent;
        }

        waiter?.Set();

        if (callbackCount == 1)
            singleCallback(asyncException);
        else if (registeredCallbacks != null)
            foreach (var callback in registeredCallbacks)
                callback(asyncException);

        return true;
    }
}
