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

namespace Esiur.Core
{
    public class AsyncReply<T>: IAsyncReply<T>
    {

        protected List<Action<T>> callbacks = new List<Action<T>>();
        protected T result;

        protected List<Action<AsyncException>> errorCallbacks = new List<Action<AsyncException>>();
        
        protected List<Action<ProgressType, int, int>> progressCallbacks = new List<Action<ProgressType, int, int>>();

        protected List<Action<T>> chunkCallbacks = new List<Action<T>>();

        //List<AsyncAwaiter> awaiters = new List<AsyncAwaiter>();

        object callbacksLock = new object();

        protected bool resultReady = false;
        AsyncException exception;


        public bool Ready
        {
            get { return resultReady; }

        }

        

        public object Result
        {
            get { return result; }
        }

        public IAsyncReply<T> Then(Action<T> callback)
        {
            callbacks.Add(callback);

            if (resultReady)
                callback(result);

            return this;
        }

        public IAsyncReply<T> Error(Action<AsyncException> callback)
        {
            errorCallbacks.Add(callback);

            if (exception != null)
                callback(exception);

            return this;
        }

        public IAsyncReply<T> Progress(Action<ProgressType, int, int> callback)
        {
            progressCallbacks.Add(callback);
            return this;
        }

        
        public IAsyncReply<T> Chunk(Action<T> callback)
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

                this.result = (T)result;
                resultReady = true;

                foreach (var cb in callbacks)
                    cb((T)result);


            }

        }

        public void TriggerError(Exception exception)
        {
            if (resultReady)
                return;

            if (exception is AsyncException)
                this.exception = exception as AsyncException;
            else
                this.exception = new AsyncException(ErrorType.Management, 0, exception.Message);
             

            lock (callbacksLock)
            {
                foreach (var cb in errorCallbacks)
                    cb(this.exception);
            }

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
                    cb((T)value);

            }
        }

        public AsyncAwaiter<T> GetAwaiter()
        {
            return new AsyncAwaiter<T>(this);
        }

    



        public AsyncReply()
        {

        }

        public AsyncReply(T result)
        {
            resultReady = true;
            this.result = result;
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

#if NETSTANDARD1_5
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
}
