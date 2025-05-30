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

    public string ReadAnnotation
    {
        get;
        set;
    }

    public string WriteAnnotation
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

        if (WriteAnnotation != null && ReadAnnotation != null)
        {
            var rexp = DC.ToBytes(ReadAnnotation);
            var wexp = DC.ToBytes(WriteAnnotation);
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
        else if (WriteAnnotation != null)
        {
            var wexp = DC.ToBytes(WriteAnnotation);
            return new BinaryList()
                .AddUInt8((byte)(0x30 | pv))
                .AddUInt8((byte)name.Length)
                .AddUInt8Array(name)
                .AddUInt8Array(ValueType.Compose())
                .AddInt32(wexp.Length)
                .AddUInt8Array(wexp)
                .ToArray();
        }
        else if (ReadAnnotation != null)
        {
            var rexp = DC.ToBytes(ReadAnnotation);
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
        RepresentationType valueType, string readAnnotation = null, string writeAnnotation = null, bool recordable = false)
        : base(template, index, name, inherited)
    {
        this.Recordable = recordable;
        //this.Storage = storage;
        if (readAnnotation != null)
            this.ReadAnnotation = readAnnotation;
        this.WriteAnnotation = writeAnnotation;
        this.ValueType = valueType;
    }

    public static PropertyTemplate MakePropertyTemplate(Type type, PropertyInfo pi, byte index = 0, string customName = null, TypeTemplate typeTemplate = null)
    {
        var genericPropType = pi.PropertyType.IsGenericType ? pi.PropertyType.GetGenericTypeDefinition() : null;

        var propType = genericPropType == typeof(PropertyContext<>) ?
                RepresentationType.FromType(pi.PropertyType.GetGenericArguments()[0]) :
                RepresentationType.FromType(pi.PropertyType);

        if (propType == null)
            throw new Exception($"Unsupported type `{pi.PropertyType}` in property `{type.Name}.{pi.Name}`");

        var annotationAttr = pi.GetCustomAttribute<AnnotationAttribute>(true);
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

        var pt = new PropertyTemplate(typeTemplate, index, customName ?? pi.Name, pi.DeclaringType != type, propType);

        if (storageAttr != null)
            pt.Recordable = storageAttr.Mode == StorageMode.Recordable;

        if (annotationAttr != null)
            pt.ReadAnnotation = annotationAttr.Annotation;
        else
            pt.ReadAnnotation = GetTypeAnnotationName(pi.PropertyType);

        pt.PropertyInfo = pi;

        return pt;
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
