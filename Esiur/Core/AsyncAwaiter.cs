using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Core
{
    public class AsyncAwaiter<T> : INotifyCompletion
    {
        public Action callback = null;
        public T result;
        private bool completed;

        public AsyncAwaiter(AsyncReply<T> reply)
        {
            reply.Then(x =>
            {
                this.completed = true;
                this.result = x;
                this.callback?.Invoke();
            });
        }

        public T GetResult()
        {
            return result;
        }

        public bool IsCompleted => completed;

        public void OnCompleted(Action continuation)
        {
            // Continue....
            callback = continuation;
        }



    }
}
