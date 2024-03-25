using Esiur.Data;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class MyGenericRecord<T> : IRecord where T : IResource
    {
        [Export] public int Start { get; set; }
        [Export] public int Needed { get; set; }
        [Export] public int Total { get; set; }
        [Export] public T[] Results { get; set; }
    }

}
