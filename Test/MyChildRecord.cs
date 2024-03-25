using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [Export]
    public class MyChildRecord : MyRecord
    {
        public string ChildName { get; set; }

    }
}
