using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Data.Types;

public class PropertyDef : MemberDef
{
    public Map<string, string> Annotations { get; set; }




    public PropertyInfo PropertyInfo
    {
        get;
        set;
    }

    public Tru ValueType { get; set; }


    /*
    public bool Serilize
    {
        get;set;
    }
    */
    //bool ReadOnly;
    //EPTypes::DataType ReturnType;
    //public PropertyPermission Permission
    //{
    //    get;
    //    set;
    //}

    public bool ReadOnly { get; set; }

    // Compatibility for code still consuming the previous permission model.
    public PropertyPermission Permission
    {
        get => ReadOnly ? PropertyPermission.Read : PropertyPermission.ReadWrite;
        set => ReadOnly = value == PropertyPermission.Read;
    }

    public bool Constant { get; set; }
    //public bool IsNullable { get; set; }

    public bool Historical { get; set; }

    public bool HasHistory
    {
        get => Historical;
        set => Historical = value;
    }

    public static async AsyncReply<ParseResult<PropertyDef>> ParseAsync(
        byte[] data, uint offset, byte index, bool inherited,
        EpConnection connection, ulong[] requestSequence)
    {
        var originalOffset = offset;
        var hasAnnotations = (data[offset] & 0x08) != 0;
        var hasHistory = (data[offset] & 0x01) != 0;
        var permission = (PropertyPermission)((data[offset++] >> 1) & 0x03);
        var name = data.GetString(offset + 1, data[offset]);
        offset += (uint)data[offset] + 1;

        var valueType = await Tru.ParseAsync(data, offset, connection, requestSequence);
        offset += valueType.Size;

        Map<string, string> annotations = null;
        if (hasAnnotations)
        {
            var (size, value) = Codec.ParseSync(data, offset, null);
            annotations = value as Map<string, string>;
            offset += size;
        }

        return new ParseResult<PropertyDef>(new PropertyDef
        {
            Index = index,
            Name = name,
            Inherited = inherited,
            Permission = permission,
            HasHistory = hasHistory,
            ValueType = valueType.Value,
            Annotations = annotations,
        }, offset - originalOffset);
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

    //public static async AsyncReply<ParseResult<PropertyDef>> ParseAsync(byte[] data, uint offset, byte index, bool inherited, EpConnection connection, ulong[] requestSequence)
    //{
    //    var oOffset = offset;



    //    var hasAnnotation = ((data[offset] & 0x8) == 0x8);
    //    var hasHistory = ((data[offset] & 1) == 1);
    //    var permission = (PropertyPermission)((data[offset++] >> 1) & 0x3);
    //    var name = data.GetString(offset + 1, data[offset]);

    //    //Console.WriteLine("Parsing propdef " + name);

    //    offset += (uint)data[offset] + 1;

    //    var valueType = await Tru.ParseAsync(data, offset, connection, requestSequence);

    //    offset += valueType.Size;

    //    Map<string, string> annotations = null;

    //    // arguments
    //    if (hasAnnotation) // Annotation ?
    //    {
    //        var (len, anns) = Codec.ParseSync(data, offset, null);

    //        if (anns is Map<string, string> map)
    //            annotations = map;

    //        offset += len;
    //    }

    //    return new ParseResult<PropertyDef>(new PropertyDef()
    //    {
    //        Index = index,
    //        Name = name,
    //        Inherited = inherited,
    //        Permission = permission,
    //        HasHistory = hasHistory,
    //        ValueType = valueType.Value,
    //        Annotations = annotations
    //    }, offset - oOffset);

    //}

    //public byte[] Compose(EpConnection connection)
    //{
    //    var name = DC.ToBytes(Name);

    //    var pv = ((byte)(Permission) << 1) | (HasHistory ? 1 : 0);

    //    if (Inherited)
    //        pv |= 0x80;

    //    //if (WriteAnnotation != null && ReadAnnotation != null)
    //    //{
    //    //    var rexp = DC.ToBytes(ReadAnnotation);
    //    //    var wexp = DC.ToBytes(WriteAnnotation);
    //    //    return new BinaryList()
    //    //        .AddUInt8((byte)(0x38 | pv))
    //    //        .AddUInt8((byte)name.Length)
    //    //        .AddUInt8Array(name)
    //    //        .AddUInt8Array(ValueType.Compose())
    //    //        .AddInt32(wexp.Length)
    //    //        .AddUInt8Array(wexp)
    //    //        .AddInt32(rexp.Length)
    //    //        .AddUInt8Array(rexp)
    //    //        .ToArray();
    //    //}
    //    //else if (WriteAnnotation != null)
    //    //{
    //    //    var wexp = DC.ToBytes(WriteAnnotation);
    //    //    return new BinaryList()
    //    //        .AddUInt8((byte)(0x30 | pv))
    //    //        .AddUInt8((byte)name.Length)
    //    //        .AddUInt8Array(name)
    //    //        .AddUInt8Array(ValueType.Compose())
    //    //        .AddInt32(wexp.Length)
    //    //        .AddUInt8Array(wexp)
    //    //        .ToArray();
    //    //}
    //    //else if (ReadAnnotation != null)
    //    //{
    //    //    var rexp = DC.ToBytes(ReadAnnotation);
    //    //    return new BinaryList()
    //    //        .AddUInt8((byte)(0x28 | pv))
    //    //        .AddUInt8((byte)name.Length)
    //    //        .AddUInt8Array(name)
    //    //        .AddUInt8Array(ValueType.Compose())
    //    //        .AddInt32(rexp.Length)
    //    //        .AddUInt8Array(rexp)
    //    //        .ToArray();
    //    //}
    //    if (Annotations != null)
    //    {
    //        var rexp = Codec.Compose(Annotations, connection.Instance.Warehouse, connection);
    //        return new BinaryList()
    //            .AddUInt8((byte)(0x28 | pv))
    //            .AddUInt8((byte)name.Length)
    //            .AddUInt8Array(name)
    //            .AddUInt8Array(ValueType.Compose(connection))
    //            .AddUInt8Array(rexp)
    //            .ToArray();
    //    }
    //    else
    //    {
    //        return new BinaryList()
    //            .AddUInt8((byte)(0x20 | pv))
    //            .AddUInt8((byte)name.Length)
    //            .AddUInt8Array(name)
    //            .AddUInt8Array(ValueType.Compose(connection))
    //            .ToArray();
    //    }
    //}

 

    public static PropertyDef MakePropertyDef(Warehouse warehouse, Type type, PropertyInfo pi, string name, byte index, TypeDef typeDef)
    {
        var genericPropType = pi.PropertyType.IsGenericType ? pi.PropertyType.GetGenericTypeDefinition() : null;
        // @TODO: need to check if the type is remote

        var propType = genericPropType == typeof(PropertyContext<>) ?
                Tru.FromType(pi.PropertyType.GetGenericArguments()[0], warehouse) :
                Tru.FromType(pi.PropertyType, warehouse);

        if (propType == null)
            throw new Exception($"Unsupported type `{pi.PropertyType}` in property `{type.Name}.{pi.Name}`");

        var annotationAttrs = pi.GetCustomAttributes<AnnotationAttribute>(true);
        var historicalAttr = pi.GetCustomAttribute<HistoricalAttribute>(true);

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


        return new PropertyDef()
        {
            Name = name,
            Index = index,
            Inherited = pi.DeclaringType != type,
            ValueType = propType,
            PropertyInfo = pi,
            Historical = historicalAttr == null,
            Annotations = annotations,
        };

 
    }

    public static PropertyDef MakePropertyDef(
        Warehouse warehouse, Type type, PropertyInfo pi, string name, byte index,
        PropertyPermission permission, TypeDef typeDef)
    {
        var definition = MakePropertyDef(warehouse, type, pi, name, index, typeDef);
        definition.Permission = permission;
        return definition;
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
