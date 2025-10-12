using Esiur.Core;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Net.IIP;
using Esiur.Resource;
using Esiur.Resource.Template;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace Esiur.Data;

public static class DataDeserializer
{
    public static object NullParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return null;
    }

    public static object NullParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return null;
    }

    public static object BooleanTrueParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return true;
    }

    public static object BooleanTrueParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return true;
    }

    public static object BooleanFalseParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return false;
    }

    public static object BooleanFalseParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return false;
    }

    public static object NotModifiedParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return NotModified.Default;
    }

    public static object NotModifiedParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return NotModified.Default;
    }

    public static object UInt8ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return data[offset];
    }
    public static object UInt8Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        return tdu.Data[tdu.Offset];
    }

    public static object Int8ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return (sbyte)data[offset];
    }
    public static object Int8Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        return (sbyte)tdu.Data[tdu.Offset];
    }

    public static unsafe object Char16ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(char*)ptr;
    }

    public static unsafe object Char16Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(char*)ptr;
    }

    public static object Char8ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return (char)data[offset];
    }

    public static object Char8Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        return (char)tdu.Data[tdu.Offset];
    }


    public static unsafe object Int16ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(short*)ptr;
    }

    public static unsafe object Int16Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(short*)ptr;
    }

    public static unsafe object UInt16ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(ushort*)ptr;
    }

    public static unsafe object UInt16Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(ushort*)ptr;
    }

    public static unsafe object Int32ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(int*)ptr;
    }

    public static unsafe object Int32Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(int*)ptr;
    }

    public static unsafe object UInt32ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(uint*)ptr;
    }

    public static unsafe object UInt32Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(uint*)ptr;
    }


    public static unsafe object Float32ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(float*)ptr;
    }

    public static unsafe object Float32Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(float*)ptr;
    }

    public static unsafe object Float64ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(double*)ptr;
    }

    public static unsafe object Float64Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(double*)ptr;
    }


    public static unsafe object Decimal128ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(decimal*)ptr;
    }

    public static unsafe object Decimal128Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(decimal*)ptr;
    }

    public static unsafe object UUIDParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return new UUID(data, offset);
    }

    public static unsafe object UUIDParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return new UUID(tdu.Data, tdu.Offset);
    }



    public static unsafe object Int128ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr1 = &data[offset])
        fixed (byte* ptr2 = &data[offset + 8])
            return new Int128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object Int128Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.Offset])
        fixed (byte* ptr2 = &tdu.Data[tdu.Offset + 8])
            return new Int128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object UInt128ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr1 = &data[offset])
        fixed (byte* ptr2 = &data[offset + 8])
            return new UInt128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object UInt128Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.Offset])
        fixed (byte* ptr2 = &tdu.Data[tdu.Offset + 8])
            return new UInt128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object Int64ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(long*)ptr;
    }

    public static unsafe object Int64Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(long*)ptr;
    }


    public static unsafe object UInt64ParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return *(ulong*)ptr;
    }

    public static unsafe object UInt64Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(ulong*)ptr;
    }


    public static unsafe object DateTimeParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            return new DateTime(*(long*)ptr, DateTimeKind.Utc);

    }
    public static unsafe object DateTimeParser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return new DateTime(*(long*)ptr, DateTimeKind.Utc);

    }


    public static unsafe object ResourceParser8Async(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        if (connection == null)
            return new ResourceId(false, data[offset]);
        else
            return connection.Fetch(data[offset], requestSequence);
    }

    public static unsafe object ResourceParser8(ParsedTDU tdu, Warehouse warehouse)
    {
        return new ResourceId(false, tdu.Data[tdu.Offset]);
    }

    public static unsafe object LocalResourceParser8Async(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        if (connection == null)
            return new ResourceId(true, data[offset]);
        else
            return connection.Instance.Warehouse.GetById(data[offset]);
    }

    public static unsafe object LocalResourceParser8(ParsedTDU tdu, Warehouse warehouse)
    {
        return new ResourceId(true, tdu.Data[tdu.Offset]);
    }

    public static unsafe object ResourceParser16Async(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            if (connection == null)
                return new ResourceId(false, *(ushort*)ptr);
            else
                return connection.Fetch(*(ushort*)ptr, requestSequence);
    }

    public static unsafe object ResourceParser16(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return new ResourceId(false, *(ushort*)ptr);
    }


    public static unsafe object LocalResourceParser16Async(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            if (connection == null)
                return new ResourceId(true, *(ushort*)ptr);
            else
                return connection.Instance.Warehouse.GetById(*(ushort*)ptr);
    }

    public static unsafe object LocalResourceParser16(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return new ResourceId(true, *(ushort*)ptr);
    }

    public static unsafe object ResourceParser32Async(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            if (connection == null)
                return new ResourceId(false, *(uint*)ptr);
            else
                return connection.Fetch(*(uint*)ptr, requestSequence);
    }

    public static unsafe object ResourceParser32(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return new ResourceId(false, *(uint*)ptr);
    }


    public static unsafe object LocalResourceParser32Async(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &data[offset])
            if (connection == null)
                return new ResourceId(true, *(uint*)ptr);
            else
                return connection.Instance.Warehouse.GetById(*(uint*)ptr);
    }

    public static unsafe object LocalResourceParser32(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return new ResourceId(true, *(uint*)ptr);
    }


    public static unsafe object RawDataParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return data.Clip(offset, length);
    }

    public static unsafe object RawDataParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return tdu.Data.Clip(tdu.Offset, (uint)tdu.ContentLength);
    }


    public static unsafe object StringParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        return data.GetString(offset, length);
    }

    public static unsafe object StringParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return tdu.Data.GetString(tdu.Offset, (uint)tdu.ContentLength);
    }

    public static unsafe object RecordParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {

        var reply = new AsyncReply<IRecord>();

        var classId = data.GetUUID(offset);
        offset += 16;
        length -= 16;


        var template = connection.Instance.Warehouse.GetTemplateByClassId(classId, TemplateType.Record);

        var initRecord = (TypeTemplate template) =>
        {
            ListParserAsync(data, offset, length, connection, requestSequence).Then(r =>
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


    public static unsafe object RecordParser(ParsedTDU tdu, Warehouse warehouse)
    {

        var classId = tdu.Metadata.GetUUID(0);
        

        var template = warehouse.GetTemplateByClassId(classId, TemplateType.Record);

        var r = ListParser(tdu, warehouse);

        var ar = (object[])r;

        if (template == null)
        {
            // @TODO: add parse if no template settings
            throw new AsyncException(ErrorType.Management, (ushort)ExceptionCode.TemplateNotFound,
                    "Template not found for record.");
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

            return record;
        }
        else
        {
            var record = new Record();

            for (var i = 0; i < template.Properties.Length; i++)
                record.Add(template.Properties[i].Name, ar[i]);

            return record;
        }
    }

    public static unsafe object ConstantParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        throw new NotImplementedException();
    }

    public static unsafe object ConstantParser(ParsedTDU tdu, Warehouse warehouse)
    {
        throw new NotImplementedException();
    }

    public static unsafe AsyncReply EnumParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {

        var classId = data.GetUUID(offset);
        offset += 16;
        var index = data[offset++];

        var template = connection.Instance.Warehouse.GetTemplateByClassId(classId, TemplateType.Enum);

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

    public static unsafe object EnumParser(ParsedTDU tdu, Warehouse warehouse)
    {

        var classId = tdu.Metadata.GetUUID(0);
        
        var index = tdu.Data[tdu.Offset];

        var template = warehouse.GetTemplateByClassId(classId, TemplateType.Enum);

        if (template != null)
        {
            return template.Constants[index].Value;
        }
        else
        {
            throw new AsyncException(ErrorType.Management, (ushort)ExceptionCode.TemplateNotFound,
                    "Template not found for enum.");
        }
    }


    public static AsyncReply RecordListParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
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

    public static object RecordListParser(ParsedTDU tdu, Warehouse warehouse)
    {
        var rt = new List<IRecord>();

        var length = tdu.ContentLength;
        var offset = tdu.Offset;

        while (length > 0)
        {
            var (cs, reply) = Codec.ParseSync(tdu.Data, offset, warehouse);

            rt.Add(reply as IRecord);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        return rt.ToArray();
    }

    public static AsyncReply ResourceListParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
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


    public static object ResourceListParser(ParsedTDU tdu, Warehouse warehouse)
    {
        var rt = new List<IResource>();

        var length = tdu.ContentLength;
        var offset = tdu.Offset;

        while (length > 0)
        {
            var (cs, reply) = Codec.ParseSync(tdu.Data, offset, warehouse);

            rt.Add(reply as IResource);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        return rt.ToArray();
    }

    public static AsyncBag<object> ListParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
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

    public static object ListParser(ParsedTDU tdu, Warehouse warehouse)
    {
        var rt = new List<object>();

        //TransmissionDataUnitIdentifier? previous = null;
        //byte[]? previousUUID = null;

        ParsedTDU? previous = null;
        ParsedTDU? current = null;

        var offset = tdu.Offset;
        var length = tdu.ContentLength;

        while (length > 0)
        {
            var (longLen, dataType) = ParsedTDU.Parse(tdu.Data, offset, (uint)tdu.ContentLength);

            if (dataType.Value.Identifier == TDUIdentifier.Same)
            {
                // Add UUID
            }

            var (cs, reply) = Codec.ParseSync(tdu.Data, offset, warehouse);

            rt.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        return rt.ToArray();
    }


    public static (uint, ulong, object[]) LimitedCountListParser(byte[] data, uint offset, ulong length, Warehouse warehouse, uint countLimit = uint.MaxValue)
    {
        var rt = new List<object>();

        while (length > 0 && rt.Count < countLimit)
        {
            var (cs, reply) = Codec.ParseSync(data, offset, warehouse);

            rt.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        return (offset, length, rt.ToArray());
    }


    public static AsyncReply TypedMapParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        // get key type
        var (keyCs, keyRepType) = RepresentationType.Parse(data, offset);
        offset += keyCs;
        length -= keyCs;

        var (valueCs, valueRepType) = RepresentationType.Parse(data, offset);
        offset += valueCs;
        length -= valueCs;

        var wh = connection.Instance.Warehouse;

        var map = (IMap)Activator.CreateInstance(typeof(Map<,>).MakeGenericType(keyRepType.GetRuntimeType(wh), valueRepType.GetRuntimeType(wh)));

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

    public static object TypedMapParser(ParsedTDU tdu, Warehouse warehouse)
    {
        // get key type

        var (keyCs, keyRepType) = RepresentationType.Parse(tdu.Metadata, 0);
        var (valueCs, valueRepType) = RepresentationType.Parse(tdu.Metadata, keyCs);
        
        var map = (IMap)Activator.CreateInstance(typeof(Map<,>).MakeGenericType(keyRepType.GetRuntimeType(warehouse), valueRepType.GetRuntimeType(warehouse)));


        var results = new List<object>();

        var offset = tdu.Offset;
        var length = tdu.ContentLength;

        while (length > 0)
        {
            var (cs, reply) = Codec.ParseSync(tdu.Data, offset, warehouse);


            results.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        for (var i = 0; i < results.Count; i += 2)
            map.Add(results[i], results[i + 1]);

        return map;
    }

    public static AsyncReply TupleParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {

        var results = new AsyncBag<object>();
        var rt = new AsyncReply();

        var tupleSize = data[offset++];
        length--;

        var types = new List<Type>();

        for (var i = 0; i < tupleSize; i++)
        {
            var (cs, rep) = RepresentationType.Parse(data, offset);
            types.Add(rep.GetRuntimeType(connection.Instance.Warehouse));
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
                    else if (ar.Length == 5)
                    {
                        var type = typeof(ValueTuple<,,,,>).MakeGenericType(types.ToArray());
                        rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3], ar[4]));
                    }
                    else if (ar.Length == 6)
                    {
                        var type = typeof(ValueTuple<,,,,,>).MakeGenericType(types.ToArray());
                        rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3], ar[4], ar[5]));
                    }
                    else if (ar.Length == 7)
                    {
                        var type = typeof(ValueTuple<,,,,,,>).MakeGenericType(types.ToArray());
                        rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3], ar[4], ar[5], ar[6]));
                    }
                });

        return rt;
    }

    public static object TupleParser(ParsedTDU tdu, Warehouse warehouse)
    {
        var results = new List<object>();


        var tupleSize = tdu.Metadata[0];// data[offset++];
        //length--;

        var types = new List<Type>();

        uint mtOffset = 1;
        for (var i = 0; i < tupleSize; i++)
        {
            
            var (cs, rep) = RepresentationType.Parse(tdu.Metadata, mtOffset);
            types.Add(rep.GetRuntimeType(warehouse));
            mtOffset += cs;
        }

        var length = tdu.ContentLength;
        var offset = tdu.Offset;
        
        while (length > 0)
        {
            var (cs, reply) = Codec.ParseSync(tdu.Data, offset, warehouse);

            results.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }


        if (results.Count == 2)
        {
            var type = typeof(ValueTuple<,>).MakeGenericType(types.ToArray());
            return Activator.CreateInstance(type, results[0], results[1]);
        }
        else if (results.Count == 3)
        {
            var type = typeof(ValueTuple<,,>).MakeGenericType(types.ToArray());
            return Activator.CreateInstance(type, results[0], results[1], results[2]);
        }
        else if (results.Count == 4)
        {
            var type = typeof(ValueTuple<,,,>).MakeGenericType(types.ToArray());
            return Activator.CreateInstance(type, results[0], results[1], results[2], results[3]);
        }
        else if (results.Count == 5)
        {
            var type = typeof(ValueTuple<,,,,>).MakeGenericType(types.ToArray());
            return Activator.CreateInstance(type, results[0], results[1], results[2], results[3], results[4]);
        }
        else if (results.Count == 6)
        {
            var type = typeof(ValueTuple<,,,,,>).MakeGenericType(types.ToArray());
            return Activator.CreateInstance(type, results[0], results[1], results[2], results[3], results[4], results[5]);
        }
        else if (results.Count == 7)
        {
            var type = typeof(ValueTuple<,,,,,,>).MakeGenericType(types.ToArray());
            return Activator.CreateInstance(type, results[0], results[1], results[2], results[3], results[4], results[5], results[6]);
        }

        throw new Exception("Unknown tuple length.");

    }

    public static AsyncReply TypedListParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)
    {
        var rt = new AsyncBag<object>();

        // get the type
        var (hdrCs, rep) = RepresentationType.Parse(data, offset);

        offset += hdrCs;
        length -= hdrCs;

        var runtimeType = rep.GetRuntimeType(connection.Instance.Warehouse);

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

    public static object TypedListParser(ParsedTDU tdu, Warehouse warehouse)
    {

        // get the type
        var (hdrCs, rep) = RepresentationType.Parse(tdu.Metadata, 0);

        //offset += hdrCs;
        //length -= hdrCs;

        var runtimeType = rep.GetRuntimeType(warehouse);

        var list = new List<object>();

        ParsedTDU? current;
        ParsedTDU? previous = null;

        var offset = tdu.Offset;
        var length = tdu.ContentLength;

        while (length > 0)
        {
            (var longLen, current) = ParsedTDU.Parse(tdu.Data, offset, (uint)tdu.ContentLength);

            if (current.Value.Identifier == TDUIdentifier.NotModified)
                current = previous;
            
            var (cs, reply) = Codec.ParseSync(tdu.Data, offset, warehouse, current);

            list.Add(reply);

            if (cs > 0)
            {
                offset += (uint)cs;
                length -= (uint)cs;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        var rt = Array.CreateInstance(runtimeType, list.Count);
        Array.Copy(list.ToArray(), rt, rt.Length);
        
        return rt;
    }

    public static AsyncBag<PropertyValue> PropertyValueArrayParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)//, bool ageIncluded = true)
    {
        var rt = new AsyncBag<PropertyValue>();


        ListParserAsync(data, offset, length, connection, requestSequence).Then(x =>
        {
            var ar = (object[])x;
            var pvs = new List<PropertyValue>();

            for (var i = 0; i < ar.Length; i += 3)
                pvs.Add(new PropertyValue(ar[2], (ulong?)ar[0], (DateTime?)ar[1]));


            rt.Trigger(pvs.ToArray());
        });

        return rt;

    }


    public static (uint, AsyncReply<PropertyValue>) PropertyValueParserAsync(byte[] data, uint offset, DistributedConnection connection, uint[] requestSequence)//, bool ageIncluded = true)
    {
        var reply = new AsyncReply<PropertyValue>();

        var age = data.GetUInt64(offset, Endian.Little);
        offset += 8;

        DateTime date = data.GetDateTime(offset, Endian.Little);
        offset += 8;


        var (valueSize, results) = Codec.ParseAsync(data, offset, connection, requestSequence);

        if (results is AsyncReply)
        {
            (results as AsyncReply).Then(value =>
            {
                reply.Trigger(new PropertyValue(value, age, date));
            });
        }
        else
        {
            reply.Trigger(new PropertyValue(results, age, date));
        }

        return (16 + valueSize, reply);
    }

    public static AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> HistoryParserAsync(byte[] data, uint offset, uint length, IResource resource, DistributedConnection connection, uint[] requestSequence)
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

            var (len, pv) = PropertyValueParserAsync(data, offset, connection, requestSequence);

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
