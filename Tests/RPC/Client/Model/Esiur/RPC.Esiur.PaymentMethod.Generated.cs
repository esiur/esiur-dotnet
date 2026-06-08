using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace RPC.EsiurTest
{
    [TypeId("fadfe3764f808d7e839fef5275490dd7")]
    [Export]
    public enum PaymentMethod
    {
        Card = 1,
        Cash = 0,
        Crypto = 3,
        Other = 4,
        Wire = 2

    }
}
