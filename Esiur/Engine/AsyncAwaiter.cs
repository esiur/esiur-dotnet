using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Engine
{
    public class AsyncAwaiter<T> : INotifyCompletion
    {
        Action callback = null;
        T result;
        private bool completed;

        public AsyncAwaiter(AsyncReply<T> reply)
        {
            reply.Then(x =>
            {
                completed = true;
                result = x;
                callback?.Invoke();
            });
        }

        public T GetResult()
        {
            return result;
        }

        public bool IsCompleted => completed;

        //From INotifyCompletion
        public void OnCompleted(Action continuation)
        {
            Console.WriteLine("Continue....");
        }



    }
}
