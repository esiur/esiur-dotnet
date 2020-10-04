using Esyur.Core;
using Esyur.Net.IIP;
using Esyur.Net.Packets;
using Esyur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esyur.Net.HTTP
{
    public class IIPoHTTP : HTTPFilter
    {
        [Attribute]
        EntryPoint EntryPoint { get; set; }

        public async override AsyncReply<bool> Execute(HTTPConnection sender)
        {
            if (sender.Request.URL != "iip")
                return false;

            IIPPacket.IIPPacketAction action = (IIPPacket.IIPPacketAction)Convert.ToByte(sender.Request.Query["a"]);

            if (action == IIPPacket.IIPPacketAction.QueryLink)
            {
                EntryPoint.Query(sender.Request.Query["l"], null).Then(x =>
                {

                });
            }

            return true;
        }

        public override AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            return new AsyncReply<bool>(true);
        }
    }
}
