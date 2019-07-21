using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Engine
{
    public interface IAsyncReply<out T>//IAsyncEnumerator<T> 
    {   
        IAsyncReply<T> Then(Action<T> callback);
        IAsyncReply<T> Error(Action<AsyncException> callback);
        IAsyncReply<T> Progress(Action<ProgressType, int, int> callback);
        IAsyncReply<T> Chunk(Action<T> callback);
        void Trigger(object result);
        void TriggerError(AsyncException exception);
        void TriggerProgress(ProgressType type, int value, int max);
        void TriggerChunk(object value);


    }
}
