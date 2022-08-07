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

namespace Esiur.Net.IIP;
public class DistributedServer : NetworkServer<DistributedConnection>, IResource
{

    

    [Attribute]
    public string IP
    {
        get;
        set;
    }

    [Attribute]
    public IMembership Membership
    {
        get;
        set;
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

    public event PropertyModifiedEvent PropertyModified;

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



    //protected override void DataReceived(DistributedConnection sender, NetworkBuffer data)
    //{
    //    //throw new NotImplementedException();

    //}



    //protected override void ClientConnected(DistributedConnection sender)
    //{
    //    //Console.WriteLine("DistributedConnection Client Connected");
    //}

    //private void ConnectionReadyEventReceiver(DistributedConnection sender)
    //{
    //    sender.OnReady -= ConnectionReadyEventReceiver;
    //    Warehouse.Put(sender, sender.LocalUsername, null, this);
    //}


    //public override void RemoveConnection(DistributedConnection connection)
    //{
    //    connection.OnReady -= Sender_OnReady;
    //    //connection.Server = null;
    //    base.RemoveConnection(connection);
    //}

    //public override void AddConnection(DistributedConnection connection)
    //{
    //    connection.OnReady += Sender_OnReady;
    //    connection.Server = this;
    //    base.AddConnection(connection);
    //}



    protected override void ClientConnected(DistributedConnection connection)
    {
        //connection.OnReady += ConnectionReadyEventReceiver;
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

    public KeyList<string, Delegate> Calls { get; } = new KeyList<string, Delegate>();

    public void MapCall(string call, Delegate handler)
    {
        Calls.Add(call, handler);
    }

}
