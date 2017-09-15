using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Resource;
using Esiur.Net.IIP;
using Esiur.Net.Sockets;
using Esiur.Engine;

namespace Esiur.Net.HTTP
{
    public class IIPoWS: HTTPFilter
    {
        public override bool Execute(HTTPConnection sender)
        {
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
                        var wsSocket = new WSSocket(tcpSocket);
                        httpServer.Connections.Remove(sender);
                        var iipConnection = new DistributedConnection();
                        iipConnection.Server = iipServer;
                        iipConnection.Assign(wsSocket);
                    }
                });

                return true;
            }

            return false;
        }

        public override AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            return new AsyncReply<bool>(true);
        }
    }
    
}
 
