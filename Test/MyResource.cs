using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [Resource]
    public partial class MyResource
    {
        [Public] string description;
        [Public] int categoryId;
    }
}
