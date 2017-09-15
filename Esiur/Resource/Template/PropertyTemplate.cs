using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template
{
    public class PropertyTemplate : MemberTemplate
    {
        public enum PropertyPermission:byte
        {
            Read = 1,
            Write,
            ReadWrite
        }

        //bool ReadOnly;
        //IIPTypes::DataType ReturnType;
        public PropertyPermission Permission {
            get;
            set;
        }
        
        public string ReadExpansion
        {
            get;
            set;
        }

        public string WriteExpansion
        {
            get;
            set;
        }

        public bool Storable
        {
            get;
            set;
        }


        public override byte[] Compose()
        {
            var name = base.Compose();

            if (WriteExpansion != null && ReadExpansion != null)
            {
                var rexp = DC.ToBytes(ReadExpansion);
                var wexp = DC.ToBytes(WriteExpansion);
                return BinaryList.ToBytes((byte)(0x38 | (byte)Permission), wexp.Length, wexp, rexp.Length, rexp, (byte)name.Length, name);
            }
            else if (WriteExpansion != null)
            {
                var wexp = DC.ToBytes(WriteExpansion);
                return BinaryList.ToBytes((byte)(0x30 | (byte)Permission), wexp.Length, wexp, (byte)name.Length, name);
            }
            else if (ReadExpansion != null)
            {
                var rexp = DC.ToBytes(ReadExpansion);
                return BinaryList.ToBytes((byte)(0x28 | (byte)Permission), rexp.Length, rexp, (byte)name.Length, name);
            }
            else
                return BinaryList.ToBytes((byte)(0x20 | (byte)Permission), (byte)name.Length, name);
        }

        public PropertyTemplate() { Type = MemberType.Property; }
    }
}
