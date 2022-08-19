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
        [Public][Annotation("Comment")] string description;
        [Public] int categoryId;


        [Public] public string Hello() => "Hi";

        [Public] public string HelloParent() => "Hi from Parent";

    }
}
