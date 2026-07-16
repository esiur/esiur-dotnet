using Esiur.Core;
using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;

#nullable enable

namespace Esiur.Data
{
    public abstract class Tru
    {

        protected static TruIdentifier[] RefTypes = new TruIdentifier[]
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
                    TruIdentifier.LocalType8,
                    TruIdentifier.LocalType16,
                    TruIdentifier.LocalType32,
                    TruIdentifier.LocalType64,
                    TruIdentifier.RemoteType8,
                    TruIdentifier.RemoteType16,
                    TruIdentifier.RemoteType32,
                    TruIdentifier.RemoteType64,
          };

        protected static Dictionary<TruIdentifier, Type> TypesMapping = new Dictionary<TruIdentifier, Type>()
        {

            [TruIdentifier.Void] = typeof(void),
            [TruIdentifier.Bool] = typeof(bool),
            [TruIdentifier.Char] = typeof(char),
            [TruIdentifier.UInt8] = typeof(byte),
            [TruIdentifier.Int8] = typeof(sbyte),
            [TruIdentifier.Int16] = typeof(short),
            [TruIdentifier.UInt16] = typeof(ushort),
            [TruIdentifier.Int32] = typeof(int),
            [TruIdentifier.UInt32] = typeof(uint),
            [TruIdentifier.Int64] = typeof(long),
            [TruIdentifier.UInt64] = typeof(ulong),
            [TruIdentifier.Float32] = typeof(float),
            [TruIdentifier.Float64] = typeof(double),
            [TruIdentifier.Decimal] = typeof(decimal),
            [TruIdentifier.String] = typeof(string),
            [TruIdentifier.DateTime] = typeof(DateTime),
            [TruIdentifier.Resource] = typeof(IResource),
            [TruIdentifier.Record] = typeof(IRecord),
            [TruIdentifier.Dynamic] = typeof(object),
            [TruIdentifier.List] = typeof(object[]),
        };

        protected static Dictionary<TruIdentifier, Type> NullableTypesMapping = new Dictionary<TruIdentifier, Type>()
        {

            [TruIdentifier.Void] = typeof(void),
            [TruIdentifier.Bool] = typeof(bool?),
            [TruIdentifier.Char] = typeof(char?),
            [TruIdentifier.UInt8] = typeof(byte?),
            [TruIdentifier.Int8] = typeof(sbyte?),
            [TruIdentifier.Int16] = typeof(short?),
            [TruIdentifier.UInt16] = typeof(ushort?),
            [TruIdentifier.Int32] = typeof(int?),
            [TruIdentifier.UInt32] = typeof(uint?),
            [TruIdentifier.Int64] = typeof(long?),
            [TruIdentifier.UInt64] = typeof(ulong?),
            [TruIdentifier.Float32] = typeof(float?),
            [TruIdentifier.Float64] = typeof(double?),
            [TruIdentifier.Decimal] = typeof(decimal?),
            [TruIdentifier.String] = typeof(string),
            [TruIdentifier.DateTime] = typeof(DateTime?),
            [TruIdentifier.Resource] = typeof(IResource),
            [TruIdentifier.Record] = typeof(IRecord),
            [TruIdentifier.Dynamic] = typeof(object),
            [TruIdentifier.List] = typeof(object[]),
        };




        //static Map<TduIdentifier, TruIdentifier> typesMap = new Map<TduIdentifier, TruIdentifier>()
        //{
        //    [TduIdentifier.UInt8] = TruIdentifier.UInt8,
        //    [TduIdentifier.Int8] = TruIdentifier.Int8,
        //    [TduIdentifier.UInt16] = TruIdentifier.UInt16,
        //    [TduIdentifier.Int16] = TruIdentifier.Int16,
        //    [TduIdentifier.UInt32] = TruIdentifier.UInt32,
        //    [TduIdentifier.Int32] = TruIdentifier.Int32,
        //    [TduIdentifier.UInt64] = TruIdentifier.UInt64,
        //    [TduIdentifier.Int64] = TruIdentifier.Int64,
        //    [TduIdentifier.UInt128] = TruIdentifier.UInt128,
        //    [TduIdentifier.Int128] = TruIdentifier.Int128,
        //    [TduIdentifier.Char8] = TruIdentifier.Char,
        //    [TduIdentifier.DateTime] = TruIdentifier.DateTime,
        //    [TduIdentifier.Float32] = TruIdentifier.Float32,
        //    [TduIdentifier.Float64] = TruIdentifier.Float64,
        //    [TduIdentifier.Decimal128] = TruIdentifier.Decimal,
        //    [TduIdentifier.False] = TruIdentifier.Bool,
        //    [TduIdentifier.True] = TruIdentifier.Bool,
        //    [TduIdentifier.Map] = TruIdentifier.Map,
        //    [TduIdentifier.List] = TruIdentifier.List,
        //    [TduIdentifier.RawData] = TruIdentifier.RawData,
        //    [TduIdentifier.Record] = TruIdentifier.Record,
        //    [TduIdentifier.String] = TruIdentifier.String,
        //};


        public abstract void SetNull(List<byte> flags);
        public abstract void SetNull(byte flag);


        public override bool Equals(object obj)
        {
            // check if the object is a Tru
            if (obj is Tru other)
                return Match(other);

            return false;
        }

        public override int GetHashCode()
        {
            // Equality is defined by Match, which always requires a matching Identifier
            // (composites additionally compare sub-types). Hashing on Identifier therefore
            // keeps equal Trus in the same bucket and honours the Equals/GetHashCode contract.
            return (int)Identifier;
        }

        public abstract void SetNotNull(List<byte> flags);

        public abstract void SetNotNull(byte flag);

        public abstract Type RuntimeType { get; }

        //public Type? GetRuntimeType(Warehouse warehouse, string domain)
        //{
        //    if (Identifier == TruIdentifier.TypedList)
        //    {
        //        var sub = SubTypes?[0].GetRuntimeType(warehouse, domain);
        //        if (sub == null)
        //            return null;

        //        var rt = sub.MakeArrayType();
        //        return rt;
        //    }
        //    else if (Identifier == TruIdentifier.TypedMap)
        //    {
        //        var subs = SubTypes.Select(x => x.GetRuntimeType(warehouse, domain)).ToArray();
        //        var rt = typeof(Map<,>).MakeGenericType(subs);

        //        return rt;
        //    }

        //    return Identifier switch
        //    {
        //        TruIdentifier.Void => typeof(void),
        //        TruIdentifier.Dynamic => typeof(object),
        //        TruIdentifier.Bool => Nullable ? typeof(bool?) : typeof(bool),
        //        TruIdentifier.Char => Nullable ? typeof(char?) : typeof(char),
        //        TruIdentifier.UInt8 => Nullable ? typeof(byte?) : typeof(byte),
        //        TruIdentifier.Int8 => Nullable ? typeof(sbyte?) : typeof(sbyte),
        //        TruIdentifier.Int16 => Nullable ? typeof(short?) : typeof(short),
        //        TruIdentifier.UInt16 => Nullable ? typeof(ushort?) : typeof(ushort),
        //        TruIdentifier.Int32 => Nullable ? typeof(int?) : typeof(int),
        //        TruIdentifier.UInt32 => Nullable ? typeof(uint?) : typeof(uint),
        //        TruIdentifier.Int64 => Nullable ? typeof(long?) : typeof(long),
        //        TruIdentifier.UInt64 => Nullable ? typeof(ulong?) : typeof(ulong),
        //        TruIdentifier.Float32 => Nullable ? typeof(float?) : typeof(float),
        //        TruIdentifier.Float64 => Nullable ? typeof(double?) : typeof(double),
        //        TruIdentifier.Decimal => Nullable ? typeof(decimal?) : typeof(decimal),
        //        TruIdentifier.String => typeof(string),
        //        TruIdentifier.DateTime => Nullable ? typeof(DateTime?) : typeof(DateTime),
        //        TruIdentifier.Resource => typeof(IResource),
        //        TruIdentifier.Record => typeof(IRecord),

        //        TruIdentifier.LocalType8
        //        or TruIdentifier.LocalType16
        //        or TruIdentifier.LocalType32
        //        or TruIdentifier.LocalType64 => (TypeDef as LocalTypeDef).DefinedType,// TypeDefId == null ? throw new Exception("TypeDef not set.")
        //                                                                              // : warehouse.GetLocalTypeDefById(TypeDefId.Value.Value).DefinedType,
        //        TruIdentifier.RemoteType8
        //        or TruIdentifier.RemoteType16
        //        or TruIdentifier.RemoteType32
        //        or TruIdentifier.RemoteType64 => (TypeDef as RemoteTypeDef).ProxyType,// TypeDefId == null ? throw new Exception("TypeDef not set.")
        //                                                                              // : warehouse.GetRemoteTypeDefById(domain, TypeDefId.Value.Value).ProxyType,
        //                                                                              //(TruIdentifier.TypedRecord) => warehouse.GetTypeDefById((Uuid)UUID!, TypeDefKind.Record)?.DefinedType,
        //                                                                              //(TruIdentifier.TypedResource) => warehouse.GetTypeDefById((Uuid)UUID!, TypeDefKind.Resource)?.DefinedType,
        //                                                                              //(TruIdentifier.Enum) => warehouse.GetTypeDefById((Uuid)UUID!, TypeDefKind.Enum)?.DefinedType,
        //        _ => null
        //    };
        //}

        public TruIdentifier Identifier;
        public bool Nullable;

        //public TypeDefId? TypeDefId;

        //public Uuid? UUID;
        //public RepresentationType? SubType1; // List + Map
        //public RepresentationType? SubType2; // Map
        //public RepresentationType? SubType3; // No types yet


        public abstract Tru ToNullable();
        //{
        //    //return new Tru(Identifier, true, TypeDefId, TypeDef, SubTypes);
        //    return new Tru(Identifier, true, TypeDef, SubTypes);
        //}

        //public bool IsTyped()
        //{
        //    if (Identifier == TruIdentifier.TypedList && SubTypes[0].Identifier == TruIdentifier.UInt8)
        //        return false;

        //    if (Identifier == TruIdentifier.TypedResource)
        //        return false;

        //    return (TypeDefId != null) || (SubTypes != null && SubTypes.Length > 0);
        //}

        public abstract bool Match(Tru other);


        //public TduIdentifier MapToTduIdentifier()
        //{
        //    switch (Identifier)
        //    {
        //        case TruIdentifier.TypedList:
        //            return TduIdentifier.TypedList;//, SubTypes[0].Compose());
        //        case TruIdentifier.LocalType16:
        //            return TduIdentifier.Record, UUID?.Data;
        //        case TruIdentifier.TypedMap:
        //            return (TduIdentifier.TypedMap,
        //                SubTypes[0].Compose().Concat(SubTypes[1].Compose()).ToArray());
        //        case TruIdentifier.Enum:
        //            return (TduIdentifier.TypedEnum, UUID?.Data);

        //        default:

        //            throw new NotImplementedException();
        //    }
        //}

        //public (TduIdentifier, byte[]) GetMetadata()
        //{
        //    switch (Identifier)
        //    {
        //        case TruIdentifier.TypedList:
        //            return (TduIdentifier.TypedList, SubTypes[0].Compose());
        //        case TruIdentifier.TypedRecord:
        //            return (TduIdentifier.Record, UUID?.Data);
        //        case TruIdentifier.TypedMap:
        //            return (TduIdentifier.TypedMap,
        //                SubTypes[0].Compose().Concat(SubTypes[1].Compose()).ToArray());
        //        case TruIdentifier.Enum:
        //            return (TduIdentifier.TypedEnum, UUID?.Data);

        //        default:

        //            throw new NotImplementedException();
        //    }
        //}

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

        //private static Dictionary<Type, Tru> cache = new Dictionary<Type, Tru>();
        //private static object cacheLook = new object();

        /// <summary>
        /// Builds the type-representation unit (Tru) describing how a CLR type maps onto the
        /// wire, recursing into element/key/value/field types for collections, maps and tuples.
        /// Results are memoized per warehouse since this is reflection-heavy and hot during
        /// serialization; returned Tru instances are immutable and safe to share.
        /// </summary>
        public static Tru? FromType(Type type, Warehouse warehouse)
        {
            // null maps to Void and cannot be a dictionary key, so compute it directly.
            if (type == null)
                return FromTypeCore(null, warehouse);

            if (warehouse.TypeRepresentationCache.TryGetValue(type, out var cached))
                return cached;

            var tru = FromTypeCore(type, warehouse);

            // Cache only fully-built results. Unrecognized types return null (or throw),
            // which we leave uncached so a later type registration can still resolve them.
            if (tru != null)
                warehouse.TypeRepresentationCache[type] = tru;

            return tru;
        }

        static Tru? FromTypeCore(Type? type, Warehouse warehouse)
        {
            if (type == null)
                return new TruPrimitive(TruIdentifier.Void, true, typeof(void));


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
                return new TruPrimitive(TruIdentifier.Resource, nullable, typeof(IResource));
            }
            else if (type == typeof(IRecord) || type == typeof(Record))
            {
                return new TruPrimitive(TruIdentifier.Record, nullable, typeof(IRecord));
            }
            else if (type == typeof(Map<object, object>)
                || type == typeof(Dictionary<object, object>))
            {
                return new TruPrimitive(TruIdentifier.Map, nullable, type);
            }
            else if (Codec.ImplementsInterface(type, typeof(IResource))
                || Codec.ImplementsInterface(type, typeof(IRecord))
                || type.IsEnum)
            {

                var remoteAttr = type.GetCustomAttribute<RemoteAttribute>();

                if (remoteAttr != null)
                {
                    var typeDef = warehouse.FindProxyTypeDef(remoteAttr.FullName, remoteAttr.Domains);

                    return new TruTypeDef(nullable, typeDef);
                }
                else
                {
                    var typeDef = warehouse.GetLocalTypeDefByType(type);

                    if (typeDef == null)
                        throw new Exception("Unregistered type: " + type.FullName + ".");

                    return new TruTypeDef(nullable, typeDef);
                }
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
                        tru = new TruPrimitive(TruIdentifier.List, nullable, typeof(object[]));
                    }
                    else
                    {
                        var subType = FromType(args[0], warehouse);

                        if (subType == null) // unrecongnized type
                            throw new Exception("Unrecognized type: " + args[0].FullName);

                        return new TruComposite(TruIdentifier.TypedList, nullable,
                            new Tru[] { subType }, type);

                    }
                }
                else if (genericType == typeof(Map<,>)
                    || genericType == typeof(Dictionary<,>))
                {
                    var args = type.GetGenericArguments();
                    if (args[0] == typeof(object) && args[1] == typeof(object))
                    {
                        tru = new TruPrimitive(TruIdentifier.Map, nullable, type);
                    }
                    else
                    {
                        var subType1 = FromType(args[0], warehouse);
                        if (subType1 == null)
                            return null;

                        var subType2 = FromType(args[1], warehouse);
                        if (subType2 == null)
                            return null;

                        tru = new TruComposite(TruIdentifier.TypedMap, nullable,
                           new Tru[] { subType1, subType2 }, type);

                    }
                }
                else if (genericType == typeof(ResourceLink<>))
                {
                    var args = type.GetGenericArguments();

                    return FromType(args[0], warehouse);
                }
                else if (genericType == typeof(ValueTuple<,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i], warehouse);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }


                    tru = new TruComposite(TruIdentifier.Tuple2, nullable, subTypes, type);

                }
                else if (genericType == typeof(ValueTuple<,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i], warehouse);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new TruComposite(TruIdentifier.Tuple3, nullable, subTypes, type);

                }
                else if (genericType == typeof(ValueTuple<,,,>))
                {

                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i], warehouse);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new TruComposite(TruIdentifier.Tuple4, nullable, subTypes, type);
                }
                else if (genericType == typeof(ValueTuple<,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i], warehouse);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new TruComposite(TruIdentifier.Tuple5, nullable, subTypes, type);
                }
                else if (genericType == typeof(ValueTuple<,,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i], warehouse);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new TruComposite(TruIdentifier.Tuple6, nullable, subTypes, type);
                }
                else if (genericType == typeof(ValueTuple<,,,,,,>))
                {
                    var args = type.GetGenericArguments();
                    var subTypes = new Tru[args.Length];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var t = FromType(args[i], warehouse);
                        if (t == null)
                            return null;
                        subTypes[i] = t;
                    }

                    tru = new TruComposite(TruIdentifier.Tuple7, nullable, subTypes, type);
                }
                else
                    return null;
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == typeof(object))
                    tru = new TruPrimitive(TruIdentifier.List, nullable, type);
                else
                {
                    var subType = FromType(elementType, warehouse);

                    if (subType == null)
                        return null;

                    tru = new TruComposite(TruIdentifier.TypedList, nullable, new Tru[] { subType }, type);
                }
            }
            //else if (type.IsEnum)
            //{
            //    tru = new Tru(TruIdentifier.Enum, nullable, TypeDef.GetTypeUUID(type));
            //}
            else if (type.IsInterface)
            {
                return null; // other interfaces are not supported
            }

            //else if (typeof(Structure).IsAssignableFrom(t) || t == typeof(ExpandoObject) => TRUIdentifier.Structure)
            //{

            //}

            if (tru != null)
            {
                // cache.Add(type, tru);
                return tru;
            }

            // last check
            return type switch
            {
                _ when type == typeof(void) => new TruPrimitive(TruIdentifier.Void, nullable, type),
                _ when type == typeof(object) => new TruPrimitive(TruIdentifier.Dynamic, nullable, type),
                _ when type == typeof(bool) => new TruPrimitive(TruIdentifier.Bool, nullable, type),
                _ when type == typeof(char) => new TruPrimitive(TruIdentifier.Char, nullable, type),
                _ when type == typeof(byte) => new TruPrimitive(TruIdentifier.UInt8, nullable, type),
                _ when type == typeof(sbyte) => new TruPrimitive(TruIdentifier.Int8, nullable, type),
                _ when type == typeof(short) => new TruPrimitive(TruIdentifier.Int16, nullable, type),
                _ when type == typeof(ushort) => new TruPrimitive(TruIdentifier.UInt16, nullable, type),
                _ when type == typeof(int) => new TruPrimitive(TruIdentifier.Int32, nullable, type),
                _ when type == typeof(uint) => new TruPrimitive(TruIdentifier.UInt32, nullable, type),
                _ when type == typeof(long) => new TruPrimitive(TruIdentifier.Int64, nullable, type),
                _ when type == typeof(ulong) => new TruPrimitive(TruIdentifier.UInt64, nullable, type),
                _ when type == typeof(float) => new TruPrimitive(TruIdentifier.Float32, nullable, type),
                _ when type == typeof(double) => new TruPrimitive(TruIdentifier.Float64, nullable, type),
                _ when type == typeof(decimal) => new TruPrimitive(TruIdentifier.Decimal, nullable, type),
                _ when type == typeof(string) => new TruPrimitive(TruIdentifier.String, nullable, type),
                _ when type == typeof(DateTime) => new TruPrimitive(TruIdentifier.DateTime, nullable, type),
                _ when type == typeof(ResourceLink) => new TruPrimitive(TruIdentifier.Resource, nullable, type),
                _ => null
            };
            //}
        }

        //public Tru(TruIdentifier identifier, bool nullable, TypeDef? typeDef = null, Tru[]? subTypes = null)
        //{
        //    Nullable = nullable;
        //    Identifier = identifier;
        //    //TypeDefId = typeDefId;
        //    SubTypes = subTypes;
        //    TypeDef = typeDef;

        //    if (typeDef == null && (identifier >= TruIdentifier.LocalType8 && identifier <= TruIdentifier.RemoteType64))// TypeDefId != null)
        //        throw new Exception("TypeDef must be provided.");
        //}

        public abstract byte[] Compose(EpConnection connection);

        //{
        //    var rt = new BinaryList();

        //    if (Nullable)
        //        rt.AddUInt8((byte)(0x80 | (byte)Identifier));
        //    else
        //        rt.AddUInt8((byte)Identifier);

        //    if (Identifier == TruIdentifier.LocalType8
        //        || Identifier == TruIdentifier.RemoteType8)
        //    {
        //        if (TypeDef == null)
        //        {
        //            throw new Exception("TypeDefId is required for LocalType8 and RemoteType8");
        //        }
        //        else if (TypeDef is LocalTypeDef localTypeDef)
        //        {
        //            rt.AddUInt8((byte)localTypeDef.Id);

        //        }
        //        else if (TypeDef is RemoteTypeDef remoteTypeDef)
        //        {
        //            rt.AddUInt8( (byte)remoteTypeDef.Id);
        //        }
        //        //                rt.AddUInt8((byte)TypeDefId?.Value!);

        //    }
        //    else if (Identifier == TruIdentifier.LocalType16
        //        || Identifier == TruIdentifier.RemoteType16)
        //    {
        //        if (TypeDefId == null)
        //            throw new Exception("TypeDefId is required for LocalType16 and RemoteType16");

        //        rt.AddUInt16((ushort)TypeDefId?.Value!);
        //    }
        //    else if (Identifier == TruIdentifier.LocalType32
        //        || Identifier == TruIdentifier.RemoteType32)
        //    {
        //        if (TypeDefId == null)
        //            throw new Exception("TypeDefId is required for LocalType32 and RemoteType32");
        //        rt.AddUInt32((uint)TypeDefId?.Value!);
        //    }
        //    else if (Identifier == TruIdentifier.LocalType64
        //        || Identifier == TruIdentifier.RemoteType64)
        //    {
        //        if (TypeDefId == null)
        //            throw new Exception("TypeDefId is required for LocalType64 and RemoteType64");
        //        rt.AddUInt64((ulong)TypeDefId?.Value!);
        //    }

        //    // TODO: don't rely on SubTypes length, but on the identifier
        //    if (SubTypes != null)
        //        for (var i = 0; i < SubTypes.Length; i++)
        //            rt.AddUInt8Array(SubTypes[i].Compose());

        //    return rt.ToArray();
        //}

        public static IParseResult<Tru> Parse(byte[] data, uint offset, Warehouse warehouse)
            => Parse(data, offset, warehouse, 1);

        private static IParseResult<Tru> Parse(
            byte[] data,
            uint offset,
            Warehouse warehouse,
            int depth)
        {
            ParserGuard.EnsureTypeMetadataDepth(warehouse, depth);

            var oOffset = offset;

            var header = data[offset++];
            bool nullable = (header & 0x80) > 0;
            var identifier = (TruIdentifier)(header & 0x7F);

            if ((header & 0x40) > 0)
            {
                var subsCount = (header >> 3) & 0x7;

                if (subsCount == 0)
                {
                    ulong typeDefId = 0;
                    bool isRemote = false;

                    if (identifier == TruIdentifier.LocalType8)
                    {
                        typeDefId = data[offset];
                        offset += 1;
                    }
                    else if (identifier == TruIdentifier.RemoteType8)
                    {
                        typeDefId = data[offset];
                        isRemote = true;
                        offset += 1;
                    }
                    else if (identifier == TruIdentifier.LocalType16)
                    {
                        typeDefId = data.GetUInt16(offset, Endian.Little);
                        offset += 2;
                    }
                    else if (identifier == TruIdentifier.RemoteType16)
                    {
                        typeDefId = data.GetUInt16(offset, Endian.Little);
                        isRemote = true;
                        offset += 2;
                    }
                    else if (identifier == TruIdentifier.LocalType32)
                    {
                        typeDefId = data.GetUInt32(offset, Endian.Little);
                        offset += 4;
                    }
                    else if (identifier == TruIdentifier.RemoteType32)
                    {
                        typeDefId = data.GetUInt32(offset, Endian.Little);
                        offset += 4;
                    }
                    else if (identifier == TruIdentifier.LocalType64)
                    {
                        typeDefId = data.GetUInt64(offset, Endian.Little);
                        offset += 8;
                    }
                    else if (identifier == TruIdentifier.RemoteType64)
                    {
                        typeDefId = data.GetUInt64(offset, Endian.Little);
                        isRemote = true;
                        offset += 8;
                    }
                    else
                    {
                        throw new Exception("Invalid identifier.");
                    }

                    // try to get the typedef from the warehouse.

                    if (isRemote)
                    {
                        throw new Exception("Unsupported operation.");
                    }
                    else
                    {
                        var td = warehouse.GetLocalTypeDefById(typeDefId);

                        if (td == null)
                            throw new Exception("TypeDef not found.");

                        return new ParseResult<TruTypeDef>(
                                                new TruTypeDef(nullable, td),
                                                offset - oOffset);
                    }
                }
                else
                {
                    var subTypes = new Tru[subsCount];
                    for (var i = 0; i < subsCount; i++)
                    {
                        var pr = Parse(data, offset, warehouse, depth + 1);
                        subTypes[i] = pr.Value;
                        offset += pr.Size;
                    }

                    Type? runtimeType = null;

                    if (identifier == TruIdentifier.TypedList)
                    {
                        runtimeType = subTypes[0].RuntimeType.MakeArrayType();
                    }
                    else if (identifier == TruIdentifier.TypedMap)
                    {
                        var subs = subTypes.Select(x => x.RuntimeType).ToArray();
                        runtimeType = typeof(Map<,>).MakeGenericType(subs);
                    }
                    // @TODO: Need Tuples

                    if (runtimeType != null && nullable)
                    {
                        runtimeType = typeof(Nullable<>).MakeGenericType(runtimeType);
                    }

                    return new ParseResult<TruComposite>(
                                                new TruComposite(identifier, nullable, subTypes, runtimeType),
                                                offset - oOffset);
                }
            }
            else
            {
                var runtimeType = nullable ? Tru.NullableTypesMapping[identifier]
                                           : Tru.TypesMapping[identifier];
                return new ParseResult<TruPrimitive>(
                            new TruPrimitive(identifier, nullable, runtimeType),
                            1);
            }
        }



        public static AsyncReply<IParseResult<Tru>> ParseAsync(byte[] data, uint offset, EpConnection connection, ulong[] requestSequence)
            => ParseAsync(data, offset, connection, requestSequence, 1);

        private static async AsyncReply<IParseResult<Tru>> ParseAsync(
            byte[] data,
            uint offset,
            EpConnection connection,
            ulong[] requestSequence,
            int depth)
        {
            ParserGuard.EnsureTypeMetadataDepth(ParserGuard.GetWarehouse(connection), depth);

            var oOffset = offset;

            var header = data[offset++];
            bool nullable = (header & 0x80) > 0;
            var identifier = (TruIdentifier)(header & 0x7F);

            if ((header & 0x40) > 0)
            {
                var subsCount = (header >> 3) & 0x7;

                if (subsCount == 0)
                {
                    ulong typeDefId = 0;
                    bool isRemote = false;

                    if (identifier == TruIdentifier.LocalType8)
                    {
                        typeDefId = data[offset];
                        offset += 1;
                    }
                    else if (identifier == TruIdentifier.RemoteType8)
                    {
                        typeDefId = data[offset];
                        isRemote = true;
                        offset += 1;
                    }
                    else if (identifier == TruIdentifier.LocalType16)
                    {
                        typeDefId = data.GetUInt16(offset, Endian.Little);
                        offset += 2;
                    }
                    else if (identifier == TruIdentifier.RemoteType16)
                    {
                        typeDefId = data.GetUInt16(offset, Endian.Little);
                        isRemote = true;
                        offset += 2;
                    }
                    else if (identifier == TruIdentifier.LocalType32)
                    {
                        typeDefId = data.GetUInt32(offset, Endian.Little);
                        offset += 4;
                    }
                    else if (identifier == TruIdentifier.RemoteType32)
                    {
                        typeDefId = data.GetUInt32(offset, Endian.Little);
                        offset += 4;
                    }
                    else if (identifier == TruIdentifier.LocalType64)
                    {
                        typeDefId = data.GetUInt64(offset, Endian.Little);
                        offset += 8;
                    }
                    else if (identifier == TruIdentifier.RemoteType64)
                    {
                        typeDefId = data.GetUInt64(offset, Endian.Little);
                        isRemote = true;
                        offset += 8;
                    }
                    else
                    {
                        throw new Exception("Invalid identifier.");
                    }

                    // try to get the typedef from the warehouse.

                    if (isRemote)
                    {
                        if (connection == null)
                            throw new Exception("Connection is required to resolve remote type definitions.");

                        var td = await connection.FetchTypeDef(typeDefId, requestSequence);

                        if (td == null)
                            throw new Exception("TypeDef not found.");

                        return new ParseResult<TruTypeDef>(
                                                new TruTypeDef(nullable, td),
                                                offset - oOffset);
                    }
                    else
                    {
                        var td = connection.Instance.Warehouse.GetLocalTypeDefById(typeDefId);

                        if (td == null)
                            throw new Exception("TypeDef not found.");

                        return new ParseResult<TruTypeDef>(
                                                new TruTypeDef(nullable, td),
                                                offset - oOffset);
                    }
                }
                else
                {
                    var subTypes = new Tru[subsCount];
                    for (var i = 0; i < subsCount; i++)
                    {
                        var pr = await ParseAsync(data, offset, connection, requestSequence, depth + 1);
                        subTypes[i] = pr.Value;
                        offset += pr.Size;
                    }

                    Type? runtimeType = null;

                    if (identifier == TruIdentifier.TypedList)
                    {
                        runtimeType = (subTypes[0].RuntimeType ?? typeof(object)).MakeArrayType();
                    }
                    else if (identifier == TruIdentifier.TypedMap)
                    {
                        var subs = subTypes.Select(x => x.RuntimeType).ToArray();
                        runtimeType = typeof(Map<,>).MakeGenericType(subs);
                    }
                    // @TODO: Need Tuples

                    if (runtimeType != null && runtimeType.IsValueType && nullable)
                    {
                        //if (runtimeType.IsValueType)// && Nullable.GetUnderlyingType(runtimeType) == null)
                        runtimeType = typeof(Nullable<>).MakeGenericType(runtimeType);
                    }

                    return new ParseResult<TruComposite>(
                                                new TruComposite(identifier, nullable, subTypes, runtimeType),
                                                offset - oOffset);
                }
            }
            else
            {
                var runtimeType = nullable ? Tru.NullableTypesMapping[identifier]
                                           : Tru.TypesMapping[identifier];
                return new ParseResult<TruPrimitive>(
                            new TruPrimitive(identifier, nullable, runtimeType),
                            1);
            }
        }
    }
}
