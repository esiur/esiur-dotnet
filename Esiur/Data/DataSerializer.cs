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

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) Int32Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (int)value;
        var rt = new byte[4];
        fixed (byte* ptr = rt)
            *((int*)ptr) = v;
        return (TransmissionDataUnitIdentifier.Int32, rt);
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) UInt32Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (uint)value;
        var rt = new byte[4];
        fixed (byte* ptr = rt)
            *((uint*)ptr) = v;
        return (TransmissionDataUnitIdentifier.UInt32, rt);
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) Int16Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (short)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((short*)ptr) = v;
        return (TransmissionDataUnitIdentifier.Int16, rt);
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) UInt16Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (ushort)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((ushort*)ptr) = v;
        return (TransmissionDataUnitIdentifier.UInt16, rt);
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) Float32Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (float)value;
        var rt = new byte[4];
        fixed (byte* ptr = rt)
            *((float*)ptr) = v;
        return (TransmissionDataUnitIdentifier.Float32, rt);
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) Float64Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (double)value;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((double*)ptr) = v;
        return (TransmissionDataUnitIdentifier.Float64, rt);
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) Int64Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (long)value;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((long*)ptr) = v;
        return (TransmissionDataUnitIdentifier.Int64, rt);
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) UIn64Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (ulong)value;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((ulong*)ptr) = v;
        return (TransmissionDataUnitIdentifier.UInt64, rt);
    }


    public static unsafe (TransmissionDataUnitIdentifier, byte[]) DateTimeComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = ((DateTime)value).ToUniversalTime().Ticks;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((long*)ptr) = v;
        return (TransmissionDataUnitIdentifier.DateTime, rt);
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) Float128Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var v = (decimal)value;
        var rt = new byte[16];
        fixed (byte* ptr = rt)
            *((decimal*)ptr) = v;
        return (TransmissionDataUnitIdentifier.Decimal128, rt);
    }



    public static (TransmissionDataUnitIdentifier, byte[]) StringComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return (TransmissionDataUnitIdentifier.String, Encoding.UTF8.GetBytes((string)value));
    }

    public static (TransmissionDataUnitIdentifier, byte[]) EnumComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);

        //var warehouse = connection?.Instance?.Warehouse ?? connection?.Server?.Instance?.Warehouse;
        //if (warehouse == null)
        //    throw new Exception("Warehouse not set.");

        var template = warehouse.GetTemplateByType(value.GetType());

        var intVal = Convert.ChangeType(value, (value as Enum).GetTypeCode());

        var ct = template.Constants.FirstOrDefault(x => x.Value.Equals(intVal));

        if (ct == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);


        var rt = new List<byte>();
        rt.AddRange(template.ClassId.Data);
        rt.Add(ct.Index);

        return (TransmissionDataUnitIdentifier.TypedEnum, rt.ToArray());
    }

    public static (TransmissionDataUnitIdentifier, byte[]) UInt8Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return (TransmissionDataUnitIdentifier.UInt8, new byte[] { (byte)value });
    }

    public static (TransmissionDataUnitIdentifier, byte[]) Int8Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return (TransmissionDataUnitIdentifier.Int8, new byte[] { (byte)(sbyte)value });
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) Char8Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return (TransmissionDataUnitIdentifier.Char8, new byte[] { (byte)(char)value });
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) Char16Composer(object value, Warehouse warehouse, DistributedConnection connection)
    {

        var v = (char)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((char*)ptr) = v;
        return (TransmissionDataUnitIdentifier.Char16, rt);

    }

    public static (TransmissionDataUnitIdentifier, byte[]) BoolComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return ((bool)value ? TransmissionDataUnitIdentifier.True : TransmissionDataUnitIdentifier.False, new byte[0]);
    }


    public static (TransmissionDataUnitIdentifier, byte[]) NotModifiedComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return (TransmissionDataUnitIdentifier.NotModified, new byte[0]);
    }

    public static (TransmissionDataUnitIdentifier, byte[]) RawDataComposerFromArray(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return (TransmissionDataUnitIdentifier.RawData, (byte[])value);
    }

    public static (TransmissionDataUnitIdentifier, byte[]) RawDataComposerFromList(dynamic value, Warehouse warehouse, DistributedConnection connection)
    {
        return (TransmissionDataUnitIdentifier.RawData, (value as List<byte>).ToArray());
    }

    //public static (TransmissionDataUnitIdentifier, byte[]) ListComposerFromArray(dynamic value, DistributedConnection connection)
    //{
    //    var rt = new List<byte>();
    //    var array = (object[])value;

    //    for (var i = 0; i < array.Length; i++)
    //        rt.AddRange(Codec.Compose(array[i], connection));

    //    return (TransmissionDataUnitIdentifier.List, rt.ToArray());
    //}

    public static (TransmissionDataUnitIdentifier, byte[]) ListComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {

        var rt = ArrayComposer((IEnumerable)value, warehouse, connection);

        if (rt == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);
        else
            return (TransmissionDataUnitIdentifier.List, rt);


        //var rt = new List<byte>();
        //var list = (IEnumerable)value;// ((List<object>)value);

        //foreach (var o in list)
        //    rt.AddRange(Codec.Compose(o, connection));

        //return (TransmissionDataUnitIdentifier.List, rt.ToArray());
    }


    public static (TransmissionDataUnitIdentifier, byte[]) TypedListComposer(IEnumerable value, Type type, Warehouse warehouse, DistributedConnection connection)
    {
        var composed = ArrayComposer((IEnumerable)value, warehouse, connection);

        if (composed == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);

        var header = RepresentationType.FromType(type).Compose();
        
        var rt = new List<byte>();

        rt.AddRange(header);
        rt.AddRange(composed);

        return (TransmissionDataUnitIdentifier.TypedList, rt.ToArray());
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

    public static (TransmissionDataUnitIdentifier, byte[]) PropertyValueArrayComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);

        var rt = new List<byte>();
        var ar = value as PropertyValue[];

        foreach (var pv in ar)
        {
            rt.AddRange(Codec.Compose(pv.Age, warehouse, connection));
            rt.AddRange(Codec.Compose(pv.Date, warehouse, connection));
            rt.AddRange(Codec.Compose(pv.Value, warehouse, connection));
        }

        return (TransmissionDataUnitIdentifier.RawData, rt.ToArray());
    }

    public static (TransmissionDataUnitIdentifier, byte[]) TypedMapComposer(object value, Type keyType, Type valueType, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);

        var kt = RepresentationType.FromType(keyType).Compose();
        var vt = RepresentationType.FromType(valueType).Compose();

        var rt = new List<byte>();

        rt.AddRange(kt);
        rt.AddRange(vt);

        var map = (IMap)value;

        foreach (var el in map.Serialize())
            rt.AddRange(Codec.Compose(el, warehouse, connection));


        return (TransmissionDataUnitIdentifier.TypedMap, rt.ToArray());
    }
    public static (TransmissionDataUnitIdentifier, byte[]) TypedDictionaryComposer(object value, Type keyType, Type valueType, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);

        var kt = RepresentationType.FromType(keyType).Compose();
        var vt = RepresentationType.FromType(valueType).Compose();

        var rt = new List<byte>();

        rt.AddRange(kt);
        rt.AddRange(vt);

        var dic = (IDictionary)value;

        var ar = new List<object>();
        foreach (var k in dic.Keys)
        {
            ar.Add(k);
            ar.Add(dic[k]);
        }

        foreach (var el in ar)
            rt.AddRange(Codec.Compose(el, warehouse, connection));


        return (TransmissionDataUnitIdentifier.TypedMap, rt.ToArray());
    }

    public static byte[] ArrayComposer(IEnumerable value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return null;

        var rt = new List<byte>();

        TransmissionDataUnitIdentifier? previous = null;
        byte[]? previousUUID = null;

        foreach (var i in value)
        {
            var (hdr, data) = Codec.ComposeInternal(i, warehouse, connection);
            if (previous == null)
                previous = hdr;
            else if (hdr == previous)
            {
                if (hdr == TransmissionDataUnitIdentifier.Record)
                {
                    var newUUID = data.Take(16).ToArray();
                    // check same uuid
                    if (newUUID.SequenceEqual(previousUUID))
                        rt.AddRange(TransmissionDataUnit.Compose(TransmissionDataUnitIdentifier.Same,
                            data.Skip(16).ToArray()));
                    else
                        rt.AddRange(TransmissionDataUnit.Compose(hdr, data));

                    previous = hdr;
                    previousUUID = newUUID;
                }
            }
                
            rt.AddRange(Codec.Compose(i, warehouse, connection));
        }

        return rt.ToArray();
    }

    public static (TransmissionDataUnitIdentifier, byte[]) ResourceListComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);


        return (TransmissionDataUnitIdentifier.ResourceList, ArrayComposer((IEnumerable)value, warehouse, connection));
    }

    public static (TransmissionDataUnitIdentifier, byte[]) RecordListComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);


        return (TransmissionDataUnitIdentifier.RecordList, ArrayComposer((IEnumerable)value, warehouse, connection));
    }


    public static unsafe (TransmissionDataUnitIdentifier, byte[]) ResourceComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var resource = (IResource)value;

        if (resource.Instance == null || resource.Instance.IsDestroyed)
        {
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);
        }

        if (Codec.IsLocalResource(resource, connection))
        {
            var rid = (resource as DistributedResource).DistributedResourceInstanceId;

            if (rid <= 0xFF)
                return (TransmissionDataUnitIdentifier.LocalResource8, new byte[] { (byte)rid });
            else if (rid <= 0xFFFF)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((ushort*)ptr) = (ushort)rid;

                return (TransmissionDataUnitIdentifier.LocalResource16, rt);
            }
            else
            {
                var rt = new byte[4];
                fixed (byte* ptr = rt)
                    *((uint*)ptr) = rid;
                return (TransmissionDataUnitIdentifier.LocalResource32, rt);
            }
        }
        else
        {

            //rt.Append((value as IResource).Instance.Template.ClassId, (value as IResource).Instance.Id);
            connection.cache.Add(value as IResource, DateTime.UtcNow);

            var rid = resource.Instance.Id;

            if (rid <= 0xFF)
                return (TransmissionDataUnitIdentifier.RemoteResource8, new byte[] { (byte)rid });
            else if (rid <= 0xFFFF)
            {
                var rt = new byte[2];
                fixed (byte* ptr = rt)
                    *((ushort*)ptr) = (ushort)rid;

                return (TransmissionDataUnitIdentifier.RemoteResource16, rt);
            }
            else
            {
                var rt = new byte[4];
                fixed (byte* ptr = rt)
                    *((uint*)ptr) = rid;
                return (TransmissionDataUnitIdentifier.RemoteResource32, rt);
            }
        }
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) MapComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0]);

        var rt = new List<byte>();
        var map = (IMap)value;

        foreach (var el in map.Serialize())
            rt.AddRange(Codec.Compose(el, warehouse, connection));

        return (TransmissionDataUnitIdentifier.Map, rt.ToArray());
    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) UUIDComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        return (TransmissionDataUnitIdentifier.UUID, ((UUID)value).Data);

    }

    public static unsafe (TransmissionDataUnitIdentifier, byte[]) RecordComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        var rt = new List<byte>();// BinaryList();
        var record = (IRecord)value;

        var template = warehouse.GetTemplateByType(record.GetType());


        rt.AddRange(template.ClassId.Data);

        foreach (var pt in template.Properties)
        {
            var propValue = pt.PropertyInfo.GetValue(record, null);
            rt.AddRange(Codec.Compose(propValue, warehouse, connection));
        }

        return (TransmissionDataUnitIdentifier.Record, rt.ToArray());
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

    public static TransmissionDataUnitIdentifier TupleComposer(object value, Warehouse warehouse, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0], new byte[0]);

        var rt = new List<byte>();

        var fields = value.GetType().GetFields();
        var list = fields.Select(x => x.GetValue(value)).ToArray();
        var types = fields.Select(x => RepresentationType.FromType(x.FieldType).Compose()).ToArray();


        foreach (var t in types)
            rt.AddRange(t);

        var composed = ArrayComposer(list, warehouse, connection);

        if (composed == null)
            return (TransmissionDataUnitIdentifier.Null, new byte[0], new byte[0]);
        else
        {
            rt.AddRange(composed);
            return (TransmissionDataUnitIdentifier.TypedTuple, rt.ToArray(), composed);
        }
    }
}


