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
using Esiur.Resource;
using System.Threading.Tasks;
using Esiur.Data;

namespace Esiur.Net.Sockets;
public class TCPSocket : ISocket
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

    //SocketAsyncEventArgs socketArgs = new SocketAsyncEventArgs();

    private AsyncCallback receiveCallback;
    private AsyncCallback sendCallback;

    public AsyncReply<bool> BeginAsync()
    {
        return new AsyncReply<bool>(Begin());
    }


    private AsyncReply<bool> currentReply = null;

    public bool Begin()
    {
        // Socket destroyed
        if (receiveBuffer == null)
            return false;

        if (began)
            return false;

        began = true;
        /*

        socketArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
        socketArgs.Completed += SocketArgs_Completed;

        if (!sock.ReceiveAsync(socketArgs))
            SocketArgs_Completed(null, socketArgs);
            */
        receiveCallback = new AsyncCallback(ReceiveCallback);
        sendCallback = new AsyncCallback(SendCallback);

        sock.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, receiveCallback, this);
        //sock.ReceiveAsync(receiveBufferSegment, SocketFlags.None).ContinueWith(DataReceived);
        return true;
    }

    private static void ReceiveCallback(IAsyncResult ar)
    {
        var socket = ar.AsyncState as TCPSocket;

        try
        {

            if (socket.state != SocketState.Established)
                return;

            var recCount = socket.sock.EndReceive(ar);

            if (recCount > 0)
            {
                socket.receiveNetworkBuffer.Write(socket.receiveBuffer, 0, (uint)recCount);
                socket.Receiver?.NetworkReceive(socket, socket.receiveNetworkBuffer);

                if (socket.state == SocketState.Established)
                    socket.sock.BeginReceive(socket.receiveBuffer, 0, socket.receiveBuffer.Length, SocketFlags.None, socket.receiveCallback, socket);
            }
            else
            {
                socket.Close();
                return;
            }
        }
        catch //(Exception ex)
        {
            if (socket.state != SocketState.Closed && !socket.sock.Connected)
            {
                //socket.state = SocketState.Terminated;
                socket.Close();
            }

            //Global.Log("TCPSocket", LogType.Error, ex.ToString());
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
                {

                    state = SocketState.Established;
                        //OnConnect?.Invoke();
                        Receiver?.NetworkConnect(this);
                    Begin();
                    rt.Trigger(true);
                }
            });
        }
        catch (Exception ex)
        {
            rt.TriggerError(ex);
        }

        return rt;
    }


    //private void DataReceived(Task<int> task)
    //{
    //    try
    //    {
    //        // SocketError err;

    //        if (state == SocketState.Closed || state == SocketState.Terminated)
    //            return;

    //        if (task.Result <= 0)
    //        {
    //            Close();
    //            return;
    //        }

    //        receiveNetworkBuffer.Write(receiveBuffer, 0, (uint)task.Result);
    //        //OnReceive?.Invoke(receiveNetworkBuffer);
    //        Receiver?.NetworkReceive(this, receiveNetworkBuffer);
    //        if (state == SocketState.Established)
    //            sock.ReceiveAsync(receiveBufferSegment, SocketFlags.None).ContinueWith(DataReceived);

    //    }
    //    catch (Exception ex)
    //    {
    //        if (state != SocketState.Closed && !sock.Connected)
    //        {
    //            state = SocketState.Terminated;
    //            Close();
    //        }

    //        Global.Log("TCPSocket", LogType.Error, ex.ToString());
    //    }
    //}

    //private void SocketArgs_Completed(object sender, SocketAsyncEventArgs e)
    //{
    //    try
    //    {
    //        if (state != SocketState.Established)
    //            return;

    //        if (e.BytesTransferred <= 0)
    //        {
    //            Close();
    //            return;
    //        }
    //        else if (e.SocketError != SocketError.Success)
    //        {
    //            Close();
    //            return;

    //        }

    //        var recCount = e.BytesTransferred > e.Count ? e.Count : e.BytesTransferred;
    //        receiveNetworkBuffer.Write(receiveBuffer, 0, (uint)recCount);

    //        //OnReceive?.Invoke(receiveNetworkBuffer);
    //        Receiver?.NetworkReceive(this, receiveNetworkBuffer);

    //        if (state == SocketState.Established)
    //            while (!sock.ReceiveAsync(e))
    //            {
    //                if (e.SocketError != SocketError.Success)
    //                {
    //                    Close();
    //                    return;
    //                }

    //                if (State != SocketState.Established)
    //                    return;

    //                //if (e.BytesTransferred < 0)
    //                //    Console.WriteLine("BytesTransferred is less than zero");

    //                if (e.BytesTransferred <= 0)
    //                {
    //                    Close();
    //                    return;
    //                }
    //                else if (e.SocketError != SocketError.Success)
    //                {
    //                    Close();
    //                    return;
    //                }


    //                //if (e.BytesTransferred > 100000)
    //                //    Console.WriteLine("BytesTransferred is large " + e.BytesTransferred);

    //                recCount = e.BytesTransferred > e.Count ? e.Count : e.BytesTransferred;

    //                receiveNetworkBuffer.Write(receiveBuffer, 0, (uint)recCount);

    //                //OnReceive?.Invoke(receiveNetworkBuffer);
    //                Receiver?.NetworkReceive(this, receiveNetworkBuffer);
    //            }

    //    }
    //    catch (Exception ex)
    //    {
    //        if (state != SocketState.Closed && !sock.Connected)
    //        {
    //            state = SocketState.Terminated;
    //            Close();
    //        }

    //        Global.Log("TCPSocket", LogType.Error, ex.ToString());
    //    }
    //}

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
        //receiveBufferSegment = new ArraySegment<byte>(receiveBuffer);

    }

    public TCPSocket(string hostname, ushort port)
    {
        // create the socket
        sock = new Socket(AddressFamily.InterNetwork,
                                         SocketType.Stream,
                                         ProtocolType.Tcp);

        receiveBuffer = new byte[sock.ReceiveBufferSize];
        //receiveBufferSegment = new ArraySegment<byte>(receiveBuffer);

        Connect(hostname, port);

    }

    //private void DataSent(Task<int> task)
    //{
    //    try
    //    {
    //        lock (sendLock)
    //        {

    //            if (sendBufferQueue.Count > 0)
    //            {
    //                byte[] data = sendBufferQueue.Dequeue();
    //                //Console.WriteLine(Encoding.UTF8.GetString(data));
    //                sock.SendAsync(new ArraySegment<byte>(data), SocketFlags.None).ContinueWith(DataSent);
    //            }

    //            else
    //            {
    //                asyncSending = false;
    //            }
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

    //        Global.Log("TCPSocket", LogType.Error, ex.ToString());
    //    }
    //}

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
        // receiveBufferSegment = new ArraySegment<byte>(receiveBuffer);
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

                }
            }

            try
            {
                sendBufferQueue?.Clear();
                Receiver?.NetworkClose(this);
            }
            catch (Exception ex)
            {
                Global.Log(ex);
            }
        }
    }

    public void Send(byte[] message)
    {
        Send(message, 0, message.Length);
    }

    public void Send(byte[] message, int offset, int size)
    {
        if (state == SocketState.Closed)// || state == SocketState.Terminated)
            return;

        var msg = message.Clip((uint)offset, (uint)size);

        lock (sendLock)
        {

            if (state == SocketState.Closed)// || state == SocketState.Terminated)
                return;

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
                    sock.BeginSend(msg, 0, msg.Length, SocketFlags.None, sendCallback, this);
                }
                catch
                {
                    asyncSending = false;
                    //state = SocketState.Closed;//.Terminated;
                    Close();
                }
                //sock.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None).ContinueWith(DataSent);
            }
        }

    }

    private static void Flush(TCPSocket socket)
    {
        lock (socket.sendLock)
        {

            socket.currentReply?.Trigger(true);
            socket.currentReply = null;

            if (socket.state == SocketState.Closed) //|| socket.state == SocketState.Terminated)
                return;

            if (socket.sendBufferQueue.Count > 0)
            {
                var kv = socket.sendBufferQueue.Dequeue();

                try
                {
                    socket.currentReply = kv.Key;
                    socket.sock.BeginSend(kv.Value, 0, kv.Value.Length, SocketFlags.None,
                                                socket.sendCallback, socket);
                }
                catch (Exception ex)
                {
                    socket.asyncSending = false;

                    try
                    {
                        kv.Key?.Trigger(false);

                        if (socket.state != SocketState.Closed && !socket.sock.Connected)
                        {
                            // socket.state = SocketState.Closed;// Terminated;
                            socket.Close();
                        }
                    }
                    catch //(Exception ex2)
                    {
                        socket.Close();
                        //socket.state = SocketState.Closed;// .Terminated;
                    }

                    Global.Log("TCPSocket", LogType.Error, ex.ToString());
                }
            }
            else
            {
                socket.asyncSending = false;
            }
        }
    }

    private static void SendCallback(IAsyncResult ar)
    {

        try
        {
            var socket = (TCPSocket)ar.AsyncState;

            socket.sock?.EndSend(ar);
            Flush(socket);

        }
        catch (Exception ex)
        {
            Global.Log(ex);
        }
    }

    public bool Trigger(ResourceTrigger trigger)
    {
        return true;
    }

    public void Destroy()
    {
 
        Close();

        receiveNetworkBuffer = null;
        receiveCallback = null;
        sendCallback = null;
        sock = null;
        receiveBuffer = null;
        receiveNetworkBuffer = null;
        sendBufferQueue = null;

        //socketArgs.Completed -= SocketArgs_Completed;
        //socketArgs.Dispose();
        //socketArgs = null;
        OnDestroy?.Invoke(this);
        OnDestroy = null;

     }

    public ISocket Accept()
    {
        try
        {
            var s = sock.Accept();
            return new TCPSocket(s);
        }
        catch
        {
            state = SocketState.Closed;// Terminated;
            return null;
        }
    }

    public async AsyncReply<ISocket> AcceptAsync()
    {
        try
        {
            var s = await sock.AcceptAsync();
            return new TCPSocket(s);
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
            Flush(this);
            //SendCallback(null);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            held = false;
        }
    }

    public AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
    {

        if (state == SocketState.Closed)// || state == SocketState.Terminated)
            return new AsyncReply<bool>(false);

        var msg = message.Clip((uint)offset, (uint)length);

        lock (sendLock)
        {
            if (state == SocketState.Closed)// || state == SocketState.Terminated)
                return new AsyncReply<bool>(false);

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
                    currentReply = rt;
                    sock.BeginSend(msg, 0, msg.Length, SocketFlags.None, sendCallback, this);// null);
                }
                catch (Exception ex)
                {
                    rt.TriggerError(ex);
                    asyncSending = false;
                    //state = SocketState.Terminated;
                    Close();
                }
                //sock.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None).ContinueWith(DataSent);
            }

            return rt;
        }
    }
}
