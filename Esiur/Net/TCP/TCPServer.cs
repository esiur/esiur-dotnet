/*
 
Copyright (c) 2017-2019 Ahmed Kh. Zamil

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

namespace Esiur.Net.TCP
{
    public class TCPServer : NetworkServer<TCPConnection>, IResource
    {

        [Storable]
        string ip
        {
            get;
            set;
        }
        [Storable]
        ushort port
        {
            get;
            set;
        }
        [Storable]
        uint timeout
        {
            get;
            set;
        }
        [Storable]
        uint clock
        {
            get;
            set;
        }
        public Instance Instance { get; set; }

        TCPFilter[] filters = null;


        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            if (trigger == ResourceTrigger.Initialize)
            {
                TCPSocket listener;


                if (ip != null)
                    listener =new TCPSocket(new IPEndPoint(IPAddress.Parse(ip), port));
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
            else if (trigger == ResourceTrigger.SystemInitialized)
            {
                Instance.Children<TCPFilter>().Then(x => filters = x);
            }

            return new AsyncReply<bool>(true);
        }



        protected override void DataReceived(TCPConnection sender, NetworkBuffer data)
        {
            var msg = data.Read();

            foreach (var filter in filters)
            { 
                    if (filter.Execute(msg, data, sender))
                        return;
            }
        }

        private void SessionModified(TCPConnection session, string key, object newValue)
        {

        }

        protected override void ClientConnected(TCPConnection sender)
        {
            foreach (var filter in filters)
            {
                filter.Connected(sender);
            }
        }

        protected override void ClientDisconnected(TCPConnection sender)
        {
            foreach (var filter in filters)
            {
                filter.Disconnected(sender);
            }
        }


    }
}
