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

namespace Esiur.Core;

interface IAsyncBag
{
    public void Add(AsyncReply reply);
}

public class AsyncBag<T> : AsyncReply, IAsyncBag
{

    protected List<AsyncReply> replies = new List<AsyncReply>();
    List<T> results = new();

    int count = 0;
    bool sealedBag = false;


    public virtual Type ArrayType { get; set; } = typeof(T);

    public AsyncBag<T> Then(Action<T[]> callback)
    {
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
        if (sealedBag)
            return;

        sealedBag = true;

        if (results.Count == 0)
        {
            if (ArrayType != null)
            {
                var ar = Array.CreateInstance(ArrayType, 0);
                Trigger(ar);
            }
            else
            {
                Trigger(new object[0]);
            }
        }

        for (var i = 0; i < results.Count; i++)
        //foreach(var reply in results.Keys)
        {
            var k = replies[i];// results.Keys.ElementAt(i);
            var index = i;

            k.Then((r) =>
            {
                results[index] = (T)r;
                count++;
                if (count == results.Count)
                {
                    if (ArrayType != null)
                    {
                        try
                        {
                            // @TODO: Safe casting check
                            var ar = Array.CreateInstance(ArrayType, count);
                            for (var i = 0; i < count; i++)
                                ar.SetValue(results[i], i);
                            Trigger(ar);
                        }
                        catch
                        {
                            Trigger(results.ToArray());
                        }
                    }
                    else
                        Trigger(results.ToArray());
                }
            });
        }
    }

    public void Add(AsyncReply reply)
    {
        if (!sealedBag)
        {
            results.Add(default(T));
            replies.Add(reply);
        }
    }

    public void AddBag(AsyncBag<T> bag)
    {
        foreach (var r in bag.replies)
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
