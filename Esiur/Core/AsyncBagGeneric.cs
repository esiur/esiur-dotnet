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

public class AsyncBag<T> : AsyncBag
{
    public AsyncBag<T> Then(Action<T[]> callback)
    {
        base.Then(new Action<object>((o) => callback(((object[])o).Select(x => (T)x).ToArray())));
        return this;
    }


    public void Add(AsyncReply<T> reply)
    {
        base.Add(reply);
    }

    public void AddBag(AsyncBag<T> bag)
    {
        foreach (var r in bag.replies)
            Add(r);
    }


    public new AsyncBagAwaiter<T> GetAwaiter()
    {
        return new AsyncBagAwaiter<T>(this);
    }

    public new T[] Wait()
    {
        return base.Wait().Select(x => (T)x).ToArray();
    }

    public AsyncBag()
    {

    }

    public AsyncBag(T[] results)
        : base(results.Select(x => (object)x).ToArray())
    {

    }
}