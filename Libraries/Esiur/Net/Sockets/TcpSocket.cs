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

        var copy = new byte[length];
        Buffer.BlockCopy(message, offset, copy, 0, length);

        lock (sendLock)
        {
            if (destroyed || state != SocketState.Established)
                return;

            sendQueue.Enqueue(new PendingSend
            {
                Buffer = copy,
                Offset = 0,
                Count = copy.Length,
                Reply = null
            });

            TryStartNextSend_NoLock();
        }
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

        var copy = new byte[length];
        Buffer.BlockCopy(message, offset, copy, 0, length);

        lock (sendLock)
        {
            if (destroyed || state != SocketState.Established)
            {
                rt.Trigger(false);
                return rt;
            }

            sendQueue.Enqueue(new PendingSend
            {
                Buffer = copy,
                Offset = 0,
                Count = copy.Length,
                Reply = rt
            });

            TryStartNextSend_NoLock();
        }

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
        held = true;
    }

    public void Unhold()
    {
        held = false;

        lock (sendLock)
            TryStartNextSend_NoLock();
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

    private void TryStartNextSend_NoLock()
    {
        if (held || destroyed || state != SocketState.Established || sendInProgress)
            return;

        sendInProgress = true;
        PumpSendQueue_NoLock();
    }

    private void PumpSendQueue_NoLock()
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
                reply?.TriggerError(ex);
                FailPendingSends_NoLock(ex);
                CloseDueToSendError_NoLock(ex);
                return;
            }

            if (pending)
            {
                return;
            }

            if (!ProcessSendCompletion_NoLock(sendArgs))
                return;
        }
    }

    private void ProcessSend(SocketAsyncEventArgs e)
    {
        lock (sendLock)
        {
            if (!ProcessSendCompletion_NoLock(e))
                return;

            PumpSendQueue_NoLock();
        }
    }

    private bool ProcessSendCompletion_NoLock(SocketAsyncEventArgs e)
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
                currentSend.Reply?.TriggerError(ex);
                currentSend = null;
                sendInProgress = false;
                FailPendingSends_NoLock(ex);
                CloseDueToSendError_NoLock(ex);
                return false;
            }

            if (e.BytesTransferred <= 0)
            {
                var ex = new SocketException((int)SocketError.ConnectionReset);
                currentSend.Reply?.TriggerError(ex);
                currentSend = null;
                sendInProgress = false;
                FailPendingSends_NoLock(ex);
                CloseDueToSendError_NoLock(ex);
                return false;
            }

            Interlocked.Add(ref bytesSent, e.BytesTransferred);

            currentSend.Offset += e.BytesTransferred;
            currentSend.Count -= e.BytesTransferred;

            if (currentSend.Count > 0)
            {
                return true;
            }

            currentSend.Reply?.Trigger(true);
            currentSend = null;
            return true;
        }
        catch (Exception ex)
        {
            currentSend?.Reply?.TriggerError(ex);
            currentSend = null;
            sendInProgress = false;
            FailPendingSends_NoLock(ex);
            CloseDueToSendError_NoLock(ex);
            return false;
        }
    }

    private void FailPendingSends_NoLock(Exception ex)
    {
        while (sendQueue.Count > 0)
        {
            var item = sendQueue.Dequeue();
            try
            {
                item.Reply?.TriggerError(ex);
            }
            catch { }
        }
    }

    private void CloseDueToSendError_NoLock(Exception ex)
    {
        bool notify = false;

        lock (stateLock)
        {
            if (state == SocketState.Closed)
                return;

            state = SocketState.Closed;
            notify = !closeNotified;
            closeNotified = true;
        }

        try { sock.Shutdown(SocketShutdown.Both); } catch { }
        try { sock.Close(); } catch { }
        try { sock.Dispose(); } catch { }

        Global.Log(ex);

        if (notify)
        {
            try { Receiver?.NetworkClose(this); }
            catch (Exception e) { Global.Log(e); }
        }
    }

    private void SafeClose(Exception ex, bool notifyReceiver)
    {
        bool notify = false;

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
                try { currentSend?.Reply?.TriggerError(ex); } catch { }
                currentSend = null;
                FailPendingSends_NoLock(ex);
            }
            else
            {
                currentSend = null;
                while (sendQueue.Count > 0)
                {
                    var item = sendQueue.Dequeue();
                    try { item.Reply?.Trigger(false); } catch { }
                }
            }
        }

        try { sock.Shutdown(SocketShutdown.Both); } catch { }
        try { sock.Close(); } catch { }
        try { sock.Dispose(); } catch { }

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
        if (offset < 0 || length < 0 || offset + length > message.Length)
            throw new ArgumentOutOfRangeException();
    }
}