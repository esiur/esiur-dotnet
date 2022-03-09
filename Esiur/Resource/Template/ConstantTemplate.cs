using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Data;

namespace Esiur.Resource.Template;

public class ConstantTemplate : MemberTemplate
{
    public readonly object Value;
    //public readonly byte[] ValueData;
    public readonly string Expansion;
    public readonly RepresentationType ValueType;

    public ConstantTemplate(TypeTemplate template, byte index, string name, bool inherited, RepresentationType valueType, object value, string expansion) 
        : base(template, index, name, inherited)
    {
        Expansion = expansion;
        ValueType = valueType;
        Value = value;
        //try
        //{
        //    Codec.Compose(value, null);
        //    Value = value;
        //}
        //catch
        //{
        //    throw new Exception($"Constant `{template.ClassName}.{name}` can't be serialized.");
        //}
    }

   public override byte[] Compose()
    {
        var name = base.Compose();

        var hdr = Inherited ? (byte)0x80 : (byte)0;


        if (Expansion != null)
        {
            var exp = DC.ToBytes(Expansion);
            hdr |= 0x70;
            return new BinaryList()
                    .AddUInt8(hdr)
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(ValueType.Compose())
                    .AddUInt8Array(Codec.Compose(Value, null))
                    .AddInt32(exp.Length)
                    .AddUInt8Array(exp)
                    .ToArray();
        }
        else
        {
            hdr |= 0x60;

            return new BinaryList()
                    .AddUInt8(hdr)
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(ValueType.Compose())
                    .AddUInt8Array(Codec.Compose(Value, null))
                    .ToArray();
        }
    }
}

