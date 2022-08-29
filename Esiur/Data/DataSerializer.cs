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

    public static unsafe (TransmissionTypeIdentifier, byte[]) Int32Composer(object value, DistributedConnection connection)
    {
        var v = (int)value;
        var rt = new byte[4];
        fixed (byte* ptr = rt)
            *((int*)ptr) = v;
        return (TransmissionTypeIdentifier.Int32, rt);
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) UInt32Composer(object value, DistributedConnection connection)
    {
        var v = (uint)value;
        var rt = new byte[4];
        fixed (byte* ptr = rt)
            *((uint*)ptr) = v;
        return (TransmissionTypeIdentifier.UInt32, rt);
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) Int16Composer(object value, DistributedConnection connection)
    {
        var v = (short)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((short*)ptr) = v;
        return (TransmissionTypeIdentifier.Int16, rt);
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) UInt16Composer(object value, DistributedConnection connection)
    {
        var v = (ushort)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((ushort*)ptr) = v;
        return (TransmissionTypeIdentifier.UInt16, rt);
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) Float32Composer(object value, DistributedConnection connection)
    {
        var v = (float)value;
        var rt = new byte[4];
        fixed (byte* ptr = rt)
            *((float*)ptr) = v;
        return (TransmissionTypeIdentifier.Float32, rt);
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) Float64Composer(object value, DistributedConnection connection)
    {
        var v = (double)value;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((double*)ptr) = v;
        return (TransmissionTypeIdentifier.Float64, rt);
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) Int64Composer(object value, DistributedConnection connection)
    {
        var v = (long)value;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((long*)ptr) = v;
        return (TransmissionTypeIdentifier.Int64, rt);
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) UIn64Composer(object value, DistributedConnection connection)
    {
        var v = (ulong)value;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((ulong*)ptr) = v;
        return (TransmissionTypeIdentifier.UInt64, rt);
    }


    public static unsafe (TransmissionTypeIdentifier, byte[]) DateTimeComposer(object value, DistributedConnection connection)
    {
        var v = ((DateTime)value).ToUniversalTime().Ticks;
        var rt = new byte[8];
        fixed (byte* ptr = rt)
            *((long*)ptr) = v;
        return (TransmissionTypeIdentifier.DateTime, rt);
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) Float128Composer(object value, DistributedConnection connection)
    {
        var v = (decimal)value;
        var rt = new byte[16];
        fixed (byte* ptr = rt)
            *((decimal*)ptr) = v;
        return (TransmissionTypeIdentifier.Float128, rt);
    }



    public static (TransmissionTypeIdentifier, byte[]) StringComposer(object value, DistributedConnection connection)
    {
        return (TransmissionTypeIdentifier.String, Encoding.UTF8.GetBytes((string)value));
    }

    public static (TransmissionTypeIdentifier, byte[]) EnumComposer(object value, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);

        var template = Warehouse.GetTemplateByType(value.GetType());

        var intVal = Convert.ChangeType(value, (value as Enum).GetTypeCode());

        var ct = template.Constants.FirstOrDefault(x => x.Value.Equals(intVal));
        
        if (ct == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);


        var rt = new List<byte>();
        rt.AddRange(template.ClassId.ToByteArray());
        rt.Add(ct.Index);

        return (TransmissionTypeIdentifier.Enum, rt.ToArray());
    }

    public static (TransmissionTypeIdentifier, byte[]) UInt8Composer(object value, DistributedConnection connection)
    {
        return (TransmissionTypeIdentifier.UInt8, new byte[] { (byte)value });
    }

    public static (TransmissionTypeIdentifier, byte[]) Int8Composer(object value, DistributedConnection connection)
    {
        return (TransmissionTypeIdentifier.Int8, new byte[] { (byte)(sbyte)value });
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) Char8Composer(object value, DistributedConnection connection)
    {
       return (TransmissionTypeIdentifier.Char8, new byte[] { (byte)(char)value });
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) Char16Composer(object value, DistributedConnection connection)
    {

        var v = (char)value;
        var rt = new byte[2];
        fixed (byte* ptr = rt)
            *((char*)ptr) = v;
        return (TransmissionTypeIdentifier.Char16, rt);

    }

    public static (TransmissionTypeIdentifier, byte[]) BoolComposer(object value, DistributedConnection connection)
    {
        return ((bool)value ? TransmissionTypeIdentifier.True : TransmissionTypeIdentifier.False, new byte[0]);
    }


    public static (TransmissionTypeIdentifier, byte[]) NotModifiedComposer(object value, DistributedConnection connection)
    {
        return (TransmissionTypeIdentifier.NotModified, new byte[0]);
    }

    public static (TransmissionTypeIdentifier, byte[]) RawDataComposerFromArray(object value, DistributedConnection connection)
    {
        return (TransmissionTypeIdentifier.RawData, (byte[])value);
    }

    public static (TransmissionTypeIdentifier, byte[]) RawDataComposerFromList(dynamic value, DistributedConnection connection)
    {
        return (TransmissionTypeIdentifier.RawData, (value as List<byte>).ToArray());
    }

    //public static (TransmissionTypeIdentifier, byte[]) ListComposerFromArray(dynamic value, DistributedConnection connection)
    //{
    //    var rt = new List<byte>();
    //    var array = (object[])value;

    //    for (var i = 0; i < array.Length; i++)
    //        rt.AddRange(Codec.Compose(array[i], connection));

    //    return (TransmissionTypeIdentifier.List, rt.ToArray());
    //}

    public static (TransmissionTypeIdentifier, byte[]) ListComposer(object value, DistributedConnection connection)
    {

        var rt = ArrayComposer((IEnumerable)value, connection);

        if (rt == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);
        else
            return (TransmissionTypeIdentifier.List, rt);


        //var rt = new List<byte>();
        //var list = (IEnumerable)value;// ((List<object>)value);

        //foreach (var o in list)
        //    rt.AddRange(Codec.Compose(o, connection));

        //return (TransmissionTypeIdentifier.List, rt.ToArray());
    }


    public static (TransmissionTypeIdentifier, byte[]) TypedListComposer(IEnumerable value, Type type, DistributedConnection connection)
    {
        var composed = ArrayComposer((IEnumerable)value, connection);

        if (composed == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);

        var header = RepresentationType.FromType(type).Compose();

        var rt = new List<byte>();

        rt.AddRange(header);
        rt.AddRange(composed);

        return (TransmissionTypeIdentifier.TypedList, rt.ToArray());
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

    public static (TransmissionTypeIdentifier, byte[])  PropertyValueArrayComposer(object value, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);

        var rt = new List<byte>();
        var ar = value as PropertyValue[];

        foreach (var pv in ar)
        {
            rt.AddRange(Codec.Compose(pv.Age, connection));
            rt.AddRange(Codec.Compose(pv.Date, connection));
            rt.AddRange(Codec.Compose(pv.Value, connection));
        }

        return (TransmissionTypeIdentifier.List, rt.ToArray());
    }

    public static (TransmissionTypeIdentifier, byte[]) TypedMapComposer(object value, Type keyType, Type valueType, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);

        var kt = RepresentationType.FromType(keyType).Compose();
        var vt = RepresentationType.FromType(valueType).Compose();

        var rt = new List<byte>();

        rt.AddRange(kt);
        rt.AddRange(vt);

        var map = (IMap)value;

        foreach(var el in map.Serialize())
            rt.AddRange(Codec.Compose(el, connection));
        
        return (TransmissionTypeIdentifier.TypedMap, rt.ToArray());
    }

    public static byte[] ArrayComposer(IEnumerable value, DistributedConnection connection)
    {
        if (value == null)
            return null;

        var rt = new List<byte>();

        foreach (var i in value)
            rt.AddRange(Codec.Compose(i, connection));

        return rt.ToArray();
    }

    public static (TransmissionTypeIdentifier, byte[]) ResourceListComposer(object value, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);


        return (TransmissionTypeIdentifier.ResourceList, ArrayComposer((IEnumerable)value, connection));
    }

    public static (TransmissionTypeIdentifier, byte[]) RecordListComposer(object value, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);


        return (TransmissionTypeIdentifier.RecordList, ArrayComposer((IEnumerable)value, connection));
    }


    public static unsafe (TransmissionTypeIdentifier, byte[]) ResourceComposer(object value, DistributedConnection connection)
    {
        var resource = (IResource)value;
        var rt = new byte[4];

        if (Codec.IsLocalResource(resource, connection))
        {

            fixed (byte* ptr = rt)
                *((uint*)ptr) = (resource as DistributedResource).DistributedResourceInstanceId;

            return (TransmissionTypeIdentifier.ResourceLocal, rt);
        }
        else
        {
            //rt.Append((value as IResource).Instance.Template.ClassId, (value as IResource).Instance.Id);
            connection.cache.Add(value as IResource, DateTime.UtcNow);

            fixed (byte* ptr = rt)
                *((uint*)ptr) = resource.Instance.Id;

            return (TransmissionTypeIdentifier.Resource, rt);
        }
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) MapComposer(object value, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);

        var rt = new List<byte>();
        var map = (IMap)value;

        foreach (var el in map.Serialize())
            rt.AddRange(Codec.Compose(el, connection));

        return (TransmissionTypeIdentifier.Map, rt.ToArray());
    }

    public static unsafe (TransmissionTypeIdentifier, byte[]) RecordComposer(object value, DistributedConnection connection)
    {
        var rt = new List<byte>();// BinaryList();
        var record = (IRecord)value;

        var template = Warehouse.GetTemplateByType(record.GetType());


        rt.AddRange(template.ClassId.ToByteArray());

        foreach (var pt in template.Properties)
        {
            var propValue = pt.PropertyInfo.GetValue(record, null);
            rt.AddRange(Codec.Compose(propValue, connection));
        }

        return (TransmissionTypeIdentifier.Record, rt.ToArray());
    }
    public static byte[] HistoryComposer(KeyList<PropertyTemplate, PropertyValue[]> history,
                                        DistributedConnection connection, bool prependLength = false)
    {
        //@TODO:Test
        var rt = new BinaryList();

        for (var i = 0; i < history.Count; i++)
            rt.AddUInt8(history.Keys.ElementAt(i).Index)
              .AddUInt8Array(Codec.Compose(history.Values.ElementAt(i), connection));

        if (prependLength)
            rt.InsertInt32(0, rt.Length);

        return rt.ToArray();
    }

    public static (TransmissionTypeIdentifier, byte[]) TupleComposer(object value, DistributedConnection connection)
    {
        if (value == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);

        var rt = new List<byte>();

        var fields = value.GetType().GetFields();
        var list =  fields.Select(x => x.GetValue(value)).ToArray();
        var types = fields.Select(x => RepresentationType.FromType(x.FieldType).Compose()).ToArray();

        rt.Add((byte)list.Length);

        foreach (var t in types)
            rt.AddRange(t);

        var composed = ArrayComposer(list, connection);

        if (composed == null)
            return (TransmissionTypeIdentifier.Null, new byte[0]);
        else
        {
            rt.AddRange(composed);
            return (TransmissionTypeIdentifier.Tuple, rt.ToArray());
        }
    }
}


