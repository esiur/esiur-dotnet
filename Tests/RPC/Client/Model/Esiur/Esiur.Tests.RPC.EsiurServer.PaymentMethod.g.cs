using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.PaymentMethod", "")]
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
