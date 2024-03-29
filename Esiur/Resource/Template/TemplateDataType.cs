﻿//using Esiur.Data;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Dynamic;
//using System.Linq;
//using System.Text;

//namespace Esiur.Resource.Template;
//public struct TemplateDataType
//{
//    public DataType Type { get; set; }
//    //public string TypeName { get; set; }
//    public TypeTemplate TypeTemplate => TypeGuid == null ? null : Warehouse.GetTemplateByClassId((Guid)TypeGuid);

//    public Guid? TypeGuid { get; set; }

//    public bool IsNullable { get; set; }
//    //public TemplateDataType(DataType type, string typeName)
//    //{
//    //    Type = type;
//    //    TypeName = typeName;
//    //}



//    public static TemplateDataType FromType(Type type)
//    {


//        bool isList = typeof(ICollection).IsAssignableFrom(type);

//        var t = type switch
//        {

//            { IsArray: true } => type.GetElementType(),
//            { IsEnum: true } => type.GetEnumUnderlyingType(),
//            _ when isList => Codec.GetGenericListType(type), 
//            (_) => type
//        };

//        DataType dt = t switch
//        {
//            _ when t == typeof(bool) => DataType.Bool,
//            _ when t == typeof(char) => DataType.Char,
//            _ when t == typeof(byte) => DataType.UInt8,
//            _ when t == typeof(sbyte) => DataType.Int8,
//            _ when t == typeof(short) => DataType.Int16,
//            _ when t == typeof(ushort) => DataType.UInt16,
//            _ when t == typeof(int) => DataType.Int32,
//            _ when t == typeof(uint) => DataType.UInt32,
//            _ when t == typeof(long) => DataType.Int64,
//            _ when t == typeof(ulong) => DataType.UInt64,
//            _ when t == typeof(float) => DataType.Float32,
//            _ when t == typeof(double) => DataType.Float64,
//            _ when t == typeof(decimal) => DataType.Decimal,
//            _ when t == typeof(string) => DataType.String,
//            _ when t == typeof(DateTime) => DataType.DateTime,
//            _ when t == typeof(IResource) => DataType.Void, // Dynamic resource (unspecified type)
//            _ when t == typeof(IRecord) => DataType.Void, // Dynamic record (unspecified type)
//            _ when typeof(Structure).IsAssignableFrom(t) || t == typeof(ExpandoObject) => DataType.Structure,
//            _ when Codec.ImplementsInterface(t, typeof(IResource)) => DataType.Resource,
//            _ when Codec.ImplementsInterface(t, typeof(IRecord)) => DataType.Record,
//            _ => DataType.Void
//        };


//        Guid? typeGuid = null;

//        if (dt == DataType.Resource || dt == DataType.Record)
//            typeGuid = TypeTemplate.GetTypeGuid(t);

//        if (type.IsArray || isList)
//            dt = (DataType)((byte)dt | 0x80);

        
//        return new TemplateDataType()
//        {
//            Type = dt,
//            TypeGuid = typeGuid,
//            IsNullable = Nullable.GetUnderlyingType(type) != null
//        };
//    }

//    public byte[] Compose()
//    {
//        if (Type == DataType.Resource ||
//            Type == DataType.ResourceArray ||
//            Type == DataType.Record ||
//            Type == DataType.RecordArray)
//        {
//            var guid = DC.ToBytes((Guid)TypeGuid);
//            if (IsNullable)
//            {
//                return new BinaryList()
//                    .AddUInt8((byte)((byte)Type | 0x40))
//                    .AddUInt8Array(guid).ToArray();
//            } else
//            {
//                return new BinaryList()
//                    .AddUInt8((byte)Type)
//                    .AddUInt8Array(guid).ToArray();
//            }
//        }
//        else if (IsNullable)
//            return new byte[] { (byte)((byte)Type | 0x40) };
//        else
//            return new byte[] { (byte)Type };
//    }

//    public override string ToString() => Type.ToString() + (IsNullable ? "?":"" )
//                    + TypeTemplate != null ? "<" + TypeTemplate.ClassName + ">" : "";


//    public static (uint, TemplateDataType) Parse(byte[] data, uint offset)
//    {
//        bool isNullable = (data[offset] & 0x40) > 0;
//        var type = (DataType)(data[offset++] & 0xBF);
        
//        if (type == DataType.Resource ||
//            type == DataType.ResourceArray ||
//            type == DataType.Record ||
//            type == DataType.RecordArray)
//        {
//            var guid = data.GetGuid(offset);
//            return (17, new TemplateDataType() { Type = type, TypeGuid = guid , IsNullable = isNullable});
//        }
//        else
//            return (1, new TemplateDataType() { Type = type, IsNullable = isNullable });
//    }
//}
