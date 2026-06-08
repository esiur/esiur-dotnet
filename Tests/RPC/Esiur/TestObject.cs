using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.RPC.EsiurServer
{
    [Resource]
    public partial class TestObject
    {
        [Export] int size;
        [Export] string name;
        [Export] object value;
    }
}
