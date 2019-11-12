using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Core
{
    public class AsyncReplyBuilder<T>
    {
        AsyncReply<T> reply;

        AsyncReplyBuilder(AsyncReply<T> reply)
        {
            this.reply = reply;
        }

        public static AsyncReplyBuilder<T> Create()
        {
            return new AsyncReplyBuilder<T>(new AsyncReply<T>());
        }

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            Console.WriteLine("SetStateMachine");
        }

        public void SetException(Exception exception)
        {
            reply.TriggerError(exception);
        }

        public void SetResult(T result)
        {
            reply.Trigger(result);
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            Console.WriteLine("AwaitOnCompleted");

        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            Console.WriteLine("AwaitUnsafeOnCompleted");

        }

        public AsyncReply<T> Task
        {
            get {
                return reply;
            }
        }

    }
}
