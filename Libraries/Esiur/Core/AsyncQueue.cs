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

public struct AsyncQueueItem<T>
{
    public AsyncReply<T> Reply;
    public int Sequence;
    public DateTime Arrival;
    public DateTime Delivered;
    public DateTime Ready;
    public int BatchSize;
    public int FlushId;
    public int NotificationsCountWaitingInTheQueueAtEnqueueing;
    public bool HasResource;
}


public class AsyncQueue<T> : AsyncReply<T>
{

    int currentId = 0;
    int currentFlushId;

    public List<AsyncQueueItem<T>> Processed = new();

    List<AsyncQueueItem<T>> list = new List<AsyncQueueItem<T>>();
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
        {
            currentId++;
            list.Add(new AsyncQueueItem<T>()
            {
                Sequence = currentId,
                NotificationsCountWaitingInTheQueueAtEnqueueing = list.Count,
                Reply = reply,
                Arrival = DateTime.Now,
                HasResource = !reply.Ready
            });
        }

        resultReady = false;
        if (reply.Ready)
            processQueue(default(T));
        else
            reply.Then(processQueue);
    }

    public void Remove(AsyncReply<T> reply)
    {
        lock (queueLock)
        {
            var item = list.FirstOrDefault(i => i.Reply == reply);
            list.Remove(item);
        }

        processQueue(default(T));
    }

    void processQueue(T o)
    {
        lock (queueLock)
        {
            var batchSize = 0;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Reply.Ready)
                {
                    batchSize++;
                }
                else
                {
                    break;
                }
            }

            var flushId = currentFlushId++;

            for (var i = 0; i < list.Count; i++)
                if (list[i].Reply.Ready)
                {
                    Trigger(list[i].Reply.Result);
                    resultReady = false;

                    var p = list[i];
                    p.Delivered = DateTime.Now;
                    p.Ready = p.Reply.ReadyTime;
                    p.BatchSize = batchSize;
                    p.FlushId = flushId;
                    //p.HasResource = p.Reply. (p.Ready - p.Arrival).TotalMilliseconds > 5;
                    Processed.Add(p);

                    list.RemoveAt(i);

                    i--;
                }
                else
                    break;
        }

        resultReady = (list.Count == 0);
    }

    public AsyncQueue()
    {

    }
}
