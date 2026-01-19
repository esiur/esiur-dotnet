using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Esiur.Data;

namespace Esiur.Resource.Template;

public class ConstantTemplate : MemberTemplate
{
    public readonly object Value;

    public Map<string, string> Annotations;
    public readonly TRU ValueType;

    public  FieldInfo FieldInfo { get; set; }

    public ConstantTemplate(TypeTemplate template, byte index, string name, bool inherited, TRU valueType, object value, Map<string, string> annotations)
        : base(template, index, name, inherited)
    {
        Annotations = annotations;
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


        var ct = new ConstantTemplate(typeTemplate, index, customName ?? ci.Name, ci.DeclaringType != type, valueType, value, annotations);
        ct.FieldInfo = ci;

        return ct;

    }

}

