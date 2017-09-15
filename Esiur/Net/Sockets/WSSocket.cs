using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Esiur.Net.Packets;
using Esiur.Misc;
using System.IO;
using Esiur.Engine;
using Esiur.Resource;

namespace Esiur.Net.Sockets
{
    public class WSSocket : ISocket
    {
        WebsocketPacket pkt_receive = new WebsocketPacket();
        WebsocketPacket pkt_send = new WebsocketPacket();

        ISocket sock;
        NetworkBuffer receiveNetworkBuffer = new NetworkBuffer();
        object sendLock = new object();

        public event ISocketReceiveEvent OnReceive;
        public event ISocketConnectEvent OnConnect;
        public event ISocketCloseEvent OnClose;
        public event DestroyedEvent OnDestroy;

        long totalSent, totalReceived;

        public IPEndPoint LocalEndPoint
        {
            get { return (IPEndPoint)sock.LocalEndPoint; }
        }

  
        
    public IPEndPoint RemoteEndPoint
    {
        get { return sock.RemoteEndPoint; }
    }
    

        public SocketState State
        {
            get
            {
                return sock.State;
            }
        }

 
        public WSSocket(ISocket socket)
        {
            pkt_send.FIN = true;
            pkt_send.Mask = false;
            pkt_send.Opcode = WebsocketPacket.WSOpcode.BinaryFrame;
            sock = socket;
            sock.OnClose += Sock_OnClose;
            sock.OnConnect += Sock_OnConnect;
            sock.OnReceive += Sock_OnReceive;
        }

        private void Sock_OnReceive(NetworkBuffer buffer)
        {

            if (sock.State == SocketState.Closed || sock.State == SocketState.Terminated)
                return;

            if (buffer.Protected)
                return;

            var msg = buffer.Read();

            var wsPacketLength = pkt_receive.Parse(msg, 0, (uint)msg.Length);

            if (wsPacketLength < 0)
            {
                buffer.Protect(msg, 0, (uint)msg.Length + (uint)-wsPacketLength);
                return;
            }

            uint offset = 0;

            while (wsPacketLength > 0)
            {
                if (pkt_receive.Opcode == WebsocketPacket.WSOpcode.ConnectionClose)
                {
                    Close();
                    return;
                }
                else if (pkt_receive.Opcode == WebsocketPacket.WSOpcode.Ping)
                {
                    var pkt_pong = new WebsocketPacket();

                    pkt_pong.FIN = true;
                    pkt_pong.Mask = false;
                    pkt_pong.Opcode = WebsocketPacket.WSOpcode.Pong;
                    pkt_pong.Message = pkt_receive.Message;
                    offset += (uint)wsPacketLength;

                    Send(pkt_pong);
                }
                else if (pkt_receive.Opcode == WebsocketPacket.WSOpcode.Pong)
                {
                    offset += (uint)wsPacketLength;
                }
                else if (pkt_receive.Opcode == WebsocketPacket.WSOpcode.BinaryFrame
                        || pkt_receive.Opcode == WebsocketPacket.WSOpcode.TextFrame
                        || pkt_receive.Opcode == WebsocketPacket.WSOpcode.ContinuationFrame)
                {
                    totalReceived += pkt_receive.Message.Length;
                    //Console.WriteLine("RX " + pkt_receive.Message.Length + "/" + totalReceived);// + " " + DC.ToHex(message, 0, (uint)size));

                    receiveNetworkBuffer.Write(pkt_receive.Message);
                    offset += (uint)wsPacketLength;
                }
                else
                    Console.WriteLine("Unknown WS opcode:" + pkt_receive.Opcode);

                if (offset == msg.Length)
                {
                    OnReceive?.Invoke(receiveNetworkBuffer);
                    return;
                }

                wsPacketLength = pkt_receive.Parse(msg, offset, (uint)msg.Length);
            }

            if (wsPacketLength < 0)//(offset < msg.Length) && (offset > 0))
            {
                //receiveNetworkBuffer.HoldFor(msg, offset, (uint)(msg.Length - offset), (uint)msg.Length + (uint)-wsPacketLength);
                // save the incomplete packet to the heldBuffer queue

                receiveNetworkBuffer.HoldFor(msg, offset, (uint)(msg.Length - offset), (uint)(msg.Length - offset) + (uint)-wsPacketLength);

            }

            OnReceive?.Invoke(receiveNetworkBuffer);
        }

        private void Sock_OnConnect()
        {
            OnConnect?.Invoke();
        }

        private void Sock_OnClose()
        {
            OnClose?.Invoke();
        }

        public void Send(WebsocketPacket packet)
        {
            lock(sendLock)
                if (packet.Compose())
                    sock.Send(packet.Data);
        }

        public void Send(byte[] message)
        {
            lock(sendLock)
            {
                totalSent += message.Length;
                //Console.WriteLine("TX " + message.Length +"/"+totalSent);// + " " + DC.ToHex(message, 0, (uint)size));

                pkt_send.Message = message;
                if (pkt_send.Compose())
                    sock.Send(pkt_send.Data);
            }
        }

        
        public void Send(byte[] message, int offset, int size)
        {
            lock (sendLock)
            {
                totalSent += size;
                //Console.WriteLine("TX " + size + "/"+totalSent);// + " " + DC.ToHex(message, 0, (uint)size));

                pkt_send.Message = new byte[size];
                Buffer.BlockCopy(message, offset, pkt_send.Message, 0, size);
                if (pkt_send.Compose())
                    sock.Send(pkt_send.Data);
            }
        }


        public void Close()
        {
            sock.Close();
        }

        public bool Connect(string hostname, ushort port)
        {
            throw new NotImplementedException();
        }

 
        public bool Begin()
        {
            return sock.Begin();
        }

        public bool Trigger(ResourceTrigger trigger)
        {
            return true;
        }

        public void Destroy()
        {
            Close();
            OnDestroy?.Invoke(this);
        }

        public AsyncReply<ISocket> Accept()
        {
            throw new NotImplementedException();
        }
    }
}