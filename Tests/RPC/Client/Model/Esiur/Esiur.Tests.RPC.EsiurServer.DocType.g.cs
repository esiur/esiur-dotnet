using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.DocType", "localhost")]
    [Export]
    public enum DocType
    {
        CreditNote = 3,
        Invoice = 2,
        Order = 1,
        Quote = 0

    }
}
