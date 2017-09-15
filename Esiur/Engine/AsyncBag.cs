using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Engine
{
    public class AsyncBag<T>:AsyncReply
    {
        //List<AsyncReply> replies = new List<AsyncReply>();
        //List<T> results = new List<T>();
        Dictionary<AsyncReply, T> results = new Dictionary<AsyncReply, T>();
        int count = 0;
        bool sealedBag = false;

        public void Then(Action<T[]> callback)
        {
            base.Then(new Action<object>(o => callback((T[])o)));
        }

        /*
        public void Trigger(T[] result)
        {
            Trigger((object)result);
        }
        */

        public void Seal()
        {
            sealedBag = true;

            if (results.Count == 0)
                Trigger(new T[0]);

            for(var i = 0; i < results.Count; i++)
            //foreach(var reply in results.Keys)
                results.Keys.ElementAt(i).Then((r) => {
                    results[results.Keys.ElementAt(i)] = (T)r;
                    count++;
                    if (count == results.Count)
                        Trigger(results.Values.ToArray());
                });
        }

        public void Add(AsyncReply reply)
        {
            if (!sealedBag)
                results.Add(reply, default(T));            
        }

        public AsyncBag()
        {

        }

        /*
        public AsyncBag(T[] result)
        {
            this.result = result;
        }
        */
    }
}
