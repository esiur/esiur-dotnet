using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Esiur.Data;

namespace Esiur.Resource.Template;

public class ConstantTemplate : MemberTemplate
{
    public object Value { get; set; }

    public Map<string, string> Annotations { get; set; }
    public TRU ValueType { get; set; }

    public FieldInfo FieldInfo { get; set; }


    public static (uint, ConstantTemplate) Parse(byte[] data, uint offset, byte index, bool inherited)
    {
        var oOffset = offset;

        var hasAnnotation = ((data[offset++] & 0x10) == 0x10);

        var name = data.GetString(offset + 1, data[offset]);
        offset += (uint)data[offset] + 1;

        var (dts, valueType) = TRU.Parse(data, offset);

        offset += dts;

        (dts, var value) = Codec.ParseSync(data, offset, Warehouse.Default);

        offset += dts;

        Map<string, string> annotations = null;

        // arguments
        if (hasAnnotation) // Annotation ?
        {
            var (len, anns) = Codec.ParseSync(data, offset, null);

            if (anns is Map<string, string> map)
                annotations = map;

            offset += len;
        }

        return (offset - oOffset, new ConstantTemplate()
        {
            Index = index,
            Name = name,
            Inherited = inherited,
            ValueType = valueType,
            Value = value,
            Annotations = annotations
        });

    }

    public byte[] Compose()
    {
        var name = DC.ToBytes(Name);

        var hdr = Inherited ? (byte)0x80 : (byte)0;


        if (Annotations != null)
        {
            var exp = Codec.Compose(Annotations, null, null);//  DC.ToBytes(Annotation);
            hdr |= 0x70;
            return new BinaryList()
                    .AddUInt8(hdr)
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(ValueType.Compose())
                    .AddUInt8Array(Codec.Compose(Value, null, null))
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
                    .AddUInt8Array(Codec.Compose(Value, null, null))
                    .ToArray();
        }
    }


    public static ConstantTemplate MakeConstantTemplate(Type type, FieldInfo ci, byte index = 0, string customName = null, TypeTemplate typeTemplate = null)
    {
        var annotationAttrs = ci.GetCustomAttributes<AnnotationAttribute>(true);

        var valueType = TRU.FromType(ci.FieldType);

        if (valueType == null)
            throw new Exception($"Unsupported type `{ci.FieldType}` in constant `{type.Name}.{ci.Name}`");

        var value = ci.GetValue(null);

        if (typeTemplate?.Type == TemplateType.Enum)
            value = Convert.ChangeType(value, ci.FieldType.GetEnumUnderlyingType());

        Map<string, string> annotations = null;

        if (annotationAttrs != null && annotationAttrs.Count() > 0)
        {
            annotations = new Map<string, string>();
            foreach (var attr in annotationAttrs)
                annotations.Add(attr.Key, attr.Value);
        }



        return new ConstantTemplate()
        {
            Name = customName,
            Index = index,
            Inherited = ci.DeclaringType != type,
            ValueType = valueType,
            Value = value,
            FieldInfo = ci,
            Annotations = annotations,
        };

    }

}

