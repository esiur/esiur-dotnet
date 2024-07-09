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
using System.IO;
using Esiur.Core;
using Esiur.Resource;
using Esiur.Data;
using System.Globalization;
using Esiur.Net.Packets.WebSocket;

namespace Esiur.Net.Sockets;
public class WSocket : ISocket, INetworkReceiver<ISocket>
{
    WebsocketPacket pkt_receive = new WebsocketPacket();
    WebsocketPacket pkt_send = new WebsocketPacket();

    ISocket sock;
    NetworkBuffer receiveNetworkBuffer = new NetworkBuffer();
    NetworkBuffer sendNetworkBuffer = new NetworkBuffer();

    object sendLock = new object();
    bool held;

    //public event ISocketReceiveEvent OnReceive;
    //public event ISocketConnectEvent OnConnect;
    //public event ISocketCloseEvent OnClose;
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

    public INetworkReceiver<ISocket> Receiver { get; set; }

    public WSocket(ISocket socket)
    {
        pkt_send.FIN = true;
        pkt_send.Mask = false;
        pkt_send.Opcode = WebsocketPacket.WSOpcode.BinaryFrame;
        sock = socket;

        sock.Receiver = this;

        //sock.OnClose += Sock_OnClose;
        //sock.OnConnect += Sock_OnConnect;
        //sock.OnReceive += Sock_OnReceive;
    }

    //private void Sock_OnReceive(NetworkBuffer buffer)
    //{

    //}

    //private void Sock_OnConnect()
    //{
    //    OnConnect?.Invoke();
    //}

    //private void Sock_OnClose()
    //{
    //    OnClose?.Invoke();
    //}

    public void Send(WebsocketPacket packet)
    {
        lock (sendLock)
            if (packet.Compose())
                sock.Send(packet.Data);
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
                //Console.WriteLine("TX " + message.Length +"/"+totalSent);// + " " + DC.ToHex(message, 0, (uint)size));

                pkt_send.Message = message;

                if (pkt_send.Compose())
                    sock?.Send(pkt_send.Data);

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
                //Console.WriteLine("TX " + size + "/"+totalSent);// + " " + DC.ToHex(message, 0, (uint)size));

                pkt_send.Message = new byte[size];
                Buffer.BlockCopy(message, offset, pkt_send.Message, 0, size);
                if (pkt_send.Compose())
                    sock.Send(pkt_send.Data);
            }
        }
    }


    public void Close()
    {
        sock?.Close();
    }

    public AsyncReply<bool> Connect(string hostname, ushort port)
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
        //OnClose = null;
        //OnConnect = null;
        //OnReceive = null;
        receiveNetworkBuffer = null;
        //sock.OnReceive -= Sock_OnReceive;
        //sock.OnClose -= Sock_OnClose;
        //sock.OnConnect -= Sock_OnConnect;
        sock.Receiver = null;
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
        //Console.WriteLine("WS Hold  ");
        held = true;
    }

    public void Unhold()
    {
        lock (sendLock)
        {
            held = false;

            var message = sendNetworkBuffer.Read();

            //Console.WriteLine("WS Unhold {0}", message == null ? 0 : message.Length);

            if (message == null)
                return;

            totalSent += message.Length;

            pkt_send.Message = message;
            if (pkt_send.Compose())
                sock.Send(pkt_send.Data);

        }
    }

    public AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
    {
        throw new NotImplementedException();
    }

    public ISocket Accept()
    {
        throw new NotImplementedException();
    }

    public AsyncReply<bool> BeginAsync()
    {
        return sock.BeginAsync();
    }

    public void NetworkClose(ISocket sender)
    {
        Receiver?.NetworkClose(sender);
    }

    public void NetworkReceive(ISocket sender, NetworkBuffer buffer)
    {

        if (sock.State == SocketState.Closed)
            return;

        if (buffer.Protected)
            return;



        var msg = buffer.Read();

        if (msg == null)
            return;

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
                var pkt_pong = new WebsocketPacket()
                {
                    FIN = true,
                    Mask = false,
                    Opcode = WebsocketPacket.WSOpcode.Pong,
                    Message = pkt_receive.Message
                };

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

                //Console.WriteLine("WS IN: " + pkt_receive.Opcode.ToString() + " " + pkt_receive.Message.Length + " | " + offset + " " + string.Join(" ", pkt_receive.Message));//  DC.ToHex(pkt_receive.Message));

            }
            else
            {
                Global.Log("WSocket", LogType.Debug, "Unknown WS opcode:" + pkt_receive.Opcode);
            }

            if (offset == msg.Length)
            {

                Receiver?.NetworkReceive(this, receiveNetworkBuffer);
                return;
            }

            wsPacketLength = pkt_receive.Parse(msg, offset, (uint)msg.Length);
        }

        if (wsPacketLength < 0)
        {
            // save the incomplete packet to the heldBuffer queue
            buffer.HoldFor(msg, offset, (uint)(msg.Length - offset), (uint)(msg.Length - offset) + (uint)-wsPacketLength);

        }

        //Console.WriteLine("WS IN: " + receiveNetworkBuffer.Available);

        Receiver?.NetworkReceive(this, receiveNetworkBuffer);


        if (buffer.Available > 0 && !buffer.Protected)
            NetworkReceive(this, buffer);
    }


    public void NetworkConnect(ISocket sender)
    {
        Receiver?.NetworkConnect(this);
    }
}
