using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Engine
{
    public class AsyncQueue<T> : AsyncReply
    {
        List<AsyncReply<T>> list = new List<AsyncReply<T>>();
        //Action<T> callback;
        object queueLock = new object();

        public void Then(Action<T> callback)
        {
            base.Then(new Action<object>(o => callback((T)o)));
        }

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
