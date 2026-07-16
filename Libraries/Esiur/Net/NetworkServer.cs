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
    private volatile Sockets.ISocket listener;
    private readonly object lifecycleLock = new object();
    public AutoList<TConnection, NetworkServer<TConnection>> Connections { get; internal set; }

    private Thread thread;

    private Timer timer;

    /// <summary>
    /// Maximum time allowed for an accepted socket to finish protocol initialization
    /// (for example, a TLS handshake). A zero or negative value disables the deadline.
    /// </summary>
    public TimeSpan ConnectionInitializationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public event DestroyedEvent OnDestroy;


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
        if (socket == null)
            throw new ArgumentNullException(nameof(socket));

        lock (lifecycleLock)
        {
            if (listener != null)
                return;

            Connections = new AutoList<TConnection, NetworkServer<TConnection>>(this);

            if (Timeout > 0 && Clock > 0)
            {
                timer = new Timer(MinuteThread, null, TimeSpan.FromMinutes(0), TimeSpan.FromSeconds(Clock));
            }

            listener = socket;

            // Bind this thread to this particular Start invocation. If the server is
            // stopped and restarted before the old Accept call unwinds, the old thread
            // must not begin accepting from the replacement listener.
            thread = new Thread(() => AcceptLoop(socket))
            {
                IsBackground = true
            };
            thread.Start();
        }
    }

    private void AcceptLoop(ISocket activeListener)
    {
        while (ReferenceEquals(listener, activeListener))
        {
            try
            {
                var acceptedSocket = activeListener.Accept();

                if (acceptedSocket == null)
                    return;

                TConnection connection = null;
                var stopped = false;

                // Admission and Stop's connection snapshot share this gate. Therefore
                // either the connection is added before Stop snapshots it, or Stop wins
                // and this accepted socket is closed without being exposed to the server.
                lock (lifecycleLock)
                {
                    if (!ReferenceEquals(listener, activeListener))
                    {
                        stopped = true;
                    }
                    else
                    {
                        connection = new TConnection();
                        connection.Assign(acceptedSocket);
                        Add(connection);
                        stopped = !ReferenceEquals(listener, activeListener);
                    }
                }

                if (stopped)
                {
                    try { acceptedSocket.Close(); } catch { }
                    return;
                }

                // A derived server can reject admission (for example, due to a per-peer
                // connection quota) by not adding the connection and closing its socket.
                if (!Connections.Contains(connection))
                    continue;

                try
                {
                    ClientConnected(connection);
                }
                catch
                {
                    // something wrong with the child.
                }

                if (!ReferenceEquals(listener, activeListener))
                {
                    try { connection.Close(); } catch { }
                    return;
                }

                // Some socket implementations perform a protocol handshake in Begin
                // (notably SSLSocket). Never run that handshake on the single accept
                // thread: a peer that stops mid-handshake would otherwise prevent all
                // subsequent clients from being accepted.
                _ = BeginAcceptedSocketAsync(acceptedSocket);
            }
            catch (Exception ex)
            {
                if (!ReferenceEquals(listener, activeListener))
                    return;

                Global.Log(ex);
            }
        }
    }

    private async Task BeginAcceptedSocketAsync(ISocket socket)
    {
        try
        {
            var beginTask = AwaitSocketBeginAsync(socket);
            var timeout = ConnectionInitializationTimeout;

            if (timeout > TimeSpan.Zero)
            {
                var completed = await Task.WhenAny(beginTask, Task.Delay(timeout)).ConfigureAwait(false);
                if (!ReferenceEquals(completed, beginTask))
                {
                    try { socket.Close(); } catch { }
                    _ = beginTask.ContinueWith(
                        completedTask => _ = completedTask.Exception,
                        TaskContinuationOptions.OnlyOnFaulted);
                    return;
                }
            }

            if (!await beginTask.ConfigureAwait(false))
                try { socket.Close(); } catch { }
        }
        catch (Exception ex)
        {
            Global.Log("NetworkServer", LogType.Warning,
                $"Accepted socket initialization failed: {ex.Message}");
            try { socket.Close(); } catch { }
        }
    }

    private static async Task<bool> AwaitSocketBeginAsync(ISocket socket)
        => await socket.BeginAsync();


    //[Attribute]
    public uint Timeout
    {
        get;
        set;
    }


    //[Attribute]
    public uint Clock
    {
        get;
        set;
    }


    public void Stop()
    {
        var port = 0;
        ISocket currentListener = null;
        TConnection[] connections = null;
        Timer currentTimer = null;

        try
        {
            lock (lifecycleLock)
            {
                currentListener = listener;
                listener = null;
                connections = Connections?.ToArray();
                currentTimer = timer;
                timer = null;
            }

            if (currentListener != null)
            {
                // Reading the endpoint can throw if the socket is already disposed (e.g. a second
                // Stop or the finalizer after Destroy), so it is best-effort and only used for logging.
                try { port = currentListener.LocalEndPoint.Port; } catch { }
                try { currentListener.Close(); } catch { }
            }

            if (connections != null)
            {
                foreach (TConnection con in connections)
                    try { con.Close(); } catch { }
            }
        }
        finally
        {
            try { currentTimer?.Dispose(); } catch { }
            Global.Log("NetworkServer", LogType.Warning, $"Server on port {port} is down.");
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
            var currentListener = listener;
            return currentListener != null && currentListener.State == SocketState.Listening;
        }
    }



    public void Destroy()
    {
        Stop();
        OnDestroy?.Invoke(this);
        GC.SuppressFinalize(this); // explicit teardown done; no need for the finalizer to run Stop again
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
        // Finalizers must never throw; Stop() is already guarded but wrap defensively.
        try { Stop(); } catch { }
        listener = null;
    }
}

