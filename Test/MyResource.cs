using Esiur.Core;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [Resource]
    [Annotation("A", "B", "C", "D")]
    public partial class MyResource
    {
        [Export][Annotation("Comment")] string description;
        [Export] int categoryId;

        [Export] public string Hello() => "Hi";

        [Export] public string HelloParent() => "Hi from Parent";

    }
}
