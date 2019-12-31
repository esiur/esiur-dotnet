using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Esyur.Core
{
    public class AsyncBagAwaiter<T> : INotifyCompletion
    {
        Action callback = null;

        AsyncException exception = null;

        T[] result;

        public AsyncBagAwaiter(AsyncBag<T> reply)
        {
            reply.Then(x =>
            {
                this.IsCompleted = true;
                this.result = x;
                this.callback?.Invoke();
            }).Error(x =>
            {
                exception = x;
                this.IsCompleted = true;
                this.callback?.Invoke();
            });
        }

        public T[] GetResult()
        {
            if (exception != null)
                throw exception;
            return result;
        }

        public bool IsCompleted { get; private set; }

        public void OnCompleted(Action continuation)
        {
            if (IsCompleted)
                continuation?.Invoke();
            else
                // Continue....
                callback = continuation;
        }


    }
}

