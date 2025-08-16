using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Data;
using Esiur.Resource.Template;
using System.Linq;
using Esiur.Misc;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Esiur.Data;

public static class DataDeserializer
{
    public static object NullParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return null;
    }

    public static object NullParser(byte[] data, uint offset, uint length)
    {
        return null;
    }

    public static object BooleanTrueParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return true;
    }

    public static object BooleanTrueParser(byte[] data, uint offset, uint length)
    {
        return true;
    }

    public static object BooleanFalseParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return false;
    }

    public static object BooleanFalseParser(byte[] data, uint offset, uint length)
    {
        return false;
    }

    public static object NotModifiedParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return NotModified.Default;
    }

    public static object NotModifiedParser(byte[] data, uint offset, uint length)
    {
        return NotModified.Default;
    }

    public static object UInt8ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return data[offset];
    }
    public static object UInt8Parser(byte[] data, uint offset, uint length)
    {
        return data[offset];
    }

    public static object Int8ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return (sbyte)data[offset];
    }
    public static object Int8Parser(byte[] data, uint offset, uint length)
    {
        return (sbyte)data[offset];
    }

    public static unsafe object Char16ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(char*)ptr;
    }

    public static unsafe object Char16Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(char*)ptr;
    }

    public static object Char8ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return (char)data[offset];
    }

    public static object Char8Parser(byte[] data, uint offset, uint length)
    {
        return (char)data[offset];
    }


    public static unsafe object Int16ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(short*)ptr;
    }

    public static unsafe object Int16Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(short*)ptr;
    }

    public static unsafe object UInt16ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(ushort*)ptr;
    }

    public static unsafe object UInt16Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(ushort*)ptr;
    }

    public static unsafe object Int32ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(int*)ptr;
    }

    public static unsafe object Int32Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(int*)ptr;
    }

    public static unsafe object UInt32ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(uint*)ptr;
    }

    public static unsafe object UInt32Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(uint*)ptr;
    }


    public static unsafe object Float32ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(float*)ptr;
    }

    public static unsafe object Float32Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(float*)ptr;
    }

    public static unsafe object Float64ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(double*)ptr;
    }

    public static unsafe object Float64Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(double*)ptr;
    }


    public static unsafe object Float128ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(decimal*)ptr;
    }

    public static unsafe object Float128Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(decimal*)ptr;
    }

    public static unsafe object Int128Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(decimal*)ptr;
    }

    public static unsafe object Int128ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(decimal*)ptr;
    }


    public static unsafe object UInt128ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(decimal*)ptr;
    }

    public static unsafe object UInt128Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(decimal*)ptr;
    }

    public static unsafe object Int64ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(long*)ptr;
    }

    public static unsafe object Int64Parser(byte[] data, uint offset, uint length)
    {
        fixed (byte* ptr = &data[offset])
            return *(long*)ptr;
    }


    public static unsafe object UInt64Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(ulong*)ptr;
    }

    public static unsafe object DateTimeParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new DateTime(*(long*)ptr, DateTimeKind.Utc);

    }

    public static unsafe object DateTimeParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new DateTime(*(long*)ptr, DateTimeKind.Utc);

    }

    public static unsafe object ResourceParser8(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        if (connection == null)
            return new ResourceId(false, data[offset]);
        else
            return connection.Fetch(data[offset], requestSequence);
    }

    public static unsafe object LocalResourceParser8(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        if (connection == null)
            return new ResourceId(true, data[offset]);
        else
            return Warehouse.GetById(data[offset]);
    }

    public static unsafe object ResourceParser16(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            if (connection == null)
                return new ResourceId(false, *(ushort*)ptr);
            else
                return connection.Fetch(*(ushort*)ptr, requestSequence);
    }

    public static unsafe object LocalResourceParser16(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            if (connection == null)
                return new ResourceId(true, *(ushort*)ptr);
            else
                return Warehouse.GetById(*(ushort*)ptr);
    }

    public static unsafe object ResourceParser32(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            if (connection == null)
                return new ResourceId(false, *(uint*)ptr);
            else
                return connection.Fetch(*(uint*)ptr, requestSequence);
    }

    public static unsafe object LocalResourceParser32(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            if (connection == null)
                return new ResourceId(true, *(uint*)ptr);
            else
                return Warehouse.GetById(*(uint*)ptr);
    }


    public static unsafe object RawDataParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return data.Clip(offset, length);
    }

    public static unsafe object StringParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return data.GetString(offset, length);
    }

    public static unsafe object RecordParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {

        var reply = new AsyncReply<IRecord>();

        var classId = data.GetUUID(offset);
        offset += 16;
        length -= 16;


        var template = Warehouse.GetTemplateByClassId(classId, TemplateType.Record);

        var initRecord = (TypeTemplate template) =>
        {
            ListParser(data, offset, length, connection, requestSequence).Then(r =>
            {
                var ar = (object[])r;

                if (template == null)
                {
                    // @TODO: add parse if no template settings
                    reply.TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.TemplateNotFound,
                            "Template not found for record."));
                }
                else if (template.DefinedType != null)
                {
                    var record = Activator.CreateInstance(template.DefinedType) as IRecord;
                    for (var i = 0; i < template.Properties.Length; i++)
                    {
                        try
                        {
                            //var v = Convert.ChangeType(ar[i], template.Properties[i].PropertyInfo.PropertyType);
                            var v = DC.CastConvert(ar[i], template.Properties[i].PropertyInfo.PropertyType);
                            template.Properties[i].PropertyInfo.SetValue(record, v);
                        }
                        catch (Exception ex)
                        {
                            Global.Log(ex);
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
        else if (connection != null)
        {
            // try to get the template from the other end
            connection.GetTemplate(classId).Then(tmp =>
            {
                initRecord(tmp);
            }).Error(x => reply.TriggerError(x));
        }
        else
        {
            initRecord(null);
        }

        return reply;
    }

    public static unsafe object ConstantParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        throw new NotImplementedException();
    }

    public static unsafe AsyncReply EnumParser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {

        var classId = data.GetUUID(offset);
        offset += 16;
        var index = data[offset++];

        var template = Warehouse.GetTemplateByClassId(classId, TemplateType.Enum);

        if (template != null)
        {
            return new AsyncReply(template.Constants[index].Value);
        }
        else
        {
            var reply = new AsyncReply();

            connection.GetTemplate(classId).Then(tmp =>
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
            var (cs, reply) = Codec.ParseAsync(data, offset, connection, requestSequence);

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
            var (cs, reply) = Codec.ParseAsync(data, offset, connection, requestSequence);

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
            var (cs, reply) = Codec.ParseAsync(data, offset, connection, requestSequence);

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
            var (cs, reply) = Codec.ParseAsync(data, offset, connection, requestSequence);


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
            var (cs, reply) = Codec.ParseAsync(data, offset, connection, requestSequence);

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
            var (cs, reply) = Codec.ParseAsync(data, offset, connection, requestSequence);

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


        var (valueSize, results) = Codec.ParseAsync(data, offset, connection, requestSequence);

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
