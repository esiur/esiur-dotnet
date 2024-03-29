﻿/*
 
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

[AsyncMethodBuilder(typeof(AsyncReplyBuilder<>))]
public class AsyncReply<T> : AsyncReply
{

    public AsyncReply<T> Then(Action<T> callback)
    {
        base.Then((x) => callback((T)x));
        return this;
    }

    public new AsyncReply<T> Progress(Action<ProgressType, int, int> callback)
    {
        base.Progress(callback);
        return this;
    }


    public AsyncReply<T> Chunk(Action<T> callback)
    {
        chunkCallbacks.Add((x) => callback((T)x));
        return this;
    }

    public AsyncReply(T result)
       : base(result)
    {

    }

    public AsyncReply()
        : base()
    {

    }

    public new AsyncAwaiter<T> GetAwaiter()
    {
        return new AsyncAwaiter<T>(this);
    }

    public new T Wait()
    {
        return (T)base.Wait();
    }

    public new T Wait(int millisecondsTimeout)
    {
        return (T)base.Wait(millisecondsTimeout);
    }

    /*
    protected new List<Action> callbacks = new List<Action>();
    protected new object result;

    protected new List<Action<AsyncException>> errorCallbacks = new List<Action<AsyncException>>();

    protected new List<Action<ProgressType, int, int>> progressCallbacks = new List<Action<ProgressType, int, int>>();

    protected new List<Action> chunkCallbacks = new List<Action>();

    //List<AsyncAwaiter> awaiters = new List<AsyncAwaiter>();

    object asyncLock = new object();

    //public Timer timeout;// = new Timer()

    AsyncException exception;
    // StackTrace trace;
    AutoResetEvent mutex = new AutoResetEvent(false);

    public static int MaxId;

    public int Id;

    public bool Ready
    {
        get { return resultReady; }

    }


    public T Wait()
    {

        if (resultReady)
            return result;

        if (Debug)
            Console.WriteLine($"AsyncReply: {Id} Wait");

        //mutex = new AutoResetEvent(false);
        mutex.WaitOne();

        if (Debug)
            Console.WriteLine($"AsyncReply: {Id} Wait ended");


        return result;
    }


    public object Result
    {
        get { return result; }
    }


    public IAsyncReply<T> Then(Action<T> callback)
    {
        //lock (callbacksLock)
        //{
        lock (asyncLock)
        {
            //  trace = new StackTrace();

            if (resultReady)
            {
                if (Debug)
                    Console.WriteLine($"AsyncReply: {Id} Then ready");

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


            if (Debug)
                Console.WriteLine($"AsyncReply: {Id} Then pending");



            callbacks.Add(callback);

            return this;
        }
    }



    public IAsyncReply<T> Error(Action<AsyncException> callback)
    {
        // lock (callbacksLock)
        //  {
        errorCallbacks.Add(callback);

        if (exception != null)
            callback(exception);

        return this;
        //}
    }

    public IAsyncReply<T> Progress(Action<ProgressType, int, int> callback)
    {
        //lock (callbacksLock)
        //{
        progressCallbacks.Add(callback);
        return this;
        //}
    }


    public IAsyncReply<T> Chunk(Action<T> callback)
    {
        // lock (callbacksLock)
        // {
        chunkCallbacks.Add(callback);
        return this;
        // }
    }

    public void Trigger(object result)
    {
        lock (asyncLock)
        {
            //timeout?.Dispose();

            if (Debug)
                Console.WriteLine($"AsyncReply: {Id} Trigger");

            if (resultReady)
                return;

            this.result = (T)result;

            resultReady = true;

            //if (mutex != null)
            mutex.Set();

            foreach (var cb in callbacks)
                cb((T)result);


            if (Debug)
                Console.WriteLine($"AsyncReply: {Id} Trigger ended");

        }
    }

    public void TriggerError(Exception exception)
    {
        //timeout?.Dispose();

        if (resultReady)
            return;

        if (exception is AsyncException)
            this.exception = exception as AsyncException;
        else
            this.exception = new AsyncException(ErrorType.Management, 0, exception.Message);


        // lock (callbacksLock)
        // {
        foreach (var cb in errorCallbacks)
            cb(this.exception);
        //  }

        mutex?.Set();

    }

    public void TriggerProgress(ProgressType type, int value, int max)
    {
        //timeout?.Dispose();

        if (resultReady)
            return;

        //lock (callbacksLock)
        //{
        foreach (var cb in progressCallbacks)
            cb(type, value, max);

        //}
    }


    public void TriggerChunk(object value)
    {

        //timeout?.Dispose();

        if (resultReady)
            return;

        //lock (callbacksLock)
        //{
        foreach (var cb in chunkCallbacks)
            cb((T)value);

        //}
    }

    public AsyncAwaiter<T> GetAwaiter()
    {
        return new AsyncAwaiter<T>(this);
    }



    public AsyncReply()
    {
        //   this.Debug = true;
        Id = MaxId++;
    }

    public AsyncReply(T result)
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



*/




}
