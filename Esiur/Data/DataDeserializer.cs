using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Data;
using Esiur.Resource.Template;
using System.Linq;

namespace Esiur.Data;

public static class DataDeserializer
{
    public static AsyncReply NullParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new AsyncReply(null);
    }

    public static AsyncReply BooleanTrueParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new AsyncReply<bool>(true);
    }

    public static AsyncReply BooleanFalseParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new AsyncReply<bool>(false);
    }

    public static AsyncReply NotModifiedParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new AsyncReply<NotModified>(new NotModified());
    }

    public static AsyncReply ByteParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new AsyncReply<byte>(data[offset]);
    }
    public static AsyncReply SByteParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new AsyncReply<sbyte>((sbyte)data[offset]);
    }
    public static unsafe AsyncReply Char16Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<char>(*(char*)ptr);
    }

    public static AsyncReply Char8Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new AsyncReply<char>((char)data[offset]);
    }


    public static unsafe AsyncReply Int16Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<short>(*(short*)ptr);
    }

    public static unsafe AsyncReply UInt16Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<ushort>(*(ushort*)ptr);
    }

    public static unsafe AsyncReply Int32Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<int>(*(int*)ptr);
    }

    public static unsafe AsyncReply UInt32Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<uint>(*(uint*)ptr);
    }

    public static unsafe AsyncReply Float32Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<float>(*(float*)ptr);
    }

    public static unsafe AsyncReply Float64Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<double>(*(double*)ptr);
    }

    public static unsafe AsyncReply Float128Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<decimal>(*(decimal*)ptr);
    }

    public static unsafe AsyncReply Int128Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<decimal>(*(decimal*)ptr);
    }


    public static unsafe AsyncReply UInt128Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<decimal>(*(decimal*)ptr);
    }


    public static unsafe AsyncReply Int64Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<long>(*(long*)ptr);
    }

    public static unsafe AsyncReply UInt64Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<ulong>(*(ulong*)ptr);
    }

    public static unsafe AsyncReply DateTimeParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new AsyncReply<DateTime>(new DateTime(*(long*)ptr, DateTimeKind.Utc));

    }


    public static unsafe AsyncReply ResourceParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return connection.Fetch(*(uint*)ptr, requestSequence);

    }

    public static unsafe AsyncReply LocalResourceParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return Warehouse.GetById(*(uint*)ptr);

    }


    public static unsafe AsyncReply RawDataParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new AsyncReply<byte[]>(data.Clip(offset, length));
    }

    public static unsafe AsyncReply StringParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new AsyncReply<string>(data.GetString(offset, length));
    }

    public static unsafe AsyncReply RecordParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {

        var reply = new AsyncReply<IRecord>();

        var classId = data.GetGuid(offset);
        offset += 16;
        length -= 16;


        var template = Warehouse.GetTemplateByClassId((Guid)classId, TemplateType.Record);

        var initRecord = (TypeTemplate template) =>
        {
            ListParser(data, offset, length, connection, requestSequence).Then(r =>
            {
                var ar = (object[])r;

                if (template.DefinedType != null)
                {
                    var record = Activator.CreateInstance(template.DefinedType) as IRecord;
                    for (var i = 0; i < template.Properties.Length; i++)
                    {
                        try
                        {
                            var v = Convert.ChangeType(ar[i], template.Properties[i].PropertyInfo.PropertyType);
                            template.Properties[i].PropertyInfo.SetValue(record, v);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }

                    reply.Trigger(record);
                }
                else
                {
                    var record = new Record();

                    for (var i = 0; i < template.Properties.Length; i++)
                        record.Add(template.Properties[i].Name, ar[i]);

                    reply.Trigger(record);
                }
            });
        };

        if (template != null)
        {
            initRecord(template);
        }
        else
        {
            connection.GetTemplate((Guid)classId).Then(tmp =>
            {
                ListParser(data, offset, length, connection, requestSequence).Then(r =>
                {
                    if (tmp == null)
                        reply.TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.TemplateNotFound,
                            "Template not found for record."));
                    else
                        initRecord(tmp);

                });
            }).Error(x => reply.TriggerError(x));
        }

        return reply;
    }

    public static unsafe AsyncReply ConstantParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        throw new NotImplementedException();
    }

    public static unsafe AsyncReply EnumParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {

        var classId = data.GetGuid(offset);
        offset += 16;
        var index = data[offset++];

        var template = Warehouse.GetTemplateByClassId((Guid)classId, TemplateType.Enum);

        if (template != null)
        {
            return new AsyncReply(template.Constants[index].Value);
        }
        else
        {
            var reply = new AsyncReply();

            connection.GetTemplate((Guid)classId).Then(tmp =>
            {
                reply.Trigger(tmp.Constants[index].Value);
            }).Error(x => reply.TriggerError(x));

            return reply;
        }
    }



    public static AsyncReply RecordListParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        var rt = new AsyncBag<IRecord>();

        while (length > 0)
        {
            var (cs, reply) = Codec.Parse(data, offset, connection, requestSequence);

            rt.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        rt.Seal();
        return rt;
    }

    public static AsyncReply ResourceListParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        var rt = new AsyncBag<IResource>();

        while (length > 0)
        {
            var (cs, reply) = Codec.Parse(data, offset, connection, requestSequence);

            rt.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        rt.Seal();
        return rt;
    }


    public static AsyncBag<object> ListParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        var rt = new AsyncBag<object>();

        while (length > 0)
        {
            var (cs, reply) = Codec.Parse(data, offset, connection, requestSequence);

            rt.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        rt.Seal();
        return rt;
    }

    public static AsyncReply TypedMapParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        // get key type
        var (keyCs, keyRepType) = RepresentationType.Parse(data, offset);
        offset += keyCs;
        length -= keyCs;

        var (valueCs, valueRepType) = RepresentationType.Parse(data, offset);
        offset += valueCs;
        length -= valueCs;

        var map = (IMap)Activator.CreateInstance(typeof(Map<,>).MakeGenericType(keyRepType.GetRuntimeType(), valueRepType.GetRuntimeType()));

        var rt = new AsyncReply();

        var results = new AsyncBag<object>();

        while (length > 0)
        {
            var (cs, reply) = Codec.Parse(data, offset, connection, requestSequence);


            results.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        results.Seal();

        results.Then(ar =>
        {
            for (var i = 0; i < ar.Length; i += 2)
                map.Add(ar[i], ar[i + 1]);

            rt.Trigger(map);
        });


        return rt;

    }

    public static AsyncReply TupleParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        var results = new AsyncBag<object>();
        var rt = new AsyncReply();

        var tupleSize = data[offset++];
        length--;

        var types = new List<Type>();

        for (var i = 0; i < tupleSize; i++)
        {
            var (cs, rep) = RepresentationType.Parse(data, offset);
            types.Add(rep.GetRuntimeType());
            offset += cs;
            length -= cs;
        }

        while (length > 0)
        {
            var (cs, reply) = Codec.Parse(data, offset, connection, requestSequence);

            results.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        results.Seal();


        results.Then(ar =>
                {
                    if (ar.Length == 2)
                    {
                        var type = typeof(ValueTuple<,>).MakeGenericType(types.ToArray());
                        rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1]));
                    }
                    else if (ar.Length == 3)
                    {
                        var type = typeof(ValueTuple<,,>).MakeGenericType(types.ToArray());
                        rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2]));
                    }
                    else if (ar.Length == 4)
                    {
                        var type = typeof(ValueTuple<,,,>).MakeGenericType(types.ToArray());
                        rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3]));
                    }
                });

        return rt;
    }

    public static AsyncReply TypedListParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        var rt = new AsyncBag<object>();

        // get the type
        var (hdrCs, rep) = RepresentationType.Parse(data, offset);

        offset += hdrCs;
        length -= hdrCs;

        var runtimeType = rep.GetRuntimeType();

        rt.ArrayType = runtimeType;

        while (length > 0)
        {
            var (cs, reply) = Codec.Parse(data, offset, connection, requestSequence);

            rt.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        rt.Seal();
        return rt;
    }


    public static AsyncBag<PropertyValue> PropertyValueArrayParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)//, bool ageIncluded = true)
    {
        var rt = new AsyncBag<PropertyValue>();


        ListParser(data, offset, length, connection, requestSequence).Then(x =>
        {
            var ar = (object[])x;
            var pvs = new List<PropertyValue>();

            for (var i = 0; i < ar.Length; i += 3)
                pvs.Add(new PropertyValue(ar[2], (ulong?)ar[0], (DateTime?)ar[1]));


            rt.Trigger(pvs.ToArray());
        });

        return rt;

    }

    public static (uint, AsyncReply<PropertyValue>) PropertyValueParser(byte[] data, uint offset, DistributedConnection connection, uint[] requestSequence)//, bool ageIncluded = true)
    {
        var reply = new AsyncReply<PropertyValue>();

        var age = data.GetUInt64(offset, Endian.Little);
        offset += 8;

        DateTime date = data.GetDateTime(offset, Endian.Little);
        offset += 8;


        var (valueSize, results) = Codec.Parse(data, offset, connection, requestSequence);

        results.Then(value =>
        {
            reply.Trigger(new PropertyValue(value, age, date));
        });

        return (16 + valueSize, reply);
    }

    public static AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> HistoryParser(byte[] data, uint offset, uint length, IResource resource, DistributedConnection connection, uint[] requestSequence)
    {
        //var count = (int)toAge - (int)fromAge;

        var list = new KeyList<PropertyTemplate, PropertyValue[]>();

        var reply = new AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>>();

        var bagOfBags = new AsyncBag<PropertyValue[]>();

        var ends = offset + length;
        while (offset < ends)
        {
            var index = data[offset++];
            var pt = resource.Instance.Template.GetPropertyTemplateByIndex(index);
            list.Add(pt, null);
            var cs = data.GetUInt32(offset, Endian.Little);
            offset += 4;

            var (len, pv) = PropertyValueParser(data, offset, connection, requestSequence);

            bagOfBags.Add(pv);// ParsePropertyValueArray(data, offset, cs, connection));
            offset += len;
        }

        bagOfBags.Seal();

        bagOfBags.Then(x =>
        {
            for (var i = 0; i < list.Count; i++)
                list[list.Keys.ElementAt(i)] = x[i];

            reply.Trigger(list);
        });

        return reply;

    }

}
