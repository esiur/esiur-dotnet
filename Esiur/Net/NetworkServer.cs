using System;
using System.Threading;
using System.Collections.Generic;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Engine;
using Esiur.Net.Sockets;
using Esiur.Resource;
using System.Threading.Tasks;

namespace Esiur.Net
{
    //public abstract class NetworkServer<TConnection, TSession> : IResource where TSession : NetworkSession, new() where TConnection : NetworkConnection<TSession>, new()

    public abstract class NetworkServer<TConnection>: IDestructible where TConnection : NetworkConnection, new()
    {
        //private bool isRunning;
        uint clock;
        private ISocket listener;
        private AutoList<TConnection, NetworkServer<TConnection>> connections;

        //private Thread thread;

        protected abstract void DataReceived(TConnection sender, NetworkBuffer data);
        protected abstract void ClientConnected(TConnection sender);
        protected abstract void ClientDisconnected(TConnection sender);

       // private int port;
       // private IPAddress ip = null;

        private uint timeout;
        private Timer timer;
        //public KeyList<string, TSession> Sessions = new KeyList<string, TSession>();

        public event DestroyedEvent OnDestroy;

        public AutoList<TConnection, NetworkServer<TConnection>> Connections
        {
            get
            {
                return connections;
            }
        }

        /*
        public void RemoveSession(string ID)
        {
            Sessions.Remove(ID);
        }

        public void RemoveSession(TSession Session)
        {
            if (Session != null)
                Sessions.Remove(Session.Id);
        }
        */

        /*
        public TSession CreateSession(string ID, int Timeout)
        {
            TSession s = new TSession();

            s.SetSession(ID, Timeout, new NetworkSession.SessionModifiedEvent(SessionModified)
                            , new NetworkSession.SessionEndedEvent(SessionEnded));


            Sessions.Add(ID, s);
            return s;
        }
        */

        /*
        private void pSessionModified(TSession session, string key, object oldValue, object newValue)
        {
            SessionModified((TSession)session, key, oldValue, newValue);
        }

        private void pSessionEnded(NetworkSession session)
        {
            SessionEnded((TSession)session);
        }
        */

            /*
        protected virtual void SessionModified(NetworkSession session, string key, object oldValue, object newValue)
        {

        }

        protected virtual void SessionEnded(NetworkSession session)
        {
            Sessions.Remove(session.Id);
            session.Destroy();
        }
        */

        private void MinuteThread(object state)
        {
            List<TConnection> ToBeClosed = null;


            lock (connections.SyncRoot)
            {
                foreach (TConnection c in connections)
                {
                    if (DateTime.Now.Subtract(c.LastAction).TotalSeconds >= timeout)
                    {
                        if (ToBeClosed == null)
                            ToBeClosed = new List<TConnection>();
                        ToBeClosed.Add(c);
                    }
                }
            }

            //Console.WriteLine("UnLock MinuteThread");

            if (ToBeClosed != null)
            {
                //Console.WriteLine("Term: " + ToBeClosed.Count + " " + this.listener.LocalEndPoint.ToString());
                foreach (TConnection c in ToBeClosed)
                    c.Close();// CloseAndWait();

                ToBeClosed.Clear();
                ToBeClosed = null;
            }
        }

        public void Start(ISocket socket, uint timeout, uint clock)
        {
            if (listener != null)
                return;

            //if (socket.State == SocketState.Listening)
            //  return;

            //if (isRunning)
            //  return;

            connections = new AutoList<TConnection, NetworkServer<TConnection>>(this);


            if (timeout > 0 & clock > 0)
            {
                timer = new Timer(MinuteThread, null, TimeSpan.FromMinutes(0), TimeSpan.FromSeconds(clock));
                this.timeout = timeout;
            }

            //this.ip = ip;
            //this.port = port;
            this.clock = clock;


            // start a new thread for the server to live on
            //isRunning = true;



            listener = socket;

            // Start accepting
            listener.Accept().Then(NewConnection);

            //var rt = listener.Accept().Then()
            //thread = new Thread(new System.Threading.ThreadStart(ListenForConnections));

            //thread.Start();

        }

        /*
        public int LocalPort
        {
            get
            {
                return port;
            }
        }
        */


        public uint Clock
        {
            get { return clock; }
        }

        public void Stop()
        {
            var port = 0;

            try
            {
                if (listener != null)
                {
                    port = listener.LocalEndPoint.Port;
                    listener.Close();
                }

                // wait until the listener stops
                //while (isRunning)
                //{
                  //  Thread.Sleep(100);
                //}

                Console.WriteLine("Listener stopped");

                var cons = connections.ToArray();

                //lock (connections.SyncRoot)
                //{
                    foreach (TConnection con in cons)
                        con.Close();
                //}

                Console.WriteLine("Sockets Closed");

                while (connections.Count > 0)
                {
                    Console.WriteLine("Waiting... " + connections.Count);
                    
                    //Thread.Sleep(1000);
                }

            }
            finally
            {
                Console.WriteLine("Server@{0} is down", port);
            }
        }



        private void NewConnection(ISocket sock)
        {

            try
            {

                /*
                    if (listener.State == SocketState.Closed || listener.State == SocketState.Terminated)
                    {
                           Console.WriteLine("Listen socket break ");
                           Console.WriteLine(listener.LocalEndPoint.Port);
                           break;
                    }
                    */

                    if (sock == null)
                    {
                        Console.Write("sock == null");
                        return;
                    }

                    //sock.ReceiveBufferSize = 102400;
                    //sock.SendBufferSize = 102400;

                    TConnection c = new TConnection();
                    c.OnDataReceived += OnDataReceived;
                    c.OnConnect += OnClientConnect;
                    c.OnClose += OnClientClose;


                    connections.Add(c);
                    c.Assign(sock);

                    ClientConnected(c);

                    // Accept more
                    listener.Accept().Then(NewConnection);



            }
            catch (Exception ex)
            {
                Console.WriteLine("TSERVER " + ex.ToString());
                Global.Log("TServer", LogType.Error, ex.ToString());
            }

            //isRunning = false;


        }

        public bool IsRunning
        {
            get
            {
                return listener.State == SocketState.Listening;
                //isRunning; 
            }
        }
 
        public void OnDataReceived(NetworkConnection sender, NetworkBuffer data)
        {
            DataReceived((TConnection)sender, data);
        }

        public void OnClientConnect(NetworkConnection sender)
        {
            if (sender == null)
                return;

            if (sender.RemoteEndPoint == null || sender.LocalEndPoint == null)
            { }
            //Console.WriteLine("NULL");
            else
                Global.Log("Connections", LogType.Debug, sender.RemoteEndPoint.Address.ToString()
                    + "->" + sender.LocalEndPoint.Port + " at " + DateTime.UtcNow.ToString("d")
                    + " " + DateTime.UtcNow.ToString("d"), false);

            // Console.WriteLine("Connected " + sender.RemoteEndPoint.ToString());
            ClientConnected((TConnection)sender);
        }

        public void OnClientClose(NetworkConnection sender)
        {
            try
            {
                sender.Destroy();
                ClientDisconnected((TConnection)sender);
            }
            catch (Exception ex)
            {
                Global.Log("NetworkServer:OnClientDisconnect", LogType.Error, ex.ToString());
            }

            sender = null;
            GC.Collect();
        }

        
        public void Destroy()
        {
            Stop();
            OnDestroy?.Invoke(this);
        }

        ~NetworkServer()
        {
            Stop();
            //Connections = null;
            listener = null;
        }
    }

}