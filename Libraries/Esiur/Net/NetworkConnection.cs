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
    private ISocket sock;
    private DateTime lastAction;

    // Re-entrancy guard for NetworkReceive. 0 = idle, 1 = a thread is draining the buffer.
    // Interlocked is used instead of a plain bool so concurrent receive callbacks cannot
    // both enter the drain loop (which is not safe to run from two threads at once).
    private int receiving;

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
        lastAction = DateTime.Now;
        sock = socket;
        sock.Receiver = this;
    }

    /// <summary>
    /// Detaches the socket from this connection without closing it and returns it,
    /// so ownership can be handed to another connection (e.g. a protocol upgrade).
    /// </summary>
    public ISocket Unassign()
    {
        if (sock == null)
            return null;

        sock.Receiver = null;

        var detached = sock;
        sock = null;
        return detached;
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
        Disconnected();
        OnClose?.Invoke(this);
    }

    public void NetworkConnect(ISocket socket)
    {
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
            // Ignore callbacks once the socket is unassigned or closed.
            if (sock == null || sock.State == SocketState.Closed)
                return;

            lastAction = DateTime.Now;

            // Only one thread drains the buffer at a time; others return immediately and
            // rely on the active drainer to pick up the newly appended data.
            if (Interlocked.CompareExchange(ref receiving, 1, 0) != 0)
                return;

            try
            {
                while (buffer.Available > 0 && !buffer.Protected)
                    DataReceived(buffer);
            }
            finally
            {
                Interlocked.Exchange(ref receiving, 0);
            }
        }
        catch (Exception ex)
        {
            Global.Log("NetworkConnection:NetworkReceive", LogType.Warning, ex.ToString());
        }
    }
}
