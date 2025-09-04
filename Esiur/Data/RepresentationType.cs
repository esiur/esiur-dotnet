using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Resource;
using Esiur.Resource.Template;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

#nullable enable

namespace Esiur.Data
{
    public enum RepresentationTypeIdentifier
    {
        Void = 0x0,
        Dynamic = 0x1,
        Bool = 0x2,
        UInt8,
        Int8,
        Char,
        UInt16,
        Int16,
        UInt32,
        Int32,
        Float32,
        UInt64,
        Int64,
        Float64,
        DateTime,
        UInt128,
        Int128,
        Decimal,
        String,
        RawData,
        Resource,
        Record,
        List,
        Map,
        Enum = 0x44,
        TypedResource = 0x45, // Followed by UUID
        TypedRecord = 0x46, // Followed by UUID
        TypedList = 0x48, // Followed by element type
        Tuple2 = 0x50, // Followed by element type
        TypedMap = 0x51, // Followed by key type and value type
        Tuple3 = 0x58,
        Tuple4 = 0x60,
        Tuple5 = 0x68,
        Tuple6 = 0x70,
        Tuple7 = 0x78
    }

    public class RepresentationType
    {

        static RepresentationTypeIdentifier[] refTypes = new RepresentationTypeIdentifier[]
          {
                    RepresentationTypeIdentifier.Dynamic,
                    RepresentationTypeIdentifier.RawData,
                    RepresentationTypeIdentifier.String,
                    RepresentationTypeIdentifier.Resource,
                    RepresentationTypeIdentifier.Record,
                    RepresentationTypeIdentifier.Map,
                    RepresentationTypeIdentifier.List,
                    RepresentationTypeIdentifier.TypedList,
                    RepresentationTypeIdentifier.TypedMap,
                    RepresentationTypeIdentifier.Tuple2,
                    RepresentationTypeIdentifier.Tuple3,
                    RepresentationTypeIdentifier.Tuple4,
                    RepresentationTypeIdentifier.Tuple5,
                    RepresentationTypeIdentifier.Tuple6,
                    RepresentationTypeIdentifier.Tuple7,
                    RepresentationTypeIdentifier.TypedRecord,
                    RepresentationTypeIdentifier.TypedResource
          };

        static Map<TDUIdentifier, RepresentationTypeIdentifier> typesMap = new Map<TDUIdentifier, RepresentationTypeIdentifier>()
        {
            [TDUIdentifier.UInt8] = RepresentationTypeIdentifier.UInt8,
            [TDUIdentifier.Int8] = RepresentationTypeIdentifier.Int8,
            [TDUIdentifier.UInt16] = RepresentationTypeIdentifier.UInt16,
            [TDUIdentifier.Int16] = RepresentationTypeIdentifier.Int16,
            [TDUIdentifier.UInt32] = RepresentationTypeIdentifier.UInt32,
            [TDUIdentifier.Int32] = RepresentationTypeIdentifier.Int32,
            [TDUIdentifier.UInt64] = RepresentationTypeIdentifier.UInt64,
            [TDUIdentifier.Int64] = RepresentationTypeIdentifier.Int64,
            [TDUIdentifier.UInt128] = RepresentationTypeIdentifier.UInt128,
            [TDUIdentifier.Int128] = RepresentationTypeIdentifier.Int128,
            [TDUIdentifier.Char8] = RepresentationTypeIdentifier.Char,
            [TDUIdentifier.DateTime] = RepresentationTypeIdentifier.DateTime,
            [TDUIdentifier.Float32] = RepresentationTypeIdentifier.Float32,
            [TDUIdentifier.Float64] = RepresentationTypeIdentifier.Float64,
            [TDUIdentifier.Decimal128] = RepresentationTypeIdentifier.Decimal,
            [TDUIdentifier.False] = RepresentationTypeIdentifier.Bool,
            [TDUIdentifier.True] = RepresentationTypeIdentifier.Bool,
            [TDUIdentifier.Map] = RepresentationTypeIdentifier.Map,
            [TDUIdentifier.List] = RepresentationTypeIdentifier.List,
            [TDUIdentifier.RawData] = RepresentationTypeIdentifier.RawData,
            [TDUIdentifier.Record] = RepresentationTypeIdentifier.Record,
            [TDUIdentifier.String] = RepresentationTypeIdentifier.String,
        };

        

        public void SetNull(List<byte> flags)
        {
            if (refTypes.Contains(Identifier))
            {
                Nullable = (flags.FirstOrDefault() == 2);
                if (flags.Count > 0)
                    flags.RemoveAt(0);
            }

            if (SubTypes != null)
                foreach (var st in SubTypes)
                    st.SetNull(flags);
        }

        public void SetNull(byte flag)
        {
            if (refTypes.Contains(Identifier))
            {
                Nullable = (flag == 2);
            }

            if (SubTypes != null)
                foreach (var st in SubTypes)
                    st.SetNull(flag);
        }


        public void SetNotNull(List<byte> flags)
        {
            if (refTypes.Contains(Identifier))
            {
                Nullable = (flags.FirstOrDefault() != 1);
                if (flags.Count > 0)
                    flags.RemoveAt(0);
            }

            if (SubTypes != null)
                foreach (var st in SubTypes)
                    st.SetNotNull(flags);
        }


        public override string ToString()
        {
            if (SubTypes != null && SubTypes.Length > 0)
                return Identifier.ToString() + "<" + String.Join(",", SubTypes.Select(x => x.ToString())) + ">" + (Nullable ? "?" : "");
            return Identifier.ToString() + (Nullable ? "?" : "");
        }
        public void SetNotNull(byte flag)
        {
            if (refTypes.Contains(Identifier))
            {
                Nullable = (flag != 1);
            }

            if (SubTypes != null)
                foreach (var st in SubTypes)
                    st.SetNotNull(flag);
        }

        public Type? GetRuntimeType(Warehouse warehouse)
        {

            if (Identifier == RepresentationTypeIdentifier.TypedList)
            {
                var sub = SubTypes?[0].GetRuntimeType(warehouse);
                if (sub == null)
                    return null;

                var rt = sub.MakeArrayType();

                return rt;
            }
            else if (Identifier == RepresentationTypeIdentifier.TypedMap)
            {
                var subs = SubTypes.Select(x => x.GetRuntimeType(warehouse)).ToArray();
                var rt = typeof(Map<,>).MakeGenericType(subs);
                return rt;
            }
            
            return Identifier switch
            {
                (RepresentationTypeIdentifier.Void) => typeof(void),
                (RepresentationTypeIdentifier.Dynamic) => typeof(object),
                (RepresentationTypeIdentifier.Bool) => Nullable ? typeof(bool?) : typeof(bool),
                (RepresentationTypeIdentifier.Char) => Nullable ? typeof(char?) : typeof(char),
                (RepresentationTypeIdentifier.UInt8) => Nullable ? typeof(byte?) : typeof(byte),
                (RepresentationTypeIdentifier.Int8) => Nullable ? typeof(sbyte?) : typeof(sbyte),
                (RepresentationTypeIdentifier.Int16) => Nullable ? typeof(short?) : typeof(short),
                (RepresentationTypeIdentifier.UInt16) => Nullable ? typeof(ushort?) : typeof(ushort),
                (RepresentationTypeIdentifier.Int32) => Nullable ? typeof(int?) : typeof(int),
                (RepresentationTypeIdentifier.UInt32) => Nullable ? typeof(uint?) : typeof(uint),
                (RepresentationTypeIdentifier.Int64) => Nullable ? typeof(ulong?) : typeof(long),
                (RepresentationTypeIdentifier.UInt64) => Nullable ? typeof(ulong?) : typeof(ulong),
                (RepresentationTypeIdentifier.Float32) => Nullable ? typeof(float?) : typeof(float),
                (RepresentationTypeIdentifier.Float64) => Nullable ? typeof(double?) : typeof(double),
                (RepresentationTypeIdentifier.Decimal) => Nullable ? typeof(decimal?) : typeof(decimal),
                (RepresentationTypeIdentifier.String) => typeof(string),
                (RepresentationTypeIdentifier.DateTime) => Nullable ? typeof(DateTime?) : typeof(DateTime),
                (RepresentationTypeIdentifier.Resource) => typeof(IResource),
                (RepresentationTypeIdentifier.Record) => typeof(IRecord),
                (RepresentationTypeIdentifier.TypedRecord) => warehouse.GetTemplateByClassId((UUID)UUID!, TemplateType.Record)?.DefinedType,
                (RepresentationTypeIdentifier.TypedResource) => warehouse.GetTemplateByClassId((UUID)UUID!, TemplateType.Resource)?.DefinedType,
                (RepresentationTypeIdentifier.Enum) =>warehouse.GetTemplateByClassId((UUID)UUID!, TemplateType.Enum)?.DefinedType,

                _ => null
            };
        }

        public RepresentationTypeIdentifier Identifier;
        public bool Nullable;
        public UUID? UUID;
        //public RepresentationType? SubType1; // List + Map
        //public RepresentationType? SubType2; // Map
        //public RepresentationType? SubType3; // No types yet

        public RepresentationType[]? SubTypes = null;

        public RepresentationType ToNullable()
        {
            return new RepresentationType(Identifier, true, UUID, SubTypes);
        }

        public static RepresentationType? FromType(Type type)
        {

            var nullable = false;

            var nullType = System.Nullable.GetUnderlyingType(type);

            if (nullType != null)
            {
                type = nullType;
                nullable = true;
            }

            if (type == typeof(IResource))
                return new RepresentationType(RepresentationTypeIdentifier.Resource, nullable);
            else if (type == typeof(IRecord) || type == typeof(Record))
                return new RepresentationType(RepresentationTypeIdentifier.Record, nullable);
            else if (type == typeof(Map<object, object>)
                || type == typeof(Dictionary<object, object>))
                return new RepresentationType(RepresentationTypeIdentifier.Map, nullable);
            else if (Codec.ImplementsInterface(type, typeof(IResource)))
            {
                return new RepresentationType(
                   RepresentationTypeIdentifier.TypedResource,
                   nullable,
                   TypeTemplate.GetTypeUUID(type)
                );
            }
            else if (Codec.ImplementsInterface(type, typeof(IRecord)))
            {
                return new RepresentationType(
                   RepresentationTypeIdentifier.TypedRecord,
                   nullable,
                   TypeTemplate.GetTypeUUID(type)
                );
            }
            else if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(List<>)
                    || genericType == typeof(VarList<>)
                    || genericType == typeof(IList<>))
                {
                    var args = type.GetGenericArguments();
                    if (args[0] == typeof(object))
                    {
                        return new RepresentationType(RepresentationTypeIdentifier.List, nullable);
                    }
                    else
                    {
                        var subType = FromType(args[0]);
                        if (subType == null) // unrecongnized type
                            return null;

                        return new RepresentationType(RepresentationTypeIdentifier.TypedList, nullable, null,
                            new RepresentationType[] { subType });

                    }
                }
                else if (genericType == typeof(Map<,>)
                    || genericType == typeof(Dictionary<,>))
                {
                    var args = type.GetGenericArguments();
                    if (args[0] == typeof(object) && args[1] == typeof(object))
                    {
                        return new RepresentationType(RepresentationTypeIdentifier.Map, nullable);
                    }
                    else
                    {
                        var subType1 = FromType(args[0]);
                        if (subType1 == null)
                            return null;

                        var subType2 = FromType(args[1]);
                        if (subType2 == null)
                            return null;

                        return new RepresentationType(RepresentationTypeIdentifier.TypedMap, nullable, null,
                            new RepresentationType[] { subType1, subType2 });
                    }
                }
                //else if (genericType == typeof(AsyncReply<>))
                //{
                //    var args = type.GetGenericArguments();
                //    return FromType(args[0]);
                //}
                //else if (genericType == typeof(DistributedPropertyContext<>))
                //{
                //    var args = type.GetGenericArguments();
                //    return FromType(args[0]);
                //}
                else if (genericType == typeof(ValueTuple<,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new RepresentationType[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new RepresentationType(RepresentationTypeIdentifier.Tuple2, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new RepresentationType[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new RepresentationType(RepresentationTypeIdentifier.Tuple3, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,>))
                {

                    var args = type.GetGenericArguments();
                    var subTypes = new RepresentationType[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new RepresentationType(RepresentationTypeIdentifier.Tuple4, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new RepresentationType[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new RepresentationType(RepresentationTypeIdentifier.Tuple5, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new RepresentationType[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new RepresentationType(RepresentationTypeIdentifier.Tuple6, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new RepresentationType[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new RepresentationType(RepresentationTypeIdentifier.Tuple7, nullable, null, subTypes);
                }
                else
                    return null;
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == typeof(object))
                    return new RepresentationType(RepresentationTypeIdentifier.List, nullable);
                else
                {
                    var subType = FromType(elementType);

                    if (subType == null)
                        return null;

                    return new RepresentationType(RepresentationTypeIdentifier.TypedList, nullable, null,
                        new RepresentationType[] { subType });

                }
            }
            else if (type.IsEnum)
            {
                return new RepresentationType(RepresentationTypeIdentifier.Enum, nullable, TypeTemplate.GetTypeUUID(type));
            }
            else if (type.IsInterface)
            {
                return null; // other interfaces are not supported
            }

            //else if (typeof(Structure).IsAssignableFrom(t) || t == typeof(ExpandoObject) => RepresentationTypeIdentifier.Structure)
            //{

            //}

            return type switch
            {
                _ when type == typeof(void) => new RepresentationType(RepresentationTypeIdentifier.Void, nullable),
                _ when type == typeof(object) => new RepresentationType(RepresentationTypeIdentifier.Dynamic, nullable),
                _ when type == typeof(bool) => new RepresentationType(RepresentationTypeIdentifier.Bool, nullable),
                _ when type == typeof(char) => new RepresentationType(RepresentationTypeIdentifier.Char, nullable),
                _ when type == typeof(byte) => new RepresentationType(RepresentationTypeIdentifier.UInt8, nullable),
                _ when type == typeof(sbyte) => new RepresentationType(RepresentationTypeIdentifier.Int8, nullable),
                _ when type == typeof(short) => new RepresentationType(RepresentationTypeIdentifier.Int16, nullable),
                _ when type == typeof(ushort) => new RepresentationType(RepresentationTypeIdentifier.UInt16, nullable),
                _ when type == typeof(int) => new RepresentationType(RepresentationTypeIdentifier.Int32, nullable),
                _ when type == typeof(uint) => new RepresentationType(RepresentationTypeIdentifier.UInt32, nullable),
                _ when type == typeof(long) => new RepresentationType(RepresentationTypeIdentifier.Int64, nullable),
                _ when type == typeof(ulong) => new RepresentationType(RepresentationTypeIdentifier.UInt64, nullable),
                _ when type == typeof(float) => new RepresentationType(RepresentationTypeIdentifier.Float32, nullable),
                _ when type == typeof(double) => new RepresentationType(RepresentationTypeIdentifier.Float64, nullable),
                _ when type == typeof(decimal) => new RepresentationType(RepresentationTypeIdentifier.Decimal, nullable),
                _ when type == typeof(string) => new RepresentationType(RepresentationTypeIdentifier.String, nullable),
                _ when type == typeof(DateTime) => new RepresentationType(RepresentationTypeIdentifier.DateTime, nullable),
                _ => null
            };

        }

        public RepresentationType(RepresentationTypeIdentifier identifier, bool nullable, UUID? uuid = null, RepresentationType[]? subTypes = null)
        {
            Nullable = nullable;
            Identifier = identifier;
            UUID = uuid;
            SubTypes = subTypes;
        }

        public byte[] Compose()
        {
            var rt = new BinaryList();

            if (Nullable)
                rt.AddUInt8((byte)(0x80 | (byte)Identifier));
            else
                rt.AddUInt8((byte)Identifier);

            if (UUID != null)
                rt.AddUInt8Array(UUID.Value.Data);

            if (SubTypes != null)
                for (var i = 0; i < SubTypes.Length; i++)
                    rt.AddUInt8Array(SubTypes[i].Compose());

            return rt.ToArray();
        }


        //public override string ToString() => Identifier.ToString() + (Nullable ? "?" : "")
        //      + TypeTemplate != null ? "<" + TypeTemplate.ClassName + ">" : "";


        public static (uint, RepresentationType) Parse(byte[] data, uint offset)
        {
            var oOffset = offset;

            var header = data[offset++];
            bool nullable = (header & 0x80) > 0;
            var identifier = (RepresentationTypeIdentifier)(header & 0x7F);


            if ((header & 0x40) > 0)
            {

                var hasUUID = (header & 0x4) > 0;
                var subsCount = (header >> 3) & 0x7;

                UUID? uuid = null;

                if (hasUUID)
                {
                    uuid = data.GetUUID(offset);
                    offset += 16;
                }

                var subs = new RepresentationType[subsCount];

                for (var i = 0; i < subsCount; i++)
                {
                    (var len, subs[i]) = RepresentationType.Parse(data, offset);
                    offset += len;
                }

                return (offset - oOffset, new RepresentationType(identifier, nullable, uuid, subs));
            }
            else
            {
                return (1, new RepresentationType(identifier, nullable));
            }
        }

    }
}