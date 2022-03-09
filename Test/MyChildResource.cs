using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [Resource]
    public partial class MyChildResource : MyResource
    {
        [Public] string childName;
        [Public] public int ChildMethod(string childName) => 111;
    }
}
