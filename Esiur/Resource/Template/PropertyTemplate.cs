using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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


        public PropertyInfo Info
        {
            get;
            set;
        }

        //bool ReadOnly;
        //IIPTypes::DataType ReturnType;
        public PropertyPermission Permission {
            get;
            set;
        }

        /*
        public bool Recordable
        {
            get;
            set;
        }*/

        public StorageMode Storage
        {
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

        /*
        public bool Storable
        {
            get;
            set;
        }*/


        public override byte[] Compose()
        {
            var name = base.Compose();
            var pv = ((byte)(Permission) << 1) | (Storage == StorageMode.Recordable ? 1 : 0);

            if (WriteExpansion != null && ReadExpansion != null)
            {
                var rexp = DC.ToBytes(ReadExpansion);
                var wexp = DC.ToBytes(WriteExpansion);
                return BinaryList.ToBytes((byte)(0x38 | pv), (byte)name.Length, name, wexp.Length, wexp, rexp.Length, rexp);
            }
            else if (WriteExpansion != null)
            {
                var wexp = DC.ToBytes(WriteExpansion);
                return BinaryList.ToBytes((byte)(0x30 | pv), (byte)name.Length, name, wexp.Length, wexp);
            }
            else if (ReadExpansion != null)
            {
                var rexp = DC.ToBytes(ReadExpansion);
                return BinaryList.ToBytes((byte)(0x28 | pv), (byte)name.Length, name, rexp.Length, rexp);
            }
            else
                return BinaryList.ToBytes((byte)(0x20 | pv), (byte)name.Length, name);
        }

        public PropertyTemplate(ResourceTemplate template, byte index, string name, string read, string write, StorageMode storage)
            :base(template, MemberType.Property, index, name)
        {
            //this.Recordable = recordable;
            this.Storage = storage;
            this.ReadExpansion = read;
            this.WriteExpansion = write;
        }
    }
}
