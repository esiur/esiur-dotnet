using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Resource;
using Esiur.Resource.Template;
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

    public static unsafe TDU Int32Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (int)value;

        if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
        {
            return new TDU(TDUIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);
        }
        else if (v >= short.MinValue && v <= short.MaxValue)
        {
            // Fits in 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((short*)ptr) = (short)v;

            return new TDU(TDUIdentifier.Int16, rt, 2);
        }
        else
        {
            // Use full 4 bytes
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((int*)ptr) = v;
            return new TDU(TDUIdentifier.Int32, rt, 4);
        }
    }

    public static unsafe TDU UInt32Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (uint)value;

        if (v <= byte.MaxValue)
        {
            // Fits in 1 byte
            return new TDU(TDUIdentifier.UInt8, new byte[] { (byte)v }, 1);
        }
        else if (v <= ushort.MaxValue)
        {
            // Fits in 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((ushort*)ptr) = (ushort)v;

            return new TDU(TDUIdentifier.UInt16, rt, 2);
        }
        else
        {
            // Use full 4 bytes
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((uint*)ptr) = v;

            return new TDU(TDUIdentifier.UInt32, rt, 4);
        }
    }

    public static unsafe TDU Int16Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (short)value;

        if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
        {
            // Fits in 1 byte
            return new TDU(TDUIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);
        }
        else
        {
            // Use full 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((short*)ptr) = v;

            return new TDU(TDUIdentifier.Int16, rt, 2);
        }
    }

    public static unsafe TDU UInt16Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (ushort)value;

        if (v <= byte.MaxValue)
        {
            // Fits in 1 byte
            return new TDU(TDUIdentifier.UInt8, new byte[] { (byte)v }, 1);
        }
        else
        {
            // Use full 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((ushort*)ptr) = v;

            return new TDU(TDUIdentifier.UInt16, rt, 2);
        }
    }


    public static  unsafe TDU Float32Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        float v = (float)value;

        // Special IEEE-754 values
        if (float.IsNaN(v) || float.IsInfinity(v))
        {
            return new TDU(TDUIdentifier.Infinity, new byte[0], 0);
        }

        // If v is an exact integer, prefer smallest signed width up to Int32
        if (v == Math.Truncate(v))
        {
            // Note: casts are safe because we check bounds first.
            if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
            {
                return new TDU(TDUIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);
            }

            if (v >= short.MinValue && v <= short.MaxValue)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((short*)ptr) = (short)v;
                return new TDU(TDUIdentifier.Int16, rt, 2);
            }
        }

        // Default: Float32
        {
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((float*)ptr) = v;
            return new TDU(TDUIdentifier.Float32, rt, 4);
        }
    }


    public unsafe static TDU Float64Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        double v = (double)value;

        // Special IEEE-754 values
        if (double.IsNaN(v) || double.IsInfinity(v))
        {
            return new TDU(TDUIdentifier.Infinity, new byte[0], 0);
        }

        // If v is an exact integer, choose the smallest signed width
        if (v == Math.Truncate(v))
        {
            if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
                return new TDU(TDUIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);

            if (v >= short.MinValue && v <= short.MaxValue)
            {
                var rt = new byte[2];

                fixed (byte* ptr = rt)
                    *((short*)ptr) = (short)v;

                return new TDU(TDUIdentifier.Int16, rt, 2);
            }

            if (v >= int.MinValue && v <= int.MaxValue)
            {
                var rt = new byte[4];

                fixed (byte* ptr = rt)
                    *((int*)ptr) = (int)v;

                return new TDU(TDUIdentifier.Int32, rt, 4);
            }

            // If it's integral but outside Int64 range, fall through to Float64.
        }

        // Try exact Float32 (decimal subset of doubles)
        var f = (float)v;
        if ((double)f == v)
        {
            var rt = new byte[4];

            fixed (byte* ptr = rt)
                *((float*)ptr) = (byte)v;

            return new TDU(TDUIdentifier.Float32, rt, 4);
        }

        // Default: Float64
        {
            var rt = new byte[8];
            fixed (byte* ptr = rt)
                *((double*)ptr) = v;
            return new TDU(TDUIdentifier.Float64, rt, 8);
        }
    }
    public static unsafe TDU Int64Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (long)value;

        if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
        {
            // Fits in 1 byte
            return new TDU(TDUIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);
        }
        else if (v >= short.MinValue && v <= short.MaxValue)
        {
            // Fits in 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((short*)ptr) = (short)v;

            return new TDU(TDUIdentifier.Int16, rt, 2);
        }
        else if (v >= int.MinValue && v <= int.MaxValue)
        {
            // Fits in 4 bytes
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((int*)ptr) = (int)v;

            return new TDU(TDUIdentifier.Int32, rt, 4);
        }
        else
        {
            // Use full 8 bytes
            var rt = new byte[8];
            fixed (byte* ptr = rt)
                *((long*)ptr) = v;

            return new TDU(TDUIdentifier.Int64, rt, 8);
        }
    }

    public static unsafe TDU UInt64Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (ulong)value;

        if (v <= byte.MaxValue)
        {
            // Fits in 1 byte
            return new TDU(TDUIdentifier.UInt8, new byte[] { (byte)v }, 1);
        }
        else if (v <= ushort.MaxValue)
        {
            // Fits in 2 bytes
            var rt = new byte[2];
            fixed (byte* ptr = rt)
                *((ushort*)ptr) = (ushort)v;

            return new TDU(TDUIdentifier.UInt16, rt, 2);
        }
        else if (v <= uint.MaxValue)
        {
            // Fits in 4 bytes
            var rt = new byte[4];
            fixed (byte* ptr = rt)
                *((uint*)ptr) = (uint)v;

            return new TDU(TDUIdentifier.UInt32, rt, 4);
        }
        else
        {
            // Use full 8 bytes
            var rt = new byte[8];
            fixed (byte* ptr = rt)
                *((ulong*)ptr) = v;

            return new TDU(TDUIdentifier.UInt64, rt, 8);
        }
    }


    public static unsafe TDU DateTimeComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = ((DateTime)value).ToUniversalTime().Ticks;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((long*)ptr) = v;

        return new TDU(TDUIdentifier.DateTime, rt, 8);
    }

    //public static unsafe TDU Decimal128Composer(object value, Warehouse warehouse, DistributedConnection connection)
    //{
    //    var v = (decimal)value;
    //    var rt = new byte[16];
    //    fixed (byte* ptr = rt)
    //        *((decimal*)ptr) = v;

    //    return new TDU(TDUIdentifier.Decimal128, rt, 16);
    //}

    public static unsafe TDU Decimal128Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (decimal)value;

        // Prefer smallest exact signed integer if no fractional part
        int[] bits = decimal.GetBits(v);
        int flags = bits[3];
        int scale = (flags >> 16) & 0x7F;

        if (scale == 0)
        {
            if (v >= sbyte.MinValue && v <= sbyte.MaxValue)
                return new TDU(TDUIdentifier.Int8, new byte[] { (byte)(sbyte)v }, 1);

            if (v >= short.MinValue && v <= short.MaxValue)
            {
                var b = new byte[2];
                BinaryPrimitives.WriteInt16LittleEndian(b, (short)v);
                return new TDU(TDUIdentifier.Int16, b, 2);
            }

            if (v >= int.MinValue && v <= int.MaxValue)
            {
                var b = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(b, (int)v);
                return new TDU(TDUIdentifier.Int32, b, 4);
            }

            if (v >= long.MinValue && v <= long.MaxValue)
            {
                var b = new byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(b, (long)v);
                return new TDU(TDUIdentifier.Int64, b, 8);
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

            return new TDU(TDUIdentifier.Float32, rt, 4);
        }

        // Try exact Float64 (8 bytes)
        double d = (double)v;
        if ((decimal)d == v)
        {
            var rt = new byte[4];

            fixed (byte* ptr = rt)
                *((double*)ptr) = d;

            return new TDU(TDUIdentifier.Float64, rt, 8);
        }

        {
            // Fallback: full .NET decimal (16 bytes): lo, mid, hi, flags (scale/sign)
            var rt = new byte[16];

            fixed (byte* ptr = rt)
                *((decimal*)ptr) = v;

            return new TDU(TDUIdentifier.Decimal128, rt, 16);
        }
    }

    public static TDU StringComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var b = Encoding.UTF8.GetBytes((string)value);

        return new TDU(TDUIdentifier.String, b, (uint)b.Length);
    }

    public static TDU EnumComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TDU(TDUIdentifier.Null, null, 0);

        //var warehouse = connection?.Instance?.Warehouse ?? connection?.Server?.Instance?.Warehouse;
        //if (warehouse == null)
        //    throw new Exception("Warehouse not set.");

        var template = warehouse.GetTemplateByType(value.GetType());

        var intVal = Convert.ChangeType(value, (value as Enum).GetTypeCode());

        var ct = template.Constants.FirstOrDefault(x => x.Value.Equals(intVal));

        if (ct == null)
            return new TDU(TDUIdentifier.Null, null, 0);


        return new TDU(TDUIdentifier.TypedEnum,
            new byte[] { ct.Index }, 1, template.ClassId.Data);
    }

    public static TDU UInt8Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TDU(TDUIdentifier.UInt8,
            new byte[] { (byte)value }, 1);
    }

    public static TDU Int8Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TDU(TDUIdentifier.Int8,
            new byte[] { (byte)(sbyte)value }, 1);
    }

    public static TDU Char8Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TDU(TDUIdentifier.Int8,
            new byte[] { (byte)(char)value }, 1);
    }

    public static unsafe TDU Char16Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (char)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((char*)ptr) = v;

        return new TDU(TDUIdentifier.Char16, rt, 2);

    }

    public static TDU BoolComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if ((bool)value)
        {
            return new TDU(TDUIdentifier.True);
        }
        else
        {
            return new TDU(TDUIdentifier.True);
        }
    }


    public static TDU NotModifiedComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TDU(TDUIdentifier.NotModified);
    }

    public static TDU RawDataComposerFromArray(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var b = (byte[])value;
        return new TDU(TDUIdentifier.RawData, b, (uint)b.Length);
    }

    public static TDU RawDataComposerFromList(dynamic value, Warehouse warehouse, DistributedConnection connection)
    {
        var b = value as List<byte>;
        return new TDU(TDUIdentifier.RawData, b.ToArray(), (uint)b.Count);
    }

    //public static (TDUIdentifier, byte[]) ListComposerFromArray(dynamic value, DistributedConnection connection)
    //{
    //    var rt = new List<byte>();
    //    var array = (object[])value;

    //    for (var i = 0; i < array.Length; i++)
    //        rt.AddRange(Codec.Compose(array[i], connection));

    //    return (TDUIdentifier.List, rt.ToArray());
    //}

    public static TDU ListComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var rt = ArrayComposer((IEnumerable)value, warehouse, connection);

        if (rt == null)
            return new TDU(TDUIdentifier.Null, new byte[0], 0);
        else
            return new TDU(TDUIdentifier.List, rt, (uint)rt.Length);
    }


    public static TDU TypedListComposer(IEnumerable value, Type type, Warehouse warehouse, DistributedConnection connection)
    {
        var composed = ArrayComposer((IEnumerable)value, warehouse, connection);

        if (composed == null)
            return new TDU(TDUIdentifier.Null, new byte[0], 0);

        var metadata = RepresentationType.FromType(type).Compose();


        return new TDU(TDUIdentifier.TypedList, composed,
            (uint)composed.Length, metadata);
    }

    //public static byte[] PropertyValueComposer(PropertyValue propertyValue, DistributedConnection connection)//, bool includeAge = true)
    //{
    //    var rt = new BinaryList();

    //    return 
    //        .AddUInt64(propertyValue.Age)
    //        .AddDateTime(propertyValue.Date)
    //        .AddUInt8Array(Codec.Compose(propertyValue.Value, connection))
    //        .ToArray();
    //}

    public static TDU PropertyValueArrayComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TDU(TDUIdentifier.Null, new byte[0], 0);

        var rt = new List<byte>();
        var ar = value as PropertyValue[];

        foreach (var pv in ar)
        {
            rt.AddRange(Codec.Compose(pv.Age, warehouse, connection));
            rt.AddRange(Codec.Compose(pv.Date, warehouse, connection));
            rt.AddRange(Codec.Compose(pv.Value, warehouse, connection));
        }

        return new TDU(TDUIdentifier.RawData, rt.ToArray(),
            (uint)rt.Count);
    }

    public static TDU TypedMapComposer(object value, Type keyType, Type valueType, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TDU(TDUIdentifier.Null, new byte[0], 0);

        var kt = RepresentationType.FromType(keyType).Compose();
        var vt = RepresentationType.FromType(valueType).Compose();

        var rt = new List<byte>();

        var map = (IMap)value;

        foreach (var el in map.Serialize())
            rt.AddRange(Codec.Compose(el, warehouse, connection));


        return new TDU(TDUIdentifier.TypedMap, rt.ToArray(), (uint)rt.Count,
            DC.Combine(kt, 0, (uint)kt.Length, vt, 0, (uint)vt.Length));
    }
    public static TDU TypedDictionaryComposer(object value, Type keyType, Type valueType, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TDU(TDUIdentifier.Null, null, 0);

        var kt = RepresentationType.FromType(keyType).Compose();
        var vt = RepresentationType.FromType(valueType).Compose();

        var rt = new List<byte>();

        //rt.AddRange(kt);
        //rt.AddRange(vt);

        var dic = (IDictionary)value;

        var ar = new List<object>();
        foreach (var k in dic.Keys)
        {
            ar.Add(k);
            ar.Add(dic[k]);
        }

        foreach (var el in ar)
            rt.AddRange(Codec.Compose(el, warehouse, connection));


        return new TDU(TDUIdentifier.TypedMap, rt.ToArray(), (uint)rt.Count,
            DC.Combine(kt, 0, (uint)kt.Length, vt, 0, (uint)vt.Length));
    }

    public static byte[] ArrayComposer(IEnumerable value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return null;

        var rt = new List<byte>();

        TDU? previous = null;

        foreach (var i in value)
        {
            var tdu = Codec.ComposeInternal(i, warehouse, connection);
            if (previous != null && tdu.MatchType(previous.Value))
            {
                var d = tdu.Composed.Clip(tdu.ContentOffset,
                    (uint)tdu.Composed.Length - tdu.ContentOffset);

                var ntd = new TDU(TDUIdentifier.TypeContinuation, d, (ulong)d.Length);
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

    public static TDU ResourceListComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TDU(TDUIdentifier.Null, new byte[0], 0);

        var composed = ArrayComposer((IEnumerable)value, warehouse, connection);

        return new TDU(TDUIdentifier.ResourceList, composed,
            (uint)composed.Length);
    }

    public static TDU RecordListComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TDU(TDUIdentifier.Null, new byte[0], 0);

        var composed = ArrayComposer((IEnumerable)value, warehouse, connection);

        return new TDU(TDUIdentifier.RecordList,
            composed, (uint)composed.Length);
    }


    public static unsafe TDU ResourceComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var resource = (IResource)value;

        if (resource.Instance == null || resource.Instance.IsDestroyed)
        {
            return new TDU(TDUIdentifier.Null, null, 0);
        }

        if (Codec.IsLocalResource(resource, connection))
        {
            var rid = (resource as DistributedResource).DistributedResourceInstanceId;

            if (rid <= 0xFF)
                return new TDU(TDUIdentifier.LocalResource8, new byte[] { (byte)rid }, 1);
            else if (rid <= 0xFFFF)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((ushort*)ptr) = (ushort)rid;

                return new TDU(TDUIdentifier.LocalResource16, rt, 2);
            }
            else
            {
                var rt = new byte[4];
                fixed (byte* ptr = rt)
                    *((uint*)ptr) = rid;
                return new TDU(TDUIdentifier.LocalResource32, rt, 4);
            }
        }
        else
        {

            //rt.Append((value as IResource).Instance.Template.ClassId, (value as IResource).Instance.Id);
            connection.cache.Add(value as IResource, DateTime.UtcNow);

            var rid = resource.Instance.Id;

            if (rid <= 0xFF)
                return new TDU(TDUIdentifier.RemoteResource8, new byte[] { (byte)rid }, 1);
            else if (rid <= 0xFFFF)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((ushort*)ptr) = (ushort)rid;

                return new TDU(TDUIdentifier.RemoteResource16, rt, 2);
            }
            else
            {
                var rt = new byte[4];
                fixed (byte* ptr = rt)
                    *((uint*)ptr) = rid;
                return new TDU(TDUIdentifier.RemoteResource32, rt, 4);
            }
        }
    }

    public static unsafe TDU MapComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TDU(TDUIdentifier.Null, new byte[0], 1);

        var rt = new List<byte>();
        var map = (IMap)value;

        foreach (var el in map.Serialize())
            rt.AddRange(Codec.Compose(el, warehouse, connection));

        return new TDU(TDUIdentifier.Map, rt.ToArray(), (uint)rt.Count);
    }

    public static unsafe TDU UUIDComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TDU(TDUIdentifier.UUID, ((UUID)value).Data, 16);

    }

    public static unsafe TDU RecordComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var rt = new List<byte>();// BinaryList();
        var record = (IRecord)value;

        var template = warehouse.GetTemplateByType(record.GetType());


        foreach (var pt in template.Properties)
        {
            var propValue = pt.PropertyInfo.GetValue(record, null);
            var rr = Codec.Compose(propValue, warehouse, connection);
            rt.AddRange(rr);
        }

        return new TDU(TDUIdentifier.Record, rt.ToArray(),
            (uint)rt.Count,
            template.ClassId.Data);
    }
    public static byte[] HistoryComposer(KeyList<PropertyTemplate, PropertyValue[]> history, Warehouse warehouse,
                                        DistributedConnection connection, bool prependLength = false)
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

    public static TDU TupleComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TDU(TDUIdentifier.Null, new byte[0], 0);

        var fields = value.GetType().GetFields();
        var list = fields.Select(x => x.GetValue(value)).ToArray();
        var types = fields.Select(x => RepresentationType.FromType(x.FieldType).Compose()).ToArray();


        var metadata = new List<byte>();
        foreach (var t in types)
            metadata.AddRange(t);

        var composed = ArrayComposer(list, warehouse, connection);

        if (composed == null)
            return new TDU(TDUIdentifier.Null, new byte[0], 0);
        else
        {
            return new TDU(TDUIdentifier.TypedTuple, composed,
                        (uint)composed.Length, metadata.ToArray());
        }
    }
}


