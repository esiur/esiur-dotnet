using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Deadlock.Server
{
    [Resource]
    public partial class Resource2
    {
        [Export] public Resource1 res1;
        [Export] public Resource2 res2;
    }
}
