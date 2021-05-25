using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace Esiur.Resource.Template
{
    public struct TemplateDataType
    {
        public DataType Type { get; set; }
        //public string TypeName { get; set; }
        public  ResourceTemplate TypeTemplate => TypeGuid == null ? null : Warehouse.GetTemplate((Guid)TypeGuid);

        public Guid? TypeGuid { get; set; }
        //public TemplateDataType(DataType type, string typeName)
        //{
        //    Type = type;
        //    TypeName = typeName;
        //}



        public static TemplateDataType FromType(Type type)
        {

            var t = type switch
            {
                { IsArray: true } => type.GetElementType(),
                { IsEnum: true } => type.GetEnumUnderlyingType(),
                (_) => type
            };

            DataType dt = t switch
            {
                _ when t == typeof(bool) => DataType.Bool,
                _ when t == typeof(char) => DataType.Char,
                _ when t == typeof(byte) => DataType.UInt8,
                _ when t == typeof(sbyte) => DataType.Int8,
                _ when t == typeof(short) => DataType.Int16,
                _ when t == typeof(ushort) => DataType.UInt16,
                _ when t == typeof(int) => DataType.Int32,
                _ when t == typeof(uint) => DataType.UInt32,
                _ when t == typeof(long) => DataType.Int64,
                _ when t == typeof(ulong) => DataType.UInt64,
                _ when t == typeof(float) => DataType.Float32,
                _ when t == typeof(double) => DataType.Float64,
                _ when t == typeof(decimal) => DataType.Decimal,
                _ when t == typeof(string) => DataType.String,
                _ when t == typeof(DateTime) => DataType.DateTime,
                _ when t == typeof(IResource) => DataType.Void, // Dynamic resource (unspecified type)
                _ when typeof(Structure).IsAssignableFrom(t) || t == typeof(ExpandoObject) => DataType.Structure,
                _ when Codec.ImplementsInterface(t, typeof(IResource)) => DataType.Resource,
                _ => DataType.Void
            };


            //string tn = dt switch
            //{
            //    DataType.Resource => t.FullName,
            //    DataType.Structure when t != typeof(Structure)  => t.FullName,
            //    _ => null
            //};

            Guid? typeGuid = null;

            if (dt == DataType.Resource)
                typeGuid = ResourceTemplate.GetTypeGuid(t);

            if (type.IsArray)
                dt = (DataType)((byte)dt | 0x80);

            return new TemplateDataType()
            {
                Type = dt,
                TypeGuid = typeGuid
            };
        }

        public byte[] Compose()
        {
            if (Type == DataType.Resource ||
                Type == DataType.ResourceArray)//||
                                               //Type == DataType.DistributedResource ||
                                               //Type == DataType.DistributedResourceArray ||
                                               //Type == DataType.Structure ||
                                               //Type == DataType.StructureArray)
            {
                var guid = DC.ToBytes((Guid)TypeGuid);
                return new BinaryList()
                    .AddUInt8((byte)Type)
                    .AddUInt8Array(guid).ToArray();
            }
            else
                return new byte[] { (byte)Type };
        }

        public override string ToString() => Type.ToString() + TypeTemplate != null ? "<" + TypeTemplate.ClassName + ">" : "";


        public static (uint, TemplateDataType) Parse(byte[] data, uint offset)
        {
            var type = (DataType)data[offset++];
            if (type == DataType.Resource ||
                type == DataType.ResourceArray)//||
                                               // type == DataType.DistributedResource ||
                                               // type == DataType.DistributedResourceArray)// ||
                                               // type == DataType.Structure ||
                                               // type == DataType.StructureArray)
            {
                var guid = DC.GetGuid(data, offset);
                return (17, new TemplateDataType() { Type = type, TypeGuid = guid });
            }
            else
                return (1, new TemplateDataType() { Type = type });
        }
    }
}
