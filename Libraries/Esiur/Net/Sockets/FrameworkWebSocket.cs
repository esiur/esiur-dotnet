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

            lock (sendLock)
            {
                if (held)
                {
                    sendNetworkBuffer.Write(message);
                }
                else
                {
                    totalSent += message.Length;
                    sock.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Binary,
                        true, new System.Threading.CancellationToken());
                }
            }
        }


        public void Send(byte[] message, int offset, int size)
        {
            lock (sendLock)
            {
                if (held)
                {
                    sendNetworkBuffer.Write(message, (uint)offset, (uint)size);
                }
                else
                {
                    totalSent += size;

                    sock.SendAsync(new ArraySegment<byte>(message, offset, size),
                        WebSocketMessageType.Binary, true, new System.Threading.CancellationToken());
                }
            }
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

        public bool Trigger(ResourceTrigger trigger)
        {
            return true;
        }

        public void Destroy()
        {
            Close();

            receiveNetworkBuffer = null;
            Receiver = null;

            sock = null;
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
            lock (sendLock)
            {
                held = false;

                var message = sendNetworkBuffer.Read();

                if (message == null)
                    return;

                totalSent += message.Length;

                sock.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Binary,
                    true, new System.Threading.CancellationToken());

            }
        }

        public async AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
        {
            if (held)
            {
                sendNetworkBuffer.Write(message, (uint)offset, (uint)length);
            }
            else
            {
                totalSent += length;

                await sock.SendAsync(new ArraySegment<byte>(message, offset, length),
                    WebSocketMessageType.Binary, true, new System.Threading.CancellationToken());
            }


            return true;
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
