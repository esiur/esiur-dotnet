using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template
{
    public class MemberTemplate
    {
        public enum MemberType
        {
            Function = 0,
            Property = 1,
            Event = 2,
        }

        public byte Index { get; set; }
        public string Name { get; set; }
        public MemberType Type { get; set; }

        public virtual byte[] Compose()
        {
            return DC.ToBytes(Name);
        }
    }

}
