using Esiur.Core;

namespace Esiur.Tests.Unit;

public class AsyncBagTests
{
    [Fact]
    public void Seal_CollectsConcurrentReplyCompletionsExactlyOnce()
    {
        const int count = 2_000;
        var replies = Enumerable.Range(0, count)
            .Select(_ => new AsyncReply<int>())
            .ToArray();
        var bag = new AsyncBag<int>();
        foreach (var reply in replies)
            bag.Add(reply);

        bag.Seal();
        Parallel.For(0, replies.Length, index => replies[index].Trigger(index));

        Assert.Equal(Enumerable.Range(0, count), bag.Wait(5_000));
    }

    [Fact]
    public void AddBag_UsesAStableSnapshot()
    {
        var source = new AsyncBag<int>();
        source.Add(1);
        source.Add(new AsyncReply<int>(2));

        var destination = new AsyncBag<int>();
        destination.AddBag(source);
        destination.Seal();

        Assert.Equal(new[] { 1, 2 }, destination.Wait(1_000));
    }
}
