using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Esiur.Core;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Resource;

namespace Esiur.Net.Sockets;

public class TcpSocket : ISocket
{
    private sealed class PendingSend
    {
        public byte[] Buffer;
        public int Offset;
        public int Count;
        public AsyncReply<bool> Reply;
    }

    private readonly struct SendReplyCompletion
    {
        public SendReplyCompletion(AsyncReply<bool> reply, bool result, Exception error)
        {
            Reply = reply;
            Result = result;
            Error = error;
        }

        public AsyncReply<bool> Reply { get; }
        public bool Result { get; }
        public Exception Error { get; }
    }

    public INetworkReceiver<ISocket> Receiver { get; set; }
    public event DestroyedEvent OnDestroy;

    private readonly Socket sock;
    private readonly byte[] receiveBuffer;
    private readonly NetworkBuffer receiveNetworkBuffer = new NetworkBuffer();

    private readonly object stateLock = new object();
    private readonly object sendLock = new object();

    private readonly Queue<PendingSend> sendQueue = new Queue<PendingSend>();

    private SocketAsyncEventArgs receiveArgs;
    private SocketAsyncEventArgs sendArgs;

    private PendingSend currentSend;
    private long pendingSendBytes;
    private long maximumPendingSendBytes = 16 * 1024 * 1024;
    private bool sendInProgress;
    private bool began;
    private bool held;
    private bool destroyed;
    private bool closeNotified;

    private int bytesSent;
    private int bytesReceived;

    private SocketState state = SocketState.Initial;

    public Socket Socket => sock;
    public SocketState State => state;
    public int BytesSent => bytesSent;
    public int BytesReceived => bytesReceived;
    public long PendingSendBytes => Interlocked.Read(ref pendingSendBytes);

    /// <summary>
    /// Maximum number of unsent bytes retained by this socket. This bounds the
    /// copies made by <see cref="Send(byte[], int, int)"/> and
    /// <see cref="SendAsync(byte[], int, int)"/> when a peer is slow.
    /// </summary>
    public long MaximumPendingSendBytes
    {
        get => Interlocked.Read(ref maximumPendingSendBytes);
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            Interlocked.Exchange(ref maximumPendingSendBytes, value);
        }
    }

    public IPEndPoint LocalEndPoint => sock.LocalEndPoint as IPEndPoint;
    public IPEndPoint RemoteEndPoint => sock.RemoteEndPoint as IPEndPoint;

    public TcpSocket()
    {
        sock = CreateSocket();
        receiveBuffer = new byte[Math.Max(sock.ReceiveBufferSize, 8192)];
    }

    public TcpSocket(string hostname, ushort port) : this()
    {
        Connect(hostname, port);
    }

    public TcpSocket(IPEndPoint localEndPoint)
    {
        sock = CreateSocket();
        receiveBuffer = new byte[Math.Max(sock.ReceiveBufferSize, 8192)];

        sock.Bind(localEndPoint);
        sock.Listen(ushort.MaxValue);
        state = SocketState.Listening;
    }

    public TcpSocket(Socket socket)
    {
        if (socket == null)
            throw new ArgumentNullException(nameof(socket));

        sock = socket;
        ConfigureSocket(sock);
        receiveBuffer = new byte[Math.Max(sock.ReceiveBufferSize, 8192)];

        if (sock.Connected)
            state = SocketState.Established;
    }

    private static Socket CreateSocket()
    {
        var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        ConfigureSocket(s);
        return s;
    }

    private static void ConfigureSocket(Socket s)
    {
        s.NoDelay = true;

        try { s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); } catch { }
        try { s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); } catch { }
    }

    public AsyncReply<bool> Connect(string hostname, ushort port)
    {
        var rt = new AsyncReply<bool>();

        lock (stateLock)
        {
            if (destroyed || state == SocketState.Closed)
            {
                rt.Trigger(false);
                return rt;
            }

            if (state == SocketState.Established)
            {
                rt.Trigger(true);
                return rt;
            }

            state = SocketState.Connecting;
        }

        try
        {
            var args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = new DnsEndPoint(hostname, port);
            args.UserToken = rt;
            args.Completed += ConnectCompleted;

            bool pending = sock.ConnectAsync(args);
            if (!pending)
                ProcessConnect(args);
        }
        catch (Exception ex)
        {
            SafeClose(ex, false);
            rt.TriggerError(ex);
        }

        return rt;
    }

    private void ConnectCompleted(object sender, SocketAsyncEventArgs e)
    {
        ProcessConnect(e);
    }

    private void ProcessConnect(SocketAsyncEventArgs e)
    {
        var rt = e.UserToken as AsyncReply<bool>;

        try
        {
            if (e.SocketError == SocketError.Success)
            {
                lock (stateLock)
                {
                    if (destroyed || state == SocketState.Closed)
                    {
                        rt?.Trigger(false);
                        return;
                    }

                    state = SocketState.Established;
                }

                Receiver?.NetworkConnect(this);
                Begin();
                rt?.Trigger(true);
            }
            else
            {
                var ex = new SocketException((int)e.SocketError);
                SafeClose(ex, false);
                rt?.TriggerError(ex);
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    public bool Begin()
    {
        lock (stateLock)
        {
            if (destroyed || state != SocketState.Established || began)
                return false;

            began = true;
        }

        StartReceiveLoop();
        return true;
    }

    public AsyncReply<bool> BeginAsync()
    {
        return new AsyncReply<bool>(Begin());
    }

    public void Send(byte[] message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        Send(message, 0, message.Length);
    }

    public void Send(byte[] message, int offset, int length)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        ValidateRange(message, offset, length);

        if (length == 0)
            return;

        if (destroyed || state != SocketState.Established)
            return;

        List<SendReplyCompletion> completions = null;
        Exception sendError = null;

        lock (sendLock)
        {
            if (destroyed || state != SocketState.Established)
                return;

            EnsureSendCapacity_NoLock(length);

            var copy = new byte[length];
            Buffer.BlockCopy(message, offset, copy, 0, length);

            sendQueue.Enqueue(new PendingSend
            {
                Buffer = copy,
                Offset = 0,
                Count = copy.Length,
                Reply = null
            });
            Interlocked.Add(ref pendingSendBytes, length);

            TryStartNextSend_NoLock(ref completions, ref sendError);
        }

        FinishSendWork(completions, sendError);
    }

    public AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
    {
        var rt = new AsyncReply<bool>();

        if (message == null)
            throw new ArgumentNullException(nameof(message));

        ValidateRange(message, offset, length);

        if (length == 0)
        {
            rt.Trigger(true);
            return rt;
        }

        if (destroyed || state != SocketState.Established)
        {
            rt.Trigger(false);
            return rt;
        }

        List<SendReplyCompletion> completions = null;
        Exception sendError = null;

        lock (sendLock)
        {
            if (destroyed || state != SocketState.Established)
            {
                rt.Trigger(false);
                return rt;
            }

            try
            {
                EnsureSendCapacity_NoLock(length);
            }
            catch (Exception ex)
            {
                rt.TriggerError(ex);
                return rt;
            }

            var copy = new byte[length];
            Buffer.BlockCopy(message, offset, copy, 0, length);

            sendQueue.Enqueue(new PendingSend
            {
                Buffer = copy,
                Offset = 0,
                Count = copy.Length,
                Reply = rt
            });
            Interlocked.Add(ref pendingSendBytes, length);

            TryStartNextSend_NoLock(ref completions, ref sendError);
        }

        FinishSendWork(completions, sendError);

        return rt;
    }

    public ISocket Accept()
    {
        try
        {
            var s = sock.Accept();
            return new TcpSocket(s);
        }
        catch
        {
            state = SocketState.Closed;
            return null;
        }
    }

    public async AsyncReply<ISocket> AcceptAsync()
    {
        try
        {
            var s = await Task<Socket>.Factory.FromAsync(sock.BeginAccept, sock.EndAccept, null);
            return new TcpSocket(s);
        }
        catch
        {
            state = SocketState.Closed;
            return null;
        }
    }

    public void Hold()
    {
        lock (sendLock)
            held = true;
    }

    public void Unhold()
    {
        List<SendReplyCompletion> completions = null;
        Exception sendError = null;

        lock (sendLock)
        {
            held = false;
            TryStartNextSend_NoLock(ref completions, ref sendError);
        }

        FinishSendWork(completions, sendError);
    }

    public void Close()
    {
        SafeClose(null, true);
    }

    public void Destroy()
    {
        if (destroyed)
            return;

        destroyed = true;
        SafeClose(null, true);

        try
        {
            if (receiveArgs != null)
            {
                receiveArgs.Completed -= IOCompleted;
                receiveArgs.Dispose();
                receiveArgs = null;
            }
        }
        catch { }

        try
        {
            if (sendArgs != null)
            {
                sendArgs.Completed -= IOCompleted;
                sendArgs.Dispose();
                sendArgs = null;
            }
        }
        catch { }

        OnDestroy?.Invoke(this);
        OnDestroy = null;
    }

    private void StartReceiveLoop()
    {
        if (receiveArgs != null)
            return;

        receiveArgs = new SocketAsyncEventArgs();
        receiveArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
        receiveArgs.Completed += IOCompleted;

        StartReceive();
    }

    private void StartReceive()
    {
        try
        {
            if (destroyed || state != SocketState.Established)
                return;

            bool pending = sock.ReceiveAsync(receiveArgs);
            if (!pending)
                ProcessReceive(receiveArgs);
        }
        catch (Exception ex)
        {
            SafeClose(ex, true);
        }
    }

    private void IOCompleted(object sender, SocketAsyncEventArgs e)
    {
        switch (e.LastOperation)
        {
            case SocketAsyncOperation.Receive:
                ProcessReceive(e);
                break;

            case SocketAsyncOperation.Send:
                ProcessSend(e);
                break;
        }
    }

    private void ProcessReceive(SocketAsyncEventArgs e)
    {
        try
        {
            if (e.SocketError != SocketError.Success)
            {
                SafeClose(new SocketException((int)e.SocketError), true);
                return;
            }

            if (e.BytesTransferred <= 0)
            {
                SafeClose(null, true);
                return;
            }

            Interlocked.Add(ref bytesReceived, e.BytesTransferred);
            receiveNetworkBuffer.Write(e.Buffer, (uint)e.Offset, (uint)e.BytesTransferred);
            Receiver?.NetworkReceive(this, receiveNetworkBuffer);

            StartReceive();
        }
        catch (Exception ex)
        {
            SafeClose(ex, true);
        }
    }

    private void TryStartNextSend_NoLock(
        ref List<SendReplyCompletion> completions,
        ref Exception sendError)
    {
        if (held || destroyed || state != SocketState.Established || sendInProgress)
            return;

        sendInProgress = true;
        PumpSendQueue_NoLock(ref completions, ref sendError);
    }

    private void PumpSendQueue_NoLock(
        ref List<SendReplyCompletion> completions,
        ref Exception sendError)
    {
        while (true)
        {
            if (held || destroyed || state != SocketState.Established)
            {
                sendInProgress = false;
                return;
            }

            if (currentSend == null)
            {
                if (sendQueue.Count == 0)
                {
                    sendInProgress = false;
                    return;
                }

                currentSend = sendQueue.Dequeue();
            }

            if (sendArgs == null)
            {
                sendArgs = new SocketAsyncEventArgs();
                sendArgs.Completed += IOCompleted;
            }

            sendArgs.SetBuffer(currentSend.Buffer, currentSend.Offset, currentSend.Count);

            bool pending;
            try
            {
                pending = sock.SendAsync(sendArgs);
            }
            catch (Exception ex)
            {
                var reply = currentSend?.Reply;
                currentSend = null;
                sendInProgress = false;
                QueueSendCompletion(ref completions, reply, false, ex);
                FailPendingSends_NoLock(ex, ref completions);
                sendError = ex;
                return;
            }

            if (pending)
            {
                return;
            }

            if (!ProcessSendCompletion_NoLock(
                    sendArgs,
                    ref completions,
                    ref sendError))
                return;
        }
    }

    private void ProcessSend(SocketAsyncEventArgs e)
    {
        List<SendReplyCompletion> completions = null;
        Exception sendError = null;

        lock (sendLock)
        {
            if (ProcessSendCompletion_NoLock(
                    e,
                    ref completions,
                    ref sendError))
                PumpSendQueue_NoLock(ref completions, ref sendError);
        }

        FinishSendWork(completions, sendError);
    }

    private bool ProcessSendCompletion_NoLock(
        SocketAsyncEventArgs e,
        ref List<SendReplyCompletion> completions,
        ref Exception sendError)
    {
        try
        {
            if (currentSend == null)
            {
                sendInProgress = false;
                return false;
            }

            if (e.SocketError != SocketError.Success)
            {
                var ex = new SocketException((int)e.SocketError);
                QueueSendCompletion(ref completions, currentSend.Reply, false, ex);
                currentSend = null;
                sendInProgress = false;
                FailPendingSends_NoLock(ex, ref completions);
                sendError = ex;
                return false;
            }

            if (e.BytesTransferred <= 0)
            {
                var ex = new SocketException((int)SocketError.ConnectionReset);
                QueueSendCompletion(ref completions, currentSend.Reply, false, ex);
                currentSend = null;
                sendInProgress = false;
                FailPendingSends_NoLock(ex, ref completions);
                sendError = ex;
                return false;
            }

            Interlocked.Add(ref bytesSent, e.BytesTransferred);
            Interlocked.Add(ref pendingSendBytes, -e.BytesTransferred);

            currentSend.Offset += e.BytesTransferred;
            currentSend.Count -= e.BytesTransferred;

            if (currentSend.Count > 0)
            {
                return true;
            }

            QueueSendCompletion(ref completions, currentSend.Reply, true, null);
            currentSend = null;
            return true;
        }
        catch (Exception ex)
        {
            QueueSendCompletion(ref completions, currentSend?.Reply, false, ex);
            currentSend = null;
            sendInProgress = false;
            FailPendingSends_NoLock(ex, ref completions);
            sendError = ex;
            return false;
        }
    }

    private void FailPendingSends_NoLock(
        Exception ex,
        ref List<SendReplyCompletion> completions)
    {
        while (sendQueue.Count > 0)
        {
            var item = sendQueue.Dequeue();
            QueueSendCompletion(ref completions, item.Reply, false, ex);
        }

        Interlocked.Exchange(ref pendingSendBytes, 0);
    }

    private bool CloseDueToSendError(Exception ex)
    {
        bool notify = false;

        lock (stateLock)
        {
            if (state == SocketState.Closed)
                return false;

            state = SocketState.Closed;
            notify = !closeNotified;
            closeNotified = true;
        }

        try { sock.Shutdown(SocketShutdown.Both); } catch { }
        try { sock.Close(); } catch { }
        try { sock.Dispose(); } catch { }

        Global.Log(ex);

        return notify;
    }

    private void FinishSendWork(
        List<SendReplyCompletion> completions,
        Exception sendError)
    {
        var notify = sendError != null && CloseDueToSendError(sendError);

        CompleteSendReplies(completions);

        if (notify)
        {
            try { Receiver?.NetworkClose(this); }
            catch (Exception e) { Global.Log(e); }
        }
    }

    private static void QueueSendCompletion(
        ref List<SendReplyCompletion> completions,
        AsyncReply<bool> reply,
        bool result,
        Exception error)
    {
        if (reply == null)
            return;

        completions ??= new List<SendReplyCompletion>();
        completions.Add(new SendReplyCompletion(reply, result, error));
    }

    private static void CompleteSendReplies(List<SendReplyCompletion> completions)
    {
        if (completions == null)
            return;

        foreach (var completion in completions)
        {
            try
            {
                if (completion.Error != null)
                    completion.Reply.TriggerError(completion.Error);
                else
                    completion.Reply.Trigger(completion.Result);
            }
            catch (Exception ex)
            {
                Global.Log(ex);
            }
        }
    }

    private void SafeClose(Exception ex, bool notifyReceiver)
    {
        bool notify = false;
        List<SendReplyCompletion> completions = null;

        lock (stateLock)
        {
            if (state == SocketState.Closed)
                return;

            state = SocketState.Closed;
            notify = notifyReceiver && !closeNotified;
            closeNotified = true;
        }

        lock (sendLock)
        {
            sendInProgress = false;

            if (ex != null)
            {
                QueueSendCompletion(ref completions, currentSend?.Reply, false, ex);
                currentSend = null;
                FailPendingSends_NoLock(ex, ref completions);
            }
            else
            {
                QueueSendCompletion(ref completions, currentSend?.Reply, false, null);
                currentSend = null;
                while (sendQueue.Count > 0)
                {
                    var item = sendQueue.Dequeue();
                    QueueSendCompletion(ref completions, item.Reply, false, null);
                }
            }

            Interlocked.Exchange(ref pendingSendBytes, 0);
        }

        try { sock.Shutdown(SocketShutdown.Both); } catch { }
        try { sock.Close(); } catch { }
        try { sock.Dispose(); } catch { }

        CompleteSendReplies(completions);

        if (ex != null)
            Global.Log(ex);

        if (notify)
        {
            try { Receiver?.NetworkClose(this); }
            catch (Exception e) { Global.Log(e); }
        }
    }

    private static void ValidateRange(byte[] message, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > message.Length - length)
            throw new ArgumentOutOfRangeException();
    }

    private void EnsureSendCapacity_NoLock(int length)
    {
        var limit = Interlocked.Read(ref maximumPendingSendBytes);
        var pending = Interlocked.Read(ref pendingSendBytes);

        if (length > limit - pending)
        {
            throw new InvalidOperationException(
                $"The socket send queue exceeded its {limit}-byte limit.");
        }
    }
}
