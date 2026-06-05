using Esiur.Core;

namespace Esiur.Tests.Unit;

public class AsyncQueueTests
{
    [Fact]
    public void ProcessedCapture_IsDisabledByDefault()
    {
        var queue = new AsyncQueue<int>();
        var delivered = new List<int>();
        queue.Then(delivered.Add);

        for (var i = 0; i < 100; i++)
            queue.Add(new AsyncReply<int>(i));

        Assert.Equal(Enumerable.Range(0, 100), delivered);
        Assert.Empty(queue.DrainProcessed());
    }

    [Fact]
    public void DrainProcessed_ReturnsCapturedItemsExactlyOnce()
    {
        var queue = new AsyncQueue<int>();
        queue.SetProcessedCapture(true);

        for (var i = 0; i < 100; i++)
            queue.Add(new AsyncReply<int>(i));

        var processed = queue.DrainProcessed();

        Assert.Equal(100, processed.Count);
        Assert.Equal(Enumerable.Range(1, 100), processed.Select(x => x.Sequence));
        Assert.All(processed, x => Assert.NotEqual(default, x.Delivered));
        Assert.Empty(queue.DrainProcessed());
    }

    [Fact]
    public void DrainProcessed_PreservesExplicitResourceWorkFlag()
    {
        var queue = new AsyncQueue<int>();
        queue.SetProcessedCapture(true);

        queue.Add(new AsyncReply<int>(1), hasResource: false);

        var pending = new AsyncReply<int>();
        queue.Add(pending, hasResource: true);
        pending.Trigger(2);

        var processed = queue.DrainProcessed();

        Assert.Collection(
            processed,
            x => Assert.False(x.HasResource),
            x => Assert.True(x.HasResource));
    }

    [Fact]
    public void DisablingCapture_DiscardsHistoryAndStopsCapturing()
    {
        var queue = new AsyncQueue<int>();
        queue.SetProcessedCapture(true);
        queue.Add(new AsyncReply<int>(1));

        queue.SetProcessedCapture(false);
        queue.Add(new AsyncReply<int>(2));

        Assert.Empty(queue.DrainProcessed());
    }

    [Fact]
    public async Task DrainProcessed_DoesNotLoseItemsDuringConcurrentAdds()
    {
        const int itemCount = 1000;
        var queue = new AsyncQueue<int>();
        var sequences = new HashSet<int>();
        queue.SetProcessedCapture(true);

        var producer = Task.Run(() =>
            Parallel.For(0, itemCount, i => queue.Add(new AsyncReply<int>(i))));

        while (!producer.IsCompleted)
        {
            foreach (var item in queue.DrainProcessed())
                Assert.True(sequences.Add(item.Sequence));

            await Task.Yield();
        }

        await producer;

        foreach (var item in queue.DrainProcessed())
            Assert.True(sequences.Add(item.Sequence));

        Assert.Equal(Enumerable.Range(1, itemCount), sequences.OrderBy(x => x));
    }
}
