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
using Esyur.Misc;
using Esyur.Core;
using Esyur.Data;
using Esyur.Net.Sockets;
using Esyur.Resource;

namespace Esyur.Net
{
    public abstract class NetworkConnection: IDestructible, INetworkReceiver<ISocket>// <TS>: IResource where TS : NetworkSession
    {
        private Sockets.ISocket sock;
        //        private bool connected;

        private DateTime lastAction;

        //public delegate void DataReceivedEvent(NetworkConnection sender, NetworkBuffer data);
        //public delegate void ConnectionClosedEvent(NetworkConnection sender);
        public delegate void NetworkConnectionEvent(NetworkConnection connection);

        public event NetworkConnectionEvent OnConnect;
        //public event DataReceivedEvent OnDataReceived;
        public event NetworkConnectionEvent OnClose;

            
        public event DestroyedEvent OnDestroy;
        //object receivingLock = new object();

        //object sendLock = new object();

        bool processing = false;

       // public INetworkReceiver<NetworkConnection> Receiver { get; set; }

        public virtual void Destroy()
        {
            // remove references
            //sock.OnClose -= Socket_OnClose;
            //sock.OnConnect -= Socket_OnConnect;
            //sock.OnReceive -= Socket_OnReceive;
            sock.Destroy();
            //Receiver = null;
            Close();
            sock = null;

            OnClose = null;
            OnConnect = null;
            //OnDataReceived = null;
            OnDestroy?.Invoke(this);
            OnDestroy = null;
        }

 



        public Sockets.ISocket Socket
        {
            get
            {
                return sock;
            }
        }

        public virtual void Assign(Sockets.ISocket socket)
        {
            lastAction = DateTime.Now;
            sock = socket;
            sock.Receiver = this;

            //socket.OnReceive += Socket_OnReceive;
            //socket.OnClose += Socket_OnClose;
            //socket.OnConnect += Socket_OnConnect;
        }

        //private void Socket_OnConnect()
        //{
        //    OnConnect?.Invoke(this);
        //}

        //private void Socket_OnClose()
        //{
        //    ConnectionClosed();
        //    OnClose?.Invoke(this);
        //}

        //protected virtual void ConnectionClosed()
        //{

        //}

        //private void Socket_OnReceive(NetworkBuffer buffer)
        //{
        //}

        public Sockets.ISocket Unassign()
        {
            if (sock != null)
            {
                // connected = false;
                //sock.OnClose -= Socket_OnClose;
                //sock.OnConnect -= Socket_OnConnect;
                //sock.OnReceive -= Socket_OnReceive;
                sock.Receiver = null;

                var rt = sock;
                sock = null;

                return rt;
            }
            else
                return null;
        }

        //protected virtual void DataReceived(NetworkBuffer data)
        //{
        //    if (OnDataReceived != null)
        //    {
        //        try
        //        {
        //            OnDataReceived?.Invoke(this, data);
        //        }
        //        catch (Exception ex)
        //        {
        //            Global.Log("NetworkConenction:DataReceived", LogType.Error, ex.ToString());
        //        }
        //    }
        //}

        public void Close()
        {
            //if (!connected)
            //  return;


            try
            {
                if (sock != null)
                    sock.Close();
            }
            catch (Exception ex)
            {
                Global.Log("NetworkConenction:Close", LogType.Error, ex.ToString());

            }

            //finally
            //{
            //connected = false;
            //}

        }

        public DateTime LastAction
        {
            get { return lastAction; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get
            {
                if (sock != null)
                    return (IPEndPoint)sock.RemoteEndPoint;
                else
                    return null;
            }
        }

        public IPEndPoint LocalEndPoint
        {
            get
            {
                if (sock != null)
                    return (IPEndPoint)sock.LocalEndPoint;
                else
                    return null;
            }
        }


        public bool IsConnected
        {
            get
            {
                return sock.State == SocketState.Established;
            }
        }


        /*
        public void CloseAndWait()
        {
            try
            {
                if (!connected)
                    return;

                    if (sock != null)
                        sock.Close();

                    while (connected)
                    {
                        Thread.Sleep(100);
                    }
            }
            finally
            {
                
            }
        }
        */

        public virtual AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
        {
            try
            {
                lastAction = DateTime.Now;
                return sock.SendAsync(message, offset, length);
            }
            catch
            {
                return new AsyncReply<bool>(false);
            }
        }

        public virtual void Send(byte[] msg)
        {
            try
            {                
                sock?.Send(msg);       
                lastAction = DateTime.Now;
            }
            catch
            {

            }
        }

        public virtual void Send(byte[] msg, int offset, int length)
        {
            try
            {
                sock.Send(msg, offset, length);
                lastAction = DateTime.Now;
            }
            catch
            {

            }
        }

        public virtual void Send(string data)
        {
            Send(Encoding.UTF8.GetBytes(data));
        }

        public  void NetworkClose(ISocket socket)
        {
            Disconencted();
            OnClose?.Invoke(this);
        }

        public void NetworkConnect(ISocket socket)
        {
            Connected();
            OnConnect?.Invoke(this);
        }

        //{
        //ConnectionClosed();
        //OnClose?.Invoke(this);

        //Receiver?.NetworkClose(this);
        //}

        //public void NetworkConenct(ISocket sender)
        //{
        //    OnConnect?.Invoke(this);
        //}

        protected abstract void DataReceived(NetworkBuffer buffer);
        protected abstract void Connected();
        protected abstract void Disconencted();

        public void NetworkReceive(ISocket sender, NetworkBuffer buffer)
        {
            try
            {
                // Unassigned ?
                if (sock == null)
                    return;

                // Closed ?
                if (sock.State == SocketState.Closed || sock.State == SocketState.Terminated) // || !connected)
                    return;

                lastAction = DateTime.Now;

                if (!processing)
                {
                    processing = true;

                    try
                    {
                        //lock(buffer.SyncLock)
                        while (buffer.Available > 0 && !buffer.Protected)
                        {
                            //Receiver?.NetworkReceive(this, buffer);
                            DataReceived(buffer);
                        }
                    }
                    catch
                    {

                    }

                    processing = false;
                }

            }
            catch (Exception ex)
            {
                Global.Log("NetworkConnection", LogType.Warning, ex.ToString());
            }
        }


        //{
          //  Receiver?.NetworkError(this);
            //throw new NotImplementedException();
        //}

        //public void NetworkConnect(ISocket sender)
        //{
          //  Receiver?.NetworkConnect(this);
            //throw new NotImplementedException();
        //}
    }
}