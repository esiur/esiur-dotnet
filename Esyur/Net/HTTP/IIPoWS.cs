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
using System.Threading.Tasks;
using Esyur.Resource;
using Esyur.Net.IIP;
using Esyur.Net.Sockets;
using Esyur.Core;

namespace Esyur.Net.HTTP
{
    public class IIPoWS: HTTPFilter
    {
        [Attribute]
        public DistributedServer Server
        {
            get;
            set;
        }

        public override bool Execute(HTTPConnection sender)
        {

            if (sender.IsWebsocketRequest())
            {
                if (Server == null)
                    return false;

                var tcpSocket = sender.Unassign();

                if (tcpSocket == null)
                    return false;

                var httpServer = sender.Parent;
                var wsSocket = new WSSocket(tcpSocket);
                httpServer.RemoveConnection(sender);

                var iipConnection = new DistributedConnection();

                Server.AddConnection(iipConnection);
                iipConnection.Assign(wsSocket);
                wsSocket.Begin();

                return true;
            }

            return false;

            /*
            if (sender.Request.Filename.StartsWith("/iip/"))
            {
                // find the service
                var path = sender.Request.Filename.Substring(5);// sender.Request.Query["path"];


                Warehouse.Get(path).Then((r) =>
                {
                    if (r is DistributedServer)
                    {
                        var httpServer = sender.Parent;
                        var iipServer = r as DistributedServer;
                        var tcpSocket = sender.Unassign();
                        if (tcpSocket == null)
                            return;

                        var wsSocket = new WSSocket(tcpSocket);
                        httpServer.RemoveConnection(sender);

                        //httpServer.Connections.Remove(sender);
                        var iipConnection = new DistributedConnection();
  //                      iipConnection.OnReady += IipConnection_OnReady;
//                        iipConnection.Server = iipServer;
    //                    iipConnection.Assign(wsSocket);

                        iipServer.AddConnection(iipConnection);
                        iipConnection.Assign(wsSocket);
                        wsSocket.Begin();
                    }
                });

                return true;
            }

            return false;
            */
        }

        private void IipConnection_OnReady(DistributedConnection sender)
        {
            Warehouse.Put(sender, sender.RemoteUsername, null, sender.Server);
        }

        public override AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            return new AsyncReply<bool>(true);
        }
    }
    
}
 
