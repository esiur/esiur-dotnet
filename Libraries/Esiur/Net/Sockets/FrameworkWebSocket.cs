using Esiur.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Net.WebSockets;
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Misc;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using Microsoft.CodeAnalysis;

namespace Esiur.Net.Sockets
{
    public class FrameworkWebSocket : ISocket
    {
        bool began;

         WebSocket sock;

        NetworkBuffer receiveNetworkBuffer = new NetworkBuffer();
        NetworkBuffer sendNetworkBuffer = new NetworkBuffer();

        byte[] websocketReceiveBuffer = new byte[10240];
        ArraySegment<byte> websocketReceiveBufferSegment;

        object sendLock = new object();
        readonly SemaphoreSlim sendSemaphore = new SemaphoreSlim(1, 1);
        int sendFailureNotified;
        bool held;

        public event DestroyedEvent OnDestroy;

        long totalSent, totalReceived;


        public IPEndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Any, 0);

        public IPEndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Any, 0);


        public SocketState State => sock == null ? SocketState.Closed : sock.State switch
        {
            WebSocketState.Aborted => SocketState.Closed,
            WebSocketState.Closed => SocketState.Closed,
            WebSocketState.Connecting => SocketState.Connecting,
            WebSocketState.Open => SocketState.Established,
            WebSocketState.CloseReceived => SocketState.Closed,
            WebSocketState.CloseSent => SocketState.Closed,
            WebSocketState.None => SocketState.Initial,
            _ => SocketState.Initial
        };

        public INetworkReceiver<ISocket> Receiver { get; set; }

        public FrameworkWebSocket()
        {
            websocketReceiveBufferSegment = new ArraySegment<byte>(websocketReceiveBuffer);
        }


        public FrameworkWebSocket(WebSocket webSocket)
        {
            websocketReceiveBufferSegment = new ArraySegment<byte>(websocketReceiveBuffer);
            sock = webSocket;

         }


        public void Send(byte[] message)
        {
            byte[] queued = null;
            lock (sendLock)
            {
                if (held)
                {
                    sendNetworkBuffer.Write(message);
                }
                else
                {
                    queued = (byte[])message.Clone();
                }
            }

            if (queued != null)
                ObserveSend(QueueSend(queued));
        }


        public void Send(byte[] message, int offset, int size)
        {
            byte[] queued = null;
            lock (sendLock)
            {
                if (held)
                {
                    sendNetworkBuffer.Write(message, (uint)offset, (uint)size);
                }
                else
                {
                    queued = new byte[size];
                    Buffer.BlockCopy(message, offset, queued, 0, size);
                }
            }

            if (queued != null)
                ObserveSend(QueueSend(queued));
        }


        public void Close()
        {
            sock?.CloseAsync(WebSocketCloseStatus.NormalClosure, "", new System.Threading.CancellationToken());
        }

        public bool Secure { get; set; }

        public async AsyncReply<bool> Connect(string hostname, ushort port)
        {
            var url = new Uri($"{(Secure ? "wss" : "ws")}://{hostname}:{port}");

            var ws = new ClientWebSocket();
            sock = ws;

            await ws.ConnectAsync(url, new CancellationToken());


            _ = sock.ReceiveAsync(websocketReceiveBufferSegment, CancellationToken.None)
               .ContinueWith(NetworkReceive);

            return true;
        }


        public bool Begin()
        {

            // Socket destroyed
            if (sock == null)
                return false;

            if (began)
                return false;

            began = true;

            sock.ReceiveAsync(websocketReceiveBufferSegment, CancellationToken.None)
                .ContinueWith(NetworkReceive);

            return true;
          
        }

        public bool Trigger(ResourceOperation trigger)
        {
            return true;
        }

        public void Destroy()
        {
            var ws = sock;

            Close(); // best-effort graceful close handshake (fire-and-forget)

            receiveNetworkBuffer = null;
            Receiver = null;
            sock = null;

            // Dispose the WebSocket so its buffers and handle are released; Close() only
            // starts the async close handshake and never disposes.
            try { ws?.Dispose(); } catch { }

            OnDestroy?.Invoke(this);
            OnDestroy = null;
        }

        public AsyncReply<ISocket> AcceptAsync()
        {
            throw new NotImplementedException();
        }

        public void Hold()
        {
            held = true;
        }

        public void Unhold()
        {
            byte[] message;
            lock (sendLock)
            {
                held = false;
                message = sendNetworkBuffer.Read();
            }

            if (message != null)
                ObserveSend(QueueSend(message));
        }

        public async AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
        {
            byte[] queued = null;
            lock (sendLock)
            {
                if (held)
                {
                    sendNetworkBuffer.Write(message, (uint)offset, (uint)length);
                }
                else
                {
                    queued = new byte[length];
                    Buffer.BlockCopy(message, offset, queued, 0, length);
                }
            }

            if (queued != null)
                await QueueSend(queued);

            return true;
        }

        async Task QueueSend(byte[] message)
        {
            await sendSemaphore.WaitAsync();
            try
            {
                var socket = sock ?? throw new InvalidOperationException("WebSocket is closed.");
                await socket.SendAsync(
                    new ArraySegment<byte>(message),
                    WebSocketMessageType.Binary,
                    true,
                    CancellationToken.None);
                Interlocked.Add(ref totalSent, message.Length);
            }
            catch
            {
                NotifySendFailure();
                throw;
            }
            finally
            {
                sendSemaphore.Release();
            }
        }

        void ObserveSend(Task task)
        {
            _ = task.ContinueWith(
                completed =>
                {
                    // Observe the exception; QueueSend already closed/notified the receiver.
                    _ = completed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        void NotifySendFailure()
        {
            if (Interlocked.Exchange(ref sendFailureNotified, 1) == 0)
            {
                try { sock?.Abort(); } catch { }
                Receiver?.NetworkClose(this);
            }
        }

        public ISocket Accept()
        {
            throw new NotImplementedException();
        }

        public AsyncReply<bool> BeginAsync()
        {
            return new AsyncReply<bool>(Begin());
        }


        private void NetworkReceive(Task<WebSocketReceiveResult> task)
        {

            if (sock.State == WebSocketState.Closed || sock.State == WebSocketState.Aborted || sock.State == WebSocketState.CloseReceived)
            {
                Receiver?.NetworkClose(this);
                return;
            }

 
            var receivedLength = task.Result.Count;

            totalReceived += receivedLength;

            receiveNetworkBuffer.Write(websocketReceiveBuffer, 0, (uint)receivedLength);

            Receiver?.NetworkReceive(this, receiveNetworkBuffer);

            sock.ReceiveAsync(websocketReceiveBufferSegment, CancellationToken.None)
                .ContinueWith(NetworkReceive);

        }


        public void NetworkConnect(ISocket sender)
        {
            Receiver?.NetworkConnect(this);
        }
    }
}
