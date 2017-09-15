using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Engine
{
    public class AsyncReply
    {
        protected List<Action<object>> callbacks = new List<Action<object>>();
        protected object result;
        object callbacksLock = new object();

        //bool fired = false;
        protected bool resultReady = false;

        public bool Ready
        {
            get { return resultReady; }
        }

        public object Result
        {
            get { return result; }
        }

        public void Then(Action<object> callback)
        {
            callbacks.Add(callback);

            if (resultReady)
                callback(result);
            //    Trigger(this.result);
        }

        public void Trigger(object result)
        {
            //if (!fired)
            //{
            this.result = result;
            resultReady = true;

            lock (callbacksLock)
            {
                foreach (var cb in callbacks)
                    cb(result);
                //callbacks.Clear();
            }
            /*
                if (callback == null)
                {
                    fireAtChance = true;
                }
                else
                {
                    callback(result);
                    fired = true;
                }
                */
            //}
        }

        public AsyncReply()
        {

        }

        public AsyncReply(object result)
        {
            resultReady = true;
            this.result = result;
        }
    }
}
