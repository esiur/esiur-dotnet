using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using Esiur.Misc;
using Esiur.Engine;
using Esiur.Data;
using Esiur.Net.Sockets;
using Esiur.Resource;

namespace Esiur.Net
{
    public class NetworkConnection: IDestructible// <TS>: IResource where TS : NetworkSession
    {
        private ISocket sock;
//        private bool connected;

        private DateTime lastAction;

        public delegate void DataReceivedEvent(NetworkConnection sender, NetworkBuffer data);
        public delegate void ConnectionClosedEvent(NetworkConnection sender);
        public delegate void ConnectionEstablishedEvent(NetworkConnection sender);

        public event ConnectionEstablishedEvent OnConnect;
        public event DataReceivedEvent OnDataReceived;
        public event ConnectionClosedEvent OnClose;
        public event DestroyedEvent OnDestroy;

        
        
        public void Destroy()
        {
           // if (connected)
            Close();
            OnDestroy?.Invoke(this);
        }

        public NetworkConnection()
        {

        }



        public ISocket Socket
        {
            get
            {
                return sock;
            }
        }

        public virtual void Assign(ISocket socket)
        {
            lastAction = DateTime.Now;
            sock = socket;
            //connected = true;
            socket.OnReceive += Socket_OnReceive;
            socket.OnClose += Socket_OnClose;
            socket.OnConnect += Socket_OnConnect;
            if (socket.State == SocketState.Established)
                socket.Begin();
        }

        private void Socket_OnConnect()
        {
            OnConnect?.Invoke(this);
        }

        private void Socket_OnClose()
        {
            OnClose?.Invoke(this);
        }

        private void Socket_OnReceive(NetworkBuffer buffer)
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

                while (buffer.Available > 0 && !buffer.Protected)
                    DataReceived(buffer);
                
            }
            catch (Exception ex)
            {
                Global.Log("NetworkConnection", LogType.Warning, ex.ToString());
            }
        }

        public ISocket Unassign()
        {
            if (sock != null)
            {
               // connected = false;
                sock.OnClose -= Socket_OnClose;
                sock.OnConnect -= Socket_OnConnect;
                sock.OnReceive -= Socket_OnReceive;

                var rt = sock;
                sock = null;

                return rt;
            }
            else
                return null;
        }

        protected virtual void DataReceived(NetworkBuffer data)
        {
            if (OnDataReceived != null)
            {
                try
                {
                    OnDataReceived?.Invoke(this, data);
                }
                catch (Exception ex)
                {
                    Global.Log("NetworkConenction:DataReceived", LogType.Error, ex.ToString());
                }
            }
        }
        
        public void Close()
        {
            //if (!connected)
              //  return;

            try
            {
                if (sock != null)
                    sock.Close();
            }
            catch(Exception ex)
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

        
        public bool Connected
        {
            get
            {
                return sock.State == SocketState.Established;// connected;
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

        public virtual void Send(byte[] msg)
        {
            //Console.WriteLine("TXX " + msg.Length);

            try
            {
                //if (!connected)
                //{
                    //Console.WriteLine("not connected");
                //    return;
                //}

                if (sock != null)
                {
                    lastAction = DateTime.Now;
                    sock.Send(msg);
                }
            }
            catch
            {

            }
        }

        public virtual void Send(string data)
        {
            Send(Encoding.UTF8.GetBytes(data));
        }
    }
}