using Esiur.Core;
using Esiur.Data;
using Esiur.Data.Gvwie;
using Esiur.Data.Types;
using Esiur.Misc;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Membership;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Esiur.Data;

public static class DataDeserializer
{
    public static object NullParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return null;
    }

    public static object NullParser(ParsedTdu tdu, Warehouse warehouse)
    {
        return null;
    }

    public static object BooleanTrueParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return true;
    }

    public static object BooleanTrueParser(ParsedTdu tdu, Warehouse warehouse)
    {
        return true;
    }

    public static object BooleanFalseParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return false;
    }

    public static object BooleanFalseParser(ParsedTdu tdu, Warehouse warehouse)
    {
        return false;
    }

    public static object NotModifiedParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return NotModified.Default;
    }

    public static object NotModifiedParser(ParsedTdu tdu, Warehouse warehouse)
    {
        return NotModified.Default;
    }

    // The Infinity token carries no payload: the serializer collapses every NaN and
    // +/- Infinity onto it (see DataSerializer.Float32/Float64Composer). Decoding it to
    // a single canonical double keeps the (lossy) round trip from throwing.
    public static object InfinityParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return double.PositiveInfinity;
    }

    public static object InfinityParser(ParsedTdu tdu, Warehouse warehouse)
    {
        return double.PositiveInfinity;
    }

    public static object UInt8ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return tdu.Data[tdu.PayloadOffset];
    }
    public static object UInt8Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        return tdu.Data[tdu.PayloadOffset];
    }

    public static object Int8ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return (sbyte)tdu.Data[tdu.PayloadOffset];
    }
    public static object Int8Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        return (sbyte)tdu.Data[tdu.PayloadOffset];
    }

    public static unsafe object Char16ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(char*)ptr;
    }

    public static unsafe object Char16Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(char*)ptr;
    }

    public static object Char8ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return (char)tdu.Data[tdu.PayloadOffset];
    }

    public static object Char8Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        return (char)tdu.Data[tdu.PayloadOffset];
    }


    public static unsafe object Int16ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(short*)ptr;
    }

    public static unsafe object Int16Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(short*)ptr;
    }

    public static unsafe object UInt16ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(ushort*)ptr;
    }

    public static unsafe object UInt16Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(ushort*)ptr;
    }

    public static unsafe object Int32ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(int*)ptr;
    }

    public static unsafe object Int32Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(int*)ptr;
    }

    public static unsafe object UInt32ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(uint*)ptr;
    }

    public static unsafe object UInt32Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(uint*)ptr;
    }


    public static unsafe object Float32ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(float*)ptr;
    }

    public static unsafe object Float32Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(float*)ptr;
    }

    public static unsafe object Float64ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(double*)ptr;
    }

    public static unsafe object Float64Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(double*)ptr;
    }


    public static unsafe object Decimal128ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(decimal*)ptr;
    }

    public static unsafe object Decimal128Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(decimal*)ptr;
    }

    public static unsafe object UUIDParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return new Uuid(tdu.Data, tdu.PayloadOffset);
    }

    public static unsafe object UUIDParser(ParsedTdu tdu, Warehouse warehouse)
    {
        return new Uuid(tdu.Data, tdu.PayloadOffset);
    }



    public static unsafe object Int128ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.PayloadOffset])
        fixed (byte* ptr2 = &tdu.Data[tdu.PayloadOffset + 8])
            return new Int128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object Int128Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.PayloadOffset])
        fixed (byte* ptr2 = &tdu.Data[tdu.PayloadOffset + 8])
            return new Int128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object UInt128ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.PayloadOffset])
        fixed (byte* ptr2 = &tdu.Data[tdu.PayloadOffset + 8])
            return new UInt128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object UInt128Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr1 = &tdu.Data[tdu.PayloadOffset])
        fixed (byte* ptr2 = &tdu.Data[tdu.PayloadOffset + 8])
            return new UInt128(*(ulong*)ptr1, *(ulong*)ptr2);
    }

    public static unsafe object Int64ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(long*)ptr;
    }

    public static unsafe object Int64Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(long*)ptr;
    }


    public static unsafe object UInt64ParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(ulong*)ptr;
    }

    public static unsafe object UInt64Parser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return *(ulong*)ptr;
    }


    public static unsafe object DateTimeParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return new DateTime(*(long*)ptr, DateTimeKind.Utc);

    }
    public static unsafe object DateTimeParser(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return new DateTime(*(long*)ptr, DateTimeKind.Utc);

    }


    public static object ResourceLinkParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        var link = tdu.Data.GetString(tdu.PayloadOffset, (uint)tdu.PayloadLength);
        if (connection == null)
        {
            return new ResourceLink(link);
        }
        else
        {
            return connection.Instance.Warehouse.Get<IResource>(link);
        }
    }

    public static object ResourceLinkParser(ParsedTdu tdu, Warehouse warehouse)
    {
        var link = tdu.Data.GetString(tdu.PayloadOffset, (uint)tdu.PayloadLength);
        return new ResourceLink(link);
    }

    public static unsafe object ResourceParser8Async(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        if (connection == null)
            return new ResourceId(false, tdu.Data[tdu.PayloadOffset]);
        else
            return connection.FetchResource(tdu.Data[tdu.PayloadOffset], requestSequence);
    }

    public static unsafe object ResourceParser8(ParsedTdu tdu, Warehouse warehouse)
    {
        return new ResourceId(false, tdu.Data[tdu.PayloadOffset]);
    }

    public static unsafe object LocalResourceParser8Async(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        if (connection == null)
            return new ResourceId(true, tdu.Data[tdu.PayloadOffset]);
        else
            return connection.Instance.Warehouse.GetById(tdu.Data[tdu.PayloadOffset]);
    }

    public static unsafe object LocalResourceParser8(ParsedTdu tdu, Warehouse warehouse)
    {
        return new ResourceId(true, tdu.Data[tdu.PayloadOffset]);
    }

    public static unsafe object ResourceParser16Async(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            if (connection == null)
                return new ResourceId(false, *(ushort*)ptr);
            else
                return connection.FetchResource(*(ushort*)ptr, requestSequence);
    }

    public static unsafe object ResourceParser16(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return new ResourceId(false, *(ushort*)ptr);
    }


    public static unsafe object LocalResourceParser16Async(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            if (connection == null)
                return new ResourceId(true, *(ushort*)ptr);
            else
                return connection.Instance.Warehouse.GetById(*(ushort*)ptr);
    }

    public static unsafe object LocalResourceParser16(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return new ResourceId(true, *(ushort*)ptr);
    }

    public static unsafe object ResourceParser32Async(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            if (connection == null)
                return new ResourceId(false, *(uint*)ptr);
            else
                return connection.FetchResource(*(uint*)ptr, requestSequence);
    }

    public static unsafe object ResourceParser32(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return new ResourceId(false, *(uint*)ptr);
    }


    public static unsafe object LocalResourceParser32Async(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            if (connection == null)
                return new ResourceId(true, *(uint*)ptr);
            else
                return connection.Instance.Warehouse.GetById(*(uint*)ptr);
    }

    public static unsafe object LocalResourceParser32(ParsedTdu tdu, Warehouse warehouse)
    {
        fixed (byte* ptr = &tdu.Data[tdu.PayloadOffset])
            return new ResourceId(true, *(uint*)ptr);
    }


    public static unsafe object RawDataParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return tdu.Data.Clip(tdu.PayloadOffset, (uint)tdu.PayloadLength);
    }

    public static unsafe object RawDataParser(ParsedTdu tdu, Warehouse warehouse)
    {
        return tdu.Data.Clip(tdu.PayloadOffset, (uint)tdu.PayloadLength);
    }


    public static unsafe object StringParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        return tdu.Data.GetString(tdu.PayloadOffset, (uint)tdu.PayloadLength);
    }

    public static unsafe object StringParser(ParsedTdu tdu, Warehouse warehouse)
    {
        return tdu.Data.GetString(tdu.PayloadOffset, (uint)tdu.PayloadLength);
    }

    public static async AsyncReply<IRecord> RecordParserAsync(ParsedTdu tdu, TypeDef recordTypeDef, EpConnection connection, uint[] requestSequence)
    {
        //if (tdu.Metadata.TypeDefId == null)
        //    throw new Exception("TypeDefId metadata is required for record parsing.");

        //TypeDefId typeDefId = tdu.Metadata.TypeDefId.Value;
        //TypeDef typeDef = null;


        //.GetUInt32(0);
        //var typeDef = connection.Instance.Warehouse.GetTypeDefById(tdu.Metadata.TypeDefId,
        //                                                            TypeDefKind.Record);
        var rt = new AsyncReply<IRecord>();


        var list = new AsyncBag<object>();

        ParsedTdu current;
        ParsedTdu? previous = null;

        var offset = tdu.PayloadOffset;
        var length = tdu.PayloadLength;
        var ends = offset + (uint)length;


        for (var i = 0; i < recordTypeDef.Properties.Length; i++)
        {
            current = await ParsedTdu.ParseAsync(tdu.Data, offset, ends, connection);

            if (current.Class == TduClass.Invalid)
                throw new Exception("Unknown type.");


            if (current.Identifier == TduIdentifier.TypeContinuation)
            {
                current.Class = previous.Value.Class;
                current.Identifier = previous.Value.Identifier;
                current.Metadata = previous.Value.Metadata;
            }
            else if (current.Identifier == TduIdentifier.TypeOfTarget)
            {
                //var idf = typeDef.Properties[i].ValueType.GetMetadata();
                var propTru = recordTypeDef.Properties[i].ValueType;

                current.Class = TduClass.Typed;
                current.Identifier = TduIdentifier.Typed;// propTru.Identifier;//  idf;
                current.Metadata = propTru;// mt;
                current.Index = (int)TduIdentifier.Typed & 0x7;// (int)idf & 0x7;
            }

            var value = Codec.ParseAsync(current, connection, requestSequence);

            list.Add(value);

            if (current.TotalLength > 0)
            {
                offset += (uint)current.TotalLength;
                length -= (uint)current.TotalLength;
                previous = current;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        list.Seal();

        var results = await list;

        Type recordType = recordTypeDef is LocalTypeDef localTypeDef ? localTypeDef.DefinedType
                  : recordTypeDef is RemoteTypeDef remoteTypeDef ? remoteTypeDef.ProxyType : null;

        if (recordType != null)
        {

            var record = Activator.CreateInstance(recordType) as IRecord;
            for (var i = 0; i < recordTypeDef.Properties.Length; i++)
            {
                try
                {
                    var v = RuntimeCaster.Cast(results[i], recordTypeDef.Properties[i].PropertyInfo.PropertyType);
                    recordTypeDef.Properties[i].PropertyInfo.SetValue(record, v);
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
            var record = new Record(recordTypeDef);

            for (var i = 0; i < recordTypeDef.Properties.Length; i++)
                record.Add(recordTypeDef.Properties[i].Name, results[i]);

            return record;
        }


        //list.Then(results =>
        //{
        //    Type recordType = recordTypeDef is LocalTypeDef localTypeDef ? localTypeDef.DefinedType
        //                      : recordTypeDef is RemoteTypeDef remoteTypeDef ? remoteTypeDef.ProxyType : null;

        //    if (recordType != null)
        //    {

        //        var record = Activator.CreateInstance(recordType) as IRecord;
        //        for (var i = 0; i < recordTypeDef.Properties.Length; i++)
        //        {
        //            try
        //            {
        //                var v = RuntimeCaster.Cast(results[i], recordTypeDef.Properties[i].PropertyInfo.PropertyType);
        //                recordTypeDef.Properties[i].PropertyInfo.SetValue(record, v);
        //            }
        //            catch (Exception ex)
        //            {
        //                Global.Log(ex);
        //            }
        //        }

        //        rt.Trigger(record);
        //    }
        //    else
        //    {
        //        var record = new Record(recordTypeDef);

        //        for (var i = 0; i < recordTypeDef.Properties.Length; i++)
        //            record.Add(recordTypeDef.Properties[i].Name, results[i]);

        //        rt.Trigger(record);
        //    }

        //}).Error(e =>
        //{
        //    rt.TriggerError(e);
        //});

        //return rt;
    }


    public static unsafe object RecordParser(ParsedTdu tdu, LocalTypeDef recordTypeDef, Warehouse warehouse)
    {

        //if (tdu.Metadata is not TypeDefId)
        //    throw new Exception("Unsupported metadata.");


        //TypeDefId typeDefId = (TypeDefId)tdu.Metadata;

        //if (typeDefId.Remote)
        //    throw new Exception("Unsupported in synchronous parsing.");

        //var typeDef = warehouse.GetLocalTypeDefById(recordTypeDef.Value);

        if (recordTypeDef == null)
        {
            // @TODO: add parse if no TypeDef settings
            throw new AsyncException(ErrorType.Management, (ushort)ExceptionCode.TypeDefNotFound,
                    "TypeDef not found for record.");
        }

        var list = new List<object>();

        ParsedTdu current;
        ParsedTdu? previous = null;

        var offset = tdu.PayloadOffset;
        var length = tdu.PayloadLength;
        var ends = offset + (uint)length;


        for (var i = 0; i < recordTypeDef.Properties.Length; i++)
        {
            current = ParsedTdu.ParseSync(tdu.Data, offset, ends, warehouse);

            if (current.Class == TduClass.Invalid)
                throw new Exception("Unknown type.");


            if (current.Identifier == TduIdentifier.TypeContinuation)
            {
                current.Class = previous.Value.Class;
                current.Identifier = previous.Value.Identifier;
                current.Metadata = previous.Value.Metadata;
            }
            else if (current.Identifier == TduIdentifier.TypeOfTarget)
            {
                //var (idf, mt) = recordTypeDef.Properties[i].ValueType.GetMetadata();

                var propTru = recordTypeDef.Properties[i].ValueType;
                current.Class = TduClass.Typed;
                current.Identifier = TduIdentifier.Typed;// idf;
                current.Metadata = propTru;// mt;
                current.Index = (int)TduIdentifier.Typed & 0x7;
            }

            var reply = Codec.ParseSync(current, warehouse);

            list.Add(reply);

            if (current.TotalLength > 0)
            {
                offset += (uint)current.TotalLength;
                length -= (uint)current.TotalLength;
                previous = current;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        if (recordTypeDef.DefinedType != null)
        {

            var record = Activator.CreateInstance(recordTypeDef.DefinedType) as IRecord;
            for (var i = 0; i < recordTypeDef.Properties.Length; i++)
            {
                try
                {
                    var v = RuntimeCaster.Cast(list[i], recordTypeDef.Properties[i].PropertyInfo.PropertyType);
                    recordTypeDef.Properties[i].PropertyInfo.SetValue(record, v);
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
            var record = new Record(recordTypeDef);

            for (var i = 0; i < recordTypeDef.Properties.Length; i++)
                record.Add(recordTypeDef.Properties[i].Name, list[i]);

            return record;
        }
    }

    public static unsafe object ConstantParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        throw new NotImplementedException();
    }

    public static unsafe object ConstantParser(ParsedTdu tdu, Warehouse warehouse)
    {
        throw new NotImplementedException();
    }

    public static unsafe AsyncReply EnumParserAsync(ParsedTdu tdu, TypeDef enumTypeDef, EpConnection connection, uint[] requestSequence)
    {
        var index = tdu.Data[tdu.PayloadOffset];
        return new AsyncReply(enumTypeDef.Constants[index].Value);

        //TypeDef typeDef = null;

        //if (tdu.Metadata is TypeDefId typeDefId)
        //{
        //    if (typeDefId.Remote)
        //    {
        //        typeDef = connection.Instance.Warehouse.GetRemoteTypeDefById(connection.RemoteDomain, typeDefId.Value);

        //        var index = tdu.Data[tdu.Offset];

        //        if (typeDef == null)
        //        {
        //            var reply = new AsyncReply();

        //            connection.GetTypeDefById(typeDefId.Value).Then(td =>
        //            {
        //                reply.Trigger(td.Constants[index].Value);
        //            }).Error(x => reply.TriggerError(x));

        //            return reply;
        //        }
        //        else
        //        {
        //            return new AsyncReply(typeDef.Constants[index].Value);
        //        }
        //    }
        //    else
        //    {
        //        typeDef = connection.Instance.Warehouse.GetLocalTypeDefById(typeDefId.Value);

        //        if (typeDef == null)
        //        {
        //            throw new AsyncException(ErrorType.Management, (ushort)ExceptionCode.TypeDefNotFound,
        //                    "TypeDef not found for enum.");
        //        }

        //        var index = tdu.Data[tdu.Offset];
        //        return new AsyncReply(typeDef.Constants[index].Value);

        //    }
        //}
        //else
        //{
        //    throw new Exception("Unsupported metadata.");
        //}
    }

    public static unsafe object EnumParser(ParsedTdu tdu, TypeDef enumTypeDef, Warehouse warehouse)
    {
        //TypeDef typeDef = null;

        //if (tdu.Metadata is TypeDefId typeDefId)
        //{
        //    if (typeDefId.Remote)
        //    {
        //        throw new Exception("Unsupported in synchronous parsing.");
        //    }

        //    typeDef = warehouse.GetLocalTypeDefById(typeDefId.Value);
        //}

        //if (typeDef == null)
        //{
        //    throw new AsyncException(ErrorType.Management, (ushort)ExceptionCode.TypeDefNotFound,
        //            "TypeDef not found for enum.");
        //}

        var index = tdu.Data[tdu.PayloadOffset];
        return enumTypeDef.Constants[index].Value;
    }


    public static async AsyncReply<object> RecordListParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        var rt = new AsyncBag<IRecord>();

        var length = tdu.PayloadLength;
        var offset = tdu.PayloadOffset;

        while (length > 0)
        {
            //var (cs, reply) 
            var pr = await Codec.ParseAsync(tdu.Data, offset, connection, requestSequence);

            rt.Add(pr.Value);

            if (pr.Size > 0)
            {
                offset += (uint)pr.Size;
                length -= (uint)pr.Size;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        rt.Seal();
        var results = await rt;

        return results;
    }

    public static object RecordListParser(ParsedTdu tdu, Warehouse warehouse)
    {
        var rt = new List<IRecord>();

        var length = tdu.PayloadLength;
        var offset = tdu.PayloadOffset;

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

    public static async AsyncReply<object> ResourceListParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        var rt = new AsyncBag<IResource>();

        var length = tdu.PayloadLength;
        var offset = tdu.PayloadOffset;

        while (length > 0)
        {
            //var (cs, reply) 
            var pr = await Codec.ParseAsync(tdu.Data, offset, connection, requestSequence);

            rt.Add(pr.Value);// reply);

            if (pr.Size > 0)
            {
                offset += (uint)pr.Size;
                length -= (uint)pr.Size;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        rt.Seal();

        var results = await rt;
        return results;
    }


    public static object ResourceListParser(ParsedTdu tdu, Warehouse warehouse)
    {
        var rt = new List<IResource>();

        var length = tdu.PayloadLength;
        var offset = tdu.PayloadOffset;

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

    public static async AsyncReply<object> ListParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
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

        ParsedTdu current;
        ParsedTdu? previous = null;

        var offset = tdu.PayloadOffset;
        var length = tdu.PayloadLength;
        var ends = offset + (uint)length;

        while (length > 0)
        {
            current = await ParsedTdu.ParseAsync(tdu.Data, offset, ends, connection);

            if (current.Class == TduClass.Invalid)
                throw new Exception("Unknown type.");


            if (current.Identifier == TduIdentifier.TypeContinuation)
            {
                current.Class = previous.Value.Class;
                current.Identifier = previous.Value.Identifier;
                current.Metadata = previous.Value.Metadata;
            }


 
            //var (cs, reply)
            var value =  Codec.ParseAsync(current, connection, requestSequence);

            rt.Add(value);

            if (current.TotalLength > 0)
            {
                offset += (uint)current.TotalLength;
                length -= (uint)current.TotalLength;
                previous = current;
            }
            else
                throw new Exception("Error while parsing structured data");

        }

        rt.Seal();

        var result = await rt;
        return result;

    }

    public static object ListParser(ParsedTdu tdu, Warehouse warehouse)
    {
        var list = new List<object>();

        ParsedTdu current;
        ParsedTdu? previous = null;

        var offset = tdu.PayloadOffset;
        var length = tdu.PayloadLength;
        var ends = offset + (uint)length;

        while (length > 0)
        {
            current = ParsedTdu.ParseSync(tdu.Data, offset, ends, warehouse);

            if (current.Class == TduClass.Invalid)
                throw new Exception("Unknown type.");


            if (current.Identifier == TduIdentifier.TypeContinuation)
            {
                current.Class = previous.Value.Class;
                current.Identifier = previous.Value.Identifier;
                current.Metadata = previous.Value.Metadata;
            }


            var reply = Codec.ParseSync(current, warehouse);

            list.Add(reply);

            if (current.TotalLength > 0)
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


    public static async AsyncReply<object> TypedMapParserAsync(ParsedTdu tdu, Tru keyTru, Tru valueTru, EpConnection connection, uint[] requestSequence)
    {

        var rt = new AsyncReply();

        // get key type

        //var (keyCs, keysTru) = Tru.Parse(tdu.Metadata, 0);
        //var (valueCs, valuesTru) = Tru.Parse(tdu.Metadata, keyCs);

        var map = (IMap)Activator.CreateInstance(typeof(Map<,>).MakeGenericType(
            keyTru.RuntimeType,
            valueTru.RuntimeType));


        var keysTdu = await ParsedTdu.ParseAsync(tdu.Data, tdu.PayloadOffset,
                                      (uint)(tdu.PayloadOffset + tdu.PayloadLength), connection);

        var valuesTdu = await ParsedTdu.ParseAsync(tdu.Data,
                                        (uint)(keysTdu.PayloadOffset + keysTdu.PayloadLength),
                                        tdu.Ends, connection);

        //var keysReply = TypedArrayParserAsync(keysTdu, keyTru, connection, requestSequence);
        //var valuesReply = TypedArrayParserAsync(valuesTdu, valueTru, connection, requestSequence);

        //keysReply.Then(keys =>
        //{
        //    valuesReply.Then(values =>
        //    {
        //        for (var i = 0; i < ((Array)keys).Length; i++)
        //            map.Add(((Array)keys).GetValue(i), ((Array)values).GetValue(i));
        //        rt.Trigger(map);
        //    }).Error(e => rt.TriggerError(e));
        //}).Error(e => rt.TriggerError(e));
        //return rt;


        var keys = await TypedArrayParserAsync2(keysTdu, keyTru, connection, requestSequence);
        var values = await TypedArrayParserAsync2(valuesTdu, valueTru, connection, requestSequence);

        for (var i = 0; i < ((Array)keys).Length; i++)
            map.Add(((Array)keys).GetValue(i), ((Array)values).GetValue(i));

        return map;

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

    public static Array TypedArrayParser(ParsedTdu tdu, Tru elementTru, Warehouse warehouse)
    {
        switch (elementTru.Identifier)
        {
            case TruIdentifier.Int32:
                return GroupInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength));
            case TruIdentifier.Int64:
                return GroupInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength));
            case TruIdentifier.Int16:
                return GroupInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength));
            case TruIdentifier.UInt32:
                return GroupUInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength));
            case TruIdentifier.UInt64:
                return GroupUInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength));
            case TruIdentifier.UInt16:
                return GroupUInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength));
            //case TruIdentifier.Enum:

            //    var enumType = tru.GetRuntimeType(warehouse);

            //    var enums = Array.CreateInstance(enumType, (int)tdu.ContentLength);
            //    var enumTypeDef = warehouse.GetTypeDefByType(enumType);

            //    for (var i = 0; i < (int)tdu.ContentLength; i++)
            //    {
            //        var index = tdu.Data[tdu.Offset + i];
            //        enums.SetValue(enumTypeDef.Constants[index].Value, i);
            //    }

            //    return enums;

            default:


                var list = new List<object>();

                ParsedTdu current;
                ParsedTdu? previous = null;

                var offset = tdu.PayloadOffset;
                var length = tdu.PayloadLength;
                var ends = offset + (uint)length;

                while (length > 0)
                {
                    current = ParsedTdu.ParseSync(tdu.Data, offset, ends, warehouse);

                    if (current.Class == TduClass.Invalid)
                        throw new Exception("Unknown type.");


                    if (current.Identifier == TduIdentifier.TypeContinuation)
                    {
                        current.Class = previous.Value.Class;
                        current.Identifier = previous.Value.Identifier;
                        current.Metadata = previous.Value.Metadata;
                    }
                    else if (current.Identifier == TduIdentifier.TypeOfTarget)
                    {
                        //var (idf, mt) = tru.GetMetadata();
                        current.Class = TduClass.Typed;
                        current.Identifier = TduIdentifier.Typed;
                        current.Metadata = elementTru;
                        current.Index = (int)TduIdentifier.Typed & 0x7;
                    }

                    var value = Codec.ParseSync(current, warehouse);

                    list.Add(value);

                    if (current.TotalLength > 0)
                    {
                        offset += (uint)current.TotalLength;
                        length -= (uint)current.TotalLength;
                        previous = current;
                    }
                    else
                        throw new Exception("Error while parsing structured data");

                }

                var runtimeType = elementTru.RuntimeType;
                var rt = Array.CreateInstance(runtimeType, list.Count);
                Array.Copy(list.ToArray(), rt, rt.Length);

                return rt;
        }

    }

    // Non-async/await version of TypedArrayParserAsync using continuations
    public static AsyncReply TypedArrayParserAsync2(ParsedTdu tdu, Tru elementTru, EpConnection connection, uint[] requestSequence)
    {
        switch (elementTru.Identifier)
        {
            case TruIdentifier.Int32:
                return new AsyncReply(GroupInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.Int64:
                return new AsyncReply(GroupInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.Int16:
                return new AsyncReply(GroupInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.UInt32:
                return new AsyncReply(GroupUInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.UInt64:
                return new AsyncReply(GroupUInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.UInt16:
                return new AsyncReply(GroupUInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));

            default:
                var rt = new AsyncReply();

                var list = new AsyncBag<object>();
                list.ArrayType = elementTru.RuntimeType;

                var offset = tdu.PayloadOffset;
                var length = tdu.PayloadLength;
                var ends = offset + (uint)length;

                // Attach error propagation from bag to our reply
                list.Error(e => rt.TriggerError(e));

                // Recursive processor using continuations
                Action<uint, uint, ParsedTdu?> processNext = null;

                processNext = (curOffset, curLength, previous) =>
                {
                    if (curLength == 0)
                    {
                        try
                        {
                            list.Seal();
                            (list as AsyncReply).Then(x => rt.Trigger(x));
                        }
                        catch (Exception ex)
                        {
                            rt.TriggerError(ex);
                        }
                        return;
                    }

                    ParsedTdu.ParseAsync(tdu.Data, curOffset, ends, connection)
                        .Then(currentObj =>
                        {
                            var current = (ParsedTdu)currentObj;

                            if (current.Class == TduClass.Invalid)
                            {
                                rt.TriggerError(new AsyncException(ErrorType.Management, 0, "Unknown type."));
                                return;
                            }

                            if (current.Identifier == TduIdentifier.TypeContinuation)
                            {
                                if (previous.HasValue)
                                {
                                    current.Class = previous.Value.Class;
                                    current.Identifier = previous.Value.Identifier;
                                    current.Metadata = previous.Value.Metadata;
                                }
                            }
                            else if (current.Identifier == TduIdentifier.TypeOfTarget)
                            {
                                current.Class = TduClass.Typed;
                                current.Identifier = TduIdentifier.Typed;
                                current.Metadata = elementTru;
                                current.Index = (int)TduIdentifier.Typed & 0x7;
                            }

                            var reply = Codec.ParseAsync(current, connection, requestSequence);

                            list.Add(reply);

                            if (current.TotalLength > 0)
                            {
                                var nextOffset = curOffset + (uint)current.TotalLength;
                                var nextLength = curLength - (uint)current.TotalLength;
                                processNext(nextOffset, nextLength, current);
                            }
                            else
                            {
                                rt.TriggerError(new AsyncException(ErrorType.Management, 0, "Error while parsing structured data"));
                                return;
                            }
                        })
                        .Error(e => rt.TriggerError(e));
                };

                // start processing
                processNext(offset, (uint)length, null);

                return rt;
        }
    }

    public static object TypedMapParser(ParsedTdu tdu, Tru keyTru, Tru valueTru, Warehouse warehouse)
    {
        // get key type

        //var (keyCs, keysTru) = Tru.Parse(tdu.Metadata, 0);
        //var (valueCs, valuesTru) = Tru.Parse(tdu.Metadata, keyCs);

        var map = (IMap)Activator.CreateInstance(typeof(Map<,>).MakeGenericType(keyTru.RuntimeType, valueTru.RuntimeType));



        var keysTdu = ParsedTdu.ParseSync(tdu.Data, tdu.PayloadOffset,
                                      (uint)(tdu.PayloadOffset + tdu.PayloadLength), warehouse);

        var valuesTdu = ParsedTdu.ParseSync(tdu.Data,
                                        (uint)(keysTdu.PayloadOffset + keysTdu.PayloadLength),
                                        tdu.Ends, warehouse);

        var keys = TypedArrayParser(keysTdu, keyTru, warehouse);
        var values = TypedArrayParser(valuesTdu, valueTru, warehouse);

        for (var i = 0; i < keys.Length; i++)
            map.Add(keys.GetValue(i), values.GetValue(i));

        return map;

    }

    public static async AsyncReply<object> TupleParserAsync(ParsedTdu tdu, Tru[] subTrus, EpConnection connection, uint[] requestSequence)
    {

        var rt = new AsyncReply();

        // var tupleSize = tdu.Metadata[0];

        //var trus = new List<Tru>();

        //uint mtOffset = 0;

        //while (mtOffset < tru.Length)
        //{
        //    var (cs, tru) = Tru.Parse(tdu.Metadata, mtOffset);
        //    trus.Add(tru);
        //    mtOffset += cs;
        //}

        var results = new AsyncBag<object>();
        var subTypes = subTrus.Select(x => x.RuntimeType).ToArray();

        ParsedTdu current;

        var offset = tdu.PayloadOffset;
        var length = tdu.PayloadLength;
        var ends = offset + (uint)length;


        for (var i = 0; i < subTypes.Length; i++)
        {
            current = await ParsedTdu.ParseAsync(tdu.Data, offset, ends, connection);

            if (current.Class == TduClass.Invalid)
                throw new Exception("Unknown type.");

            if (current.Identifier == TduIdentifier.TypeOfTarget)
            {
                //var (idf, mt) = trus[i].GetMetadata();
                current.Class = TduClass.Typed;
                current.Identifier = TduIdentifier.Typed;// idf;
                current.Metadata = subTrus[i];// mt;
                current.Index = (int)(TduIdentifier.Typed) & 0x7;// idf & 0x7;
            }

            // var (_, reply) 
            var value = Codec.ParseAsync(current, connection, requestSequence);

            results.Add(value);

            if (current.TotalLength > 0)
            {
                offset += (uint)current.TotalLength;
                length -= (uint)current.TotalLength;
            }
            else
                throw new Exception("Error while parsing structured data");

        }


        results.Seal();

        var ar = await results;

        if (ar.Length == 2)
        {
            var type = typeof(ValueTuple<,>).MakeGenericType(subTypes.ToArray());
            return Activator.CreateInstance(type, ar[0], ar[1]);
        }
        else if (ar.Length == 3)
        {
            var type = typeof(ValueTuple<,,>).MakeGenericType(subTypes.ToArray());
            return Activator.CreateInstance(type, ar[0], ar[1], ar[2]);
        }
        else if (ar.Length == 4)
        {
            var type = typeof(ValueTuple<,,,>).MakeGenericType(subTypes.ToArray());
            return Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3]);
        }
        else if (ar.Length == 5)
        {
            var type = typeof(ValueTuple<,,,,>).MakeGenericType(subTypes.ToArray());
            return Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3], ar[4]);
        }
        else if (ar.Length == 6)
        {
            var type = typeof(ValueTuple<,,,,,>).MakeGenericType(subTypes.ToArray());
            return Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3], ar[4], ar[5]);
        }
        else if (ar.Length == 7)
        {
            var type = typeof(ValueTuple<,,,,,,>).MakeGenericType(subTypes.ToArray());
            return Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3], ar[4], ar[5], ar[6]);
        }

        throw new Exception("Unknown tuple size.");


        //results.Then(ar =>
        //        {
        //            if (ar.Length == 2)
        //            {
        //                var type = typeof(ValueTuple<,>).MakeGenericType(subTypes.ToArray());
        //                rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1]));
        //            }
        //            else if (ar.Length == 3)
        //            {
        //                var type = typeof(ValueTuple<,,>).MakeGenericType(subTypes.ToArray());
        //                rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2]));
        //            }
        //            else if (ar.Length == 4)
        //            {
        //                var type = typeof(ValueTuple<,,,>).MakeGenericType(subTypes.ToArray());
        //                rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3]));
        //            }
        //            else if (ar.Length == 5)
        //            {
        //                var type = typeof(ValueTuple<,,,,>).MakeGenericType(subTypes.ToArray());
        //                rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3], ar[4]));
        //            }
        //            else if (ar.Length == 6)
        //            {
        //                var type = typeof(ValueTuple<,,,,,>).MakeGenericType(subTypes.ToArray());
        //                rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3], ar[4], ar[5]));
        //            }
        //            else if (ar.Length == 7)
        //            {
        //                var type = typeof(ValueTuple<,,,,,,>).MakeGenericType(subTypes.ToArray());
        //                rt.Trigger(Activator.CreateInstance(type, ar[0], ar[1], ar[2], ar[3], ar[4], ar[5], ar[6]));
        //            }
        //        });

        //return rt;
    }

    public static object TupleParser(ParsedTdu tdu, Tru[] subTrus, Warehouse warehouse)
    {
        var tupleSize = subTrus.Length;

        var trus = new List<Tru>();

        //uint mtOffset = 1;
        //for (var i = 0; i < tupleSize; i++)
        //{
        //    var (cs, tru) = Tru.Parse(tdu.Metadata, mtOffset);
        //    trus.Add(tru);
        //    mtOffset += cs;
        //}

        var results = new List<object>();
        var types = subTrus.Select(x => x.RuntimeType).ToArray();

        ParsedTdu current;

        var offset = tdu.PayloadOffset;
        var length = tdu.PayloadLength;
        var ends = offset + (uint)length;


        for (var i = 0; i < tupleSize; i++)
        {
            current = ParsedTdu.ParseSync(tdu.Data, offset, ends, warehouse);

            if (current.Class == TduClass.Invalid)
                throw new Exception("Unknown type.");

            if (current.Identifier == TduIdentifier.TypeOfTarget)
            {
                //var (idf, mt) = trus[i].GetMetadata();
                current.Class = TduClass.Typed;
                current.Identifier = TduIdentifier.Typed;
                current.Metadata = subTrus[i];
                current.Index = (int)TduIdentifier.Typed & 0x7;
            }

            var reply = Codec.ParseSync(current, warehouse);

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

    public static async AsyncReply<object> TypedArrayParserAsyncOld(ParsedTdu tdu, Tru elementTru, EpConnection connection, uint[] requestSequence)
    {
        switch (elementTru.Identifier)
        {
            case TruIdentifier.Int32:
                return new AsyncReply(GroupInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.Int64:
                return new AsyncReply(GroupInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.Int16:
                return new AsyncReply(GroupInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.UInt32:
                return new AsyncReply(GroupUInt32Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.UInt64:
                return new AsyncReply(GroupUInt64Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));
            case TruIdentifier.UInt16:
                return new AsyncReply(GroupUInt16Codec.Decode(tdu.Data.AsSpan(
                                                    (int)tdu.PayloadOffset, (int)tdu.PayloadLength)));

            //case TruIdentifier.Enum:
            //    var enumType = elementTru.GetRuntimeType(connection.Instance.Warehouse);

            //    var rt = Array.CreateInstance(enumType, (int)tdu.ContentLength);
            //    var enumTypeDef = connection.Instance.Warehouse.GetTypeDefByType(enumType);

            //    for (var i = 0; i < (int)tdu.ContentLength; i++)
            //    {
            //        var index = tdu.Data[tdu.Offset + i];

            //        rt.SetValue(Enum.ToObject(enumType, enumTypeDef.Constants[index].Value), i);
            //    }

            //    return new AsyncReply(rt);

            default:


                var list = new AsyncBag<object>();

                list.ArrayType = elementTru.RuntimeType;

                ParsedTdu current;
                ParsedTdu? previous = null;

                var offset = tdu.PayloadOffset;
                var length = tdu.PayloadLength;
                var ends = offset + (uint)length;

                while (length > 0)
                {
                    current = await ParsedTdu.ParseAsync(tdu.Data, offset, ends, connection);

                    if (current.Class == TduClass.Invalid)
                        throw new Exception("Unknown type.");

                    if (current.Identifier == TduIdentifier.TypeContinuation)
                    {
                        current.Class = previous.Value.Class;
                        current.Identifier = previous.Value.Identifier;
                        current.Metadata = previous.Value.Metadata;
                    }
                    else if (current.Identifier == TduIdentifier.TypeOfTarget)
                    {
                        //var (idf, mt) = tru.GetMetadata();
                        current.Class = TduClass.Typed;
                        current.Identifier = TduIdentifier.Typed;// idf;
                        current.Metadata = elementTru;// mt;
                        current.Index = (int)TduIdentifier.Typed & 0x7;// (int)idf & 0x7;
                    }

                    //var (cs, reply) 

                    var reply = Codec.ParseAsync(current, connection, requestSequence);

                    list.Add(reply);

                    if (current.TotalLength > 0)
                    {
                        offset += (uint)current.TotalLength;
                        length -= (uint)current.TotalLength;
                        previous = current;
                    }
                    else
                        throw new Exception("Error while parsing structured data");

                }

                list.Seal();
                return await list;
        }

    }

    //public static AsyncReply TypedListParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    //{
    //    // get the type
    //    var (hdrCs, tru) = Tru.Parse(tdu.Metadata, 0);

    //    return TypedArrayParserAsync(tdu, tru, connection, requestSequence);

    //    //switch (rep.Identifier)
    //    //{
    //    //    case TRUIdentifier.Int32:
    //    //        return new AsyncReply(GroupInt32Codec.Decode(tdu.Data.AsSpan(
    //    //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
    //    //    case TRUIdentifier.Int64:
    //    //        return new AsyncReply(GroupInt64Codec.Decode(tdu.Data.AsSpan(
    //    //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
    //    //    case TRUIdentifier.Int16:
    //    //        return new AsyncReply(GroupInt16Codec.Decode(tdu.Data.AsSpan(
    //    //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
    //    //    case TRUIdentifier.UInt32:
    //    //        return new AsyncReply(GroupUInt32Codec.Decode(tdu.Data.AsSpan(
    //    //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
    //    //    case TRUIdentifier.UInt64:
    //    //        return new AsyncReply(GroupUInt64Codec.Decode(tdu.Data.AsSpan(
    //    //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
    //    //    case TRUIdentifier.UInt16:
    //    //        return new AsyncReply(GroupUInt16Codec.Decode(tdu.Data.AsSpan(
    //    //                                            (int)tdu.Offset, (int)tdu.ContentLength)));
    //    //    default:

    //    //        var rt = new AsyncBag<object>();

    //    //        var runtimeType = rep.GetRuntimeType(connection.Instance.Warehouse);

    //    //        rt.ArrayType = runtimeType;

    //    //        ParsedTDU current;
    //    //        ParsedTDU? previous = null;

    //    //        var offset = tdu.Offset;
    //    //        var length = tdu.ContentLength;
    //    //        var ends = offset + (uint)length;

    //    //        while (length > 0)
    //    //        {

    //    //            current = ParsedTDU.Parse(tdu.Data, offset, ends);

    //    //            if (current.Class == TDUClass.Invalid)
    //    //                throw new Exception("Unknown type.");


    //    //            if (current.Identifier == TDUIdentifier.TypeContinuation)
    //    //            {
    //    //                current.Class = previous.Value.Class;
    //    //                current.Identifier = previous.Value.Identifier;
    //    //                current.Metadata = previous.Value.Metadata;
    //    //            }

    //    //            var (cs, reply) = Codec.ParseAsync(tdu.Data, offset, connection, requestSequence);

    //    //            rt.Add(reply);

    //    //            if (cs > 0)
    //    //            {
    //    //                offset += (uint)cs;
    //    //                length -= (uint)cs;
    //    //            }
    //    //            else
    //    //                throw new Exception("Error while parsing structured data");

    //    //        }

    //    //        rt.Seal();
    //    //        return rt;
    //    //}
    //}

    public static AsyncReply TypedObjectParserAsync(ParsedTdu tdu, TypeDef typeDef, EpConnection connection, uint[] requestSequence)
    {
        // get the TypeDef
        var warehouse = connection.Instance.Warehouse;
        //TypeDef typeDef = null;

        var rt = new AsyncReply();

        if (typeDef.Kind == TypeDefKind.Record)
        {
            // parse record
            RecordParserAsync(tdu, typeDef, connection, requestSequence)
                            .Then(x => rt.Trigger(x))
                            .Error(e => rt.TriggerError(e));
        }
        else if (typeDef.Kind == TypeDefKind.Enum)
        {
            // paese enum
            EnumParserAsync(tdu, typeDef, connection, requestSequence)
                            .Then(x => rt.Trigger(x))
                            .Error(e => rt.TriggerError(e));

        }
        else//  if (td.Kind == TypeDefKind.Resource)
        {
            // parse resource : this is kept for future support
            throw new Exception("Typed resource parsing is not supported yet.");
        }


        //if (typeDefId.Remote)
        //{
        //    typeDef = warehouse.GetRemoteTypeDefById(connection.RemoteDomain, typeDefId.Value);

        //    if (typeDef == null)
        //    {
        //        // get it from the other end
        //        connection.GetTypeDefById(typeDefId.Value)
        //            .Then(x => parseTyped(x))
        //            .Error(x => rt.TriggerError(x));

        //    }
        //}
        //else
        //{
        //    typeDef = warehouse.GetLocalTypeDefById(typeDefId.Value);

        //    if (typeDef == null)
        //    {
        //        throw new Exception("Bad TypeDefId.");
        //    }

        //    parseTyped(typeDef);

        //}

        return rt;
    }


    public static object TypedObjectParser(ParsedTdu tdu, TypeDef typeDef, Warehouse warehouse)
    {
        // get the TypeDef

        if (typeDef.Kind == TypeDefKind.Record)
        {
            // parse record
            return RecordParser(tdu, typeDef as LocalTypeDef, warehouse);
        }
        else if (typeDef.Kind == TypeDefKind.Enum)
        {
            // paese enum
            return EnumParser(tdu, typeDef as LocalTypeDef, warehouse);
        }
        else//  if (td.Kind == TypeDefKind.Resource)
        {
            // parse resource : this is kept for future support
            throw new Exception("Typed resource parsing is not supported yet.");
        }
    }

    public static AsyncReply TypedParserAsync(ParsedTdu tdu, EpConnection connection, uint[] requestSequence)
    {
        var tru = tdu.Metadata;

        // do we need to get all subtypes typedefs here ?

        if (tru is TruComposite truComposite)
        {
            return tru.Identifier switch
            {
                TruIdentifier.TypedList => TypedArrayParserAsync2(tdu, truComposite.SubTypes[0], connection, requestSequence),
                TruIdentifier.TypedMap => TypedMapParserAsync(tdu, truComposite.SubTypes[0], truComposite.SubTypes[1], connection, requestSequence),
                TruIdentifier.Tuple2 => TupleParserAsync(tdu, truComposite.SubTypes, connection, requestSequence),
                TruIdentifier.Tuple3 => TupleParserAsync(tdu, truComposite.SubTypes, connection, requestSequence),
                TruIdentifier.Tuple4 => TupleParserAsync(tdu, truComposite.SubTypes, connection, requestSequence),
                TruIdentifier.Tuple5 => TupleParserAsync(tdu, truComposite.SubTypes, connection, requestSequence),
                TruIdentifier.Tuple6 => TupleParserAsync(tdu, truComposite.SubTypes, connection, requestSequence),
                TruIdentifier.Tuple7 => TupleParserAsync(tdu, truComposite.SubTypes, connection, requestSequence),
                _ => throw new Exception("Unsupported type for typed parser.")
            };
        }
        else if (tru is TruTypeDef truTypeDef)
        {
            return TypedObjectParserAsync(tdu, truTypeDef.TypeDef, connection, requestSequence);
        }

        throw new Exception("Unknown TRU.");

    }

    public static object TypedParser(ParsedTdu tdu, Warehouse warehouse)
    {
        var tru = tdu.Metadata;

        // do we need to get all subtypes typedefs here ?

        if (tru is TruComposite truComposite)
        {
            return tru.Identifier switch
            {
                TruIdentifier.TypedList => TypedArrayParser(tdu, truComposite.SubTypes[0], warehouse),
                TruIdentifier.TypedMap => TypedMapParser(tdu, truComposite.SubTypes[0], truComposite.SubTypes[1], warehouse),
                TruIdentifier.Tuple2 => TupleParser(tdu, truComposite.SubTypes, warehouse),
                TruIdentifier.Tuple3 => TupleParser(tdu, truComposite.SubTypes, warehouse),
                TruIdentifier.Tuple4 => TupleParser(tdu, truComposite.SubTypes, warehouse),
                TruIdentifier.Tuple5 => TupleParser(tdu, truComposite.SubTypes, warehouse),
                TruIdentifier.Tuple6 => TupleParser(tdu, truComposite.SubTypes, warehouse),
                TruIdentifier.Tuple7 => TupleParser(tdu, truComposite.SubTypes, warehouse),
                _ => throw new Exception("Unsupported type for typed parser.")
            };
        }
        else if (tru is TruTypeDef truTypeDef)
        {
            return TypedObjectParser(tdu, truTypeDef.TypeDef, warehouse);
        }

        throw new Exception("Unknown TRU.");
    }

    public static AsyncBag<PropertyValue> PropertyValueArrayParserAsync(byte[] data, uint offset, uint length, EpConnection connection, uint[] requestSequence)//, bool ageIncluded = true)
    {
        var rt = new AsyncBag<PropertyValue>();


        ListParserAsync(new ParsedTdu() { Data = data, PayloadOffset = offset, PayloadLength = length }
                        , connection, requestSequence).Then(x =>
        {

            var ar = (object[])x;
            var pvs = new List<PropertyValue>();

            for (var i = 0; i < ar.Length; i += 3)
                pvs.Add(new PropertyValue(ar[i + 2], Convert.ToUInt64(ar[i]), (DateTime?)ar[i + 1]));


            rt.Trigger(pvs.ToArray());
        });

        return rt;

    }


    public static async AsyncReply<ParseResult<PropertyValue>> PropertyValueParserAsync(byte[] data, uint offset, EpConnection connection, uint[] requestSequence)//, bool ageIncluded = true)
    {
        // var reply = new AsyncReply<PropertyValue>();

        var age = data.GetUInt64(offset, Endian.Little);
        offset += 8;

        DateTime date = data.GetDateTime(offset, Endian.Little);
        offset += 8;


        //var (valueSize, results) = 
        var pr = await Codec.ParseAsync(data, offset, connection, requestSequence);

        if (pr.Value is AsyncReply asycReply)
        {
            var value = await asycReply;
            return new ParseResult<PropertyValue>(new PropertyValue(value, age, date), 16 + pr.Size);
            //asycReply.Then(value =>
            //{
            //    reply.Trigger(new PropertyValue(value, age, date));
            //});
        }
        else
        {
            return new ParseResult<PropertyValue>(new PropertyValue(pr.Value, age, date), 16 + pr.Size);
            //reply.Trigger(new PropertyValue(results, age, date));
        }

        // return (16 + valueSize, reply);
    }

    public static async AsyncReply<KeyList<PropertyDef, PropertyValue[]>> HistoryParserAsync(byte[] data, uint offset, uint length, IResource resource, EpConnection connection, uint[] requestSequence)
    {
        //var count = (int)toAge - (int)fromAge;

        var list = new KeyList<PropertyDef, PropertyValue[]>();

        var reply = new AsyncReply<KeyList<PropertyDef, PropertyValue[]>>();

        var bagOfBags = new AsyncBag<PropertyValue[]>();

        var ends = offset + length;
        while (offset < ends)
        {
            var index = data[offset++];
            var pt = resource.Instance.Definition.GetPropertyDefByIndex(index);
            list.Add(pt, null);
            var cs = data.GetUInt32(offset, Endian.Little);
            offset += 4;

            //var (len, pv) = 
            var pr = await PropertyValueParserAsync(data, offset, connection, requestSequence);

            bagOfBags.Add(pr.Value);// ParsePropertyValueArray(data, offset, cs, connection));
            offset += pr.Size;
        }

        bagOfBags.Seal();

        var x = await bagOfBags;

        for (var i = 0; i < list.Count; i++)
            list[list.Keys.ElementAt(i)] = x[i];

        return list;

        //bagOfBags.Then(x =>
        //{
        //    for (var i = 0; i < list.Count; i++)
        //        list[list.Keys.ElementAt(i)] = x[i];

        //    reply.Trigger(list);
        //});

        //return reply;

    }




}
