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
                return BinaryList.ToBytes((byte)(0x10 | (IsVoid ? 0x8 : 0x0)), (byte)name.Length, name, exp.Length, exp);
            }
            else
                return BinaryList.ToBytes((byte)(IsVoid ? 0x8 : 0x0), (byte)name.Length, name);
        }


        public FunctionTemplate(ResourceTemplate template, byte index, string name,bool isVoid, string expansion)
            :base(template, MemberType.Property, index, name)
        {
            this.IsVoid = isVoid;
            this.Expansion = expansion;
        }
    }
}
