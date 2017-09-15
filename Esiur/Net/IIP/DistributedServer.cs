using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esiur.Net.Sockets;
using Esiur.Misc;
using System.Threading;
using Esiur.Data;
using Esiur.Engine;
using System.Net;
using Esiur.Resource;
using Esiur.Security.Membership;

namespace Esiur.Net.IIP
{
    public class DistributedServer : NetworkServer<DistributedConnection>, IResource
    {

        [Storable]
        [ResourceProperty]
        public string ip
        {
            get;
            set;
        }

        [Storable]
        [ResourceProperty]
        public IMembership Membership
        {
            get;
            set;
        }
        [Storable]
        [ResourceProperty]
        public ushort port
        {
            get;
            set;
        }

        [Storable]
        [ResourceProperty]
        public uint timeout
        {
            get;
            set;
        }
        
        [Storable]
        [ResourceProperty]
        public uint clock
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

                if (ip != null)
                    listener = new TCPSocket(new IPEndPoint(IPAddress.Parse(ip), port));
                else
                    listener = new TCPSocket(new IPEndPoint(IPAddress.Any, port));

                Start(listener, timeout, clock);
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

        private void SessionModified(DistributedConnection Session, string Key, object NewValue)
        {

        }



        protected override void ClientConnected(DistributedConnection sender)
        {
            Console.WriteLine("DistributedConnection Client Connected");
            sender.Server = this;
         }

        protected override void ClientDisconnected(DistributedConnection sender)
        {
          Console.WriteLine("DistributedConnection Client Disconnected");
        }
    }
}
