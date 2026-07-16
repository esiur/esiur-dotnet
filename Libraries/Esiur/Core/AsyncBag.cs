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

using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esiur.Core;

interface IAsyncBag
{
    public void Add(object replyOrValue);
}

public class AsyncBag<T> : AsyncReply, IAsyncBag
{

    protected List<object> replies = new List<object>();
    //List<T> results = new();

    int count = 0;
    bool sealedBag = false;
    readonly object bagLock = new object();


    public virtual Type ArrayType { get; set; } = typeof(T);

    public AsyncBag<T> Then(Action<T[]> callback)
    {
        //if (!sealedBag && !resultReady)
        //    throw new Exception("Not sealed");

        //Timeout(6000, () =>
        //{
        //Console.WriteLine("Timeout " + count + this.Result);
        //});

        base.Then(new Action<object>(o => callback((T[])o)));
        return this;
    }

    public new AsyncBagAwaiter<T> GetAwaiter()
    {
        return new AsyncBagAwaiter<T>(this);
    }

    public new T[] Wait()
    {
        return (T[])base.Wait();
    }

    public new T[] Wait(int timeout)
    {
        return (T[])base.Wait(timeout);
    }

    public void Seal()
    {
        object[] pending;
        lock (bagLock)
        {
            if (sealedBag)
                return;

            sealedBag = true;
            pending = replies.ToArray();
        }

        var results = ArrayType == null ? new T[pending.Length]
                                        : Array.CreateInstance(ArrayType, pending.Length);

        if (pending.Length == 0)
        {
            Trigger(results);
            return;
        }

        for (var i = 0; i < pending.Length; i++)
        {
            var k = pending[i];
            var index = i;

            if (k is AsyncReply reply)
            {
                reply.Then((r) =>
                {
                    results.SetValue(r, index);
                    if (Interlocked.Increment(ref count) == pending.Length)
                        Trigger(results);
                }).Error(e => TriggerError(e));
            }
            else
            {
                if (ArrayType != null)
                    k = RuntimeCaster.Cast(k, ArrayType);

                results.SetValue(k, index);
                if (Interlocked.Increment(ref count) == pending.Length)
                    Trigger(results);
            }
        }
    }


    public void Add(object valueOrReply)
    {
        lock (bagLock)
        {
            if (!sealedBag)
                replies.Add(valueOrReply);
        }
    }


    public void AddBag(AsyncBag<T> bag)
    {
        if (bag == null)
            throw new ArgumentNullException(nameof(bag));

        object[] source;
        lock (bag.bagLock)
            source = bag.replies.ToArray();

        foreach (var r in source)
            Add(r);
    }



    public AsyncBag()
    {

    }

    public AsyncBag(T[] results)
        : base(results)
    {

    }


}
