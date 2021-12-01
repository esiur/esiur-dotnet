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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Core;

public class AsyncReply
{




    protected List<Action<object>> callbacks = new List<Action<object>>();
    protected object result;

    protected List<Action<AsyncException>> errorCallbacks = new List<Action<AsyncException>>();

    protected List<Action<ProgressType, int, int>> progressCallbacks = new List<Action<ProgressType, int, int>>();

    protected List<Action<object>> chunkCallbacks = new List<Action<object>>();

    object callbacksLock = new object();

    protected bool resultReady = false;
    AsyncException exception;

    TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();


    public bool Ready
    {
        get { return resultReady; }
    }

    public object Result
    {
        get { return result; }
    }

    public AsyncReply Then(Action<object> callback)
    {
        callbacks.Add(callback);

        if (resultReady)
            callback(result);

        return this;
    }

    public AsyncReply Error(Action<AsyncException> callback)
    {
        errorCallbacks.Add(callback);

        if (exception != null)
        {
            callback(exception);
            tcs.SetException(exception);
        }

        return this;
    }

    public AsyncReply Progress(Action<ProgressType, int, int> callback)
    {
        progressCallbacks.Add(callback);
        return this;
    }

    public AsyncReply Chunk(Action<object> callback)
    {
        chunkCallbacks.Add(callback);
        return this;
    }

    public void Trigger(object result)
    {

        lock (callbacksLock)
        {
            if (resultReady)
                return;

            this.result = result;
            resultReady = true;

            foreach (var cb in callbacks)
                cb(result);

            tcs.TrySetResult(result);

        }

    }

    public void TriggerError(AsyncException exception)
    {
        if (resultReady)
            return;

        this.exception = exception;


        lock (callbacksLock)
        {
            foreach (var cb in errorCallbacks)
                cb(exception);
        }

        tcs.TrySetException(exception);
    }

    public void TriggerProgress(ProgressType type, int value, int max)
    {
        if (resultReady)
            return;

        lock (callbacksLock)
        {
            foreach (var cb in progressCallbacks)
                cb(type, value, max);

        }
    }

    public void TriggerChunk(object value)
    {
        if (resultReady)
            return;

        lock (callbacksLock)
        {
            foreach (var cb in chunkCallbacks)
                cb(value);

        }
    }


    public Task Task
    {
        get
        {
            return tcs.Task;
        }
    }

    public AsyncReply()
    {

    }

    public AsyncReply(object result)
    {
        resultReady = true;
        tcs.SetResult(result);
        this.result = result;
    }
}
