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
using Esyur.Net.Sockets;
using Esyur.Misc;
using System.Threading;
using Esyur.Data;
using Esyur.Core;
using System.Net;
using Esyur.Resource;
using Esyur.Security.Membership;

namespace Esyur.Net.IIP
{
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

                Start(listener, Timeout, Clock);
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



        protected override void DataReceived(DistributedConnection sender, NetworkBuffer data)
        {
            //throw new NotImplementedException();
 
        }

        private void SessionModified(DistributedConnection session, string key, object newValue)
        {

        }

        protected override void ClientConnected(DistributedConnection sender)
        {
            //Console.WriteLine("DistributedConnection Client Connected");
        }

        private void Sender_OnReady(DistributedConnection sender)
        {
            Warehouse.Put(sender, sender.LocalUsername, null, this);
        }

        public override void RemoveConnection(DistributedConnection connection)
        {
            connection.OnReady -= Sender_OnReady;
            //connection.Server = null;
            base.RemoveConnection(connection);
        }

        public override void AddConnection(DistributedConnection connection)
        {
            connection.OnReady += Sender_OnReady;
            connection.Server = this;
            base.AddConnection(connection);
        }

        protected override void ClientDisconnected(DistributedConnection sender)
        {
            sender.Destroy();

            Warehouse.Remove(sender);

            //Console.WriteLine("DistributedConnection Client Disconnected");
        }
    }
}
