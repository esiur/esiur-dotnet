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
        [Export] string childName;
        [Export("Hell2o")] public int ChildMethod(string childName) => 111;
        [Export] public new string Hello() => "Hi from Child";

        [Export] public string HelloChild() => "Hi from Child";

    }
}
