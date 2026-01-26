using Esiur.Data;
using Esiur.Net.IIP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template;

public class PropertyTemplate : MemberTemplate
{
    public Map<string, string> Annotations { get; set; }




    public PropertyInfo PropertyInfo
    {
        get;
        set;
    }

    public TRU ValueType { get; set; }


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

    //public bool IsNullable { get; set; }

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

    //public string ReadAnnotation
    //{
    //    get;
    //    set;
    //}

    //public string WriteAnnotation
    //{
    //    get;
    //    set;
    //}

    /*
    public bool Storable
    {
        get;
        set;
    }*/

    public override string ToString()
    {
        return $"{Name}: {ValueType}";
    }

    public static (uint, PropertyTemplate) Parse(byte[] data, uint offset, byte index, bool inherited)
    {
        var oOffset = offset;



        var hasAnnotation = ((data[offset] & 0x8) == 0x8);
        var recordable = ((data[offset] & 1) == 1);
        var permission = (PropertyPermission)((data[offset++] >> 1) & 0x3);
        var name = data.GetString(offset + 1, data[offset]);

        offset += (uint)data[offset] + 1;

        var (dts, valueType) = TRU.Parse(data, offset);

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

        return (offset - oOffset, new PropertyTemplate()
        {
            Index = index,
            Name = name,
            Inherited = inherited,
            Permission = permission,
            Recordable = recordable,
            ValueType = valueType,
            Annotations = annotations
        });

    }

    public byte[] Compose()
    {
        var name = DC.ToBytes(Name);

        var pv = ((byte)(Permission) << 1) | (Recordable ? 1 : 0);

        if (Inherited)
            pv |= 0x80;

        //if (WriteAnnotation != null && ReadAnnotation != null)
        //{
        //    var rexp = DC.ToBytes(ReadAnnotation);
        //    var wexp = DC.ToBytes(WriteAnnotation);
        //    return new BinaryList()
        //        .AddUInt8((byte)(0x38 | pv))
        //        .AddUInt8((byte)name.Length)
        //        .AddUInt8Array(name)
        //        .AddUInt8Array(ValueType.Compose())
        //        .AddInt32(wexp.Length)
        //        .AddUInt8Array(wexp)
        //        .AddInt32(rexp.Length)
        //        .AddUInt8Array(rexp)
        //        .ToArray();
        //}
        //else if (WriteAnnotation != null)
        //{
        //    var wexp = DC.ToBytes(WriteAnnotation);
        //    return new BinaryList()
        //        .AddUInt8((byte)(0x30 | pv))
        //        .AddUInt8((byte)name.Length)
        //        .AddUInt8Array(name)
        //        .AddUInt8Array(ValueType.Compose())
        //        .AddInt32(wexp.Length)
        //        .AddUInt8Array(wexp)
        //        .ToArray();
        //}
        //else if (ReadAnnotation != null)
        //{
        //    var rexp = DC.ToBytes(ReadAnnotation);
        //    return new BinaryList()
        //        .AddUInt8((byte)(0x28 | pv))
        //        .AddUInt8((byte)name.Length)
        //        .AddUInt8Array(name)
        //        .AddUInt8Array(ValueType.Compose())
        //        .AddInt32(rexp.Length)
        //        .AddUInt8Array(rexp)
        //        .ToArray();
        //}
        if (Annotations != null)
        {
            var rexp = Codec.Compose(Annotations, null, null);
            return new BinaryList()
                .AddUInt8((byte)(0x28 | pv))
                .AddUInt8((byte)name.Length)
                .AddUInt8Array(name)
                .AddUInt8Array(ValueType.Compose())
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

    //public PropertyTemplate(TypeTemplate template, byte index, string name, bool inherited, 
    //    TRU valueType, string readAnnotation = null, string writeAnnotation = null, bool recordable = false)
    //    : base(template, index, name, inherited)
    //{
    //    this.Recordable = recordable;
    //    //this.Storage = storage;
    //    if (readAnnotation != null)
    //        this.ReadAnnotation = readAnnotation;
    //    this.WriteAnnotation = writeAnnotation;
    //    this.ValueType = valueType;
    //}

    public static PropertyTemplate MakePropertyTemplate(Type type, PropertyInfo pi, string name, byte index, PropertyPermission permission, TypeTemplate typeTemplate)
    {
        var genericPropType = pi.PropertyType.IsGenericType ? pi.PropertyType.GetGenericTypeDefinition() : null;

        var propType = genericPropType == typeof(PropertyContext<>) ?
                TRU.FromType(pi.PropertyType.GetGenericArguments()[0]) :
                TRU.FromType(pi.PropertyType);

        if (propType == null)
            throw new Exception($"Unsupported type `{pi.PropertyType}` in property `{type.Name}.{pi.Name}`");

        var annotationAttrs = pi.GetCustomAttributes<AnnotationAttribute>(true);
        var storageAttr = pi.GetCustomAttribute<StorageAttribute>(true);

        //var nullabilityContext = new NullabilityInfoContext();
        //propType.Nullable = nullabilityContext.Create(pi).ReadState is NullabilityState.Nullable;

        var nullableAttr = pi.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().FullName == "System.Runtime.CompilerServices.NullableAttribute");
        var nullableContextAttr = pi.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().FullName == "System.Runtime.CompilerServices.NullableContextAttribute");

        var nullableAttrFlags = (nullableAttr?.GetType().GetField("NullableFlags")?.GetValue(nullableAttr) as byte[] ?? new byte[0]).ToList();
        var nullableContextAttrFlag = (byte)(nullableContextAttr?.GetType().GetField("Flag")?.GetValue(nullableContextAttr) ?? (byte)0);


        //var nullableAttr = pi.GetCustomAttribute<NullableAttribute>(true);
        //var flags = ((byte[]) nullableAttr?.NullableFlags ?? new byte[0]).ToList();

        if (nullableAttrFlags.Count > 0 && genericPropType == typeof(PropertyContext<>))
            nullableAttrFlags.RemoveAt(0);

        if (nullableContextAttrFlag == 2)
        {
            if (nullableAttrFlags.Count == 1)
                propType.SetNotNull(nullableAttrFlags.FirstOrDefault());
            else
                propType.SetNotNull(nullableAttrFlags);
        }
        else
        {
            if (nullableAttrFlags.Count == 1)
                propType.SetNull(nullableAttrFlags.FirstOrDefault());
            else
                propType.SetNull(nullableAttrFlags);
        }


        Map<string, string> annotations = null;

        if (annotationAttrs != null && annotationAttrs.Count() > 0)
        {
            annotations = new Map<string, string>();
            foreach (var attr in annotationAttrs)
                annotations.Add(attr.Key, attr.Value);
        }
        else
        {
            annotations = new Map<string, string>();
            annotations.Add("", GetTypeAnnotationName(pi.PropertyType));
        }


        return new PropertyTemplate()
        {
            Name = name,
            Index = index,
            Inherited = pi.DeclaringType != type,
            ValueType = propType,
            PropertyInfo = pi,
            Recordable = storageAttr == null ? false : storageAttr.Mode == StorageMode.Recordable,
            Permission = permission,
            Annotations = annotations,
        };

        //var pt = new PropertyTemplate(typeTemplate, index, customName ?? pi.Name, pi.DeclaringType != type, propType);

        //if (storageAttr != null)
        //    pt.Recordable = storageAttr.Mode == StorageMode.Recordable;

        //if (annotationAttr != null)
        //    pt.ReadAnnotation = annotationAttr.Annotation;
        //else
        //    pt.ReadAnnotation = GetTypeAnnotationName(pi.PropertyType);

        //pt.PropertyInfo = pi;

        //return pt;
    }


    public static string GetTypeAnnotationName(Type type)
    {
        var nullType = Nullable.GetUnderlyingType(type);
        if (nullType == null)
            return type.Name;
        else
            return type.Name + "?";
    }


}
