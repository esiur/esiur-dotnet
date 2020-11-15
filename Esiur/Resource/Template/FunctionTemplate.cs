using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template
{
    public class FunctionTemplate : MemberTemplate
    {

        public string Expansion
        {
            get;
            set;
        }

        public bool IsVoid
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
                return new BinaryList().AddUInt8((byte)(0x10 | (IsVoid ? 0x8 : 0x0)))
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddInt32(exp.Length)
                    .AddUInt8Array(exp)
                    .ToArray();
            }
            else
                return new BinaryList().AddUInt8((byte)(IsVoid ? 0x8 : 0x0))
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .ToArray();
        }


        public FunctionTemplate(ResourceTemplate template, byte index, string name,bool isVoid, string expansion = null)
            :base(template, MemberType.Property, index, name)
        {
            this.IsVoid = isVoid;
            this.Expansion = expansion;
        }
    }
}
