using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Resource;

namespace Esiur.Engine
{
    public class AsyncReply<T>: AsyncReply
    {
        public void Then(Action<T> callback)
        {
           base.Then(new Action<object>(o => callback((T)o)));
        }

        public void Trigger(T result)
        {
            Trigger((object)result);
        }

        public AsyncReply()
        {
            
        }

        public AsyncReply(T result)
            : base(result)
        {

        }

 

    }
}
