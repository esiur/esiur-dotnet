using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace RPC.EsiurTest
{
    [TypeId("c44e42333dfd8d3485bb2a79fd7a9f6f")]
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
