/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Esiur.Misc;
using Esiur.Core;
using System.Threading;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Esiur.Resource;
using System.Threading.Tasks;
using Esiur.Data;

namespace Esiur.Net.Sockets;
public class SSLSocket : ISocket
{

    private sealed class PendingSend
    {
        public AsyncReply<bool> Reply;
        public byte[] Buffer;
    }

    public INetworkReceiver<ISocket> Receiver { get; set; }

    Socket sock;
    byte[] receiveBuffer;

    bool held;

    //ArraySegment<byte> receiveBufferSegment;

    NetworkBuffer receiveNetworkBuffer = new NetworkBuffer();

    readonly object sendLock = new object();

    readonly Queue<PendingSend> sendBufferQueue = new Queue<PendingSend>();
    PendingSend currentSend;
    long pendingSendBytes;
    long maximumPendingSendBytes = 16 * 1024 * 1024;

    bool asyncSending;
    bool began = false;

    SocketState state = SocketState.Initial;

    //public event ISocketReceiveEvent OnReceive;
    //public event ISocketConnectEvent OnConnect;
    //public event ISocketCloseEvent OnClose;
    public event DestroyedEvent OnDestroy;


    SslStream ssl;
    X509Certificate2 cert;
    bool server;
    string hostname;

    public long PendingSendBytes => Interlocked.Read(ref pendingSendBytes);

    /// <summary>Maximum number of unsent plaintext bytes retained for a slow TLS peer.</summary>
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


    public async AsyncReply<bool> Connect(string hostname, ushort port)
    {
        this.hostname = hostname;
        this.server = false;

        state = SocketState.Connecting;
        try
        {
            await sock.ConnectAsync(hostname, port);
            ssl = new SslStream(new NetworkStream(sock));
            state = SocketState.Established;

            if (!await BeginAsync())
            {
                Close();
                return false;
            }

            //OnConnect?.Invoke();
            Receiver?.NetworkConnect(this);
            return true;
        }
        catch (Exception ex)
        {
            Close();
            Global.Log(ex);
            return false;
        }
    }

    //private void DataSent(Task task)
    //{
    //    try
    //    {

    //        if (sendBufferQueue.Count > 0)
    //        {
    //            byte[] data = sendBufferQueue.Dequeue();
    //            lock (sendLock)
    //                ssl.WriteAsync(data, 0, data.Length).ContinueWith(DataSent);
    //        }
    //        else
    //        {
    //            asyncSending = false;
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        if (state != SocketState.Closed && !sock.Connected)
    //        {
    //            state = SocketState.Terminated;
    //            Close();
    //        }

    //        asyncSending = false;

    //        Global.Log("SSLSocket", LogType.Error, ex.ToString());
    //    }
    //}


    private async Task ProcessSendQueueAsync()
    {
        while (true)
        {
            PendingSend pending;

            lock (sendLock)
            {
                if (held || state == SocketState.Closed || sendBufferQueue.Count == 0)
                {
                    asyncSending = false;
                    return;
                }

                pending = sendBufferQueue.Dequeue();
                currentSend = pending;
            }

            try
            {
                await ssl.WriteAsync(pending.Buffer, 0, pending.Buffer.Length).ConfigureAwait(false);

                lock (sendLock)
                {
                    if (ReferenceEquals(currentSend, pending))
                    {
                        currentSend = null;
                        Interlocked.Add(ref pendingSendBytes, -pending.Buffer.Length);
                    }
                }

                TryCompleteSend(pending, true, null);
            }
            catch (Exception exception)
            {
                lock (sendLock)
                {
                    if (ReferenceEquals(currentSend, pending))
                    {
                        currentSend = null;
                        Interlocked.Add(ref pendingSendBytes, -pending.Buffer.Length);
                    }

                    asyncSending = false;
                }

                TryCompleteSend(pending, false, exception);
                Close();
                return;
            }
        }
    }

    public IPEndPoint LocalEndPoint
    {
        get { return (IPEndPoint)sock.LocalEndPoint; }
    }

    public SSLSocket()
    {
        sock = new Socket(AddressFamily.InterNetwork,
                             SocketType.Stream,
                             ProtocolType.Tcp);
        receiveBuffer = new byte[sock.ReceiveBufferSize];
    }

    public SSLSocket(IPEndPoint localEndPoint, X509Certificate2 certificate)
    {
        // create the socket
        sock = new Socket(AddressFamily.InterNetwork,
                                         SocketType.Stream,
                                         ProtocolType.Tcp);

        state = SocketState.Listening;

        // bind
        sock.Bind(localEndPoint);

        // start listening
        sock.Listen(UInt16.MaxValue);

        cert = certificate;
    }

    public IPEndPoint RemoteEndPoint
    {
        get { return (IPEndPoint)sock.RemoteEndPoint; }
    }

    public SocketState State
    {
        get
        {
            return state;
        }
    }



    public SSLSocket(Socket socket, X509Certificate2 certificate, bool authenticateAsServer)
    {
        cert = certificate;
        sock = socket;
        receiveBuffer = new byte[sock.ReceiveBufferSize];

        ssl = new SslStream(new NetworkStream(sock));

        server = authenticateAsServer;

        if (socket.Connected)
            state = SocketState.Established;
    }


    public void Close()
    {
        List<PendingSend> abandoned;
        lock (sendLock)
        {
            if (state == SocketState.Closed)
                return;

            state = SocketState.Closed;
            abandoned = sendBufferQueue.ToList();
            sendBufferQueue.Clear();
            if (currentSend != null)
            {
                abandoned.Insert(0, currentSend);
                currentSend = null;
            }

            foreach (var pending in abandoned)
                Interlocked.Add(ref pendingSendBytes, -pending.Buffer.Length);
            asyncSending = false;
        }

        if (sock != null)
        {
            try
            {
                if (sock.Connected)
                    sock.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                //state = SocketState.Terminated;
            }

            // Closing the underlying socket is what aborts an in-progress TLS
            // handshake. Shutdown alone can leave AuthenticateAsServerAsync waiting
            // forever for a peer that never sends a ClientHello.
            try { sock.Close(); } catch { }
        }

        try { ssl?.Dispose(); } catch { }

        foreach (var pending in abandoned)
            TryCompleteSend(pending, false, null);

        try { Receiver?.NetworkClose(this); }
        catch (Exception exception) { Global.Log(exception); }
        //OnClose?.Invoke();
    }


    public void Send(byte[] message)
    {
        Send(message, 0, message.Length);
    }


    public void Send(byte[] message, int offset, int size)
    {
        ValidateRange(message, offset, size);
        if (size == 0)
            return;

        bool startPump = false;
        lock (sendLock)
        {
            if (state != SocketState.Established)
                return;

            EnsureSendCapacity_NoLock(size);
            var msg = new byte[size];
            Buffer.BlockCopy(message, offset, msg, 0, size);
            sendBufferQueue.Enqueue(new PendingSend { Buffer = msg });
            Interlocked.Add(ref pendingSendBytes, size);
            startPump = TryStartSendPump_NoLock();
        }

        if (startPump)
            _ = ProcessSendQueueAsync();
    }

    //public void Send(byte[] message)
    //{
    //    Send(message, 0, message.Length);
    //}

    //public void Send(byte[] message, int offset, int size)
    //{
    //    lock (sendLock)
    //    {
    //        if (asyncSending)
    //        {
    //            sendBufferQueue.Enqueue(message.Clip((uint)offset, (uint)size));
    //        }
    //        else
    //        {
    //            asyncSending = true;
    //            ssl.WriteAsync(message, offset, size).ContinueWith(DataSent);
    //        }
    //    }
    //}



    //private void DataReceived(Task<int> task)
    //{
    //    try
    //    {
    //        if (state == SocketState.Closed || state == SocketState.Terminated)
    //            return;

    //        if (task.Result <= 0)
    //        {
    //            Close();
    //            return;
    //        }

    //        receiveNetworkBuffer.Write(receiveBuffer, 0, (uint)task.Result);
    //        OnReceive?.Invoke(receiveNetworkBuffer);
    //        if (state == SocketState.Established)
    //            ssl.ReadAsync(receiveBuffer, 0, receiveBuffer.Length).ContinueWith(DataReceived);
    //    }
    //    catch (Exception ex)
    //    {
    //        if (state != SocketState.Closed && !sock.Connected)
    //        {
    //            state = SocketState.Terminated;
    //            Close();
    //        }

    //        Global.Log("SSLSocket", LogType.Error, ex.ToString());
    //    }
    //}


    public bool Begin()
    {
        if (began)
            return false;

        began = true;

        if (server)
            ssl.AuthenticateAsServer(cert);
        else
            ssl.AuthenticateAsClient(hostname);

        if (state == SocketState.Established)
        {
            ssl.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveCallback, this);
            return true;
        }
        else
            return false;
    }

    public async AsyncReply<bool> BeginAsync()
    {
        if (began)
            return false;

        began = true;

        if (server)
            await ssl.AuthenticateAsServerAsync(cert);
        else
            await ssl.AuthenticateAsClientAsync(hostname);

        if (state == SocketState.Established)
        {
            ssl.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveCallback, this);
            return true;
        }
        else
            return false;
    }

    private void ReceiveCallback(IAsyncResult results)
    {
        try
        {
            if (state != SocketState.Established)
                return;

            var bytesReceived = ssl.EndRead(results);

            if (bytesReceived <= 0)
            {
                Close();
                return;
            }

            receiveNetworkBuffer.Write(receiveBuffer, 0, (uint)bytesReceived);

            //OnReceive?.Invoke(receiveNetworkBuffer);

            Receiver?.NetworkReceive(this, receiveNetworkBuffer);

            ssl.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveCallback, this);

        }
        catch (Exception ex)
        {
            // Socket.Connected reports the state of the last operation and can
            // remain true after a TLS read failure. Any read exception ends this
            // receive loop, so close deterministically instead of leaving a
            // half-open connection that will never read again.
            if (state != SocketState.Closed)
                Close();

            Global.Log("SSLSocket", LogType.Warning, ex.ToString());
        }
    }

    public bool Trigger(ResourceOperation trigger)
    {
        return true;
    }

    public void Destroy()
    {
        Close();

        // Release the TLS stream and the underlying socket handle. NetworkStream(sock) does
        // not own the socket, so disposing the stream alone would leak the socket — dispose
        // both explicitly. Guarded because teardown may race with in-flight I/O callbacks.
        try { ssl?.Dispose(); } catch { }
        try { sock?.Close(); } catch { }
        try { sock?.Dispose(); } catch { }

        Receiver = null;
        receiveNetworkBuffer = null;
        OnDestroy?.Invoke(this);
        OnDestroy = null;
    }

    public async AsyncReply<ISocket> AcceptAsync()
    {
        try
        {
            var s = await sock.AcceptAsync();
            return new SSLSocket(s, cert, true);
        }
        catch
        {
            state = SocketState.Closed;// Terminated;
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
        bool startPump;
        lock (sendLock)
        {
            held = false;
            startPump = TryStartSendPump_NoLock();
        }

        if (startPump)
            _ = ProcessSendQueueAsync();
    }


    public AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
    {
        ValidateRange(message, offset, length);
        if (length == 0)
            return new AsyncReply<bool>(true);

        var rt = new AsyncReply<bool>();
        bool startPump = false;
        Exception capacityError = null;
        lock (sendLock)
        {
            if (state != SocketState.Established)
                return new AsyncReply<bool>(false);

            try
            {
                EnsureSendCapacity_NoLock(length);
                var msg = new byte[length];
                Buffer.BlockCopy(message, offset, msg, 0, length);
                sendBufferQueue.Enqueue(new PendingSend { Reply = rt, Buffer = msg });
                Interlocked.Add(ref pendingSendBytes, length);
                startPump = TryStartSendPump_NoLock();
            }
            catch (Exception exception)
            {
                capacityError = exception;
            }
        }

        if (capacityError != null)
            rt.TriggerError(capacityError);
        else if (startPump)
            _ = ProcessSendQueueAsync();

        return rt;
    }

    private bool TryStartSendPump_NoLock()
    {
        if (asyncSending || held || state != SocketState.Established || sendBufferQueue.Count == 0)
            return false;

        asyncSending = true;
        return true;
    }

    private void EnsureSendCapacity_NoLock(int length)
    {
        var limit = Interlocked.Read(ref maximumPendingSendBytes);
        var pending = Interlocked.Read(ref pendingSendBytes);
        if (length > limit - pending)
            throw new InvalidOperationException($"The TLS send queue exceeded its {limit}-byte limit.");
    }

    private static void ValidateRange(byte[] message, int offset, int length)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (offset < 0 || length < 0 || offset > message.Length - length)
            throw new ArgumentOutOfRangeException();
    }

    private static void TryCompleteSend(PendingSend pending, bool succeeded, Exception exception)
    {
        if (pending?.Reply == null)
            return;

        try
        {
            if (exception != null)
                pending.Reply.TriggerError(exception);
            else
                pending.Reply.Trigger(succeeded);
        }
        catch (Exception callbackException)
        {
            Global.Log(callbackException);
        }
    }


    public ISocket Accept()
    {
        try
        {
            return new SSLSocket(sock.Accept(), cert, true);
        }
        catch
        {
            state = SocketState.Closed;// .Terminated;
            return null;
        }
    }
}

