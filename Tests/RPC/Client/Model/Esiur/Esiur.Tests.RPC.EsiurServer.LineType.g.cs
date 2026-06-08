using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.LineType", "")]
    [Export]
    public enum LineType
    {
        Discount = 2,
        Product = 0,
        Service = 1,
        Shipping = 3

    }
}
