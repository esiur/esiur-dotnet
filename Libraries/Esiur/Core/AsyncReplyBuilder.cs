using Esiur.Misc;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Core;

public class AsyncReplyBuilder
{
    AsyncReply reply;

    AsyncReplyBuilder(AsyncReply reply)
    {
        this.reply = reply;
    }

    public static AsyncReplyBuilder Create()
    {
        return new AsyncReplyBuilder(new AsyncReply());
    }

    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine
    {
        stateMachine.MoveNext();
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine)
    {
        Global.Log("AsyncReplyBuilder", LogType.Debug, "SetStateMachine");
    }

    public void SetException(Exception exception)
    {
        reply.TriggerError(exception);
    }

    public void SetResult()
    {
        reply.Trigger(null);
    }

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        awaiter.OnCompleted(stateMachine.MoveNext);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
    }

    public AsyncReply Task
    {
        get
        {
            return reply;
        }
    }

}
