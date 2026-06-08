using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.Kind", "")]
    [Export]
    public enum Kind
    {
        Bool = 1,
        Bytes = 7,
        DateTime = 8,
        Decimal = 5,
        Double = 4,
        Guid = 9,
        Int64 = 2,
        Null = 0,
        String = 6,
        UInt64 = 3

    }
}
