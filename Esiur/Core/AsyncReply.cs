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
using Esiur.Resource;
using System.Reflection;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace Esiur.Core;

[AsyncMethodBuilder(typeof(AsyncReplyBuilder))]
public class AsyncReply
{

    protected List<Action<object>> callbacks = new List<Action<object>>();
    protected object result;

    protected List<Action<AsyncException>> errorCallbacks = null;

    protected List<Action<ProgressType, uint, uint>> progressCallbacks = null;

    protected List<Action<object>> chunkCallbacks = null;

    protected List<Action<object>> propagationCallbacks = null;
    protected List<Action<byte, string>> warningCallbacks = null;


    object asyncLock = new object();

    //public Timer timeout;// = new Timer()
    protected bool resultReady = false;
    AsyncException exception;
    // StackTrace trace;
    AutoResetEvent mutex = new AutoResetEvent(false);

    public static int MaxId;

    public int Id;

    public bool Ready
    {
        get { return resultReady; }
    }

    public bool Failed => exception != null;

    public Exception Exception => exception;

    public static AsyncReply<T> FromResult<T>(T result) => new AsyncReply<T>(result);

    public object Wait()
    {
        if (resultReady)
            return result;

        mutex.WaitOne();

        if (exception != null)
            throw exception;

        return result;
    }

    //int timeoutMilliseconds = 0;
    public AsyncReply Timeout(int milliseconds, Action callback = null)
    {

        //timeoutMilliseconds = milliseconds;

        Task.Delay(milliseconds).ContinueWith(x =>
        {
            if (!resultReady && exception == null)
            {
                TriggerError(new AsyncException(ErrorType.Management,
                   (ushort)ExceptionCode.Timeout, "Execution timeout expired."));

                callback?.Invoke();
            }
        });

        return this;
    }

    public object Wait(int millisecondsTimeout)
    {
        if (resultReady)
            return result;

        //if (Debug)
        //    Console.WriteLine($"AsyncReply: {Id} Wait");

        if (!mutex.WaitOne(millisecondsTimeout))
        {
            var e = new Exception("AsyncReply timeout");
            TriggerError(e);
            throw e;
        }

        //if (Debug)
        //    Console.WriteLine($"AsyncReply: {Id} Wait ended");

        return result;
    }

    public object Result
    {
        get { return result; }
    }


    protected string codePath, codeMethod;
    protected int codeLine;

    public AsyncReply Then(Action<object> callback, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (codeLine == 0)
        {
            codeLine = lineNumber; codeMethod = methodName; codePath = filePath;
        }
        //lock (callbacksLock)
        //{
        lock (asyncLock)
        {
            //  trace = new StackTrace();

            if (resultReady)
            {
                //if (Debug)
                //    Console.WriteLine($"AsyncReply: {Id} Then ready");

                callback(result);
                return this;
            }


            //timeout = new Timer(x =>
            //{
            //    // Get calling method name
            //    Console.WriteLine(trace.GetFrame(1).GetMethod().Name);

            //    var tr = String.Join("\r\n", trace.GetFrames().Select(f => f.GetMethod().Name));
            //    timeout.Dispose();

            //    tr = trace.ToString();
            //    throw new Exception("Request timeout " + Id);
            //}, null, 15000, 0);


            //if (Debug)
            //    Console.WriteLine($"AsyncReply: {Id} Then pending");

            callbacks.Add(callback);

            return this;
        }
    }



    public AsyncReply Error(Action<AsyncException> callback)
    {

        if (errorCallbacks == null)
            errorCallbacks = new List<Action<AsyncException>>();

        errorCallbacks.Add(callback);

        if (exception != null)
            callback(exception);

        return this;
    }

    public AsyncReply Progress(Action<ProgressType, uint, uint> callback)
    {
        if (progressCallbacks == null)
            progressCallbacks = new List<Action<ProgressType, uint, uint>>();

        progressCallbacks.Add(callback);
        return this;
    }

    public AsyncReply Warning(Action<byte, string> callback)
    {
        if (warningCallbacks == null)
            warningCallbacks = new List<Action<byte, string>>();

        warningCallbacks.Add(callback);
        return this;
    }

    public AsyncReply Chunk(Action<object> callback)
    {
        if (chunkCallbacks == null)
            chunkCallbacks = new List<Action<object>>();

        chunkCallbacks.Add(callback);
        return this;
    }

    public AsyncReply Propagation(Action<object> callback)
    {
        if (propagationCallbacks == null)
            propagationCallbacks = new List<Action<object>>();

        propagationCallbacks.Add(callback);
        return this;
    }

    public AsyncReply Trigger(object result)
    {
        lock (asyncLock)
        {
            //timeout?.Dispose();

            if (exception != null)
                return this;

            //if (Debug)
            //    Console.WriteLine($"AsyncReply: {Id} Trigger");

            if (resultReady)
                return this;

            this.result = result;

            resultReady = true;

            //if (mutex != null)
            mutex.Set();

            foreach (var cb in callbacks)
                cb(result);


            //if (Debug)
            //    Console.WriteLine($"AsyncReply: {Id} Trigger ended");

        }

        return this;
    }

    public AsyncReply TriggerError(Exception exception)
    {
        //timeout?.Dispose();

        if (resultReady)
            return this;

        if (exception is AsyncException)
            this.exception = exception as AsyncException;
        else
            this.exception = new AsyncException(exception);

        if (errorCallbacks != null)
        {
            foreach (var cb in errorCallbacks)
                cb(this.exception);
        }
        else
        {
            // no error handlers found
            throw exception;
        }

        mutex?.Set();

        return this;
    }

    public AsyncReply TriggerProgress(ProgressType type, uint value, uint max)
    {
        //timeout?.Dispose();

        if (progressCallbacks != null)
            foreach (var cb in progressCallbacks)
                cb(type, value, max);

        return this;
    }

    public AsyncReply TriggerWarning(byte level, string message)
    {
        //timeout?.Dispose();

        if (warningCallbacks != null)
            foreach (var cb in warningCallbacks)
                cb(level, message);

        return this;
    }


    public AsyncReply TriggerPropagation(object value)
    {
        //timeout?.Dispose();

        if (propagationCallbacks != null)
            foreach (var cb in propagationCallbacks)
                cb(value);

        return this;
    }



    public AsyncReply TriggerChunk(object value)
    {

        //timeout?.Dispose();


        if (chunkCallbacks != null)
            foreach (var cb in chunkCallbacks)
                cb(value);


        return this;
    }

    public AsyncAwaiter GetAwaiter()
    {
        return new AsyncAwaiter(this);
    }



    public AsyncReply()
    {
        //   this.Debug = true;
        Id = MaxId++;
    }

    public AsyncReply(object result)
    {
        //   this.Debug = true;
        resultReady = true;
        this.result = result;

        Id = MaxId++;
    }

    /*
public AsyncReply<T> Then(Action<T> callback)
    {
       base.Then(new Action<object>(o => callback((T)o)));
        return this;
    }

    public void Trigger(T result)
    {
        Trigger((object)result);
    }

    public Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
     }

    public AsyncReply()
    {

    }

    public new Task<T> Task
    {
        get
        {
            return base.Task.ContinueWith<T>((t) =>
            {

#if NETSTANDARD
                return (T)t.GetType().GetTypeInfo().GetProperty("Result").GetValue(t);
#else
                return (T)t.GetType().GetProperty("Result").GetValue(t);
#endif
            });
        }
    }

    public T Current => throw new NotImplementedException();

    public AsyncReply(T result)
        : base(result)
    {

    }

*/



}
