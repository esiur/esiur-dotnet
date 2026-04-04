using Esiur.Core;
using Esiur.Data.Types;
using Esiur.Resource;
using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;

#nullable enable

namespace Esiur.Data
{
    public class Tru
    {

        static TruIdentifier[] refTypes = new TruIdentifier[]
          {
                    TruIdentifier.Dynamic,
                    TruIdentifier.RawData,
                    TruIdentifier.String,
                    TruIdentifier.Resource,
                    TruIdentifier.Record,
                    TruIdentifier.Map,
                    TruIdentifier.List,
                    TruIdentifier.TypedList,
                    TruIdentifier.TypedMap,
                    TruIdentifier.Tuple2,
                    TruIdentifier.Tuple3,
                    TruIdentifier.Tuple4,
                    TruIdentifier.Tuple5,
                    TruIdentifier.Tuple6,
                    TruIdentifier.Tuple7,
                    TruIdentifier.TypedRecord,
                    TruIdentifier.TypedResource
          };

        static Map<TduIdentifier, TruIdentifier> typesMap = new Map<TduIdentifier, TruIdentifier>()
        {
            [TduIdentifier.UInt8] = TruIdentifier.UInt8,
            [TduIdentifier.Int8] = TruIdentifier.Int8,
            [TduIdentifier.UInt16] = TruIdentifier.UInt16,
            [TduIdentifier.Int16] = TruIdentifier.Int16,
            [TduIdentifier.UInt32] = TruIdentifier.UInt32,
            [TduIdentifier.Int32] = TruIdentifier.Int32,
            [TduIdentifier.UInt64] = TruIdentifier.UInt64,
            [TduIdentifier.Int64] = TruIdentifier.Int64,
            [TduIdentifier.UInt128] = TruIdentifier.UInt128,
            [TduIdentifier.Int128] = TruIdentifier.Int128,
            [TduIdentifier.Char8] = TruIdentifier.Char,
            [TduIdentifier.DateTime] = TruIdentifier.DateTime,
            [TduIdentifier.Float32] = TruIdentifier.Float32,
            [TduIdentifier.Float64] = TruIdentifier.Float64,
            [TduIdentifier.Decimal128] = TruIdentifier.Decimal,
            [TduIdentifier.False] = TruIdentifier.Bool,
            [TduIdentifier.True] = TruIdentifier.Bool,
            [TduIdentifier.Map] = TruIdentifier.Map,
            [TduIdentifier.List] = TruIdentifier.List,
            [TduIdentifier.RawData] = TruIdentifier.RawData,
            [TduIdentifier.Record] = TruIdentifier.Record,
            [TduIdentifier.String] = TruIdentifier.String,
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

            if (Identifier == TruIdentifier.TypedList)
            {
                var sub = SubTypes?[0].GetRuntimeType(warehouse);
                if (sub == null)
                    return null;

                var rt = sub.MakeArrayType();

                return rt;
            }
            else if (Identifier == TruIdentifier.TypedMap)
            {
                var subs = SubTypes.Select(x => x.GetRuntimeType(warehouse)).ToArray();
                var rt = typeof(Map<,>).MakeGenericType(subs);
                return rt;
            }

            return Identifier switch
            {
                (TruIdentifier.Void) => typeof(void),
                (TruIdentifier.Dynamic) => typeof(object),
                (TruIdentifier.Bool) => Nullable ? typeof(bool?) : typeof(bool),
                (TruIdentifier.Char) => Nullable ? typeof(char?) : typeof(char),
                (TruIdentifier.UInt8) => Nullable ? typeof(byte?) : typeof(byte),
                (TruIdentifier.Int8) => Nullable ? typeof(sbyte?) : typeof(sbyte),
                (TruIdentifier.Int16) => Nullable ? typeof(short?) : typeof(short),
                (TruIdentifier.UInt16) => Nullable ? typeof(ushort?) : typeof(ushort),
                (TruIdentifier.Int32) => Nullable ? typeof(int?) : typeof(int),
                (TruIdentifier.UInt32) => Nullable ? typeof(uint?) : typeof(uint),
                (TruIdentifier.Int64) => Nullable ? typeof(ulong?) : typeof(long),
                (TruIdentifier.UInt64) => Nullable ? typeof(ulong?) : typeof(ulong),
                (TruIdentifier.Float32) => Nullable ? typeof(float?) : typeof(float),
                (TruIdentifier.Float64) => Nullable ? typeof(double?) : typeof(double),
                (TruIdentifier.Decimal) => Nullable ? typeof(decimal?) : typeof(decimal),
                (TruIdentifier.String) => typeof(string),
                (TruIdentifier.DateTime) => Nullable ? typeof(DateTime?) : typeof(DateTime),
                (TruIdentifier.Resource) => typeof(IResource),
                (TruIdentifier.Record) => typeof(IRecord),
                (TruIdentifier.TypedRecord) => warehouse.GetTypeDefById((Uuid)UUID!, TypeDefKind.Record)?.DefinedType,
                (TruIdentifier.TypedResource) => warehouse.GetTypeDefById((Uuid)UUID!, TypeDefKind.Resource)?.DefinedType,
                (TruIdentifier.Enum) => warehouse.GetTypeDefById((Uuid)UUID!, TypeDefKind.Enum)?.DefinedType,

                _ => null
            };
        }

        public TruIdentifier Identifier;
        public bool Nullable;
        public Uuid? UUID;
        //public RepresentationType? SubType1; // List + Map
        //public RepresentationType? SubType2; // Map
        //public RepresentationType? SubType3; // No types yet

        public Tru[]? SubTypes = null;

        public Tru ToNullable()
        {
            return new Tru(Identifier, true, UUID, SubTypes);
        }

        public bool IsTyped()
        {
            if (Identifier == TruIdentifier.TypedList && SubTypes[0].Identifier == TruIdentifier.UInt8)
                return false;

            if (Identifier == TruIdentifier.TypedResource)
                return false;

            return (UUID != null) || (SubTypes != null && SubTypes.Length > 0);
        }

        public bool Match(Tru other)
        {
            //if (UUID == null && (SubTypes == null || SubTypes.Length == 0))
            //    return false;

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


        public (TduIdentifier, byte[]) GetMetadata()
        {
            switch (Identifier)
            {
                case TruIdentifier.TypedList:
                    return (TduIdentifier.TypedList, SubTypes[0].Compose());
                case TruIdentifier.TypedRecord:
                    return (TduIdentifier.Record, UUID?.Data);
                case TruIdentifier.TypedMap:
                    return (TduIdentifier.TypedMap,
                        SubTypes[0].Compose().Concat(SubTypes[1].Compose()).ToArray());
                case TruIdentifier.Enum:
                    return (TduIdentifier.TypedEnum, UUID?.Data);

                default:

                    throw new NotImplementedException();
            }
        }

        //public TDUIdentifier GetTDUIdentifer()
        //{
        //    switch (Identifier)
        //    {

        //        case TRUIdentifier.TypedList: return TDUIdentifier.TypedList

        //        case TRUIdentifier.Int8: return TDUIdentifier.Int8;
        //        case TRUIdentifier.Int16: return TDUIdentifier.Int16;
        //        case TRUIdentifier.Int32: return TDUIdentifier.Int32;
        //        case TRUIdentifier.Int64: return TDUIdentifier.Int64;

        //        case TRUIdentifier.UInt8: return TDUIdentifier.UInt8;
        //        case TRUIdentifier.UInt16: return TDUIdentifier.UInt16;
        //        case TRUIdentifier.UInt32: return TDUIdentifier.UInt32;
        //        case TRUIdentifier.UInt64: return TDUIdentifier.UInt64;

        //        case TRUIdentifier.String: return TDUIdentifier.String;
        //        case TRUIdentifier.Float32: return TDUIdentifier.Float32;
        //        case TRUIdentifier.Float64: return TDUIdentifier.Float64;
        //        case TRUIdentifier.            }
        //}

        private static Dictionary<Type, Tru> _cache = new Dictionary<Type, Tru>();

        public static Tru? FromType(Type type)
        {
            if (type == null)
                return new Tru(TruIdentifier.Void, true);

            if (_cache.ContainsKey(type))
                return _cache[type];

            var nullable = false;

            var nullType = System.Nullable.GetUnderlyingType(type);

            if (nullType != null)
            {
                type = nullType;
                nullable = true;
            }

            Tru? tru = null;

            if (type == typeof(IResource))
            {
                return new Tru(TruIdentifier.Resource, nullable);
            }
            else if (type == typeof(IRecord) || type == typeof(Record))
            {
                return new Tru(TruIdentifier.Record, nullable);
            }
            else if (type == typeof(Map<object, object>)
                || type == typeof(Dictionary<object, object>)) 
            { 
                return new Tru(TruIdentifier.Map, nullable);
            }
            else if (Codec.ImplementsInterface(type, typeof(IResource)))
            {
                tru = new Tru(
                   TruIdentifier.TypedResource,
                   nullable,
                   TypeDef.GetTypeUUID(type)
                );
            }
            else if (Codec.ImplementsInterface(type, typeof(IRecord)))
            {
                tru = new Tru(
                   TruIdentifier.TypedRecord,
                   nullable,
                   TypeDef.GetTypeUUID(type)
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
                        tru = new Tru(TruIdentifier.List, nullable);
                    }
                    else
                    {
                        var subType = FromType(args[0]);
                        if (subType == null) // unrecongnized type
                            return null;


                        tru = new Tru(TruIdentifier.TypedList, nullable, null,
                            new Tru[] { subType });

                    }
                }
                else if (genericType == typeof(Map<,>)
                    || genericType == typeof(Dictionary<,>))
                {
                    var args = type.GetGenericArguments();
                    if (args[0] == typeof(object) && args[1] == typeof(object))
                    {
                        tru = new Tru(TruIdentifier.Map, nullable);
                    }
                    else
                    {
                        var subType1 = FromType(args[0]);
                        if (subType1 == null)
                            return null;

                        var subType2 = FromType(args[1]);
                        if (subType2 == null)
                            return null;

                        tru = new Tru(TruIdentifier.TypedMap, nullable, null,
                           new Tru[] { subType1, subType2 });

                    }
                }
                else if (genericType == typeof(ResourceLink<>))
                {
                    var args = type.GetGenericArguments();

                    return FromType(args[0]);
                }
                else if (genericType == typeof(ValueTuple<,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }


                    tru = new Tru(TruIdentifier.Tuple2, nullable, null, subTypes);

                }
                else if (genericType == typeof(ValueTuple<,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new Tru(TruIdentifier.Tuple3, nullable, null, subTypes);

                }
                else if (genericType == typeof(ValueTuple<,,,>))
                {

                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new Tru(TruIdentifier.Tuple4, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new Tru(TruIdentifier.Tuple5, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new Tru(TruIdentifier.Tuple6, nullable, null, subTypes);
                }
                else if (genericType == typeof(ValueTuple<,,,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i]);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new Tru(TruIdentifier.Tuple7, nullable, null, subTypes);
                }
                else
                    return null;
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == typeof(object))
                    tru = new Tru(TruIdentifier.List, nullable);
                else
                {
                    var subType = FromType(elementType);

                    if (subType == null)
                        return null;

                    tru = new Tru(TruIdentifier.TypedList, nullable, null,
                        new Tru[] { subType });

                }
            }
            else if (type.IsEnum)
            {
                tru = new Tru(TruIdentifier.Enum, nullable, TypeDef.GetTypeUUID(type));
            }
            else if (type.IsInterface)
            {
                return null; // other interfaces are not supported
            }

            //else if (typeof(Structure).IsAssignableFrom(t) || t == typeof(ExpandoObject) => TRUIdentifier.Structure)
            //{

            //}

            if (tru != null)
            {
                _cache.Add(type, tru);
                return tru;
            }

            // last check
            return type switch
            {
                _ when type == typeof(void) => new Tru(TruIdentifier.Void, nullable),
                _ when type == typeof(object) => new Tru(TruIdentifier.Dynamic, nullable),
                _ when type == typeof(bool) => new Tru(TruIdentifier.Bool, nullable),
                _ when type == typeof(char) => new Tru(TruIdentifier.Char, nullable),
                _ when type == typeof(byte) => new Tru(TruIdentifier.UInt8, nullable),
                _ when type == typeof(sbyte) => new Tru(TruIdentifier.Int8, nullable),
                _ when type == typeof(short) => new Tru(TruIdentifier.Int16, nullable),
                _ when type == typeof(ushort) => new Tru(TruIdentifier.UInt16, nullable),
                _ when type == typeof(int) => new Tru(TruIdentifier.Int32, nullable),
                _ when type == typeof(uint) => new Tru(TruIdentifier.UInt32, nullable),
                _ when type == typeof(long) => new Tru(TruIdentifier.Int64, nullable),
                _ when type == typeof(ulong) => new Tru(TruIdentifier.UInt64, nullable),
                _ when type == typeof(float) => new Tru(TruIdentifier.Float32, nullable),
                _ when type == typeof(double) => new Tru(TruIdentifier.Float64, nullable),
                _ when type == typeof(decimal) => new Tru(TruIdentifier.Decimal, nullable),
                _ when type == typeof(string) => new Tru(TruIdentifier.String, nullable),
                _ when type == typeof(DateTime) => new Tru(TruIdentifier.DateTime, nullable),
                _ when type == typeof(ResourceLink) => new Tru(TruIdentifier.Resource, nullable),
                _ => null
            };

        }

        public Tru(TruIdentifier identifier, bool nullable, Uuid? uuid = null, Tru[]? subTypes = null)
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

        public static (uint, Tru) Parse(byte[] data, uint offset)
        {
            var oOffset = offset;

            var header = data[offset++];
            bool nullable = (header & 0x80) > 0;
            var identifier = (TruIdentifier)(header & 0x7F);


            if ((header & 0x40) > 0)
            {

                var hasUUID = (header & 0x4) > 0;
                var subsCount = (header >> 3) & 0x7;

                Uuid? uuid = null;

                if (hasUUID)
                {
                    uuid = data.GetUUID(offset);
                    offset += 16;
                }

                var subs = new Tru[subsCount];

                for (var i = 0; i < subsCount; i++)
                {
                    (var len, subs[i]) = Tru.Parse(data, offset);
                    offset += len;
                }

                return (offset - oOffset, new Tru(identifier, nullable, uuid, subs));
            }
            else
            {
                return (1, new Tru(identifier, nullable));
            }
        }

    }
}