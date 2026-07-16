using Esiur.Net;

namespace Esiur.Tests.Unit;

public class NetworkBufferTests
{
    [Fact]
    public void FragmentedWritesAreConcatenatedAndReadResetsState()
    {
        var buffer = new NetworkBuffer();

        buffer.Write(new byte[] { 1, 2 });
        buffer.Write(new byte[] { 99, 3, 4, 100 }, 1, 2);

        Assert.Equal(4u, buffer.Available);
        Assert.True(buffer.CanRead);
        Assert.False(buffer.Protected);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer.Read());
        Assert.Equal(0u, buffer.Available);
        Assert.False(buffer.CanRead);
        Assert.False(buffer.Protected);
        Assert.Null(buffer.Read());
    }

    [Fact]
    public void HoldForRetainsPartialPacketUntilThreshold()
    {
        var buffer = new NetworkBuffer();

        buffer.HoldFor(new byte[] { 1, 2 }, 5);
        Assert.True(buffer.Protected);
        Assert.False(buffer.CanRead);
        Assert.Null(buffer.Read());

        buffer.Write(new byte[] { 3, 4 });
        Assert.True(buffer.Protected);
        Assert.Null(buffer.Read());

        buffer.Write(new byte[] { 5 });
        Assert.False(buffer.Protected);
        Assert.True(buffer.CanRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer.Read());
    }

    [Fact]
    public void HoldForPrependsPartialPacketToBufferedData()
    {
        var buffer = new NetworkBuffer();

        buffer.Write(new byte[] { 3, 4 });
        buffer.HoldFor(new byte[] { 0, 1, 2, 9 }, 1, 2, 5);
        buffer.Write(new byte[] { 5 });

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer.Read());
    }

    [Fact]
    public void ProtectRetainsOnlyTheRemainingSlice()
    {
        var buffer = new NetworkBuffer();
        var source = new byte[] { 99, 1, 2 };

        Assert.True(buffer.Protect(source, 1, 4));
        Assert.Equal(2u, buffer.Available);
        Assert.True(buffer.Protected);

        buffer.Write(new byte[] { 3, 4 });
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer.Read());
    }

    [Fact]
    public void ProtectDoesNotRetainACompleteSlice()
    {
        var buffer = new NetworkBuffer();

        Assert.False(buffer.Protect(new byte[] { 0, 1, 2, 3 }, 1, 3));
        Assert.Equal(0u, buffer.Available);
        Assert.Null(buffer.Read());
    }

    [Fact]
    public void InvalidRangesAndImpossibleThresholdsAreRejected()
    {
        var buffer = new NetworkBuffer();
        var source = new byte[4];

        Assert.Throws<ArgumentNullException>(() => buffer.Write(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Write(source, 5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Write(source, 3, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Protect(source, 5, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Protect(source, 0, uint.MaxValue));
        Assert.Throws<Exception>(() => buffer.HoldFor(source, 0, 2, 2));
    }

    [Fact]
    public void FragmentedGrowthHasLinearAllocationBound()
    {
        const int payloadLength = 512 * 1024;
        const int chunkLength = 1024;
        var chunk = new byte[chunkLength];

        // Warm up the methods used below so JIT allocation is outside the measurement.
        var warmup = new NetworkBuffer();
        warmup.Write(chunk);
        warmup.Write(chunk);
        _ = warmup.Read();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var buffer = new NetworkBuffer();
        for (var offset = 0; offset < payloadLength; offset += chunkLength)
            buffer.Write(chunk);

        var result = buffer.Read();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.NotNull(result);
        Assert.Equal(payloadLength, result.Length);
        Assert.True(
            allocated < payloadLength * 8L,
            $"Fragmented buffering allocated {allocated:N0} bytes for a {payloadLength:N0}-byte payload.");
    }
}
