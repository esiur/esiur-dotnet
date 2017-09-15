using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template
{
    public class EventTemplate : MemberTemplate
    {
        public string Expansion
        {
            get;
            set;
        }

        public override byte[] Compose()
        {
            var name = base.Compose();

            if (Expansion != null)
            {
                var exp = DC.ToBytes(Expansion);
                return BinaryList.ToBytes((byte)0x50, exp.Length, exp, (byte)name.Length, name);
            }
            else
                return BinaryList.ToBytes((byte)0x40, (byte)name.Length, name);
        }

        public EventTemplate() { Type = MemberType.Event; }
    }
}
