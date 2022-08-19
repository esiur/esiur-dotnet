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
        [Public("Hell2o")] public int ChildMethod(string childName) => 111;
        [Public] public new string Hello() => "Hi from Child";

        [Public] public string HelloChild() => "Hi from Child";

    }
}
