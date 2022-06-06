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
        [Public] public int Start { get; set; }
        [Public] public int Needed { get; set; }
        [Public] public int Total { get; set; }
        [Public] public T[] Results { get; set; }
    }

}
