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
    public class TRU
    {

        static TRUIdentifier[] refTypes = new TRUIdentifier[]
          {
                    TRUIdentifier.Dynamic,
                    TRUIdentifier.RawData,
                    TRUIdentifier.String,
                    TRUIdentifier.Resource,
                    TRUIdentifier.Record,
                    TRUIdentifier.Map,
                    TRUIdentifier.List,
                    TRUIdentifier.TypedList,
                    TRUIdentifier.TypedMap,
                    TRUIdentifier.Tuple2,
                    TRUIdentifier.Tuple3,
                    TRUIdentifier.Tuple4,
                    TRUIdentifier.Tuple5,
                    TRUIdentifier.Tuple6,
                    TRUIdentifier.Tuple7,
                    TRUIdentifier.TypedRecord,
                    TRUIdentifier.TypedResource
          };

        static Map<TDUIdentifier, TRUIdentifier> typesMap = new Map<TDUIdentifier, TRUIdentifier>()
        {
            [TDUIdentifier.UInt8] = TRUIdentifier.UInt8,
            [TDUIdentifier.Int8] = TRUIdentifier.Int8,
            [TDUIdentifier.UInt16] = TRUIdentifier.UInt16,
            [TDUIdentifier.Int16] = TRUIdentifier.Int16,
            [TDUIdentifier.UInt32] = TRUIdentifier.UInt32,
            [TDUIdentifier.Int32] = TRUIdentifier.Int32,
            [TDUIdentifier.UInt64] = TRUIdentifier.UInt64,
            [TDUIdentifier.Int64] = TRUIdentifier.Int64,
            [TDUIdentifier.UInt128] = TRUIdentifier.UInt128,
            [TDUIdentifier.Int128] = TRUIdentifier.Int128,
            [TDUIdentifier.Char8] = TRUIdentifier.Char,
            [TDUIdentifier.DateTime] = TRUIdentifier.DateTime,
            [TDUIdentifier.Float32] = TRUIdentifier.Float32,
            [TDUIdentifier.Float64] = TRUIdentifier.Float64,
            [TDUIdentifier.Decimal128] = TRUIdentifier.Decimal,
            [TDUIdentifier.False] = TRUIdentifier.Bool,
            [TDUIdentifier.True] = TRUIdentifier.Bool,
            [TDUIdentifier.Map] = TRUIdentifier.Map,
            [TDUIdentifier.List] = TRUIdentifier.List,
            [TDUIdentifier.RawData] = TRUIdentifier.RawData,
            [TDUIdentifier.Record] = TRUIdentifier.Record,
            [TDUIdentifier.String] = TRUIdentifier.String,
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

            if (Identifier == TRUIdentifier.TypedList)
            {
                var sub = SubTypes?[0].GetRuntimeType(warehouse);
                if (sub == null)
                    return null;

                var rt = sub.MakeArrayType();

                return rt;
            }
            else if (Identifier == TRUIdentifier.TypedMap)
            {
                var subs = SubTypes.Select(x => x.GetRuntimeType(warehouse)).ToArray();
                var rt = typeof(Map<,>).MakeGenericType(subs);
                return rt;
            }
            
            return Identifier switch
            {
                (TRUIdentifier.Void) => typeof(void),
                (TRUIdentifier.Dynamic) => typeof(object),
                (TRUIdentifier.Bool) => Nullable ? typeof(bool?) : typeof(bool),
                (TRUIdentifier.Char) => Nullable ? typeof(char?) : typeof(char),
                (TRUIdentifier.UInt8) => Nullable ? typeof(byte?) : typeof(byte),
                (TRUIdentifier.Int8) => Nullable ? typeof(sbyte?) : typeof(sbyte),
                (TRUIdentifier.Int16) => Nullable ? typeof(short?) : typeof(short),
                (TRUIdentifier.UInt16) => Nullable ? typeof(ushort?) : typeof(ushort),
                (TRUIdentifier.Int32) => Nullable ? typeof(int?) : typeof(int),
                (TRUIdentifier.UInt32) => Nullable ? typeof(uint?) : typeof(uint),
                (TRUIdentifier.Int64) => Nullable ? typeof(ulong?) : typeof(long),
                (TRUIdentifier.UInt64) => Nullable ? typeof(ulong?) : typeof(ulong),
                (TRUIdentifier.Float32) => Nullable ? typeof(float?) : typeof(float),
                (TRUIdentifier.Float64) => Nullable ? typeof(double?) : typeof(double),
                (TRUIdentifier.Decimal) => Nullable ? typeof(decimal?) : typeof(decimal),
                (TRUIdentifier.String) => typeof(string),
                (TRUIdentifier.DateTime) => Nullable ? typeof(DateTime?) : typeof(DateTime),
                (TRUIdentifier.Resource) => typeof(IResource),
                (TRUIdentifier.Record) => typeof(IRecord),
                (TRUIdentifier.TypedRecord) => warehouse.GetTemplateByClassId((UUID)UUID!, TemplateType.Record)?.DefinedType,
                (TRUIdentifier.TypedResource) => warehouse.GetTemplateByClassId((UUID)UUID!, TemplateType.Resource)?.DefinedType,
                (TRUIdentifier.Enum) =>warehouse.GetTemplateByClassId((UUID)UUID!, TemplateType.Enum)?.DefinedType,

                _ => null
            };
        }

        public TRUIdentifier Identifier;
        public bool Nullable;
        public UUID? UUID;
        //public RepresentationType? SubType1; // List + Map
        //public RepresentationType? SubType2; // Map
        //public RepresentationType? SubType3; // No types yet

        public TRU[]? SubTypes = null;

        public TRU ToNullable()
        {
            return new TRU(Identifier, true, UUID, SubTypes);
        }

        public bool Match(TRU other)
        {
            if (other.Identifier != Identifier)
                return false;
            if (other.UUID != UUID)
                return false;

            if (other.SubTypes != null)
            {
                if (other.SubTypes.Length != (SubTypes?.Length ?? -1))
                    return false;

                for (var i = 0; i < SubTypes?.Length; i++)
                    if (!SubTypes[i].Match(other.SubTypes[i]))
                        return false;
            }

            return true;
        }

        public static TRU? FromType(Type type)
        {
            if (type == null)
                return new TRU(TRUIdentifier.Void, true);

            var nullable = false;

            var nullType = System.Nullable.GetUnderlyingType(type);

            if (nullType != null)
            {
                type = nullType;
                nullable = true;
            }

            if (type == typeof(IResource))
                return new TRU(TRUIdentifier.Resource, nullable);
            else if (type == typeof(IRecord) || type == typeof(Record))
                return new TRU(TRUIdentifier.Record, nullable);
            else if (type == typeof(Map<object, object>)
                || type == typeof(Dictionary<object, object>))
                return new TRU(TRUIdentifier.Map, nullable);
            else if (Codec.ImplementsInterface(type, typeof(IResource)))
            {
                return new TRU(
                   TRUIdentifier.TypedResource,
                   nullable,
                   TypeTemplate.GetTypeUUID(type)
                );
            }
            else if (Codec.ImplementsInterface(type, typeof(IRecord)))
            {
                return new TRU(
                   TRUIdentifier.TypedRecord,
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
                        return new TRU(TRUIdentifier.List, nullable);
                    }
                    else
                    {
                        var subType = FromType(args[0]);
                        if (subType == null) // unrecongnized type
                            return null;

                        return new TRU(TRUIdentifier.TypedList, nullable, null,
                            new TRU[] { subType });

                    }
                }
                else if (genericType == typeof(Map<,>)
                    || genericType == typeof(Dictionary<,>))
                {
                    var args = type.GetGenericArguments();
                    if (args[0] == typeof(object) && args[1] == typeof(object))
                    {
                        return new TRU(TRUIdentifier.Map, nullable);
                    }
                    else
                    {
                        var subType1 = FromType(args[0]);
                        if (subType1 == null)
                            return null;

                        var subType2 = FromType(args[1]);
                        if (subType2 == null)
                            return null;

                        return new TRU(TRUIdentifier.TypedMap, nullable, null,
                            new TRU[] { subType1, subType2 });
                    }
                }
                else if (genericType == typeof(ValueTuple<,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new TRU[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new TRU(TRUIdentifier.Tuple2, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new TRU[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new TRU(TRUIdentifier.Tuple3, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,>))
                {

                    var args = type.GetGenericArguments();
                    var subTypes = new TRU[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new TRU(TRUIdentifier.Tuple4, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new TRU[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new TRU(TRUIdentifier.Tuple5, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new TRU[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new TRU(TRUIdentifier.Tuple6, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new TRU[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    return new TRU(TRUIdentifier.Tuple7, nullable, null, subTypes);
                }
                else
                    return null;
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == typeof(object))
                    return new TRU(TRUIdentifier.List, nullable);
                else
                {
                    var subType = FromType(elementType);

                    if (subType == null)
                        return null;

                    return new TRU(TRUIdentifier.TypedList, nullable, null,
                        new TRU[] { subType });

                }
            }
            else if (type.IsEnum)
            {
                return new TRU(TRUIdentifier.Enum, nullable, TypeTemplate.GetTypeUUID(type));
            }
            else if (type.IsInterface)
            {
                return null; // other interfaces are not supported
            }

            //else if (typeof(Structure).IsAssignableFrom(t) || t == typeof(ExpandoObject) => TRUIdentifier.Structure)
            //{

            //}

            return type switch
            {
                _ when type == typeof(void) => new TRU(TRUIdentifier.Void, nullable),
                _ when type == typeof(object) => new TRU(TRUIdentifier.Dynamic, nullable),
                _ when type == typeof(bool) => new TRU(TRUIdentifier.Bool, nullable),
                _ when type == typeof(char) => new TRU(TRUIdentifier.Char, nullable),
                _ when type == typeof(byte) => new TRU(TRUIdentifier.UInt8, nullable),
                _ when type == typeof(sbyte) => new TRU(TRUIdentifier.Int8, nullable),
                _ when type == typeof(short) => new TRU(TRUIdentifier.Int16, nullable),
                _ when type == typeof(ushort) => new TRU(TRUIdentifier.UInt16, nullable),
                _ when type == typeof(int) => new TRU(TRUIdentifier.Int32, nullable),
                _ when type == typeof(uint) => new TRU(TRUIdentifier.UInt32, nullable),
                _ when type == typeof(long) => new TRU(TRUIdentifier.Int64, nullable),
                _ when type == typeof(ulong) => new TRU(TRUIdentifier.UInt64, nullable),
                _ when type == typeof(float) => new TRU(TRUIdentifier.Float32, nullable),
                _ when type == typeof(double) => new TRU(TRUIdentifier.Float64, nullable),
                _ when type == typeof(decimal) => new TRU(TRUIdentifier.Decimal, nullable),
                _ when type == typeof(string) => new TRU(TRUIdentifier.String, nullable),
                _ when type == typeof(DateTime) => new TRU(TRUIdentifier.DateTime, nullable),
                _ => null
            };

        }

        public TRU(TRUIdentifier identifier, bool nullable, UUID? uuid = null, TRU[]? subTypes = null)
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

        public static (uint, TRU) Parse(byte[] data, uint offset)
        {
            var oOffset = offset;

            var header = data[offset++];
            bool nullable = (header & 0x80) > 0;
            var identifier = (TRUIdentifier)(header & 0x7F);


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

                var subs = new TRU[subsCount];

                for (var i = 0; i < subsCount; i++)
                {
                    (var len, subs[i]) = TRU.Parse(data, offset);
                    offset += len;
                }

                return (offset - oOffset, new TRU(identifier, nullable, uuid, subs));
            }
            else
            {
                return (1, new TRU(identifier, nullable));
            }
        }

    }
}