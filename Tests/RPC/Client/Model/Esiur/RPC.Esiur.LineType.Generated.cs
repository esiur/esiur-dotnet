using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace RPC.EsiurTest
{
    [TypeId("7e474e8826e288f28bddddf69782c580")]
    [Export]
    public enum LineType
    {
        Discount = 2,
        Product = 0,
        Service = 1,
        Shipping = 3

    }
}
