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
using Esiur.Net.Sockets;
using Esiur.Misc;
using System.Threading;
using Esiur.Data;
using Esiur.Core;
using System.Net;
using Esiur.Resource;
using Esiur.Security.Membership;
using System.Threading.Tasks;
using Esiur.Data.Types;
using Esiur.Net;
using Esiur.Security.Authority;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Esiur.Protocol;

public class EpServer : NetworkServer<EpConnection>, IResource
{
    sealed class PeerAttemptWindow
    {
        public DateTime StartedUtc;
        public int Count;
    }

    readonly object _peerConnectionsLock = new object();
    readonly Dictionary<IPAddress, int> _peerConnectionCounts = new Dictionary<IPAddress, int>();
    readonly Dictionary<EpConnection, IPAddress?> _admittedConnections =
        new Dictionary<EpConnection, IPAddress?>();
    readonly Dictionary<IPAddress, PeerAttemptWindow> _peerAttemptWindows =
        new Dictionary<IPAddress, PeerAttemptWindow>();
    readonly PeerAttemptWindow _globalAttemptWindow = new PeerAttemptWindow();
    uint _attemptSweepSequence;


    //[Attribute]
    public string IP
    {
        get;
        set;
    }

    /// <summary>
    /// Authentication provider protocol names that incoming connections may negotiate.
    /// Providers must also be registered with the server Warehouse. An empty list denies
    /// authenticated negotiation; anonymous access remains controlled separately by
    /// <see cref="AllowUnauthorizedAccess"/>.
    /// </summary>
    public string[] AllowedAuthenticationProviders { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Encryption provider protocol names that incoming connections may negotiate.
    /// Providers must also be registered with the server Warehouse.
    /// </summary>
    public string[] AllowedEncryptionProviders { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Rejects incoming sessions that do not request authenticated encryption.
    /// </summary>
    public bool RequireEncryption { get; set; }

    /// <summary>
    /// Maximum time an incoming connection may spend authenticating, negotiating
    /// encryption, and completing required protected key rotation.
    /// </summary>
    public TimeSpan AuthenticationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    //[Attribute]
    public bool AllowUnauthorizedAccess { get; set; }

    //IMembership membership;

    //[Attribute]
    //public IMembership Membership
    //{
    //    get => membership;
    //    set
    //    {
    //        //if (membership != null)
    //        //    membership.Authorization -= Membership_Authorization;

    //        membership = value;

    //        //if (membership != null)
    //        //    membership.Authorization += Membership_Authorization;
    //    }
    //}

    //[Attribute]
    //public string MembershipProvider { get; set; }

    //private void Membership_Authorization(AuthorizationIndication indication)
    //{
    //    lock (Connections.SyncRoot)
    //        foreach (var connection in Connections)
    //            if (connection.Session == indication.Session)
    //                connection.ProcessAuthorization(indication.Results);
    //}

    //[Attribute]
    public EntryPoint EntryPoint
    {
        get;
        set;
    }

    //[Attribute]
    public ushort Port
    {
        get;
        set;
    } = 10518;

    /// <summary>
    /// Controls whether warehouse initialization opens Esiur's native TCP listener.
    /// Disable this when an external host, such as ASP.NET Core, supplies connections.
    /// </summary>
    public bool EnableTcpListener { get; set; } = true;


    //[Attribute]
    public ExceptionLevel ExceptionLevel { get; set; }
        = ExceptionLevel.Code
        | ExceptionLevel.Source
        | ExceptionLevel.Message
        | ExceptionLevel.Trace;

    
    public Instance Instance
    {
        get;
        set;
    }


    public AsyncReply<bool> Handle(ResourceOperation operation, IResourceContext context = null)

    {
        if (operation == ResourceOperation.Initialize)
        {
            if (!EnableTcpListener)
                return new AsyncReply<bool>(true);

            TcpSocket listener;

            if (IP != null)
                listener = new TcpSocket(new IPEndPoint(IPAddress.Parse(IP), Port));
            else
                listener = new TcpSocket(new IPEndPoint(IPAddress.Any, Port));

            Start(listener);
        }
        else if (operation == ResourceOperation.Terminate)
        {
            Stop();
        }
        else if (operation == ResourceOperation.SystemReloading)
        {
            Handle(ResourceOperation.Terminate);
            Handle(ResourceOperation.Initialize);
        }

        return new AsyncReply<bool>(true);
    }



    protected override void ClientConnected(EpConnection connection)
    {
        //Task.Delay(10000).ContinueWith((x) =>
        //{
        //    Console.WriteLine("By bye");
        //    // Remove me from here
        //    connection.Close();
        //    one = true;
        //});

    }

    public override void Add(EpConnection connection)
        => TryAdd(connection);

    /// <summary>
    /// Applies admission controls, configures, and tracks an accepted EP connection.
    /// The connection must already have its socket assigned so peer-based policies can
    /// inspect the actual remote endpoint.
    /// </summary>
    /// <returns><see langword="true"/> when the connection was admitted.</returns>
    public bool TryAdd(EpConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        if (connection.Socket == null)
            throw new InvalidOperationException(
                "Assign a socket before adding an EP connection to the server.");

        if (!TryAdmitConnection(connection, out var rejectionReason))
        {
            Global.Log(
                "EpServer:ConnectionLimit",
                LogType.Debug,
                $"Rejected connection from {connection.RemoteEndPoint?.Address}: {rejectionReason}");
            connection.Close();
            return false;
        }

        try
        {
            connection.Handle(ResourceOperation.Configure,
                            new EpServerConnectionContext()
                            {
                                Server = this,
                                Warehouse = Instance.Warehouse
                            });

            connection.ExceptionLevel = ExceptionLevel;
            connection.AuthenticationTimeout = AuthenticationTimeout;
            connection.RestartAuthenticationDeadline();
            base.Add(connection);
            return true;
        }
        catch
        {
            ReleaseConnection(connection);
            connection.Close();
            throw;
        }
    }

    public override void Remove(EpConnection connection)
    {
        try
        {
            base.Remove(connection);
        }
        finally
        {
            ReleaseConnection(connection);
        }
    }

    private bool TryAdmitConnection(EpConnection connection, out string rejectionReason)
    {
        rejectionReason = null;
        var address = NormalizeAddress(connection.RemoteEndPoint?.Address);

        lock (_peerConnectionsLock)
        {
            var configuration = Instance.Warehouse.Configuration.Connections;
            var now = DateTime.UtcNow;

            if (_admittedConnections.ContainsKey(connection))
            {
                rejectionReason = "connection is already admitted";
                return false;
            }

            if (configuration.MaximumConnectionAttempts > 0
                && configuration.ConnectionAttemptWindow > TimeSpan.Zero)
            {
                if (now - _globalAttemptWindow.StartedUtc
                    >= configuration.ConnectionAttemptWindow)
                {
                    _globalAttemptWindow.StartedUtc = now;
                    _globalAttemptWindow.Count = 0;
                }

                if (_globalAttemptWindow.Count >= configuration.MaximumConnectionAttempts)
                {
                    rejectionReason = "global connection-attempt rate reached";
                    return false;
                }

                _globalAttemptWindow.Count++;
            }

            var globalLimit = configuration.MaximumConnections;
            if (globalLimit > 0 && _admittedConnections.Count >= globalLimit)
            {
                rejectionReason = "global concurrent-connection limit reached";
                return false;
            }

            if (address == null)
            {
                _admittedConnections[connection] = null;
                return true;
            }

            if (++_attemptSweepSequence % 256 == 0)
            {
                foreach (var expired in _peerAttemptWindows
                    .Where(entry => now - entry.Value.StartedUtc
                        >= configuration.ConnectionAttemptWindow)
                    .Select(entry => entry.Key)
                    .ToArray())
                    _peerAttemptWindows.Remove(expired);
            }

            if (configuration.MaximumConnectionAttemptsPerIpAddress > 0
                && configuration.ConnectionAttemptWindow > TimeSpan.Zero)
            {
                if (!_peerAttemptWindows.TryGetValue(address, out var attempts)
                    || now - attempts.StartedUtc >= configuration.ConnectionAttemptWindow)
                {
                    attempts = new PeerAttemptWindow
                    {
                        StartedUtc = now,
                    };
                    _peerAttemptWindows[address] = attempts;
                }

                if (attempts.Count
                    >= configuration.MaximumConnectionAttemptsPerIpAddress)
                {
                    rejectionReason = "per-IP connection-attempt rate reached";
                    return false;
                }

                attempts.Count++;
            }

            var count = _peerConnectionCounts.TryGetValue(address, out var current)
                ? current
                : 0;
            var limit = configuration.MaximumConnectionsPerIpAddress;

            if (limit > 0 && count >= limit)
            {
                rejectionReason = "per-IP concurrent-connection limit reached";
                return false;
            }

            _peerConnectionCounts[address] = count + 1;
            _admittedConnections[connection] = address;
            return true;
        }
    }

    private void ReleaseConnection(EpConnection connection)
    {
        lock (_peerConnectionsLock)
        {
            if (!_admittedConnections.TryGetValue(connection, out var address))
                return;

            _admittedConnections.Remove(connection);

            if (address == null)
                return;

            if (!_peerConnectionCounts.TryGetValue(address, out var count))
                return;

            if (count <= 1)
                _peerConnectionCounts.Remove(address);
            else
                _peerConnectionCounts[address] = count - 1;
        }
    }

    private static IPAddress NormalizeAddress(IPAddress address)
        => address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address;

    internal int GetConnectionCount(IPAddress address)
    {
        address = NormalizeAddress(address);
        lock (_peerConnectionsLock)
            return address != null && _peerConnectionCounts.TryGetValue(address, out var count)
                ? count
                : 0;
    }

    protected override void ClientDisconnected(EpConnection connection)
    {
        //connection.OnReady -= ConnectionReadyEventReceiver;
        //Warehouse.Remove(connection);
    }

    public KeyList<string, CallInfo?> Calls { get; } = new KeyList<string, CallInfo?>();

    public struct CallInfo
    {
        public FunctionDef Definition;
        public Delegate Delegate;
    }

    public EpServer MapCall(string call, Delegate handler)
    {
        var fd = FunctionDef.MakeFunctionDef(Instance.Warehouse, null, handler.Method, 0, call, null);
        Calls.Add(call, new CallInfo() { Delegate = handler, Definition = fd });
        return this;
    }

}
