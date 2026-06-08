using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace RPC.EsiurTest
{
    [TypeId("6ded4eca74c8886a85a74e082770be4b")]
    [Export]
    public enum DocType
    {
        CreditNote = 3,
        Invoice = 2,
        Order = 1,
        Quote = 0

    }
}
