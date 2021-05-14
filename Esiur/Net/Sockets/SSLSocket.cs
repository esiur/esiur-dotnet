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

        public INetworkReceiver<ISocket> Receiver { get; set; }

        Socket sock;
        byte[] receiveBuffer;

        bool held;

        //ArraySegment<byte> receiveBufferSegment;

        NetworkBuffer receiveNetworkBuffer = new NetworkBuffer();

        readonly object sendLock = new object();

        Queue<KeyValuePair<AsyncReply<bool>, byte[]>> sendBufferQueue = new Queue<KeyValuePair<AsyncReply<bool>, byte[]>>();// Queue<byte[]>();

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


        public async AsyncReply<bool> Connect(string hostname, ushort port)
        {
            var rt = new AsyncReply<bool>();

            this.hostname = hostname;
            this.server = false;

            state = SocketState.Connecting;
            await sock.ConnectAsync(hostname, port);


            try
            {
                await BeginAsync();
                state = SocketState.Established;
                //OnConnect?.Invoke();
                Receiver?.NetworkConnect(this);
            }
            catch (Exception ex)
            {
                state = SocketState.Closed;// .Terminated;
                Close();
                Global.Log(ex);
            }

            return true;
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


        private void SendCallback(IAsyncResult ar)
        {
            if (ar != null)
            {
                try
                {
                    ssl.EndWrite(ar);

                    if (ar.AsyncState != null)
                        ((AsyncReply<bool>)ar.AsyncState).Trigger(true);
                }
                catch
                {
                    if (state != SocketState.Closed && !sock.Connected)
                    {
                        //state = SocketState.Closed;//.Terminated;
                        Close();
                    }
                }
            }

            lock (sendLock)
            {

                if (sendBufferQueue.Count > 0)
                {
                    var kv = sendBufferQueue.Dequeue();

                    try
                    {
                        ssl.BeginWrite(kv.Value, 0, kv.Value.Length, SendCallback, kv.Key);
                    }
                    catch //(Exception ex)
                    {
                        asyncSending = false;
                        try
                        {
                            if (kv.Key != null)
                                kv.Key.Trigger(false);

                            if (state != SocketState.Closed && !sock.Connected)
                            {
                                //state = SocketState.Terminated;
                                Close();
                            }
                        }
                        catch //(Exception ex2)
                        {
                            //state = SocketState.Closed;// .Terminated;
                            Close();
                        }

                        //Global.Log("TCPSocket", LogType.Error, ex.ToString());
                    }
                }
                else
                {
                    asyncSending = false;
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
            if (state != SocketState.Closed)// && state != SocketState.Terminated)
            {
                state = SocketState.Closed;

                if (sock.Connected)
                {
                    try
                    {
                        sock.Shutdown(SocketShutdown.Both);
                    }
                    catch
                    {
                        //state = SocketState.Terminated;
                    }
                }

                Receiver?.NetworkClose(this);
                //OnClose?.Invoke();
            }
        }


        public void Send(byte[] message)
        {
            Send(message, 0, message.Length);
        }


        public void Send(byte[] message, int offset, int size)
        {


            var msg = message.Clip((uint)offset, (uint)size);

            lock (sendLock)
            {

                if (!sock.Connected)
                    return;

                if (asyncSending || held)
                {
                    sendBufferQueue.Enqueue(new KeyValuePair<AsyncReply<bool>, byte[]>(null, msg));// message.Clip((uint)offset, (uint)size));
                }
                else
                {
                    asyncSending = true;
                    try
                    {
                        ssl.BeginWrite(msg, 0, msg.Length, SendCallback, null);
                    }
                    catch
                    {
                        asyncSending = false;
                        //state = SocketState.Terminated;
                        Close();
                    }
                }
            }
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
            catch //(Exception ex)
            {
                if (state != SocketState.Closed && !sock.Connected)
                {
                    //state = SocketState.Terminated;
                    Close();
                }

                //Global.Log("SSLSocket", LogType.Error, ex.ToString());
            }
        }

        public bool Trigger(ResourceTrigger trigger)
        {
            return true;
        }

        public void Destroy()
        {
            Close();
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
            held = true;
        }

        public void Unhold()
        {
            try
            {
                SendCallback(null);
            }
            catch (Exception ex)
            {
                Global.Log(ex);
            }
            finally
            {
                held = false;
            }
        }


        public AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
        {

            var msg = message.Clip((uint)offset, (uint)length);

            lock (sendLock)
            {
                if (!sock.Connected)
                    return new AsyncReply<bool>(false);

                var rt = new AsyncReply<bool>();

                if (asyncSending || held)
                {
                    sendBufferQueue.Enqueue(new KeyValuePair<AsyncReply<bool>, byte[]>(rt, msg));
                }
                else
                {
                    asyncSending = true;
                    try
                    {
                        ssl.BeginWrite(msg, 0, msg.Length, SendCallback, rt);// null);
                    }
                    catch (Exception ex)
                    {
                        rt.TriggerError(ex);
                        asyncSending = false;
                        //state = SocketState.Terminated;
                        Close();
                    }
                }

                return rt;
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
}