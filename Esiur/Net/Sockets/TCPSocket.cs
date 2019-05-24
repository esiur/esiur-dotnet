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
using Esiur.Engine;
using System.Threading;
using Esiur.Resource;
using System.Threading.Tasks;
using Esiur.Data;

namespace Esiur.Net.Sockets
{
    public class TCPSocket : ISocket
    {
        Socket sock;
        byte[] receiveBuffer;

        ArraySegment<byte> receiveBufferSegment;

        NetworkBuffer receiveNetworkBuffer = new NetworkBuffer();

        object sendLock = new object();

        Queue<byte[]> sendBufferQueue = new Queue<byte[]>();

        bool asyncSending;
        bool began = false;

        SocketState state = SocketState.Initial;

        public event ISocketReceiveEvent OnReceive;
        public event ISocketConnectEvent OnConnect;
        public event ISocketCloseEvent OnClose;
        public event DestroyedEvent OnDestroy;

        SocketAsyncEventArgs socketArgs = new SocketAsyncEventArgs();

        private void Connected(Task t)
        {
            state = SocketState.Established;
            OnConnect?.Invoke();
            Begin();
        }

        public bool Begin()
        {
            if (began)
                return false;

            began = true;

            socketArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
            socketArgs.Completed += SocketArgs_Completed;

            if (!sock.ReceiveAsync(socketArgs))
                SocketArgs_Completed(null, socketArgs);

            //sock.ReceiveAsync(receiveBufferSegment, SocketFlags.None).ContinueWith(DataReceived);
            return true;
        }

        public bool Connect(string hostname, ushort port)
        {
            try
            {
                state = SocketState.Connecting;
                sock.ConnectAsync(hostname, port).ContinueWith(Connected);
                return true;
            }
            catch
            {
                return false;
            }
        }


        private void DataReceived(Task<int> task)
        {
            try
            {
                // SocketError err;

                if (state == SocketState.Closed || state == SocketState.Terminated)
                    return;

                if (task.Result <= 0)
                {
                    Close();
                    return;
                }

                //if (receiveNetworkBuffer.Protected)
                  //  Console.WriteLine();

                //lock (receiveNetworkBuffer.SyncLock)
                receiveNetworkBuffer.Write(receiveBuffer, 0, (uint)task.Result);

                //Console.WriteLine("TC IN: " + (uint)task.Result + " " + DC.ToHex(receiveBuffer, 0, (uint)task.Result));

                OnReceive?.Invoke(receiveNetworkBuffer);
                if (state == SocketState.Established)
                {
                    sock.ReceiveAsync(receiveBufferSegment, SocketFlags.None).ContinueWith(DataReceived);
                    
                }

            }
            catch (Exception ex)
            {
                if (state != SocketState.Closed && !sock.Connected)
                {
                    state = SocketState.Terminated;
                    Close();
                }

                Global.Log("TCPSocket", LogType.Error, ex.ToString());
            }
        }

        private void SocketArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                // SocketError err;
               
                if (state == SocketState.Closed || state == SocketState.Terminated)
                    return;

                if (e.BytesTransferred == 0)
                {
                    Close();
                    return;
                }

                //if (receiveNetworkBuffer.Protected)
                //    Console.WriteLine();

                
                //lock (receiveNetworkBuffer.SyncLock)
                receiveNetworkBuffer.Write(receiveBuffer, 0, (uint)e.BytesTransferred);

                //Console.WriteLine("TC IN: " + (uint)e.BytesTransferred + " " + DC.ToHex(receiveBuffer, 0, (uint)e.BytesTransferred));




                OnReceive?.Invoke(receiveNetworkBuffer);

                if (state == SocketState.Established)
                {
                    if (!sock.ReceiveAsync(socketArgs))
                    {
                        //Console.WriteLine("Sync");
                        SocketArgs_Completed(sender, e);
                    }
                }

            }
            catch (Exception ex)
            {
                if (state != SocketState.Closed && !sock.Connected)
                {
                    state = SocketState.Terminated;
                    Close();
                }

                Global.Log("TCPSocket", LogType.Error, ex.ToString());
            }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return (IPEndPoint)sock.LocalEndPoint; }
        }

        public TCPSocket()
        {
            sock = new Socket(AddressFamily.InterNetwork,
                                 SocketType.Stream,
                                 ProtocolType.Tcp);
            receiveBuffer = new byte[sock.ReceiveBufferSize];
            receiveBufferSegment = new ArraySegment<byte>(receiveBuffer);

        }

        public TCPSocket(string hostname, ushort port)
        {
            // create the socket
            sock = new Socket(AddressFamily.InterNetwork,
                                             SocketType.Stream,
                                             ProtocolType.Tcp);

            receiveBuffer = new byte[sock.ReceiveBufferSize];
            receiveBufferSegment = new ArraySegment<byte>(receiveBuffer);

            Connect(hostname, port);

        }

        private void DataSent(Task<int> task)
        {
            try
            {

                if (sendBufferQueue.Count > 0)
                {
                    byte[] data = sendBufferQueue.Dequeue();
                    lock (sendLock)
                        sock.SendAsync(new ArraySegment<byte>(data), SocketFlags.None).ContinueWith(DataSent);
                }
                else
                {
                    asyncSending = false;
                }
            }
            catch (Exception ex)
            {
                if (state != SocketState.Closed && !sock.Connected)
                {
                    state = SocketState.Terminated;
                    Close();
                }

                asyncSending = false;

                Global.Log("TCPSocket", LogType.Error, ex.ToString());
            }
        }

        public TCPSocket(IPEndPoint localEndPoint)
        {
            // create the socket
            sock = new Socket(AddressFamily.InterNetwork,
                                             SocketType.Stream,
                                             ProtocolType.Tcp);

            receiveBuffer = new byte[sock.ReceiveBufferSize];

            state = SocketState.Listening;


            // bind
            sock.Bind(localEndPoint);

            // start listening
            sock.Listen(UInt16.MaxValue);


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


        public TCPSocket(Socket socket)
        {
            sock = socket;
            receiveBuffer = new byte[sock.ReceiveBufferSize];
            receiveBufferSegment = new ArraySegment<byte>(receiveBuffer);
            if (socket.Connected)
                state = SocketState.Established;
        }

        public void Close()
        {
            if (state != SocketState.Closed && state != SocketState.Terminated)
                state = SocketState.Closed;

            if (sock.Connected)
            {
                try
                {
                    sock.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    state = SocketState.Terminated;
                }

                sock.Shutdown(SocketShutdown.Both);// Close();
                OnClose?.Invoke();
            }

        }

        public void Send(byte[] message)
        {
            Send(message, 0, message.Length);
        }

        public void Send(byte[] message, int offset, int size)
        {
            if (sock.Connected)
                lock (sendLock)
                {
                    if (asyncSending)
                    {
                        sendBufferQueue.Enqueue(message.Clip((uint)offset, (uint)size));
                    }
                    else
                    {
                        asyncSending = true;
                        sock.SendAsync(new ArraySegment<byte>(message, offset, size), SocketFlags.None).ContinueWith(DataSent);
                    }
                }
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
            var reply = new AsyncReply<ISocket>();

            try
            {
                sock.AcceptAsync().ContinueWith((x) =>
                {
                    try
                    {
                        reply.Trigger(new TCPSocket(x.Result));
                    }
                    catch
                    {
                        reply.Trigger(null);
                    }
                });
            }
            catch
            {
                state = SocketState.Terminated;
                return null;
            }

            return reply;
        }
    }
}