using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.Currency", "")]
    [Export]
    public enum Currency
    {
        CNH = 1,
        EUR = 3,
        GBP = 5,
        IQD = 0,
        JPY = 4,
        USD = 2

    }
}
