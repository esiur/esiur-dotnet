using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template;
public class PropertyTemplate : MemberTemplate
{
    public enum PropertyPermission : byte
    {
        Read = 1,
        Write,
        ReadWrite
    }


    public PropertyInfo PropertyInfo
    {
        get;
        set;
    }

    public RepresentationType ValueType { get; set; }


    /*
    public bool Serilize
    {
        get;set;
    }
    */
    //bool ReadOnly;
    //IIPTypes::DataType ReturnType;
    public PropertyPermission Permission
    {
        get;
        set;
    }

    public bool IsNullable { get; set; }

    public bool Recordable
    {
        get;
        set;
    }

    /*
    public PropertyType Mode
    {
        get;
        set;
    }*/

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
        var pv = ((byte)(Permission) << 1) | (Recordable ? 1 : 0);

        if (Inherited)
            pv |= 0x80;

        if (WriteExpansion != null && ReadExpansion != null)
        {
            var rexp = DC.ToBytes(ReadExpansion);
            var wexp = DC.ToBytes(WriteExpansion);
            return new BinaryList()
                .AddUInt8((byte)(0x38 | pv))
                .AddUInt8((byte)name.Length)
                .AddUInt8Array(name)
                .AddUInt8Array(ValueType.Compose())
                .AddInt32(wexp.Length)
                .AddUInt8Array(wexp)
                .AddInt32(rexp.Length)
                .AddUInt8Array(rexp)
                .ToArray();
        }
        else if (WriteExpansion != null)
        {
            var wexp = DC.ToBytes(WriteExpansion);
            return new BinaryList()
                .AddUInt8((byte)(0x30 | pv))
                .AddUInt8((byte)name.Length)
                .AddUInt8Array(name)
                .AddUInt8Array(ValueType.Compose())
                .AddInt32(wexp.Length)
                .AddUInt8Array(wexp)
                .ToArray();
        }
        else if (ReadExpansion != null)
        {
            var rexp = DC.ToBytes(ReadExpansion);
            return new BinaryList()
                .AddUInt8((byte)(0x28 | pv))
                .AddUInt8((byte)name.Length)
                .AddUInt8Array(name)
                .AddUInt8Array(ValueType.Compose())
                .AddInt32(rexp.Length)
                .AddUInt8Array(rexp)
                .ToArray();
        }
        else
        {
            return new BinaryList()
                .AddUInt8((byte)(0x20 | pv))
                .AddUInt8((byte)name.Length)
                .AddUInt8Array(name)
                .AddUInt8Array(ValueType.Compose())
                .ToArray();
        }
    }

    public PropertyTemplate(TypeTemplate template, byte index, string name, bool inherited, 
        RepresentationType valueType, string read = null, string write = null, bool recordable = false)
        : base(template, index, name, inherited)
    {
        this.Recordable = recordable;
        //this.Storage = storage;
        if (read != null)
            this.ReadExpansion = read;
        this.WriteExpansion = write;
        this.ValueType = valueType;
    }
}
