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
using Esiur.Resource.Template;

namespace Esiur.Net.IIP;
public class DistributedServer : NetworkServer<DistributedConnection>, IResource
{


    [Attribute]
    public string IP
    {
        get;
        set;
    }

    IMembership membership;

    [Attribute]
    public IMembership Membership
    {
        get => membership;
        set
        {
            if (membership != null)
                membership.Authorization -= Membership_Authorization;

            membership = value;

            if (membership != null)
                membership.Authorization += Membership_Authorization;
        }
    }

    private void Membership_Authorization(AuthorizationIndication indication)
    {
        lock (Connections.SyncRoot)
            foreach (var connection in Connections)
                if (connection.Session == indication.Session)
                    connection.ProcessAuthorization(indication.Results);
    }

    [Attribute]
    public EntryPoint EntryPoint
    {
        get;
        set;
    }

    [Attribute]
    public ushort Port
    {
        get;
        set;
    } = 10518;


    [Attribute]
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


    public AsyncReply<bool> Trigger(ResourceTrigger trigger)
    {
        if (trigger == ResourceTrigger.Initialize)
        {
            TCPSocket listener;

            if (IP != null)
                listener = new TCPSocket(new IPEndPoint(IPAddress.Parse(IP), Port));
            else
                listener = new TCPSocket(new IPEndPoint(IPAddress.Any, Port));

            Start(listener);
        }
        else if (trigger == ResourceTrigger.Terminate)
        {
            Stop();
        }
        else if (trigger == ResourceTrigger.SystemReload)
        {
            Trigger(ResourceTrigger.Terminate);
            Trigger(ResourceTrigger.Initialize);
        }

        return new AsyncReply<bool>(true);
    }



    protected override void ClientConnected(DistributedConnection connection)
    {
        //Task.Delay(10000).ContinueWith((x) =>
        //{
        //    Console.WriteLine("By bye");
        //    // Remove me from here
        //    connection.Close();
        //    one = true;
        //});

    }

    public override void Add(DistributedConnection connection)
    {
        connection.Server = this;
        connection.ExceptionLevel = ExceptionLevel;
        base.Add(connection);
    }

    public override void Remove(DistributedConnection connection)
    {
        connection.Server = null;
        base.Remove(connection);
    }

    protected override void ClientDisconnected(DistributedConnection connection)
    {
        //connection.OnReady -= ConnectionReadyEventReceiver;
        //Warehouse.Remove(connection);
    }

    public KeyList<string, CallInfo?> Calls { get; } = new KeyList<string, CallInfo?>();

    public struct CallInfo
    {
        public FunctionTemplate Template;
        public Delegate Delegate;
    }

    public DistributedServer MapCall(string call, Delegate handler)
    {
        var ft = FunctionTemplate.MakeFunctionTemplate(null, handler.Method, 0, call, null);
        Calls.Add(call, new CallInfo() { Delegate = handler, Template = ft });
        return this;
    }

}
