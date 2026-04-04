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
using System.Threading;
using System.Collections.Generic;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Core;
using Esiur.Net.Sockets;
using Esiur.Resource;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Esiur.Net;

public abstract class NetworkServer<TConnection> : IDestructible where TConnection : NetworkConnection, new()
{
    //private bool isRunning;
    private Sockets.ISocket listener;
    public AutoList<TConnection, NetworkServer<TConnection>> Connections { get; internal set; }

    private Thread thread;

    //protected abstract void DataReceived(TConnection sender, NetworkBuffer data);
    //protected abstract void ClientConnected(TConnection sender);
    //protected abstract void ClientDisconnected(TConnection sender);


    private Timer timer;
    //public KeyList<string, TSession> Sessions = new KeyList<string, TSession>();

    public event DestroyedEvent OnDestroy;

    //public AutoList<TConnection, NetworkServer<TConnection>> Connections => connections;

    private void MinuteThread(object state)
    {
        List<TConnection> ToBeClosed = null;


        lock (Connections.SyncRoot)
        {
            foreach (TConnection c in Connections)
            {
                if (DateTime.Now.Subtract(c.LastAction).TotalSeconds >= Timeout)
                {
                    if (ToBeClosed == null)
                        ToBeClosed = new List<TConnection>();
                    ToBeClosed.Add(c);
                }
            }
        }

        if (ToBeClosed != null)
        {
            //Console.WriteLine("Term: " + ToBeClosed.Count + " " + this.listener.LocalEndPoint.ToString());
            foreach (TConnection c in ToBeClosed)
                c.Close();// CloseAndWait();

            ToBeClosed.Clear();
            ToBeClosed = null;
        }
    }

    public void Start(Sockets.ISocket socket)//, uint timeout, uint clock)
    {
        if (listener != null)
            return;


        Connections = new AutoList<TConnection, NetworkServer<TConnection>>(this);


        if (Timeout > 0 & Clock > 0)
        {
            timer = new Timer(MinuteThread, null, TimeSpan.FromMinutes(0), TimeSpan.FromSeconds(Clock));
        }


        listener = socket;

        // Start accepting
        //var r = listener.Accept();
        //r.Then(NewConnection);
        //r.timeout?.Dispose();

        //var rt = listener.Accept().Then()
        thread = new Thread(new ThreadStart(() =>
        {
            while (true)
            {
                try
                {
                    var s = listener.Accept();

                    if (s == null)
                    {
                        Global.Log("NetworkServer", LogType.Error, "sock == null");
                        return;
                    }

                    //Console.WriteLine("New Socket ... " + DateTime.Now);

                    var c = new TConnection();

                    c.Assign(s);
                    Add(c);

                    try
                    {
                        ClientConnected(c);
                    }
                    catch
                    {
                        // something wrong with the child.
                    }

                    s.Begin();

                    // Accept more
                    //listener.Accept().Then(NewConnection);

                }
                catch (Exception ex)
                {
                    Global.Log(ex);
                }
            }
        }));

        thread.Start();

    }


    [Attribute]
    public uint Timeout
    {
        get;
        set;
    }


    [Attribute]
    public uint Clock
    {
        get;
        set;
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

            //Console.WriteLine("Listener stopped");

            var cons = Connections.ToArray();

            //lock (connections.SyncRoot)
            //{
            foreach (TConnection con in cons)
                con.Close();
            //}

            //Console.WriteLine("Sockets Closed");

            //while (connections.Count > 0)
            //{
            //    Console.WriteLine("Waiting... " + connections.Count);  
            //    Thread.Sleep(1000);
            //}

        }
        finally
        {
            Global.Log("NetworkServer", LogType.Warning, $"Server@{port} is down.");
        }
    }


    public virtual void Remove(TConnection connection)
    {
        connection.OnClose -= ClientDisconnectedEventReceiver;
        Connections.Remove(connection);
    }

    public virtual void Add(TConnection connection)
    {
        connection.OnClose += ClientDisconnectedEventReceiver;
        Connections.Add(connection);
    }


    public bool IsRunning
    {
        get
        {
            return listener.State == SocketState.Listening;
            //isRunning; 
        }
    }

    //public void OnDataReceived(ISocket sender, NetworkBuffer data)
    //{
    //    DataReceived((TConnection)sender, data);
    //}

    //public void OnClientConnect(ISocket sender)
    //{
    //    if (sender == null)
    //        return;

    //    if (sender.RemoteEndPoint == null || sender.LocalEndPoint == null)
    //    { }
    //    //Console.WriteLine("NULL");
    //    else
    //        Global.Log("Connections", LogType.Debug, sender.RemoteEndPoint.Address.ToString()
    //            + "->" + sender.LocalEndPoint.Port + " at " + DateTime.UtcNow.ToString("d")
    //            + " " + DateTime.UtcNow.ToString("d"), false);

    //    // Console.WriteLine("Connected " + sender.RemoteEndPoint.ToString());
    //    ClientConnected((TConnection)sender);
    //}

    //public void OnClientClose(ISocket sender)
    //{
    //}


    public void Destroy()
    {
        Stop();
        OnDestroy?.Invoke(this);
    }

    private void ClientDisconnectedEventReceiver(NetworkConnection connection)
    {
        try
        {
            var con = connection as TConnection;
            con.Destroy();
            Remove(con);
            ClientDisconnected(con);
        }
        catch (Exception ex)
        {
            Global.Log(ex);
        }
    }

    protected abstract void ClientDisconnected(TConnection connection);
    protected abstract void ClientConnected(TConnection connection);

    ~NetworkServer()
    {
        Stop();
        listener = null;
    }
}

