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
using System.Net;
using System.Text;
using System.Threading;
using Esiur.Misc;
using Esiur.Core;
using Esiur.Net.Sockets;

namespace Esiur.Net;

/// <summary>
/// Base class for a logical connection layered on top of an <see cref="ISocket"/>.
/// It owns the socket, forwards inbound buffers to <see cref="DataReceived"/>, and
/// exposes send helpers. Derived classes implement the protocol-specific framing.
/// </summary>
public abstract class NetworkConnection : IDestructible, INetworkReceiver<ISocket>
{
    private volatile ISocket sock;
    private DateTime lastAction;

    private readonly object receiveLock = new object();
    private readonly Queue<PendingReceive> pendingReceives = new Queue<PendingReceive>();
    private bool receiving;
    private long socketGeneration;

    private readonly struct PendingReceive
    {
        public PendingReceive(ISocket sender, NetworkBuffer buffer, long generation)
        {
            Sender = sender;
            Buffer = buffer;
            Generation = generation;
        }

        public ISocket Sender { get; }
        public NetworkBuffer Buffer { get; }
        public long Generation { get; }
    }

    public delegate void NetworkConnectionEvent(NetworkConnection connection);

    public event NetworkConnectionEvent OnConnect;
    public event NetworkConnectionEvent OnClose;
    public event DestroyedEvent OnDestroy;

    public virtual void Destroy()
    {
        sock?.Destroy();
        Close();
        sock = null;

        OnClose = null;
        OnConnect = null;
        OnDestroy?.Invoke(this);
        OnDestroy = null;
    }

    public ISocket Socket => sock;

    public virtual void Assign(ISocket socket)
    {
        lock (receiveLock)
        {
            lastAction = DateTime.Now;

            if (!ReferenceEquals(sock, socket))
            {
                socketGeneration++;
                pendingReceives.Clear();
            }

            sock = socket;
            sock.Receiver = this;
        }
    }

    /// <summary>
    /// Detaches the socket from this connection without closing it and returns it,
    /// so ownership can be handed to another connection (e.g. a protocol upgrade).
    /// </summary>
    public ISocket Unassign()
    {
        lock (receiveLock)
        {
            if (sock == null)
                return null;

            sock.Receiver = null;

            var detached = sock;
            sock = null;
            socketGeneration++;
            pendingReceives.Clear();
            return detached;
        }
    }

    public void Close()
    {
        try
        {
            sock?.Close();
        }
        catch (Exception ex)
        {
            Global.Log("NetworkConnection:Close", LogType.Error, ex.ToString());
        }
    }

    public DateTime LastAction => lastAction;

    public IPEndPoint RemoteEndPoint => sock != null ? (IPEndPoint)sock.RemoteEndPoint : null;

    public IPEndPoint LocalEndPoint => sock != null ? (IPEndPoint)sock.LocalEndPoint : null;

    public bool IsConnected => sock != null && sock.State == SocketState.Established;

    public virtual AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
    {
        var socket = sock;
        if (socket == null)
            return new AsyncReply<bool>(false);

        try
        {
            lastAction = DateTime.Now;
            return socket.SendAsync(message, offset, length);
        }
        catch (Exception ex)
        {
            // Sends fail routinely when the peer drops, so this is logged at debug level
            // rather than thrown, but it is no longer swallowed silently.
            Global.Log("NetworkConnection:SendAsync", LogType.Debug, ex.Message);
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
        catch (Exception ex)
        {
            Global.Log("NetworkConnection:Send", LogType.Debug, ex.Message);
        }
    }

    public virtual void Send(byte[] msg, int offset, int length)
    {
        try
        {
            sock?.Send(msg, offset, length);
            lastAction = DateTime.Now;
        }
        catch (Exception ex)
        {
            Global.Log("NetworkConnection:Send", LogType.Debug, ex.Message);
        }
    }

    public virtual void Send(string data)
    {
        Send(Encoding.UTF8.GetBytes(data));
    }

    public void NetworkClose(ISocket socket)
    {
        lock (receiveLock)
        {
            if (!ReferenceEquals(socket, sock))
                return;

            pendingReceives.Clear();
        }

        Disconnected();
        OnClose?.Invoke(this);
    }

    public void NetworkConnect(ISocket socket)
    {
        lock (receiveLock)
            if (!ReferenceEquals(socket, sock))
                return;

        Connected();
        OnConnect?.Invoke(this);
    }

    protected abstract void DataReceived(NetworkBuffer buffer);
    protected abstract void Connected();
    protected abstract void Disconnected();

    public void NetworkReceive(ISocket sender, NetworkBuffer buffer)
    {
        try
        {
            bool drain;
            lock (receiveLock)
            {
                // A callback can outlive Unassign/Assign. Only the socket that currently
                // owns this connection may enqueue work for its protocol parser.
                if (!ReferenceEquals(sender, sock) || sender.State == SocketState.Closed)
                    return;

                lastAction = DateTime.Now;
                pendingReceives.Enqueue(new PendingReceive(sender, buffer, socketGeneration));

                if (receiving)
                    return;

                receiving = true;
                drain = true;
            }

            if (drain)
                DrainReceiveQueue();
        }
        catch (Exception ex)
        {
            Global.Log("NetworkConnection:NetworkReceive", LogType.Warning, ex.ToString());
        }
    }

    private void DrainReceiveQueue()
    {
        while (true)
        {
            PendingReceive pending = default;
            var found = false;

            lock (receiveLock)
            {
                while (pendingReceives.Count > 0)
                {
                    var candidate = pendingReceives.Dequeue();
                    if (ReferenceEquals(candidate.Sender, sock)
                        && candidate.Generation == socketGeneration)
                    {
                        pending = candidate;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    receiving = false;
                    return;
                }
            }

            try
            {
                while (IsCurrentReceive(pending)
                    && pending.Buffer.Available > 0
                    && !pending.Buffer.Protected)
                {
                    DataReceived(pending.Buffer);
                }
            }
            catch (Exception ex)
            {
                // Keep the queue usable after a protocol parser fails. Any work already
                // queued for a replacement socket can still be drained safely.
                Global.Log("NetworkConnection:NetworkReceive", LogType.Warning, ex.ToString());
            }
        }
    }

    private bool IsCurrentReceive(PendingReceive pending)
    {
        lock (receiveLock)
        {
            if (!ReferenceEquals(pending.Sender, sock)
                || pending.Generation != socketGeneration)
                return false;

            try
            {
                return pending.Sender.State != SocketState.Closed;
            }
            catch
            {
                return false;
            }
        }
    }
}
