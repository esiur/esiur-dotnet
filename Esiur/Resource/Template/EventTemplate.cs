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
                return new BinaryList()
                        .AddUInt8(0x50)
                        .AddInt32(exp.Length)
                        .AddUInt8Array(exp)
                        .AddUInt8((byte)name.Length)
                        .AddUInt8Array(name)
                        .ToArray();
            }
            else
                return new BinaryList()
                        .AddUInt8(0x40)
                        .AddUInt8((byte)name.Length)
                        .AddUInt8Array(name)
                        .ToArray();
        }


        public EventTemplate(ResourceTemplate template, byte index, string name, string expansion)
            :base(template, MemberType.Property, index, name)
        {
            this.Expansion = expansion;
        }
    }
}
