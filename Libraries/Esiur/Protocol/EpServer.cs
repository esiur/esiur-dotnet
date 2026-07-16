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
    readonly object _peerConnectionsLock = new object();
    readonly Dictionary<IPAddress, int> _peerConnectionCounts = new Dictionary<IPAddress, int>();
    readonly Dictionary<EpConnection, IPAddress> _admittedConnections = new Dictionary<EpConnection, IPAddress>();


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
    {
        if (!TryAdmitConnection(connection))
        {
            Global.Log(
                "EpServer:ConnectionLimit",
                LogType.Warning,
                $"Rejected connection from {connection.RemoteEndPoint?.Address}: per-IP limit reached.");
            connection.Close();
            return;
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
            base.Add(connection);
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

    private bool TryAdmitConnection(EpConnection connection)
    {
        var address = NormalizeAddress(connection.RemoteEndPoint?.Address);
        if (address == null)
            return true;

        lock (_peerConnectionsLock)
        {
            var count = _peerConnectionCounts.TryGetValue(address, out var current)
                ? current
                : 0;
            var limit = Instance.Warehouse.Configuration.Connections.MaximumConnectionsPerIpAddress;

            if (limit > 0 && count >= limit)
                return false;

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
