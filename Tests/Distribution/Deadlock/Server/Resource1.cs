using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Protocol;
using Esiur.Resource;

namespace Esiur.Tests.Deadlock.Server
{
    [Resource]
    public partial class Resource1
    {
        [Export] public Resource1 res1;
        [Export] public Resource2 res2;
    }
}
