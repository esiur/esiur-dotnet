using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using Esiur.Data;
using Esiur.Misc;
using System.Collections.Concurrent;
using Esiur.Resource;
using Esiur.Engine;

namespace Esiur.Net.Sockets
{
    public delegate void ISocketReceiveEvent(NetworkBuffer buffer);
    public delegate void ISocketConnectEvent();
    public delegate void ISocketCloseEvent();

    public interface ISocket: IDestructible
    {
        SocketState State { get; }

        event ISocketReceiveEvent OnReceive;
        event ISocketConnectEvent OnConnect;
        event ISocketCloseEvent OnClose;

        void Send(byte[] message);
        void Send(byte[] message, int offset, int size);
        void Close();
        bool Connect(string hostname, ushort port);
        bool Begin();
        //ISocket Accept();
        AsyncReply<ISocket> Accept();
        IPEndPoint RemoteEndPoint { get; }
        IPEndPoint LocalEndPoint { get; }
    }
}
