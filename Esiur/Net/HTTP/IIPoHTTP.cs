using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Net.Packets;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.HTTP;
public class IIPoHTTP : HTTPFilter
{
    [Attribute]
    EntryPoint EntryPoint { get; set; }

    public override AsyncReply<bool> Execute(HTTPConnection sender)
    {
        if (sender.Request.URL != "iip")
            return new AsyncReply<bool>(false);

        IIPPacketRequest action = (IIPPacketRequest)Convert.ToByte(sender.Request.Query["a"]);

        if (action == IIPPacketRequest.Query)
        {
            EntryPoint.Query(sender.Request.Query["l"], null).Then(x =>
            {

            });
        }

        return new AsyncReply<bool>(true);
    }

    public override AsyncReply<bool> Trigger(ResourceTrigger trigger)
    {
        return new AsyncReply<bool>(true);
    }
}
