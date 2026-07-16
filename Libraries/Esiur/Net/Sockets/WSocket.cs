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
    NetworkBuffer fragmentedMessageBuffer = new NetworkBuffer();

    bool fragmentedMessage;
    WebsocketPacket.WSOpcode fragmentedMessageOpcode;
    ulong fragmentedMessageLength;

    object sendLock = new object();
    bool held;
    bool destroyed;
    ulong maximumMessageLength = WebsocketPacket.DefaultMaximumPayloadLength;

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

    /// <summary>Whether this endpoint receives client frames and sends server frames.</summary>
    public bool IsServer { get; }

    /// <summary>Maximum payload accepted for one complete message.</summary>
    public ulong MaximumMessageLength
    {
        get => maximumMessageLength;
        set
        {
            maximumMessageLength = value;
            pkt_receive.MaximumPayloadLength = value;
        }
    }

    public WSocket(ISocket socket)
        : this(socket, true)
    {
    }

    public WSocket(ISocket socket, bool isServer)
    {
        IsServer = isServer;
        pkt_send.FIN = true;
        pkt_send.Mask = !isServer;
        pkt_send.Opcode = WebsocketPacket.WSOpcode.BinaryFrame;
        pkt_receive.ExpectedMask = isServer;
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
        {
            PrepareOutboundPacket(packet);
            if (packet.Compose())
                sock.Send(packet.Data);
        }
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

                PrepareOutboundPacket(pkt_send);
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
                PrepareOutboundPacket(pkt_send);
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

    public bool Trigger(ResourceOperation trigger)
    {
        return true;
    }

    public void Destroy()
    {
        ISocket socket;
        DestroyedEvent onDestroy;

        lock (sendLock)
        {
            if (destroyed)
                return;

            destroyed = true;
            socket = sock;
            onDestroy = OnDestroy;
            OnDestroy = null;
        }

        // Close can synchronously re-enter Destroy through NetworkClose. The
        // guard above keeps that path idempotent while the captured socket is
        // still valid for the outer cleanup.
        try { socket?.Close(); } catch (Exception ex) { Global.Log(ex); }

        lock (sendLock)
        {
            if (socket != null && ReferenceEquals(socket.Receiver, this))
                socket.Receiver = null;

            sock = null;
            receiveNetworkBuffer = null;
            fragmentedMessageBuffer = null;
        }

        onDestroy?.Invoke(this);
    }

    public AsyncReply<ISocket> AcceptAsync()
    {
        throw new NotImplementedException();
    }

    public void Hold()
    {
        lock (sendLock)
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
            PrepareOutboundPacket(pkt_send);
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
        Receiver?.NetworkClose(this);
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

        if (!TryParseFrame(msg, 0, out var wsPacketLength))
            return;

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
                    Mask = !IsServer,
                    Opcode = WebsocketPacket.WSOpcode.Pong,
                    Message = pkt_receive.Message
                };
                Send(pkt_pong);
            }
            else if (pkt_receive.Opcode == WebsocketPacket.WSOpcode.BinaryFrame
                    || pkt_receive.Opcode == WebsocketPacket.WSOpcode.TextFrame
                    || pkt_receive.Opcode == WebsocketPacket.WSOpcode.ContinuationFrame)
            {
                totalReceived += pkt_receive.Message.Length;
                if (!ProcessDataFrame(pkt_receive))
                    return;
            }

            // Pong frames need no further processing. All successfully handled frames
            // advance by the same parsed length.
            offset += (uint)wsPacketLength;

            if (offset == msg.Length)
            {
                DeliverReceivedData();
                return;
            }

            if (!TryParseFrame(msg, offset, out wsPacketLength))
                return;
        }

        if (wsPacketLength < 0)
        {
            // save the incomplete packet to the heldBuffer queue
            buffer.HoldFor(msg, offset, (uint)(msg.Length - offset), (uint)(msg.Length - offset) + (uint)-wsPacketLength);

        }

        //Console.WriteLine("WS IN: " + receiveNetworkBuffer.Available);

        DeliverReceivedData();


        if (buffer.Available > 0 && !buffer.Protected)
            NetworkReceive(this, buffer);
    }

    private bool ProcessDataFrame(WebsocketPacket packet)
    {
        var payload = packet.Message ?? Array.Empty<byte>();

        if (MaximumMessageLength > 0 && (ulong)payload.LongLength > MaximumMessageLength)
            return RejectProtocol($"WebSocket message exceeds the {MaximumMessageLength}-byte limit.");

        if (packet.Opcode == WebsocketPacket.WSOpcode.TextFrame
            || packet.Opcode == WebsocketPacket.WSOpcode.BinaryFrame)
        {
            if (fragmentedMessage)
                return RejectProtocol("A new WebSocket data frame arrived before the fragmented message completed.");

            if (packet.FIN)
            {
                receiveNetworkBuffer.Write(payload);
                return true;
            }

            fragmentedMessage = true;
            fragmentedMessageOpcode = packet.Opcode;
            fragmentedMessageLength = 0;
            fragmentedMessageBuffer.Read();
            return AppendFragment(payload);
        }

        if (!fragmentedMessage)
            return RejectProtocol("A WebSocket continuation frame arrived without an active fragmented message.");

        if (!AppendFragment(payload))
            return false;

        if (!packet.FIN)
            return true;

        var message = fragmentedMessageBuffer.Read() ?? Array.Empty<byte>();
        try
        {
            if (fragmentedMessageOpcode == WebsocketPacket.WSOpcode.TextFrame)
                WebsocketPacket.ValidateTextPayload(message);
        }
        catch (InvalidDataException exception)
        {
            return RejectProtocol(exception.Message);
        }

        receiveNetworkBuffer.Write(message);
        ResetFragmentedMessage();
        return true;
    }

    private bool AppendFragment(byte[] payload)
    {
        var nextLength = fragmentedMessageLength + (ulong)payload.LongLength;
        if (nextLength < fragmentedMessageLength
            || nextLength > int.MaxValue
            || (MaximumMessageLength > 0 && nextLength > MaximumMessageLength))
            return RejectProtocol($"WebSocket fragmented message exceeds the {MaximumMessageLength}-byte limit.");

        fragmentedMessageBuffer.Write(payload);
        fragmentedMessageLength = nextLength;
        return true;
    }

    private void DeliverReceivedData()
    {
        if (receiveNetworkBuffer != null && receiveNetworkBuffer.Available > 0)
            Receiver?.NetworkReceive(this, receiveNetworkBuffer);
    }

    private bool RejectProtocol(string message)
    {
        Global.Log("WSocket", LogType.Warning, message);
        ResetFragmentedMessage();
        Close();
        return false;
    }

    private void ResetFragmentedMessage()
    {
        fragmentedMessage = false;
        fragmentedMessageLength = 0;
        fragmentedMessageOpcode = default;
        fragmentedMessageBuffer?.Read();
    }

    private void PrepareOutboundPacket(WebsocketPacket packet)
    {
        packet.Mask = !IsServer;

        // RFC 6455 requires a fresh unpredictable masking key for every client
        // frame. pkt_send is intentionally reused, so discard its previous key.
        if (packet.Mask)
            packet.MaskKey = null;
    }

    private bool TryParseFrame(byte[] message, uint offset, out long packetLength)
    {
        try
        {
            packetLength = pkt_receive.Parse(message, offset, (uint)message.Length);
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidDataException ||
            exception is ParserLimitException ||
            exception is ArgumentException)
        {
            Global.Log(exception);
            packetLength = 0;
            Close();
            return false;
        }
    }


    public void NetworkConnect(ISocket sender)
    {
        Receiver?.NetworkConnect(this);
    }
}
