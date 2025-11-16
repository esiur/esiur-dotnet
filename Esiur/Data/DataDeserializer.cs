using Esiur.Core;
using Esiur.Data;
using Esiur.Data.GVWIE;
using Esiur.Misc;
using Esiur.Net.IIP;
using Esiur.Resource;
using Esiur.Resource.Template;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace Esiur.Data;

public static class DataDeserializer
{
    public static object NullParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return null;
    }

    public static object NullParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return null;
    }

    public static object BooleanTrueParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return true;
    }

    public static object BooleanTrueParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return true;
    }

    public static object BooleanFalseParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return false;
    }

    public static object BooleanFalseParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return false;
    }

    public static object NotModifiedParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return NotModified.Default;
    }

    public static object NotModifiedParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return NotModified.Default;
    }

    public static object UInt8ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return tdu.Data[tdu.Offset];
    }
    public static object UInt8Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        return tdu.Data[tdu.Offset];
    }

    public static object Int8ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return (sbyte)tdu.Data[tdu.Offset];
    }
    public static object Int8Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        return (sbyte)tdu.Data[tdu.Offset];
    }

    public static unsafe object Char16ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(char*)ptr;
    }

    public static unsafe object Char16Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(char*)ptr;
    }

    public static object Char8ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return (char)tdu.Data[tdu.Offset];
    }

    public static object Char8Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        return (char)tdu.Data[tdu.Offset];
    }


    public static unsafe object Int16ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(short*)ptr;
    }

    public static unsafe object Int16Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(short*)ptr;
    }

    public static unsafe object UInt16ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(ushort*)ptr;
    }

    public static unsafe object UInt16Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(ushort*)ptr;
    }

    public static unsafe object Int32ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(int*)ptr;
    }

    public static unsafe object Int32Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(int*)ptr;
    }

    public static unsafe object UInt32ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(uint*)ptr;
    }

    public static unsafe object UInt32Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(uint*)ptr;
    }


    public static unsafe object Float32ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(float*)ptr;
    }

    public static unsafe object Float32Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(float*)ptr;
    }

    public static unsafe object Float64ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(double*)ptr;
    }

    public static unsafe object Float64Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(double*)ptr;
    }


    public static unsafe object Decimal128ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(decimal*)ptr;
    }

    public static unsafe object Decimal128Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(decimal*)ptr;
    }

    public static unsafe object UUIDParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return new UUID(tdu.Data, tdu.Offset);
    }

    public static unsafe object UUIDParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return new UUID(tdu.Data, tdu.Offset);
    }



    public static unsafe object Int128ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.Offset])
        fixed (byte* ptr2 = &tdu.Data[tdu.Offset + 8])
            return new Int128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object Int128Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.Offset])
        fixed (byte* ptr2 = &tdu.Data[tdu.Offset + 8])
            return new Int128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object UInt128ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.Offset])
        fixed (byte* ptr2 = &tdu.Data[tdu.Offset + 8])
            return new UInt128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object UInt128Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.Offset])
        fixed (byte* ptr2 = &tdu.Data[tdu.Offset + 8])
            return new UInt128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object Int64ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(long*)ptr;
    }

    public static unsafe object Int64Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(long*)ptr;
    }


    public static unsafe object UInt64ParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(ulong*)ptr;
    }

    public static unsafe object UInt64Parser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return *(ulong*)ptr;
    }


    public static unsafe object DateTimeParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return new DateTime(*(long*)ptr, DateTimeKind.Utc);

    }
    public static unsafe object DateTimeParser(ParsedTDU tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
            return new DateTime(*(long*)ptr, DateTimeKind.Utc);

    }


    public static unsafe object ResourceParser8Async(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        if (connection == null)
            return new ResourceId(false, tdu.Data[tdu.Offset]);
        else
            return connection.Fetch(tdu.Data[tdu.Offset], requestSequence);
    }

    public static unsafe object ResourceParser8(ParsedTDU tdu, Warehouse warehouse)
    {
        return new ResourceId(false, tdu.Data[tdu.Offset]);
    }

    public static unsafe object LocalResourceParser8Async(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        if (connection == null)
            return new ResourceId(true, tdu.Data[tdu.Offset]);
        else
            return connection.Instance.Warehouse.GetById(tdu.Data[tdu.Offset]);
    }

    public static unsafe object LocalResourceParser8(ParsedTDU tdu, Warehouse warehouse)
    {
        return new ResourceId(true, tdu.Data[tdu.Offset]);
    }

    public static unsafe object ResourceParser16Async(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
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


    public static unsafe object LocalResourceParser16Async(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
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

    public static unsafe object ResourceParser32Async(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
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


    public static unsafe object LocalResourceParser32Async(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.Offset])
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


    public static unsafe object RawDataParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return tdu.Data.Clip(tdu.Offset, (uint)tdu.ContentLength);
    }

    public static unsafe object RawDataParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return tdu.Data.Clip(tdu.Offset, (uint)tdu.ContentLength);
    }


    public static unsafe object StringParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        return tdu.Data.GetString(tdu.Offset, (uint)tdu.ContentLength);
    }

    public static unsafe object StringParser(ParsedTDU tdu, Warehouse warehouse)
    {
        return tdu.Data.GetString(tdu.Offset, (uint)tdu.ContentLength);
    }

    public static unsafe object RecordParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        var classId = tdu.Metadata.GetUUID(0);
        var template = connection.Instance.Warehouse.GetTemplateByClassId(classId,
                                                                    TemplateType.Record);
        var rt = new AsyncReply<IRecord>();


        var list = new AsyncBag<object>();

        ParsedTDU current;
        ParsedTDU? previous = null;

        var offset = tdu.Offset;
        var length = tdu.ContentLength;
        var ends = offset + (uint)length;

        var initRecord = (TypeTemplate template) =>
        {
            for (var i = 0; i < template.Properties.Length; i++)
            {
                current = ParsedTDU.Parse(tdu.Data, offset, ends);

                if (current.Class == TDUClass.Invalid)
                    throw new Exception("Unknown type.");


                if (current.Identifier == TDUIdentifier.TypeContinuation)
                {
                    current.Class = previous.Value.Class;
                    current.Identifier = previous.Value.Identifier;
                    current.Metadata = previous.Value.Metadata;
                }
                else if (current.Identifier == TDUIdentifier.TypeOfTarget)
                {
                    var (idf, mt) = template.Properties[i].ValueType.GetMetadata();
                    current.Class = TDUClass.Typed;
                    current.Identifier = idf;
                    current.Metadata = mt;
                    current.Index = (int)idf & 0x7;
                }

                var (cs, reply) = Codec.ParseAsync(current, connection, requestSequence);

                list.Add(reply);

                if (cs > 0)
                {
                    offset += (uint)current.TotalLength;
                    length -= (uint)current.TotalLength;
                    previous = current;
                }
                else
                    throw new Exception("Error while parsing structured data");

            }

            list.Seal();

            list.Then(results =>
            {
                if (template.DefinedType != null)
                {

                    var record = Activator.CreateInstance(template.DefinedType) as IRecord;
                    for (var i = 0; i < template.Properties.Length; i++)
                    {
                        try
                        {
                            var v = RuntimeCaster.Cast(results[i], template.Properties[i].PropertyInfo.PropertyType);
                            template.Properties[i].PropertyInfo.SetValue(record, v);
                        }
                        catch (Exception ex)
                        {
                            Global.Log(ex);
                        }
                    }

                    rt.Trigger(record);
                }
                else
                {
                    var record = new Record();

                    for (var i = 0; i < template.Properties.Length; i++)
                        record.Add(template.Properties[i].Name, results[i]);

                    rt.Trigger(record);
                }

            }).Error(e =>
            {
                //Console.WriteLine(e);
                rt.TriggerError(e);
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
            }).Error(x => rt.TriggerError(x));
        }
        else
        {
            initRecord(null);
        }

        return rt;



        //var classId = tdu.Metadata.GetUUID(0);

        //var template = connection.Instance.Warehouse.GetTemplateByClassId(classId, TemplateType.Record);

        //var initRecord = (TypeTemplate template) =>
        //{
        //    ListParserAsync(tdu, connection, requestSequence).Then(r =>
        //    {
        //        var ar = (object[])r;

        //        if (template == null)
        //        {
        //            // @TODO: add parse if no template settings
        //            reply.TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.TemplateNotFound,
        //                    "Template not found for record."));
        //        }
        //        else if (template.DefinedType != null)
        //        {
        //            var record = Activator.CreateInstance(template.DefinedType) as IRecord;
        //            for (var i = 0; i < template.Properties.Length; i++)
        //            {
        //                try
        //                {
        //                    //var v = Convert.ChangeType(ar[i], template.Properties[i].PropertyInfo.PropertyType);
        //                    var v = RuntimeCaster.Cast(ar[i], template.Properties[i].PropertyInfo.PropertyType);
        //                    template.Properties[i].PropertyInfo.SetValue(record, v);
        //                }
        //                catch (Exception ex)
        //                {
        //                    Global.Log(ex);
        //                }
        //            }

        //            reply.Trigger(record);
        //        }
        //        else
        //        {
        //            var record = new Record();

        //            for (var i = 0; i < template.Properties.Length; i++)
        //                record.Add(template.Properties[i].Name, ar[i]);

        //            reply.Trigger(record);
        //        }

        //    });
        //};

        //if (template != null)
        //{
        //    initRecord(template);
        //}
        //else if (connection != null)
        //{
        //    // try to get the template from the other end
        //    connection.GetTemplate(classId).Then(tmp =>
        //    {
        //        initRecord(tmp);
        //    }).Error(x => reply.TriggerError(x));
        //}
        //else
        //{
        //    initRecord(null);
        //}

        //return reply;
    }


    public static unsafe object RecordParser(ParsedTDU tdu, Warehouse warehouse)
    {
        var classId = tdu.Metadata.GetUUID(0);
        var template = warehouse.GetTemplateByClassId(classId, TemplateType.Record);

        if (template == null)
        {
            // @TODO: add parse if no template settings
            throw new AsyncException(ErrorType.Management, (ushort)ExceptionCode.TemplateNotFound,
                    "Template not found for record.");
        }

        var list = new List<object>();

        ParsedTDU current;
        ParsedTDU? previous = null;

        var offset = tdu.Offset;
        var length = tdu.ContentLength;
        var ends = offset + (uint)length;


        for (var i = 0; i < template.Properties.Length; i++)
        {
            current = ParsedTDU.Parse(tdu.Data, offset, ends);

            if (current.Class == TDUClass.Invalid)
                throw new Exception("Unknown type.");


            if (current.Identifier == TDUIdentifier.TypeContinuation)
            {
                current.Class = previous.Value.Class;
                current.Identifier = previous.Value.Identifier;
                current.Metadata = previous.Value.Metadata;
            }
            else if (current.Identifier == TDUIdentifier.TypeOfTarget)
            {
                var (idf, mt) = template.Properties[i].ValueType.GetMetadata();
                current.Class = TDUClass.Typed;
                current.Identifier = idf;
                current.Metadata = mt;
                current.Index = (int)idf & 0x7;
            }

            var (cs, reply) = Codec.ParseSync(current, warehouse);

            list.Add(reply);

            if (cs > 0)
            {
                offset += (uint)current.TotalLength;
                length -= (uint)current.TotalLength;
                previous = current;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        if (template.DefinedType != null)
        {

            var record = Activator.CreateInstance(template.DefinedType) as IRecord;
            for (var i = 0; i < template.Properties.Length; i++)
            {
                try
                {
                    var v = RuntimeCaster.Cast(list[i], template.Properties[i].PropertyInfo.PropertyType);
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
                record.Add(template.Properties[i].Name, list[i]);

            return record;
        }
    }

    public static unsafe object ConstantParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        throw new NotImplementedException();
    }

    public static unsafe object ConstantParser(ParsedTDU tdu, Warehouse warehouse)
    {
        throw new NotImplementedException();
    }

    public static unsafe AsyncReply EnumParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {

        var classId = tdu.Metadata.GetUUID(0);


        var index = tdu.Data[tdu.Offset];

        var template = connection.Instance.Warehouse.GetTemplateByClassId(classId,
                                                                        TemplateType.Enum);

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


    public static AsyncReply RecordListParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        var rt = new AsyncBag<IRecord>();

        var length = tdu.ContentLength;
        var offset = tdu.Offset;

        while (length > 0)
        {
            var (cs, reply) = Codec.ParseAsync(tdu.Data, offset, connection, requestSequence);

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

    public static AsyncReply ResourceListParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        var rt = new AsyncBag<IResource>();

        var length = tdu.ContentLength;
        var offset = tdu.Offset;

        while (length > 0)
        {
            var (cs, reply) = Codec.ParseAsync(tdu.Data, offset, connection, requestSequence);

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

    public static AsyncBag<object> ListParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        //var rt = new AsyncBag<object>();


        //var offset = tdu.Offset;
        //var length = tdu.ContentLength;

        //while (length > 0)
        //{
        //    var (cs, reply) = Codec.ParseAsync(tdu.Data, offset, connection, requestSequence);

        //    rt.Add(reply);

        //    if (cs > 0)
        //    {
        //        offset += (uint)cs;
        //        length -= (uint)cs;
        //    }
        //    else
        //        throw new Exception("Error while parsing structured data");

        //}

        //rt.Seal();
        //return rt;





        var rt = new AsyncBag<object>();

        //var list = new List<object>();

        ParsedTDU current;
        ParsedTDU? previous = null;

        var offset = tdu.Offset;
        var length = tdu.ContentLength;
        var ends = offset + (uint)length;

        while (length > 0)
        {
            current = ParsedTDU.Parse(tdu.Data, offset, ends);

            if (current.Class == TDUClass.Invalid)
                throw new Exception("Unknown type.");


            if (current.Identifier == TDUIdentifier.TypeContinuation)
            {
                current.Class = previous.Value.Class;
                current.Identifier = previous.Value.Identifier;
                current.Metadata = previous.Value.Metadata;
            }


            var (cs, reply) = Codec.ParseAsync(current, connection, requestSequence);

            rt.Add(reply);

            if (cs > 0)
            {
                offset += (uint)current.TotalLength;
                length -= (uint)current.TotalLength;
                previous = current;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        rt.Seal();

        return rt;

    }

    public static object ListParser(ParsedTDU tdu, Warehouse warehouse)
    {
        var list = new List<object>();

        ParsedTDU current;
        ParsedTDU? previous = null;

        var offset = tdu.Offset;
        var length = tdu.ContentLength;
        var ends = offset + (uint)length;

        while (length > 0)
        {
            current = ParsedTDU.Parse(tdu.Data, offset, ends);

            if (current.Class == TDUClass.Invalid)
                throw new Exception("Unknown type.");


            if (current.Identifier == TDUIdentifier.TypeContinuation)
            {
                current.Class = previous.Value.Class;
                current.Identifier = previous.Value.Identifier;
                current.Metadata = previous.Value.Metadata;
            }


            var (cs, reply) = Codec.ParseSync(current, warehouse);

            list.Add(reply);

            if (cs > 0)
            {
                offset += (uint)current.TotalLength;
                length -= (uint)current.TotalLength;
                previous = current;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        return list.ToArray();

    }


    public static (uint, ulong, object[]) LimitedCountListParser(byte[] data, uint offset, ulong length, Warehouse warehouse, uint countLimit = uint.MaxValue)
    {

        // @TODO: add TypeContinuation
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


    public static AsyncReply TypedMapParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {

        var rt = new AsyncReply();

        // get key type

        var (keyCs, keysTru) = TRU.Parse(tdu.Metadata, 0);
        var (valueCs, valuesTru) = TRU.Parse(tdu.Metadata, keyCs);

        var map = (IMap)Activator.CreateInstance(typeof(Map<,>).MakeGenericType(
            keysTru.GetRuntimeType(connection.Instance.Warehouse),
            valuesTru.GetRuntimeType(connection.Instance.Warehouse)));


        var keysTdu = ParsedTDU.Parse(tdu.Data, tdu.Offset,
                                      (uint)(tdu.Offset + tdu.ContentLength));

        var valuesTdu = ParsedTDU.Parse(tdu.Data,
                                        (uint)(keysTdu.Offset + keysTdu.ContentLength),
                                        tdu.Ends);

        var keysReply = TypedArrayParserAsync(keysTdu, keysTru, connection, requestSequence);
        var valuesReply = TypedArrayParserAsync(valuesTdu, valuesTru, connection, requestSequence);


        keysReply.Then(keys =>
        {
            valuesReply.Then(values =>
            {
                for (var i = 0; i < ((Array)keys).Length; i++)
                    map.Add(((Array)keys).GetValue(i), ((Array)values).GetValue(i));
                rt.Trigger(map);
            }).Error(e => rt.TriggerError(e));
        }).Error(e => rt.TriggerError(e));


        return rt;


        //// get key type

        //var (keyCs, keyRepType) = TRU.Parse(tdu.Metadata, 0);
        //var (valueCs, valueRepType) = TRU.Parse(tdu.Metadata, keyCs);

        //var wh = connection.Instance.Warehouse;

        //var map = (IMap)Activator.CreateInstance(typeof(Map<,>).MakeGenericType(keyRepType.GetRuntimeType(wh), valueRepType.GetRuntimeType(wh)));

        //var rt = new AsyncReply();

        //var results = new AsyncBag<object>();

        //var offset = tdu.Offset;
        //var length = tdu.ContentLength;

        //while (length > 0)
        //{
        //    var (cs, reply) = Codec.ParseAsync(tdu.Data, offset, connection, requestSequence);


        //    results.Add(reply);

        //    if (cs > 0)
        //    {
        //        offset += (uint)cs;
        //        length -= (uint)cs;
        //    }
        //    else
        //        throw new Exception("Error while parsing structured data");

        //}

        //results.Seal();

        //results.Then(ar =>
        //{
        //    for (var i = 0; i < ar.Length; i += 2)
        //        map.Add(ar[i], ar[i + 1]);

        //    rt.Trigger(map);
        //});


        //return rt;

    }

    public static Array TypedArrayParser(ParsedTDU tdu, TRU tru, Warehouse warehouse)
    {
        switch (tru.Identifier)
        {
            case TRUIdentifier.Int32:
                return GroupInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength));
            case TRUIdentifier.Int64:
                return GroupInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength));
            case TRUIdentifier.Int16:
                return GroupInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength));
            case TRUIdentifier.UInt32:
                return GroupUInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength));
            case TRUIdentifier.UInt64:
                return GroupUInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength));
            case TRUIdentifier.UInt16:
                return GroupUInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength));
            case TRUIdentifier.Enum:

                var enumType = tru.GetRuntimeType(warehouse);

                var enums = Array.CreateInstance(enumType, (int)tdu.ContentLength);
                var enumTemplate = warehouse.GetTemplateByType(enumType);

                for (var i = 0; i < (int)tdu.ContentLength; i++)
                {
                    var index = tdu.Data[tdu.Offset + i];
                    enums.SetValue(enumTemplate.Constants[index].Value, i);
                }

                return enums;

            default:


                var list = new List<object>();

                ParsedTDU current;
                ParsedTDU? previous = null;

                var offset = tdu.Offset;
                var length = tdu.ContentLength;
                var ends = offset + (uint)length;

                while (length > 0)
                {
                    current = ParsedTDU.Parse(tdu.Data, offset, ends);

                    if (current.Class == TDUClass.Invalid)
                        throw new Exception("Unknown type.");


                    if (current.Identifier == TDUIdentifier.TypeContinuation)
                    {
                        current.Class = previous.Value.Class;
                        current.Identifier = previous.Value.Identifier;
                        current.Metadata = previous.Value.Metadata;
                    }
                    else if (current.Identifier == TDUIdentifier.TypeOfTarget)
                    {
                        var (idf, mt) = tru.GetMetadata();
                        current.Class = TDUClass.Typed;
                        current.Identifier = idf;
                        current.Metadata = mt;
                        current.Index = (int)idf & 0x7;
                    }

                    var (cs, reply) = Codec.ParseSync(current, warehouse);

                    list.Add(reply);

                    if (cs > 0)
                    {
                        offset += (uint)current.TotalLength;
                        length -= (uint)current.TotalLength;
                        previous = current;
                    }
                    else
                        throw new Exception("Error while parsing structured data");

                }

                var runtimeType = tru.GetRuntimeType(warehouse);
                var rt = Array.CreateInstance(runtimeType, list.Count);
                Array.Copy(list.ToArray(), rt, rt.Length);

                return rt;
        }

    }

    public static object TypedMapParser(ParsedTDU tdu, Warehouse warehouse)
    {
        // get key type

        var (keyCs, keysTru) = TRU.Parse(tdu.Metadata, 0);
        var (valueCs, valuesTru) = TRU.Parse(tdu.Metadata, keyCs);

        var map = (IMap)Activator.CreateInstance(typeof(Map<,>).MakeGenericType(keysTru.GetRuntimeType(warehouse), valuesTru.GetRuntimeType(warehouse)));



        var keysTdu = ParsedTDU.Parse(tdu.Data, tdu.Offset,
                                      (uint)(tdu.Offset + tdu.ContentLength));

        var valuesTdu = ParsedTDU.Parse(tdu.Data,
                                        (uint)(keysTdu.Offset + keysTdu.ContentLength),
                                        tdu.Ends);

        var keys = TypedArrayParser(keysTdu, keysTru, warehouse);
        var values = TypedArrayParser(valuesTdu, valuesTru, warehouse);

        for (var i = 0; i < keys.Length; i++)
            map.Add(keys.GetValue(i), values.GetValue(i));

        return map;

    }

    public static AsyncReply TupleParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {

        var rt = new AsyncReply();

        // var tupleSize = tdu.Metadata[0];

        var trus = new List<TRU>();

        uint mtOffset = 0;

        while (mtOffset < tdu.Metadata.Length)
        {
            var (cs, tru) = TRU.Parse(tdu.Metadata, mtOffset);
            trus.Add(tru);
            mtOffset += cs;
        }

        var results = new AsyncBag<object>();
        var types = trus.Select(x => x.GetRuntimeType(connection.Instance.Warehouse)).ToArray();

        ParsedTDU current;
        ParsedTDU? previous = null;

        var offset = tdu.Offset;
        var length = tdu.ContentLength;
        var ends = offset + (uint)length;


        for (var i = 0; i < trus.Count; i++)
        {
            current = ParsedTDU.Parse(tdu.Data, offset, ends);

            if (current.Class == TDUClass.Invalid)
                throw new Exception("Unknown type.");

            if (current.Identifier == TDUIdentifier.TypeOfTarget)
            {
                var (idf, mt) = trus[i].GetMetadata();
                current.Class = TDUClass.Typed;
                current.Identifier = idf;
                current.Metadata = mt;
                current.Index = (int)idf & 0x7;
            }

            var (_, reply) = Codec.ParseAsync(current, connection, requestSequence);

            results.Add(reply);

            if (current.TotalLength > 0)
            {
                offset += (uint)current.TotalLength;
                length -= (uint)current.TotalLength;
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
        var tupleSize = tdu.Metadata[0];

        var trus = new List<TRU>();

        uint mtOffset = 1;
        for (var i = 0; i < tupleSize; i++)
        {
            var (cs, tru) = TRU.Parse(tdu.Metadata, mtOffset);
            trus.Add(tru);
            mtOffset += cs;
        }

        var results = new List<object>();
        var types = trus.Select(x => x.GetRuntimeType(warehouse)).ToArray();

        ParsedTDU current;
        ParsedTDU? previous = null;

        var offset = tdu.Offset;
        var length = tdu.ContentLength;
        var ends = offset + (uint)length;


        for (var i = 0; i < tupleSize; i++)
        {
            current = ParsedTDU.Parse(tdu.Data, offset, ends);

            if (current.Class == TDUClass.Invalid)
                throw new Exception("Unknown type.");

            if (current.Identifier == TDUIdentifier.TypeOfTarget)
            {
                var (idf, mt) = trus[i].GetMetadata();
                current.Class = TDUClass.Typed;
                current.Identifier = idf;
                current.Metadata = mt;
                current.Index = (int)idf & 0x7;
            }

            var (_, reply) = Codec.ParseSync(current, warehouse);

            results.Add(reply);

            if (current.TotalLength > 0)
            {
                offset += (uint)current.TotalLength;
                length -= (uint)current.TotalLength;
            }
            else
                throw new Exception("Error while parsing structured data");

        }


        if (results.Count == 2)
        {
            var type = typeof(ValueTuple<,>).MakeGenericType(types);
            return Activator.CreateInstance(type, results[0], results[1]);
        }
        else if (results.Count == 3)
        {
            var type = typeof(ValueTuple<,,>).MakeGenericType(types);
            return Activator.CreateInstance(type, results[0], results[1], results[2]);
        }
        else if (results.Count == 4)
        {
            var type = typeof(ValueTuple<,,,>).MakeGenericType(types);
            return Activator.CreateInstance(type, results[0], results[1], results[2], results[3]);
        }
        else if (results.Count == 5)
        {
            var type = typeof(ValueTuple<,,,,>).MakeGenericType(types);
            return Activator.CreateInstance(type, results[0], results[1], results[2], results[3], results[4]);
        }
        else if (results.Count == 6)
        {
            var type = typeof(ValueTuple<,,,,,>).MakeGenericType(types);
            return Activator.CreateInstance(type, results[0], results[1], results[2], results[3], results[4], results[5]);
        }
        else if (results.Count == 7)
        {
            var type = typeof(ValueTuple<,,,,,,>).MakeGenericType(types);
            return Activator.CreateInstance(type, results[0], results[1], results[2], results[3], results[4], results[5], results[6]);
        }

        throw new Exception("Unknown tuple length.");

    }

    public static AsyncReply TypedArrayParserAsync(ParsedTDU tdu, TRU tru, DistributedConnection connection, uint[] requestSequence)
    {
        switch (tru.Identifier)
        {
            case TRUIdentifier.Int32:
                return new AsyncReply(GroupInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength)));
            case TRUIdentifier.Int64:
                return new AsyncReply(GroupInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength)));
            case TRUIdentifier.Int16:
                return new AsyncReply(GroupInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength)));
            case TRUIdentifier.UInt32:
                return new AsyncReply(GroupUInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength)));
            case TRUIdentifier.UInt64:
                return new AsyncReply(GroupUInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength)));
            case TRUIdentifier.UInt16:
                return new AsyncReply(GroupUInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.Offset, (int)tdu.ContentLength)));

            case TRUIdentifier.Enum:
                var enumType = tru.GetRuntimeType(connection.Instance.Warehouse);

                var rt = Array.CreateInstance(enumType, (int)tdu.ContentLength);
                var enumTemplate = connection.Instance.Warehouse.GetTemplateByType(enumType);

                for (var i = 0; i < (int)tdu.ContentLength; i++)
                {
                    var index = tdu.Data[tdu.Offset + i];

                    rt.SetValue(Enum.ToObject(enumType, enumTemplate.Constants[index].Value), i);
                }

                return new AsyncReply(rt);

            default:


                var list = new AsyncBag<object>();

                list.ArrayType = tru.GetRuntimeType(connection.Instance.Warehouse);

                ParsedTDU current;
                ParsedTDU? previous = null;

                var offset = tdu.Offset;
                var length = tdu.ContentLength;
                var ends = offset + (uint)length;

                while (length > 0)
                {
                    current = ParsedTDU.Parse(tdu.Data, offset, ends);

                    if (current.Class == TDUClass.Invalid)
                        throw new Exception("Unknown type.");


                    if (current.Identifier == TDUIdentifier.TypeContinuation)
                    {
                        current.Class = previous.Value.Class;
                        current.Identifier = previous.Value.Identifier;
                        current.Metadata = previous.Value.Metadata;
                    }
                    else if (current.Identifier == TDUIdentifier.TypeOfTarget)
                    {
                        var (idf, mt) = tru.GetMetadata();
                        current.Class = TDUClass.Typed;
                        current.Identifier = idf;
                        current.Metadata = mt;
                        current.Index = (int)idf & 0x7;
                    }

                    var (cs, reply) = Codec.ParseAsync(current, connection, requestSequence);

                    list.Add(reply);

                    if (cs > 0)
                    {
                        offset += (uint)current.TotalLength;
                        length -= (uint)current.TotalLength;
                        previous = current;
                    }
                    else
                        throw new Exception("Error while parsing structured data");

                }

                list.Seal();
                return list;
        }

    }

    public static AsyncReply TypedListParserAsync(ParsedTDU tdu, DistributedConnection connection, uint[] requestSequence)
    {
        // get the type
        var (hdrCs, tru) = TRU.Parse(tdu.Metadata, 0);

        return TypedArrayParserAsync(tdu, tru, connection, requestSequence);

        //switch (rep.Identifier)
        //{
        //    case TRUIdentifier.Int32:
        //        return new AsyncReply(GroupInt32Codec.Decode(tdu.Data.AsSpan(
        //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
        //    case TRUIdentifier.Int64:
        //        return new AsyncReply(GroupInt64Codec.Decode(tdu.Data.AsSpan(
        //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
        //    case TRUIdentifier.Int16:
        //        return new AsyncReply(GroupInt16Codec.Decode(tdu.Data.AsSpan(
        //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
        //    case TRUIdentifier.UInt32:
        //        return new AsyncReply(GroupUInt32Codec.Decode(tdu.Data.AsSpan(
        //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
        //    case TRUIdentifier.UInt64:
        //        return new AsyncReply(GroupUInt64Codec.Decode(tdu.Data.AsSpan(
        //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
        //    case TRUIdentifier.UInt16:
        //        return new AsyncReply(GroupUInt16Codec.Decode(tdu.Data.AsSpan(
        //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
        //    default:

        //        var rt = new AsyncBag<object>();

        //        var runtimeType = rep.GetRuntimeType(connection.Instance.Warehouse);

        //        rt.ArrayType = runtimeType;

        //        ParsedTDU current;
        //        ParsedTDU? previous = null;

        //        var offset = tdu.Offset;
        //        var length = tdu.ContentLength;
        //        var ends = offset + (uint)length;

        //        while (length > 0)
        //        {

        //            current = ParsedTDU.Parse(tdu.Data, offset, ends);

        //            if (current.Class == TDUClass.Invalid)
        //                throw new Exception("Unknown type.");


        //            if (current.Identifier == TDUIdentifier.TypeContinuation)
        //            {
        //                current.Class = previous.Value.Class;
        //                current.Identifier = previous.Value.Identifier;
        //                current.Metadata = previous.Value.Metadata;
        //            }

        //            var (cs, reply) = Codec.ParseAsync(tdu.Data, offset, connection, requestSequence);

        //            rt.Add(reply);

        //            if (cs > 0)
        //            {
        //                offset += (uint)cs;
        //                length -= (uint)cs;
        //            }
        //            else
        //                throw new Exception("Error while parsing structured data");

        //        }

        //        rt.Seal();
        //        return rt;
        //}
    }

    public static object TypedListParser(ParsedTDU tdu, Warehouse warehouse)
    {
        // get the type
        var (hdrCs, tru) = TRU.Parse(tdu.Metadata, 0);

        return TypedArrayParser(tdu, tru, warehouse);
    }

    public static AsyncBag<PropertyValue> PropertyValueArrayParserAsync(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence)//, bool ageIncluded = true)
    {
        var rt = new AsyncBag<PropertyValue>();


        ListParserAsync(new ParsedTDU() { Data = data, Offset = offset, ContentLength = length }
                        , connection, requestSequence).Then(x =>
        {

            var ar = (object[])x;
            var pvs = new List<PropertyValue>();

            for (var i = 0; i < ar.Length; i += 3)
                pvs.Add(new PropertyValue(ar[2], Convert.ToUInt64(ar[0]), (DateTime?)ar[1]));


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
