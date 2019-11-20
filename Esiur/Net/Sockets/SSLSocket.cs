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

namespace Esiur.Net.Sockets
{
    public class SSLSocket : ISocket
    {
        Socket sock;
        byte[] receiveBuffer;
 
        NetworkBuffer receiveNetworkBuffer = new NetworkBuffer();

        object sendLock = new object();

        Queue<byte[]> sendBufferQueue = new Queue<byte[]>();

        bool asyncSending;


        SocketState state = SocketState.Initial;

        public event ISocketReceiveEvent OnReceive;
        public event ISocketConnectEvent OnConnect;
        public event ISocketCloseEvent OnClose;
        public event DestroyedEvent OnDestroy;

        SslStream ssl;
        X509Certificate2 cert;
        bool server;
        string hostname;

        private void Connected(Task t)
        {
            if (server)
            {
                ssl.AuthenticateAsServerAsync(cert).ContinueWith(Authenticated);
            }
            else
            {
                ssl.AuthenticateAsClientAsync(hostname).ContinueWith(Authenticated);
            }
        }

        public AsyncReply<bool> Connect(string hostname, ushort port)
        {
            var rt = new AsyncReply<bool>();

            try
            {
                state = SocketState.Connecting;
                sock.ConnectAsync(hostname, port).ContinueWith((x) =>
                {
                    if (x.IsFaulted)
                        rt.TriggerError(x.Exception);
                    else
                        rt.Trigger(true);

                    Connected(x);
                });
            }
            catch (Exception ex)
            {
                rt.TriggerError(ex);
            }

            return rt;
        }

        private void DataSent(Task task)
        {
            try
            {

                if (sendBufferQueue.Count > 0)
                {
                    byte[] data = sendBufferQueue.Dequeue();
                    lock (sendLock)
                        ssl.WriteAsync(data, 0, data.Length).ContinueWith(DataSent);
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
            
                Global.Log("SSLSocket", LogType.Error, ex.ToString());
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



        public SSLSocket(Socket Socket, X509Certificate2 certificate, bool authenticateAsServer)
        {
            cert = certificate;
            sock = Socket;
            receiveBuffer = new byte[sock.ReceiveBufferSize];
 
            ssl = new SslStream(new NetworkStream(sock));

            server = authenticateAsServer;

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
            }

            sock.Shutdown(SocketShutdown.Both);

            OnClose?.Invoke();
        }

        public void Send(byte[] message)
        {
            Send(message, 0, message.Length);
        }

        public void Send(byte[] message, int offset, int size)
        {
            lock (sendLock)
            {
                if (asyncSending)
                {
                    sendBufferQueue.Enqueue(message.Clip((uint)offset, (uint)size));
                }
                else
                {
                    asyncSending = true;
                    ssl.WriteAsync(message, offset, size).ContinueWith(DataSent);
                }
            }
        }

        void Authenticated(Task task)
        {
            try
            {
                state = SocketState.Established;
                OnConnect?.Invoke();

                if (!server)
                    Begin();
            }
            catch (Exception ex)
            {
                state = SocketState.Terminated;
                Close();
                Global.Log(ex);
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


                receiveNetworkBuffer.Write(receiveBuffer, 0, (uint)task.Result);
                OnReceive?.Invoke(receiveNetworkBuffer);
                if (state == SocketState.Established)
                    ssl.ReadAsync(receiveBuffer, 0, receiveBuffer.Length).ContinueWith(DataReceived);
            }
            catch (Exception ex)
            {
                if (state != SocketState.Closed && !sock.Connected)
                {
                    state = SocketState.Terminated;
                    Close();
                }

                Global.Log("SSLSocket", LogType.Error, ex.ToString());
            }
        }

        public bool Begin()
        {
            if (state == SocketState.Established)
            {
                ssl.ReadAsync(receiveBuffer, 0, receiveBuffer.Length).ContinueWith(DataReceived);
                return true;
            }
            else
                return false;
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
                        reply.Trigger(new SSLSocket(x.Result, cert, true));
                    }
                    catch
                    {
                        reply.Trigger(null);
                    }

                }, null);

            }
            catch
            {
                state = SocketState.Terminated;
                return null;
            }

            return reply;
        }

        public void Hold()
        {
            throw new NotImplementedException();
        }

        public void Unhold()
        {
            throw new NotImplementedException();
        }
    }
}