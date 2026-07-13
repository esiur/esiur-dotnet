using Esiur.Core;
using Esiur.Data.Types;
using Esiur.Resource;

namespace Esiur.Tests.Unit.Integration;

[Resource]
public partial class StreamResource
{
    [Export]
    public async IAsyncEnumerable<int> Numbers(int count, InvocationContext context)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(5, context.CancellationToken);
            yield return i;
        }
    }

    [Export]
    public async IAsyncEnumerable<int> Infinite(InvocationContext context)
    {
        var value = 0;
        while (true)
        {
            await Task.Delay(5, context.CancellationToken);
            yield return value++;
        }
    }

    [Export]
    [Stream(StreamMode.Push, Pausable = true)]
    public AsyncReply<int> Pausable(InvocationContext context)
    {
        var reply = new AsyncReply<int>();

        Task.Run(async () =>
        {
            await Task.Delay(100);

            for (var i = 0; i < 3; i++)
            {
                await context.WaitWhileHaltedAsync();
                reply.TriggerChunk(i);
                await Task.Delay(100);
            }

            reply.Trigger(0);
        });

        return reply;
    }
}
