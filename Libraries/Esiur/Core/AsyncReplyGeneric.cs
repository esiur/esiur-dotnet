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
using System.Runtime.CompilerServices;

namespace Esiur.Core;

[AsyncMethodBuilder(typeof(AsyncReplyBuilder<>))]
public class AsyncReply<T> : AsyncReply
{
    public AsyncReply<T> Then(
        Action<T> callback,
        [CallerMemberName] string methodName = null,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        base.Then(value => callback((T)value), methodName, filePath, lineNumber);
        return this;
    }

    public new AsyncReply<T> Progress(Action<ProgressType, uint, uint> callback)
    {
        base.Progress(callback);
        return this;
    }

    public AsyncReply<T> Chunk(Action<T> callback)
    {
        base.Chunk(value => callback((T)value));
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

    public new AsyncAwaiter<T> GetAwaiter() => new AsyncAwaiter<T>(this);

    public new T Wait() => (T)base.Wait();

    public new T Wait(int millisecondsTimeout) => (T)base.Wait(millisecondsTimeout);
}
