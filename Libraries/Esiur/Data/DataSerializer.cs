using Esiur.Core;
using Esiur.Data.Gvwie;
using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Microsoft.CodeAnalysis;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Data;

public static class DataSerializer
{
    public delegate byte[] Serializer(object value);

    public static unsafe Tdu Int32Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        var v = (int)value;

        if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
        {
            return new Tdu(TduIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);
        }
        else if (v >= short.MinValue && v <= short.MaxValue)
        {
            // Fits in 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((short*)ptr) = (short)v;

            return new Tdu(TduIdentifier.Int16, rt, 2);
        }
        else
        {
            // Use full 4 bytes
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((int*)ptr) = v;
            return new Tdu(TduIdentifier.Int32, rt, 4);
        }
    }

    public static unsafe Tdu UInt32Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        var v = (uint)value;

        if (v <= byte.MaxValue)
        {
            // Fits in 1 byte
            return new Tdu(TduIdentifier.UInt8, new byte[] { (byte)v }, 1);
        }
        else if (v <= ushort.MaxValue)
        {
            // Fits in 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((ushort*)ptr) = (ushort)v;

            return new Tdu(TduIdentifier.UInt16, rt, 2);
        }
        else
        {
            // Use full 4 bytes
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((uint*)ptr) = v;

            return new Tdu(TduIdentifier.UInt32, rt, 4);
        }
    }

    public static unsafe Tdu Int16Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        var v = (short)value;

        if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
        {
            // Fits in 1 byte
            return new Tdu(TduIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);
        }
        else
        {
            // Use full 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((short*)ptr) = v;

            return new Tdu(TduIdentifier.Int16, rt, 2);
        }
    }

    public static unsafe Tdu UInt16Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        var v = (ushort)value;

        if (v <= byte.MaxValue)
        {
            // Fits in 1 byte
            return new Tdu(TduIdentifier.UInt8, new byte[] { (byte)v }, 1);
        }
        else
        {
            // Use full 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((ushort*)ptr) = v;

            return new Tdu(TduIdentifier.UInt16, rt, 2);
        }
    }


    public static unsafe Tdu Float32Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        float v = (float)value;

        // Special IEEE-754 values
        if (float.IsNaN(v) || float.IsInfinity(v))
        {
            return new Tdu(TduIdentifier.Infinity, new byte[0], 0);
        }

        // If v is an exact integer, prefer smallest signed width up to Int32
        if (v == Math.Truncate(v))
        {
            // Note: casts are safe because we check bounds first.
            if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
            {
                return new Tdu(TduIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);
            }

            if (v >= short.MinValue && v <= short.MaxValue)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((short*)ptr) = (short)v;
                return new Tdu(TduIdentifier.Int16, rt, 2);
            }
        }

        // Default: Float32
        {
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((float*)ptr) = v;
            return new Tdu(TduIdentifier.Float32, rt, 4);
        }
    }


    public unsafe static Tdu Float64Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        double v = (double)value;

        // Special IEEE-754 values
        if (double.IsNaN(v) || double.IsInfinity(v))
        {
            return new Tdu(TduIdentifier.Infinity, new byte[0], 0);
        }

        // If v is an exact integer, choose the smallest signed width
        if (v == Math.Truncate(v))
        {
            if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
                return new Tdu(TduIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);

            if (v >= short.MinValue && v <= short.MaxValue)
            {
                var rt = new byte[2];

                fixed (byte* ptr = rt)
                    *((short*)ptr) = (short)v;

                return new Tdu(TduIdentifier.Int16, rt, 2);
            }

            if (v >= int.MinValue && v <= int.MaxValue)
            {
                var rt = new byte[4];

                fixed (byte* ptr = rt)
                    *((int*)ptr) = (int)v;

                return new Tdu(TduIdentifier.Int32, rt, 4);
            }

            // If it's integral but outside Int64 range, fall through to Float64.
        }

        // Try exact Float32 (decimal subset of doubles)
        var f = (float)v;
        if ((double)f == v)
        {
            var rt = new byte[4];

            fixed (byte* ptr = rt)
                *((float*)ptr) = f;

            return new Tdu(TduIdentifier.Float32, rt, 4);
        }

        // Default: Float64
        {
            var rt = new byte[8];
            fixed (byte* ptr = rt)
                *((double*)ptr) = v;
            return new Tdu(TduIdentifier.Float64, rt, 8);
        }
    }
    public static unsafe Tdu Int64Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        var v = (long)value;

        if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
        {
            // Fits in 1 byte
            return new Tdu(TduIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);
        }
        else if (v >= short.MinValue && v <= short.MaxValue)
        {
            // Fits in 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((short*)ptr) = (short)v;

            return new Tdu(TduIdentifier.Int16, rt, 2);
        }
        else if (v >= int.MinValue && v <= int.MaxValue)
        {
            // Fits in 4 bytes
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((int*)ptr) = (int)v;

            return new Tdu(TduIdentifier.Int32, rt, 4);
        }
        else
        {
            // Use full 8 bytes
            var rt = new byte[8];
            fixed (byte* ptr = rt)
                *((long*)ptr) = v;

            return new Tdu(TduIdentifier.Int64, rt, 8);
        }
    }

    public static unsafe Tdu UInt64Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        var v = (ulong)value;

        if (v <= byte.MaxValue)
        {
            // Fits in 1 byte
            return new Tdu(TduIdentifier.UInt8, new byte[] { (byte)v }, 1);
        }
        else if (v <= ushort.MaxValue)
        {
            // Fits in 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((ushort*)ptr) = (ushort)v;

            return new Tdu(TduIdentifier.UInt16, rt, 2);
        }
        else if (v <= uint.MaxValue)
        {
            // Fits in 4 bytes
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((uint*)ptr) = (uint)v;

            return new Tdu(TduIdentifier.UInt32, rt, 4);
        }
        else
        {
            // Use full 8 bytes
            var rt = new byte[8];
            fixed (byte* ptr = rt)
                *((ulong*)ptr) = v;

            return new Tdu(TduIdentifier.UInt64, rt, 8);
        }
    }


    public static unsafe Tdu DateTimeComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        var v = ((DateTime)value).ToUniversalTime().Ticks;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((long*)ptr) = v;

        return new Tdu(TduIdentifier.DateTime, rt, 8);
    }

    //public static unsafe TDU Decimal128Composer(object value, Warehouse warehouse, EpConnection connection)
    //{
    //    var v = (decimal)value;
    //    var rt = new byte[16];
    //    fixed (byte* ptr = rt)
    //        *((decimal*)ptr) = v;

    //    return new TDU(TDUIdentifier.Decimal128, rt, 16);
    //}

    public static unsafe Tdu Decimal128Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        var v = (decimal)value;

        // Prefer smallest exact signed integer if no fractional part
        int[] bits = decimal.GetBits(v);
        int flags = bits[3];
        int scale = (flags >> 16) & 0x7F;

        if (scale == 0)
        {
            if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
                return new Tdu(TduIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);

            if (v >= short.MinValue && v <= short.MaxValue)
            {
                var b = new byte[2];
                BinaryPrimitives.WriteInt16LittleEndian(b, (short)v);
                return new Tdu(TduIdentifier.Int16, b, 2);
            }

            if (v >= int.MinValue && v <= int.MaxValue)
            {
                var b = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(b, (int)v);
                return new Tdu(TduIdentifier.Int32, b, 4);
            }

            if (v >= long.MinValue && v <= long.MaxValue)
            {
                var b = new byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(b, (long)v);
                return new Tdu(TduIdentifier.Int64, b, 8);
            }
            // else fall through (needs 96+ bits)
        }

        // Try exact Float32 (4 bytes)
        // Exactness test: decimal -> float -> decimal must equal original
        float f = (float)v;
        if ((decimal)f == v)
        {
            var rt = new byte[4];

            fixed (byte* ptr = rt)
                *((float*)ptr) = f;

            return new Tdu(TduIdentifier.Float32, rt, 4);
        }

        // Try exact Float64 (8 bytes)
        double d = (double)v;
        if ((decimal)d == v)
        {
            var rt = new byte[4];

            fixed (byte* ptr = rt)
                *((double*)ptr) = d;

            return new Tdu(TduIdentifier.Float64, rt, 8);
        }

        {
            // Fallback: full .NET decimal (16 bytes): lo, mid, hi, flags (scale/sign)
            var rt = new byte[16];

            fixed (byte* ptr = rt)
                *((decimal*)ptr) = v;

            return new Tdu(TduIdentifier.Decimal128, rt, 16);
        }
    }

    public static Tdu StringComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        var b = Encoding.UTF8.GetBytes((string)value);

        return new Tdu(TduIdentifier.String, b, (uint)b.Length);
    }

    public static Tdu ResourceLinkComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        var b = Encoding.UTF8.GetBytes((ResourceLink)value);

        return new Tdu(TduIdentifier.ResourceLink, b, (uint)b.Length);
    }


    public static Tdu EnumComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        if (value == null)
            return new Tdu(TduIdentifier.Null, null, 0);

        
        //var warehouse = connection?.Instance?.Warehouse ?? connection?.Server?.Instance?.Warehouse;
        //if (warehouse == null)
        //    throw new Exception("Warehouse not set.");

        var typeDef = warehouse.GetTypeDefByType(value.GetType());

        var intVal = Convert.ChangeType(value, (value as Enum).GetTypeCode());

        var ct = typeDef.Constants.FirstOrDefault(x => x.Value.Equals(intVal));

        if (ct == null)
            return new Tdu(TduIdentifier.Null, null, 0);

        //return Codec.ComposeInternal(intVal, warehouse, connection);

        return new Tdu(TduIdentifier.TypedEnum,
            new byte[] { ct.Index }, 1, typeDef.Id.Data);
    }

    public static Tdu UInt8Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        return new Tdu(TduIdentifier.UInt8,
            new byte[] { (byte)value }, 1);
    }

    public static Tdu Int8Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        return new Tdu(TduIdentifier.Int8,
            new byte[] { (byte)(sbyte)value }, 1);
    }

    public static Tdu Char8Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        return new Tdu(TduIdentifier.Int8,
            new byte[] { (byte)(char)value }, 1);
    }

    public static unsafe Tdu Char16Composer(object value, Warehouse warehouse, EpConnection connection)
    {
        var v = (char)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((char*)ptr) = v;

        return new Tdu(TduIdentifier.Char16, rt, 2);

    }

    public static Tdu BoolComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        if ((bool)value)
        {
            return new Tdu(TduIdentifier.True, null, 0);
        }
        else
        {
            return new Tdu(TduIdentifier.False, null, 0);
        }
    }


    public static Tdu NotModifiedComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        return new Tdu(TduIdentifier.NotModified, null, 0);
    }

    public static Tdu RawDataComposerFromArray(object value, Warehouse warehouse, EpConnection connection)
    {
        var b = (byte[])value;
        return new Tdu(TduIdentifier.RawData, b, (uint)b.Length);
    }

    public static Tdu RawDataComposerFromList(dynamic value, Warehouse warehouse, EpConnection connection)
    {
        var b = value as List<byte>;
        return new Tdu(TduIdentifier.RawData, b.ToArray(), (uint)b.Count);
    }

    //public static (TDUIdentifier, byte[]) ListComposerFromArray(dynamic value, EpConnection connection)
    //{
    //    var rt = new List<byte>();
    //    var array = (object[])value;

    //    for (var i = 0; i < array.Length; i++)
    //        rt.AddRange(Codec.Compose(array[i], connection));

    //    return (TDUIdentifier.List, rt.ToArray());
    //}

    public static Tdu ListComposer(object value, Warehouse warehouse, EpConnection connection)
    {

        var composed = DynamicArrayComposer((IEnumerable)value, warehouse, connection);

        if (composed == null)
            return new Tdu(TduIdentifier.Null, new byte[0], 0);
        else
        {
            return new Tdu(TduIdentifier.List, composed, (uint)composed.Length);
        }

        //if (value == null)
        //    return new TDU(TDUIdentifier.Null, new byte[0], 0);

        //var list = value;

        //var rt = new List<byte>();

        //TDU? previous = null;

        //foreach (var i in list)
        //{
        //    var tdu = Codec.ComposeInternal(i, warehouse, connection);
        //    if (previous != null && tdu.MatchType(previous.Value))
        //    {
        //        var d = tdu.Composed.Clip(tdu.ContentOffset,
        //            (uint)tdu.Composed.Length - tdu.ContentOffset);

        //        var ntd = new TDU(TDUIdentifier.TypeContinuation, d, (ulong)d.Length);
        //        rt.AddRange(ntd.Composed);
        //    }
        //    else
        //    {
        //        rt.AddRange(tdu.Composed);
        //    }

        //    previous = tdu;
        //}

        //return new TDU(TDUIdentifier.List, rt.ToArray(), (uint)rt.Count);
    }

    public static byte[] TypedArrayComposer(IEnumerable value, Tru tru, Warehouse warehouse, EpConnection connection)
    {
        byte[] composed;

        if (value == null)
            return null;


        if (tru.Identifier == TruIdentifier.Int32)
        {
            composed = GroupInt32Codec.Encode((IList<int>)value);
        }
        else if (tru.Identifier == TruIdentifier.Int64)
        {
            composed = GroupInt64Codec.Encode((IList<long>)value);
        }
        else if (tru.Identifier == TruIdentifier.Int16)
        {
            composed = GroupInt16Codec.Encode((IList<short>)value);
        }
        else if (tru.Identifier == TruIdentifier.UInt32)
        {
            composed = GroupUInt32Codec.Encode((IList<uint>)value);
        }
        else if (tru.Identifier == TruIdentifier.UInt64)
        {
            composed = GroupUInt64Codec.Encode((IList<ulong>)value);
        }
        else if (tru.Identifier == TruIdentifier.UInt16)
        {
            composed = GroupUInt16Codec.Encode((IList<ushort>)value);
        }
        else if (tru.Identifier == TruIdentifier.Enum)
        {

            var rt = new List<byte>();
            var typeDef = warehouse.GetTypeDefByType(tru.GetRuntimeType(warehouse));

            foreach (var v in value)
            {
                var intVal = Convert.ChangeType(v, (v as Enum).GetTypeCode());
                var ct = typeDef.Constants.FirstOrDefault(x => x.Value.Equals(intVal));
                if (ct == null)
                    throw new Exception("Unknown Enum.");
                rt.Add(ct.Index);
            }

            composed = rt.ToArray();
        }
        else
        {
            var rt = new List<byte>();

            Tdu? previous = null;
            var isTyped = tru.IsTyped();

            foreach (var i in value)
            {
                var tdu = Codec.ComposeInternal(i, warehouse, connection);

                var currentTru = Tru.FromType(i?.GetType());

                if (isTyped && tru.Match(currentTru))
                {
                    var d = tdu.Composed.Clip(tdu.ContentOffset,
                        (uint)tdu.Composed.Length - tdu.ContentOffset);

                    var ntd = new Tdu(TduIdentifier.TypeOfTarget, d, (ulong)d.Length);
                    rt.AddRange(ntd.Composed);
                }
                else

                if (previous != null && tdu.MatchType(previous.Value))
                {
                    var d = tdu.Composed.Clip(tdu.ContentOffset,
                        (uint)tdu.Composed.Length - tdu.ContentOffset);

                    var ntd = new Tdu(TduIdentifier.TypeContinuation, d, (ulong)d.Length);
                    rt.AddRange(ntd.Composed);
                }
                else
                {
                    rt.AddRange(tdu.Composed);
                }

                previous = tdu;
            }

            composed = rt.ToArray();

        }

        return composed;

    }

    public static Tdu TypedListComposer(IEnumerable value, Type type, Warehouse warehouse, EpConnection connection)
    {
        var tru = Tru.FromType(type);

        byte[] composed = TypedArrayComposer(value, tru, warehouse, connection);

        if (composed == null)
            return new Tdu(TduIdentifier.Null, new byte[0], 0);

        var metadata = tru.Compose();

        return new Tdu(TduIdentifier.TypedList, composed, (uint)composed.Length, metadata);
    }

    //public static byte[] PropertyValueComposer(PropertyValue propertyValue, EpConnection connection)//, bool includeAge = true)
    //{
    //    var rt = new BinaryList();

    //    return 
    //        .AddUInt64(propertyValue.Age)
    //        .AddDateTime(propertyValue.Date)
    //        .AddUInt8Array(Codec.Compose(propertyValue.Value, connection))
    //        .ToArray();
    //}

    public static Tdu PropertyValueArrayComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        if (value == null)
            return new Tdu(TduIdentifier.Null, new byte[0], 0);

        var rt = new List<byte>();
        var ar = value as PropertyValue[];

        foreach (var pv in ar)
        {
            rt.AddRange(Codec.Compose(pv.Age, warehouse, connection));
            rt.AddRange(Codec.Compose(pv.Date, warehouse, connection));
            rt.AddRange(Codec.Compose(pv.Value, warehouse, connection));
        }

        return new Tdu(TduIdentifier.RawData, rt.ToArray(),
            (uint)rt.Count);
    }

    public static Tdu TypedMapComposer(object value, Type keyType, Type valueType, Warehouse warehouse, EpConnection connection)
    {
        if (value == null)
            return new Tdu(TduIdentifier.Null, new byte[0], 0);

        var kt = Tru.FromType(keyType);
        var vt = Tru.FromType(valueType);

        //var rt = new List<byte>();

        var map = (IMap)value;

        var keys = map.GetKeys();
        var values = map.GetValues();

        var compsedKeys = TypedArrayComposer(keys, kt, warehouse, connection);
        var compsedValues = TypedArrayComposer(values, vt, warehouse, connection);

        var ktb = kt.Compose();
        var vtb = vt.Compose();

        var metadata = DC.Combine(ktb, 0, (uint)ktb.Length, vtb, 0, (uint)vtb.Length);

        //foreach (var el in map.Serialize())
        //    rt.AddRange(Codec.Compose(el, warehouse, connection));

        var keysTdu = new Tdu(TduIdentifier.TypeOfTarget, compsedKeys, (uint)compsedKeys.Length).Composed;
        var valuesTdu = new Tdu(TduIdentifier.TypeOfTarget, compsedValues, (uint)compsedValues.Length).Composed;

        var all = DC.Combine(keysTdu, 0, (uint)keysTdu.Length, valuesTdu, 0, (uint)valuesTdu.Length);

        return new Tdu(TduIdentifier.TypedMap, all, (uint)all.Length, metadata);



        //return new TDU(TDUIdentifier.TypedMap, rt.ToArray(), (uint)rt.Count,
        //    );
    }
    public static Tdu TypedDictionaryComposer(object value, Type keyType, Type valueType, Warehouse warehouse, EpConnection connection)
    {

        if (value == null)
            return new Tdu(TduIdentifier.Null, new byte[0], 0);

        var kt = Tru.FromType(keyType);
        var vt = Tru.FromType(valueType);

        //var rt = new List<byte>();

        var map = (IDictionary)value;

        var keys = map.Keys;
        var values = map.Values;

        var compsedKeys = TypedArrayComposer(keys, kt, warehouse, connection);
        var compsedValues = TypedArrayComposer(values, vt, warehouse, connection);

        var ktb = kt.Compose();
        var vtb = vt.Compose();

        var metadata = DC.Combine(ktb, 0, (uint)ktb.Length, vtb, 0, (uint)vtb.Length);

        //foreach (var el in map.Serialize())
        //    rt.AddRange(Codec.Compose(el, warehouse, connection));

        var keysTdu = new Tdu(TduIdentifier.TypeOfTarget, compsedKeys, (uint)compsedKeys.Length).Composed;
        var valuesTdu = new Tdu(TduIdentifier.TypeOfTarget, compsedValues, (uint)compsedValues.Length).Composed;

        var all = DC.Combine(keysTdu, 0, (uint)keysTdu.Length, valuesTdu, 0, (uint)valuesTdu.Length);

        return new Tdu(TduIdentifier.TypedMap, all, (uint)all.Length, metadata);


        //if (value == null)
        //    return new TDU(TDUIdentifier.Null, null, 0);

        //var kt = TRU.FromType(keyType).Compose();
        //var vt = TRU.FromType(valueType).Compose();

        //var rt = new List<byte>();

        ////rt.AddRange(kt);
        ////rt.AddRange(vt);

        //var dic = (IDictionary)value;

        //var ar = new List<object>();
        //foreach (var k in dic.Keys)
        //{
        //    ar.Add(k);
        //    ar.Add(dic[k]);
        //}

        //foreach (var el in ar)
        //    rt.AddRange(Codec.Compose(el, warehouse, connection));


        //return new TDU(TDUIdentifier.TypedMap, rt.ToArray(), (uint)rt.Count,
        //    DC.Combine(kt, 0, (uint)kt.Length, vt, 0, (uint)vt.Length));
    }

    public static byte[] DynamicArrayComposer(IEnumerable value, Warehouse warehouse, EpConnection connection)
    {
        if (value == null)
            return null;

        var rt = new List<byte>();

        Tdu? previous = null;

        foreach (var i in value)
        {
            var tdu = Codec.ComposeInternal(i, warehouse, connection);
            if (previous != null && tdu.MatchType(previous.Value))
            {
                var d = tdu.Composed.Clip(tdu.ContentOffset,
                    (uint)tdu.Composed.Length - tdu.ContentOffset);

                var ntd = new Tdu(TduIdentifier.TypeContinuation, d, (ulong)d.Length);
                rt.AddRange(ntd.Composed);
            }
            else
            {
                rt.AddRange(tdu.Composed);
            }

            previous = tdu;
        }

        return rt.ToArray();
    }

    public static Tdu ResourceListComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        if (value == null)
            return new Tdu(TduIdentifier.Null, new byte[0], 0);

        var composed = DynamicArrayComposer((IEnumerable)value, warehouse, connection);

        return new Tdu(TduIdentifier.ResourceList, composed,
            (uint)composed.Length);
    }

    public static Tdu RecordListComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        if (value == null)
            return new Tdu(TduIdentifier.Null, new byte[0], 0);

        var composed = DynamicArrayComposer((IEnumerable)value, warehouse, connection);

        return new Tdu(TduIdentifier.RecordList,
            composed, (uint)composed.Length);
    }


    public static unsafe Tdu ResourceComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        var resource = (IResource)value;

        if (resource.Instance == null || resource.Instance.IsDestroyed)
        {
            return new Tdu(TduIdentifier.Null, null, 0);
        }

        if (Codec.IsLocalResource(resource, connection))
        {
            var rid = (resource as EpResource).DistributedResourceInstanceId;

            if (rid <= 0xFF)
                return new Tdu(TduIdentifier.LocalResource8, new byte[] { (byte)rid }, 1);
            else if (rid <= 0xFFFF)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((ushort*)ptr) = (ushort)rid;

                return new Tdu(TduIdentifier.LocalResource16, rt, 2);
            }
            else
            {
                var rt = new byte[4];
                fixed (byte* ptr = rt)
                    *((uint*)ptr) = rid;
                return new Tdu(TduIdentifier.LocalResource32, rt, 4);
            }
        }
        else
        {

            connection.cache.Add(value as IResource, DateTime.UtcNow);

            var rid = resource.Instance.Id;

            if (rid <= 0xFF)
                return new Tdu(TduIdentifier.RemoteResource8, new byte[] { (byte)rid }, 1);
            else if (rid <= 0xFFFF)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((ushort*)ptr) = (ushort)rid;

                return new Tdu(TduIdentifier.RemoteResource16, rt, 2);
            }
            else
            {
                var rt = new byte[4];
                fixed (byte* ptr = rt)
                    *((uint*)ptr) = rid;
                return new Tdu(TduIdentifier.RemoteResource32, rt, 4);
            }
        }
    }

    public static unsafe Tdu MapComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        if (value == null)
            return new Tdu(TduIdentifier.Null, new byte[0], 1);

        var rt = new List<byte>();
        var map = (IMap)value;

        foreach (var el in map.Serialize())
            rt.AddRange(Codec.Compose(el, warehouse, connection));

        return new Tdu(TduIdentifier.Map, rt.ToArray(), (uint)rt.Count);
    }

    public static unsafe Tdu UUIDComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        return new Tdu(TduIdentifier.UUID, ((Uuid)value).Data, 16);

    }

    public static unsafe Tdu RecordComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        var rt = new List<byte>();
        var record = (IRecord)value;

        var typeDef = warehouse.GetTypeDefByType(record.GetType());


        foreach (var pt in typeDef.Properties)
        {
            var propValue = pt.PropertyInfo.GetValue(record, null);

            //if (propValue == null)
            //    return  TDU(TDUIdentifier.Null, null, 0);
            var tru = Tru.FromType(propValue?.GetType());
            var tdu = Codec.ComposeInternal(propValue, warehouse, connection);


            if (pt.ValueType.IsTyped() &&
                pt.ValueType.Match(tru))
            {
                // strip metadata
                var len = (uint)tdu.Composed.Length - tdu.ContentOffset;
                tdu = new Tdu(TduIdentifier.TypeOfTarget,
                    tdu.Composed.Clip(tdu.ContentOffset, len), len);
            }

            rt.AddRange(tdu.Composed);
        }

        return new Tdu(TduIdentifier.Record, rt.ToArray(),
            (uint)rt.Count,
            typeDef.Id.Data);
    }

    public static byte[] HistoryComposer(KeyList<PropertyDef, PropertyValue[]> history, Warehouse warehouse,
                                        EpConnection connection, bool prependLength = false)
    {
        //@TODO:Test
        var rt = new BinaryList();

        for (var i = 0; i < history.Count; i++)
            rt.AddUInt8(history.Keys.ElementAt(i).Index)
              .AddUInt8Array(Codec.Compose(history.Values.ElementAt(i), warehouse, connection));

        if (prependLength)
            rt.InsertInt32(0, rt.Length);

        return rt.ToArray();
    }

    public static Tdu TupleComposer(object value, Warehouse warehouse, EpConnection connection)
    {
        if (value == null)
            return new Tdu(TduIdentifier.Null, new byte[0], 0);

        var fields = value.GetType().GetFields();
        var list = fields.Select(x => x.GetValue(value)).ToArray();
        var trus = fields.Select(x => Tru.FromType(x.FieldType)).ToArray();


        var metadata = new List<byte>();

        foreach (var t in trus)
            metadata.AddRange(t.Compose());

        var rt = new List<byte>();

        for (var i = 0; i < fields.Length; i++)
        {
            var tupleValue = list[i];
            var targetTru = trus[i];

            var tdu = Codec.ComposeInternal(tupleValue, warehouse, connection);

            var valueTru = Tru.FromType(tupleValue?.GetType());

            if (targetTru.IsTyped() &&
                targetTru.Match(valueTru))
            {
                // strip metadata
                var len = (uint)tdu.Composed.Length - tdu.ContentOffset;
                tdu = new Tdu(TduIdentifier.TypeOfTarget,
                    tdu.Composed.Clip(tdu.ContentOffset, len), len);
            }

            rt.AddRange(tdu.Composed);
        }

        return new Tdu(TduIdentifier.TypedTuple, rt.ToArray(),
            (uint)rt.Count, metadata.ToArray());

    }
}


