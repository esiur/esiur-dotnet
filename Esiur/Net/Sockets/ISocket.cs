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
using Esiur.Core;

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
        AsyncReply<bool> Connect(string hostname, ushort port);
        bool Begin();
        //ISocket Accept();
        AsyncReply<ISocket> Accept();
        IPEndPoint RemoteEndPoint { get; }
        IPEndPoint LocalEndPoint { get; }
    }
}
