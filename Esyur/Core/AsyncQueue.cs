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

namespace Esyur.Core
{
    public class AsyncQueue<T> : AsyncReply<T>
    {
        List<AsyncReply<T>> list = new List<AsyncReply<T>>();
        //Action<T> callback;
        object queueLock = new object();

        //public AsyncQueue<T> Then(Action<T> callback)
        //{
          //  base.Then(new Action<object>(o => callback((T)o)));

            //return this;
        //}

        public void Add(AsyncReply<T> reply)
        {
            lock (queueLock)
                list.Add(reply);

            resultReady = false;
            reply.Then(processQueue);
        }

        public void Remove(AsyncReply<T> reply)
        {
            lock (queueLock)
                list.Remove(reply);
            processQueue(default(T));
        }

        void processQueue(T o)
        {
            lock (queueLock)
                for (var i = 0; i < list.Count; i++)
                    if (list[i].Ready)
                    {
                        Trigger(list[i].Result);
                        resultReady = false;
                        list.RemoveAt(i);
                        i--;
                    }
                    else
                        break;

            resultReady = (list.Count == 0);
        }

        public AsyncQueue()
        {

        }
    }
}
