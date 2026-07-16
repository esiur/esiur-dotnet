using Esiur.Net;
using Esiur.Net.Sockets;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;

namespace Esiur.Tests.Unit;

public class FrameworkWebSocketTests
{
    [Fact]
    public async Task AcceptedSocket_ReceivesBinaryDataAndCompletesOnceOnPeerClose()
    {
        var platformSocket = new TestWebSocket();
        platformSocket.QueueBinary(new byte[] { 1, 2, 3 });
        platformSocket.QueueClose();

        var local = new IPEndPoint(IPAddress.Loopback, 443);
        var remote = new IPEndPoint(IPAddress.Parse("192.0.2.10"), 55123);
        var socket = new FrameworkWebSocket(platformSocket, local, remote);
        var receiver = new RecordingReceiver();
        socket.Receiver = receiver;

        Assert.True(socket.Begin());
        await socket.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        await socket.CloseNotification.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(local, socket.LocalEndPoint);
        Assert.Equal(remote, socket.RemoteEndPoint);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(receiver.Messages));
        Assert.Equal(1, receiver.CloseCount);
        Assert.Equal(1, platformSocket.CloseOutputCount);

        socket.Close();
        socket.Destroy();
        Assert.Equal(1, receiver.CloseCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ep")]
    [InlineData("not-ep")]
    public void AcceptedSocket_RequiresExactEpSubprotocol(string? subProtocol)
    {
        var platformSocket = new TestWebSocket(subProtocol);

        var exception = Assert.Throws<ArgumentException>(() =>
            new FrameworkWebSocket(
                platformSocket,
                new IPEndPoint(IPAddress.Loopback, 443),
                new IPEndPoint(IPAddress.Parse("192.0.2.10"), 55123)));

        Assert.Contains("'EP' subprotocol", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OwnerCancellation_EndsReceiveLoopWithoutFaultOrDuplicateClose()
    {
        var platformSocket = new TestWebSocket();
        using var cancellation = new CancellationTokenSource();
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000),
            cancellation.Token);
        var receiver = new RecordingReceiver();
        socket.Receiver = receiver;

        Assert.True(socket.Begin());
        cancellation.Cancel();
        await socket.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        await socket.CloseNotification.WaitAsync(TimeSpan.FromSeconds(2));

        socket.Close();
        socket.Destroy();
        socket.Destroy();

        Assert.Equal(1, receiver.CloseCount);
        Assert.Equal(SocketState.Closed, socket.State);
    }

    [Fact]
    public async Task LocalClose_WaitsForPeerAcknowledgementBeforeCompleting()
    {
        var platformSocket = new TestWebSocket();
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000));
        var receiver = new RecordingReceiver();
        socket.Receiver = receiver;

        Assert.True(socket.Begin());
        var completion = socket.CloseAsync();

        await Task.Delay(25);
        Assert.False(completion.IsCompleted);
        Assert.Equal(1, platformSocket.CloseOutputCount);

        platformSocket.QueueClose();
        await completion.WaitAsync(TimeSpan.FromSeconds(2));
        await socket.CloseNotification.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, receiver.CloseCount);
        Assert.Equal(SocketState.Closed, socket.State);
    }

    [Fact]
    public async Task SimultaneousPeerClose_AcknowledgesAfterAnInFlightSend()
    {
        var platformSocket = new TestWebSocket();
        platformSocket.BlockSends();
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000));
        socket.Receiver = new RecordingReceiver();

        Assert.True(socket.Begin());
        var send = socket.SendAsync(new byte[] { 1 }, 0, 1);
        await platformSocket.SendStarted.WaitAsync(TimeSpan.FromSeconds(2));

        var completion = socket.CloseAsync();
        platformSocket.QueueClose();
        platformSocket.ReleaseSends();

        await completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(await send);
        Assert.Equal(1, platformSocket.CloseOutputCount);
    }

    [Fact]
    public async Task SendQueue_OverflowAbortsOnceAndSettlesEveryReply()
    {
        var platformSocket = new TestWebSocket();
        platformSocket.BlockSends();
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000))
        {
            MaximumPendingSendBytes = 4,
        };
        var receiver = new RecordingReceiver();
        socket.Receiver = receiver;

        var first = socket.SendAsync(new byte[] { 1, 2 }, 0, 2);
        await platformSocket.SendStarted.WaitAsync(TimeSpan.FromSeconds(2));

        var second = socket.SendAsync(new byte[] { 3, 4 }, 0, 2);
        var overflow = socket.SendAsync(new byte[] { 5 }, 0, 1);

        Assert.True(overflow.Failed);
        Assert.Contains("send queue exceeded", overflow.Exception?.Message);
        Assert.True(second.Failed);
        Assert.Contains("send queue exceeded", second.Exception?.Message);
        Assert.False(await first);
        await Assert.ThrowsAsync<InvalidOperationException>(() => socket.Completion);
        await socket.CloseNotification.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(SocketState.Closed, socket.State);
        Assert.Equal(0, socket.PendingSendBytes);
        Assert.Equal(1, platformSocket.AbortCount);
        Assert.Equal(1, receiver.CloseCount);
        Assert.Empty(platformSocket.SentMessages);

        var later = socket.SendAsync(new byte[] { 6 }, 0, 1);
        Assert.True(later.Ready);
        Assert.False(await later);

        socket.Destroy();
        Assert.Equal(1, platformSocket.AbortCount);
        Assert.Equal(1, receiver.CloseCount);
    }

    [Fact]
    public async Task SynchronousSend_OverflowAbortsBeforeThrowing()
    {
        var platformSocket = new TestWebSocket();
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000))
        {
            MaximumPendingSendBytes = 1,
        };
        var receiver = new RecordingReceiver();
        socket.Receiver = receiver;
        socket.Hold();

        socket.Send(new byte[] { 1 }, 0, 1);
        var error = Assert.Throws<InvalidOperationException>(() =>
            socket.Send(new byte[] { 2 }, 0, 1));

        Assert.Contains("send queue exceeded", error.Message);
        await socket.CloseNotification.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(SocketState.Closed, socket.State);
        Assert.Equal(0, socket.PendingSendBytes);
        Assert.Equal(1, platformSocket.AbortCount);
        Assert.Equal(1, receiver.CloseCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => socket.Completion);
    }

    [Fact]
    public async Task SharedSendBudget_BoundsAggregateQueuedBytesAcrossSockets()
    {
        var budget = new TestPendingSendBudget(3);
        var firstPlatform = new TestWebSocket();
        firstPlatform.BlockSends();
        var firstSocket = CreateAcceptedSocket(firstPlatform, budget);
        var secondSocket = CreateAcceptedSocket(new TestWebSocket(), budget);

        var firstSend = firstSocket.SendAsync(new byte[] { 1, 2 }, 0, 2);
        await firstPlatform.SendStarted.WaitAsync(TimeSpan.FromSeconds(2));

        var overflow = secondSocket.SendAsync(new byte[] { 3, 4 }, 0, 2);

        Assert.True(overflow.Failed);
        Assert.Contains("host-wide", overflow.Exception?.Message);
        Assert.Equal(SocketState.Closed, secondSocket.State);
        Assert.Equal(2, budget.ReservedBytes);

        firstSocket.Destroy();
        Assert.False(await firstSend);
        await firstSocket.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, budget.ReservedBytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => secondSocket.Completion);
    }

    [Fact]
    public async Task Completion_DoesNotWaitForAnInProgressSendReplyCallback()
    {
        var platformSocket = new TestWebSocket();
        platformSocket.BlockSends();
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000));
        using var callbackStarted = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();

        try
        {
            var send = socket.SendAsync(new byte[] { 1 }, 0, 1);
            _ = send.Then(_ =>
            {
                callbackStarted.Set();
                releaseCallback.Wait();
            });
            await platformSocket.SendStarted.WaitAsync(TimeSpan.FromSeconds(2));
            platformSocket.ReleaseSends();
            Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(2)));

            socket.Destroy();
            await socket.Completion.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.False(releaseCallback.IsSet);
            Assert.True(send.Ready);
            Assert.True(await send);
        }
        finally
        {
            releaseCallback.Set();
            socket.Destroy();
        }
    }

    [Fact]
    public async Task Destroy_DoesNotWaitForABlockedQueuedSendReplyCallback()
    {
        var platformSocket = new TestWebSocket();
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000));
        using var callbackStarted = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();

        try
        {
            socket.Hold();
            var send = socket.SendAsync(new byte[] { 1 }, 0, 1);
            _ = send.Then(_ =>
            {
                callbackStarted.Set();
                releaseCallback.Wait();
            });

            socket.Destroy();
            await socket.Completion.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(send.Ready);
            Assert.False(await send);
            Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(2)));
            Assert.False(releaseCallback.IsSet);
        }
        finally
        {
            releaseCallback.Set();
            socket.Destroy();
        }
    }

    [Fact]
    public async Task TextMessage_FaultsCompletionAndNotifiesCloseOnce()
    {
        var platformSocket = new TestWebSocket();
        platformSocket.QueueText("not EP");
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000));
        var receiver = new RecordingReceiver();
        socket.Receiver = receiver;

        Assert.True(socket.Begin());
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await socket.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        await socket.CloseNotification.WaitAsync(TimeSpan.FromSeconds(2));

        socket.Close();
        socket.Destroy();
        Assert.Equal(1, receiver.CloseCount);
    }

    [Fact]
    public async Task Completion_DoesNotWaitForABlockedReceiverCloseCallback()
    {
        var platformSocket = new TestWebSocket();
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000));
        using var callbackStarted = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        socket.Receiver = new BlockingCloseReceiver(callbackStarted, releaseCallback);

        try
        {
            socket.Destroy();
            await socket.Completion.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(2)));
            Assert.False(socket.CloseNotification.IsCompleted);
        }
        finally
        {
            releaseCallback.Set();
        }

        await socket.CloseNotification.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ThrowingSendCallback_DoesNotCorruptQueueOrCloseTransport()
    {
        var platformSocket = new TestWebSocket();
        var socket = new FrameworkWebSocket(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000));

        socket.Hold();
        var reply = socket.SendAsync(new byte[] { 42 }, 0, 1);
        _ = reply.Then(_ => throw new InvalidOperationException("consumer callback failed"));
        socket.Unhold();

        Assert.True(await reply);
        Assert.Equal(SocketState.Established, socket.State);
        Assert.Equal(0, socket.PendingSendBytes);
        Assert.Equal(new byte[] { 42 }, Assert.Single(platformSocket.SentMessages));

        socket.Destroy();
        await socket.Completion.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void OutboundUri_RequiresWebSocketScheme()
    {
        Assert.Throws<ArgumentException>(() =>
            new FrameworkWebSocket(new Uri("https://example.test/esiur")));

        _ = new FrameworkWebSocket(new Uri("wss://example.test/esiur?tenant=one"));
        Assert.Equal("EP", FrameworkWebSocket.SubProtocol);
    }

    private sealed class RecordingReceiver : INetworkReceiver<ISocket>
    {
        public List<byte[]> Messages { get; } = new List<byte[]>();
        public int CloseCount;

        public void NetworkClose(ISocket sender) => Interlocked.Increment(ref CloseCount);

        public void NetworkConnect(ISocket sender)
        {
        }

        public void NetworkReceive(ISocket sender, NetworkBuffer buffer)
        {
            var message = buffer.Read();
            if (message != null)
                Messages.Add(message);
        }
    }

    private sealed class BlockingCloseReceiver : INetworkReceiver<ISocket>
    {
        private readonly ManualResetEventSlim callbackStarted;
        private readonly ManualResetEventSlim releaseCallback;

        public BlockingCloseReceiver(
            ManualResetEventSlim callbackStarted,
            ManualResetEventSlim releaseCallback)
        {
            this.callbackStarted = callbackStarted;
            this.releaseCallback = releaseCallback;
        }

        public void NetworkClose(ISocket sender)
        {
            callbackStarted.Set();
            releaseCallback.Wait();
        }

        public void NetworkConnect(ISocket sender)
        {
        }

        public void NetworkReceive(ISocket sender, NetworkBuffer buffer)
        {
        }
    }

    private static FrameworkWebSocket CreateAcceptedSocket(
        TestWebSocket platformSocket,
        IPendingSendBudget budget)
        => new(
            platformSocket,
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000))
        {
            PendingSendBudget = budget,
        };

    private sealed class TestPendingSendBudget : IPendingSendBudget
    {
        private readonly long limit;
        private long reservedBytes;

        public TestPendingSendBudget(long limit) => this.limit = limit;

        public long ReservedBytes => Interlocked.Read(ref reservedBytes);

        public bool TryReserve(int byteCount)
        {
            while (true)
            {
                var current = ReservedBytes;
                if (byteCount > limit - current)
                    return false;
                if (Interlocked.CompareExchange(
                        ref reservedBytes,
                        current + byteCount,
                        current) == current)
                    return true;
            }
        }

        public void Release(int byteCount) =>
            Interlocked.Add(ref reservedBytes, -byteCount);
    }

    private sealed class TestWebSocket : WebSocket
    {
        private readonly ConcurrentQueue<ReceiveFrame> receiveFrames = new();
        private readonly SemaphoreSlim receiveSignal = new(0);
        private WebSocketState state = WebSocketState.Open;
        private WebSocketCloseStatus? closeStatus;
        private string? closeStatusDescription;
        private readonly string? subProtocol;
        private TaskCompletionSource? sendStarted;
        private TaskCompletionSource? sendRelease;

        public TestWebSocket(string? subProtocol = FrameworkWebSocket.SubProtocol)
            => this.subProtocol = subProtocol;

        public List<byte[]> SentMessages { get; } = new List<byte[]>();
        public int AbortCount;
        public int CloseOutputCount;
        public Task SendStarted => sendStarted?.Task ?? Task.CompletedTask;

        public void BlockSends()
        {
            sendStarted = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            sendRelease = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void ReleaseSends() => sendRelease?.TrySetResult();

        public override WebSocketCloseStatus? CloseStatus => closeStatus;
        public override string? CloseStatusDescription => closeStatusDescription;
        public override WebSocketState State => state;
        public override string? SubProtocol => subProtocol;

        public void QueueBinary(byte[] data) =>
            Queue(new ReceiveFrame(data, WebSocketMessageType.Binary, true));

        public void QueueText(string data) =>
            Queue(new ReceiveFrame(
                System.Text.Encoding.UTF8.GetBytes(data),
                WebSocketMessageType.Text,
                true));

        public void QueueClose() =>
            Queue(new ReceiveFrame(Array.Empty<byte>(), WebSocketMessageType.Close, true));

        public override void Abort()
        {
            Interlocked.Increment(ref AbortCount);
            state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            this.closeStatus = closeStatus;
            closeStatusDescription = statusDescription;
            state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CloseOutputCount);
            this.closeStatus = closeStatus;
            closeStatusDescription = statusDescription;
            state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override void Dispose() => state = WebSocketState.Closed;

        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            await receiveSignal.WaitAsync(cancellationToken);
            if (!receiveFrames.TryDequeue(out var frame))
                throw new InvalidOperationException("The receive signal had no frame.");

            if (frame.MessageType == WebSocketMessageType.Close)
            {
                state = WebSocketState.CloseReceived;
                closeStatus = WebSocketCloseStatus.NormalClosure;
            }

            Buffer.BlockCopy(frame.Data, 0, buffer.Array!, buffer.Offset, frame.Data.Length);
            return new WebSocketReceiveResult(
                frame.Data.Length,
                frame.MessageType,
                frame.EndOfMessage,
                frame.MessageType == WebSocketMessageType.Close
                    ? WebSocketCloseStatus.NormalClosure
                    : null,
                null);
        }

        public override async Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sendStarted?.TrySetResult();
            if (sendRelease is not null)
                await sendRelease.Task.WaitAsync(cancellationToken);

            var copy = new byte[buffer.Count];
            Buffer.BlockCopy(buffer.Array!, buffer.Offset, copy, 0, buffer.Count);
            SentMessages.Add(copy);
        }

        private void Queue(ReceiveFrame frame)
        {
            receiveFrames.Enqueue(frame);
            receiveSignal.Release();
        }

        private readonly record struct ReceiveFrame(
            byte[] Data,
            WebSocketMessageType MessageType,
            bool EndOfMessage);
    }
}
