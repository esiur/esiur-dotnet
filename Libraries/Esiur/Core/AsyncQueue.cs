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

    bool captureProcessedItems;
    List<AsyncQueueItem<T>> processed = new();

    List<AsyncQueueItem<T>> list = new List<AsyncQueueItem<T>>();
    //Action<T> callback;
    object queueLock = new object();

    //public AsyncQueue<T> Then(Action<T> callback)
    //{
    //  base.Then(new Action<object>(o => callback((T)o)));

    //return this;
    //}

    /// <summary>
    /// Enables or disables retaining delivered queue items for diagnostics.
    /// Capture is disabled by default, and disabling it discards retained items.
    /// </summary>
    public void SetProcessedCapture(bool enabled)
    {
        lock (queueLock)
        {
            captureProcessedItems = enabled;

            if (!enabled)
                processed = new();
        }
    }

    /// <summary>
    /// Atomically returns and removes the delivered queue items retained since the previous drain.
    /// </summary>
    public List<AsyncQueueItem<T>> DrainProcessed()
    {
        lock (queueLock)
        {
            var result = processed;
            processed = new();
            return result;
        }
    }

    public void Add(AsyncReply<T> reply)
        => Add(reply, !reply.Ready);

    public void Add(AsyncReply<T> reply, bool hasResource)
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
                HasResource = hasResource
            });
            resultReady = false;
        }

        if (reply.Ready)
            processQueue(default(T));
        else
            reply.Then(processQueue).Error(TriggerError);
    }

    public void Remove(AsyncReply<T> reply)
    {
        lock (queueLock)
        {
            var index = list.FindIndex(item => ReferenceEquals(item.Reply, reply));
            if (index >= 0)
                list.RemoveAt(index);
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

            if (batchSize > 0)
            {
                var flushId = currentFlushId++;
                var readyItems = list.GetRange(0, batchSize);

                // Shift the remaining list once. The previous RemoveAt(0) loop shifted
                // it once per delivered item and became quadratic for large batches.
                list.RemoveRange(0, batchSize);

                foreach (var item in readyItems)
                {
                    Trigger(item.Reply.Result);
                    resultReady = false;

                    if (captureProcessedItems)
                    {
                        var processedItem = item;
                        processedItem.Delivered = DateTime.Now;
                        processedItem.Ready = processedItem.Reply.ReadyTime;
                        processedItem.BatchSize = batchSize;
                        processedItem.FlushId = flushId;
                        processed.Add(processedItem);
                    }
                }
            }

            resultReady = list.Count == 0;
        }
    }

    public AsyncQueue()
    {

    }
}
