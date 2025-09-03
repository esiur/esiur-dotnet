using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Resource;
using Esiur.Resource.Template;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Data;

public static class DataSerializer
{
    public delegate byte[] Serializer(object value);

    public static unsafe TransmissionDataUnit Int32Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (int)value;
        var rt = new byte[4];
        fixed (byte* ptr = rt)
            *((int*)ptr) = v;
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Int32, rt, 0, 4);
    }

    public static unsafe TransmissionDataUnit UInt32Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (uint)value;
        var rt = new byte[4];
        fixed (byte* ptr = rt)
            *((uint*)ptr) = v;

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.UInt32, rt, 0, 4);

    }

    public static unsafe TransmissionDataUnit Int16Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (short)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((short*)ptr) = v;

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Int16, rt, 0, 2);
    }

    public static unsafe TransmissionDataUnit UInt16Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (ushort)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((ushort*)ptr) = v;

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.UInt16, rt, 0, 2);
    }

    public static unsafe TransmissionDataUnit Float32Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (float)value;
        var rt = new byte[4];
        fixed (byte* ptr = rt)
            *((float*)ptr) = v;
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Float32, rt, 0, 4);
    }

    public static unsafe TransmissionDataUnit Float64Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (double)value;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((double*)ptr) = v;
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Float64, rt, 0, 8);
    }

    public static unsafe TransmissionDataUnit Int64Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (long)value;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((long*)ptr) = v;
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Int64, rt, 0, 8);
    }

    public static unsafe TransmissionDataUnit UIn64Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (ulong)value;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((ulong*)ptr) = v;
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.UInt64, rt, 0, 8);
    }


    public static unsafe TransmissionDataUnit DateTimeComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = ((DateTime)value).ToUniversalTime().Ticks;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((long*)ptr) = v;

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.DateTime, rt, 0, 8);
    }

    public static unsafe TransmissionDataUnit Decimal128Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (decimal)value;
        var rt = new byte[16];
        fixed (byte* ptr = rt)
            *((decimal*)ptr) = v;

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Decimal128, rt, 0, 16);
    }



    public static TransmissionDataUnit StringComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var b = Encoding.UTF8.GetBytes((string)value);

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.String, b, 0, (uint)b.Length);
    }

    public static TransmissionDataUnit EnumComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, null, 0, 0);

        //var warehouse = connection?.Instance?.Warehouse ?? connection?.Server?.Instance?.Warehouse;
        //if (warehouse == null)
        //    throw new Exception("Warehouse not set.");

        var template = warehouse.GetTemplateByType(value.GetType());

        var intVal = Convert.ChangeType(value, (value as Enum).GetTypeCode());

        var ct = template.Constants.FirstOrDefault(x => x.Value.Equals(intVal));

        if (ct == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, null, 0, 0);


        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.TypedEnum,
            new byte[] { ct.Index }, 0, 1, template.ClassId.Data);
    }

    public static TransmissionDataUnit UInt8Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.UInt8,
            new byte[] { (byte)value }, 0, 1);
    }

    public static TransmissionDataUnit Int8Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Int8,
            new byte[] { (byte)(sbyte)value }, 0, 1);
    }

    public static TransmissionDataUnit Char8Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Int8,
            new byte[] { (byte)(char)value }, 0, 1);
    }

    public static unsafe TransmissionDataUnit Char16Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (char)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((char*)ptr) = v;

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Char16, rt, 0, 2);

    }

    public static TransmissionDataUnit BoolComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if ((bool)value)
        {
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.True, null, 0, 0);
        }
        else
        {
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.True, null, 0, 0);
        }
    }


    public static TransmissionDataUnit NotModifiedComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.NotModified, null, 0, 0);
    }

    public static TransmissionDataUnit RawDataComposerFromArray(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var b = (byte[])value;
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.RawData, b, 0, (uint)b.Length);
    }

    public static TransmissionDataUnit RawDataComposerFromList(dynamic value, Warehouse warehouse, DistributedConnection connection)
    {
        var b = value as List<byte>;
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.RawData, b.ToArray(), 0, (uint)b.Count);
    }

    //public static (TransmissionDataUnitIdentifier, byte[]) ListComposerFromArray(dynamic value, DistributedConnection connection)
    //{
    //    var rt = new List<byte>();
    //    var array = (object[])value;

    //    for (var i = 0; i < array.Length; i++)
    //        rt.AddRange(Codec.Compose(array[i], connection));

    //    return (TransmissionDataUnitIdentifier.List, rt.ToArray());
    //}

    public static TransmissionDataUnit ListComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var rt = ArrayComposer((IEnumerable)value, warehouse, connection);

        if (rt == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, new byte[0], 0, 0);
        else
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.List, rt, 0, (uint)rt.Length);
    }


    public static TransmissionDataUnit TypedListComposer(IEnumerable value, Type type, Warehouse warehouse, DistributedConnection connection)
    {
        var composed = ArrayComposer((IEnumerable)value, warehouse, connection);

        if (composed == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, new byte[0], 0, 0);

        var metadata = RepresentationType.FromType(type).Compose();


        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.TypedList, composed, 0,
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

    public static TransmissionDataUnit PropertyValueArrayComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, new byte[0], 0, 0);

        var rt = new List<byte>();
        var ar = value as PropertyValue[];

        foreach (var pv in ar)
        {
            rt.AddRange(Codec.Compose(pv.Age, warehouse, connection));
            rt.AddRange(Codec.Compose(pv.Date, warehouse, connection));
            rt.AddRange(Codec.Compose(pv.Value, warehouse, connection));
        }

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.RawData, rt.ToArray(), 0,
            (uint)rt.Count);
    }

    public static TransmissionDataUnit TypedMapComposer(object value, Type keyType, Type valueType, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, new byte[0], 0, 0);

        var kt = RepresentationType.FromType(keyType).Compose();
        var vt = RepresentationType.FromType(valueType).Compose();

        var rt = new List<byte>();

        var map = (IMap)value;

        foreach (var el in map.Serialize())
            rt.AddRange(Codec.Compose(el, warehouse, connection));


        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.TypedMap, rt.ToArray(), 0, (uint)rt.Count,
            DC.Combine(kt, 0, (uint)kt.Length, vt, 0, (uint)vt.Length));
    }
    public static TransmissionDataUnit TypedDictionaryComposer(object value, Type keyType, Type valueType, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, null, 0, 0);

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


        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.TypedMap, rt.ToArray(), 0, (uint)rt.Count,
            DC.Combine(kt, 0, (uint)kt.Length, vt, 0, (uint)vt.Length));
    }

    public static byte[] ArrayComposer(IEnumerable value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return null;

        var rt = new List<byte>();

        TransmissionDataUnit? previous = null;

        foreach (var i in value)
        {
            var tdu = Codec.ComposeInternal(i, warehouse, connection);
            if (tdu.MatchType(previous.Value))
            {
                rt.AddRange(TransmissionDataUnit.Compose(TransmissionDataUnitIdentifier.NotModified,
                    tdu.Data, null));
            }
            else
            {
                rt.AddRange(tdu.Compose());
            }

            previous = tdu;
        }

        return rt.ToArray();
    }

    public static TransmissionDataUnit ResourceListComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, new byte[0], 0, 0);

        var composed = ArrayComposer((IEnumerable)value, warehouse, connection);

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.ResourceList, composed, 0,
            (uint)composed.Length);
    }

    public static TransmissionDataUnit RecordListComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, new byte[0], 0, 0);

        var composed = ArrayComposer((IEnumerable)value, warehouse, connection);

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.RecordList,
            composed, 0, (uint)composed.Length);
    }


    public static unsafe TransmissionDataUnit ResourceComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var resource = (IResource)value;

        if (resource.Instance == null || resource.Instance.IsDestroyed)
        {
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, null, 0, 0);
        }

        if (Codec.IsLocalResource(resource, connection))
        {
            var rid = (resource as DistributedResource).DistributedResourceInstanceId;

            if (rid <= 0xFF)
                return new TransmissionDataUnit(TransmissionDataUnitIdentifier.LocalResource8, new byte[] { (byte)rid }, 0, 1);
            else if (rid <= 0xFFFF)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((ushort*)ptr) = (ushort)rid;

                return new TransmissionDataUnit(TransmissionDataUnitIdentifier.LocalResource16, rt, 0, 2);
            }
            else
            {
                var rt = new byte[4];
                fixed (byte* ptr = rt)
                    *((uint*)ptr) = rid;
                return new TransmissionDataUnit(TransmissionDataUnitIdentifier.LocalResource32, rt, 0, 4);
            }
        }
        else
        {

            //rt.Append((value as IResource).Instance.Template.ClassId, (value as IResource).Instance.Id);
            connection.cache.Add(value as IResource, DateTime.UtcNow);

            var rid = resource.Instance.Id;

            if (rid <= 0xFF)
                return new TransmissionDataUnit(TransmissionDataUnitIdentifier.RemoteResource8, new byte[] { (byte)rid }, 0, 1);
            else if (rid <= 0xFFFF)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((ushort*)ptr) = (ushort)rid;

                return new TransmissionDataUnit(TransmissionDataUnitIdentifier.RemoteResource16, rt, 0, 2);
            }
            else
            {
                var rt = new byte[4];
                fixed (byte* ptr = rt)
                    *((uint*)ptr) = rid;
                return new TransmissionDataUnit(TransmissionDataUnitIdentifier.RemoteResource32, rt, 0, 4);
            }
        }
    }

    public static unsafe TransmissionDataUnit MapComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, new byte[0], 0, 1);

        var rt = new List<byte>();
        var map = (IMap)value;

        foreach (var el in map.Serialize())
            rt.AddRange(Codec.Compose(el, warehouse, connection));

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Map, rt.ToArray(), 0, (uint)rt.Count);
    }

    public static unsafe TransmissionDataUnit UUIDComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.UUID, ((UUID)value).Data, 0, 16);

    }

    public static unsafe TransmissionDataUnit RecordComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var rt = new List<byte>();// BinaryList();
        var record = (IRecord)value;

        var template = warehouse.GetTemplateByType(record.GetType());


        foreach (var pt in template.Properties)
        {
            var propValue = pt.PropertyInfo.GetValue(record, null);
            rt.AddRange(Codec.Compose(propValue, warehouse, connection));
        }

        return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Record, rt.ToArray(), 0,
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

    public static TransmissionDataUnit TupleComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, new byte[0], 0, 0);

        var fields = value.GetType().GetFields();
        var list = fields.Select(x => x.GetValue(value)).ToArray();
        var types = fields.Select(x => RepresentationType.FromType(x.FieldType).Compose()).ToArray();


        var metadata = new List<byte>();
        foreach (var t in types)
            metadata.AddRange(t);

        var composed = ArrayComposer(list, warehouse, connection);

        if (composed == null)
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.Null, new byte[0], 0, 0);
        else
        {
            return new TransmissionDataUnit(TransmissionDataUnitIdentifier.TypedTuple, composed, 0,
                        (uint)composed.Length, metadata.ToArray());
        }
    }
}


