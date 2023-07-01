/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Misc;
using System.ComponentModel;
using Esiur.Data;
using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Resource;
using System.Linq;
using System.Reflection;
using Esiur.Resource.Template;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Dynamic;

namespace Esiur.Data;

public static class Codec
{
 
    delegate AsyncReply Parser(byte[] data, uint offset, uint length, DistributedConnection connection, uint[] requestSequence);


    static Parser[][] FixedParsers = new Parser[][]
    {
        new Parser[]{
            DataDeserializer.NullParser,
            DataDeserializer.BooleanFalseParser,
            DataDeserializer.BooleanTrueParser,
            DataDeserializer.NotModifiedParser,
        },
        new Parser[]{
            DataDeserializer.ByteParser,
            DataDeserializer.SByteParser,
            DataDeserializer.Char8Parser,
        },
        new Parser[]{
            DataDeserializer.Int16Parser,
            DataDeserializer.UInt16Parser,
            DataDeserializer.Char16Parser,
        },
        new Parser[]{
            DataDeserializer.Int32Parser,
            DataDeserializer.UInt32Parser,
            DataDeserializer.Float32Parser,
            DataDeserializer.ResourceParser,
            DataDeserializer.LocalResourceParser,
        },
        new Parser[]{
            DataDeserializer.Int64Parser,
            DataDeserializer.UInt64Parser,
            DataDeserializer.Float64Parser,
            DataDeserializer.DateTimeParser,
        },
        new Parser[]
        {
            DataDeserializer.Int128Parser, // int 128
            DataDeserializer.UInt128Parser, // uint 128
            DataDeserializer.Float128Parser,
        }
    };

    static Parser[] DynamicParsers = new Parser[]
    {
        DataDeserializer.RawDataParser,
        DataDeserializer.StringParser,
        DataDeserializer.ListParser,
        DataDeserializer.ResourceListParser,
        DataDeserializer.RecordListParser,
    };

    static Parser[] TypedParsers = new Parser[]
    {
        DataDeserializer.RecordParser,
        DataDeserializer.TypedListParser,
        DataDeserializer.TypedMapParser,
        DataDeserializer.TupleParser,
        DataDeserializer.EnumParser,
        DataDeserializer.ConstantParser,
    };


    /// <summary>
    /// Parse a value
    /// </summary>
    /// <param name="data">Bytes array</param>
    /// <param name="offset">Zero-indexed offset.</param>
    /// <param name="size">Output the number of bytes parsed</param>
    /// <param name="connection">DistributedConnection is required in case a structure in the array holds items at the other end.</param>
    /// <param name="dataType">DataType, in case the data is not prepended with DataType</param>
    /// <returns>Value</returns>
    public static (uint, AsyncReply) Parse(byte[] data, uint offset, DistributedConnection connection, uint[] requestSequence, TransmissionType? dataType = null)
    {

        uint len = 0;

        if (dataType == null)
        {
            (var longLen, dataType) = TransmissionType.Parse(data, offset, (uint)data.Length);
            len = (uint)longLen;
            offset = dataType.Value.Offset;
        }
        else
            len = (uint)dataType.Value.ContentLength;

        var tt = dataType.Value;

        if (tt.Class == TransmissionTypeClass.Fixed)
        {
            return (len, FixedParsers[tt.Exponent][tt.Index](data, dataType.Value.Offset, (uint)tt.ContentLength, connection, requestSequence));
        }
        else if (tt.Class == TransmissionTypeClass.Dynamic)
        {
            return (len, DynamicParsers[tt.Index](data, dataType.Value.Offset, (uint)tt.ContentLength, connection, requestSequence));
        }
        else //if (tt.Class == TransmissionTypeClass.Typed)
        {
            return (len, TypedParsers[tt.Index](data, dataType.Value.Offset, (uint)tt.ContentLength, connection, requestSequence));
        }
    }


    /// <summary>
    /// Check if a resource is local to a given connection.
    /// </summary>
    /// <param name="resource">Resource to check.</param>
    /// <param name="connection">DistributedConnection to check if the resource is local to it.</param>
    /// <returns>True, if the resource owner is the given connection, otherwise False.</returns>
    public static bool IsLocalResource(IResource resource, DistributedConnection connection)
    {
        if (resource is DistributedResource)
            if ((resource as DistributedResource).DistributedResourceConnection == connection)
                return true;

        return false;
    }

    public delegate (TransmissionTypeIdentifier, byte[]) Composer(object value, DistributedConnection connection);

    public static Dictionary<Type, Composer> Composers = new Dictionary<Type, Composer>()
    {
        // Fixed
        [typeof(bool)] = DataSerializer.BoolComposer,
        [typeof(bool?)] = DataSerializer.BoolComposer,
        [typeof(NotModified)] = DataSerializer.NotModifiedComposer,
        [typeof(byte)] = DataSerializer.UInt8Composer,
        [typeof(byte?)] = DataSerializer.UInt8Composer,
        [typeof(sbyte)] = DataSerializer.Int8Composer,
        [typeof(sbyte?)] = DataSerializer.Int8Composer,
        [typeof(char)] = DataSerializer.Char16Composer,
        [typeof(char?)] = DataSerializer.Char16Composer,
        [typeof(short)] = DataSerializer.Int16Composer,
        [typeof(short?)] = DataSerializer.Int16Composer,
        [typeof(ushort)] = DataSerializer.UInt16Composer,
        [typeof(ushort?)] = DataSerializer.UInt16Composer,
        [typeof(int)] = DataSerializer.Int32Composer,
        [typeof(int?)] = DataSerializer.Int32Composer,
        [typeof(uint)] = DataSerializer.UInt32Composer,
        [typeof(uint?)] = DataSerializer.UInt32Composer,
        [typeof(float)] = DataSerializer.Float32Composer,
        [typeof(float?)] = DataSerializer.Float32Composer,
        [typeof(long)] = DataSerializer.Int64Composer,
        [typeof(long?)] = DataSerializer.Int64Composer,
        [typeof(ulong)] = DataSerializer.UIn64Composer,
        [typeof(ulong?)] = DataSerializer.UIn64Composer,
        [typeof(double)] = DataSerializer.Float64Composer,
        [typeof(double?)] = DataSerializer.Float64Composer,
        [typeof(DateTime)] = DataSerializer.DateTimeComposer,
        [typeof(DateTime?)] = DataSerializer.DateTimeComposer,
        [typeof(decimal)] = DataSerializer.Float128Composer,
        [typeof(decimal?)] = DataSerializer.Float128Composer,
        [typeof(byte[])] = DataSerializer.RawDataComposerFromArray,
        //[typeof(byte?[])] = DataSerializer.RawDataComposerFromArray,
        [typeof(List<byte>)] = DataSerializer.RawDataComposerFromList,
        //[typeof(List<byte?>)] = DataSerializer.RawDataComposerFromList,
        [typeof(string)] = DataSerializer.StringComposer,
 
        // Special
        [typeof(object[])] = DataSerializer.ListComposer,// DataSerializer.ListComposerFromArray,
        [typeof(List<object>)] = DataSerializer.ListComposer,// DataSerializer.ListComposerFromList,
        [typeof(VarList<object>)] = DataSerializer.ListComposer,// DataSerializer.ListComposerFromList,
        [typeof(IResource[])] = DataSerializer.ResourceListComposer,// (value, con) => (TransmissionTypeIdentifier.ResourceList, DC.ToBytes((decimal)value)),
        [typeof(IResource?[])] = DataSerializer.ResourceListComposer,// (value, con) => (TransmissionTypeIdentifier.ResourceList, DC.ToBytes((decimal)value)),
        [typeof(List<IResource>)] = DataSerializer.ResourceListComposer, //(value, con) => (TransmissionTypeIdentifier.ResourceList, DC.ToBytes((decimal)value)),
        [typeof(List<IResource?>)] = DataSerializer.ResourceListComposer, //(value, con) => (TransmissionTypeIdentifier.ResourceList, DC.ToBytes((decimal)value)),
        [typeof(VarList<IResource>)] = DataSerializer.ResourceListComposer, //(value, con) => (TransmissionTypeIdentifier.ResourceList, DC.ToBytes((decimal)value)),
        [typeof(VarList<IResource?>)] = DataSerializer.ResourceListComposer, //(value, con) => (TransmissionTypeIdentifier.ResourceList, DC.ToBytes((decimal)value)),
        [typeof(IRecord[])] = DataSerializer.RecordListComposer,// (value, con) => (TransmissionTypeIdentifier.RecordList, DC.ToBytes((decimal)value)),
        [typeof(IRecord?[])] = DataSerializer.RecordListComposer,// (value, con) => (TransmissionTypeIdentifier.RecordList, DC.ToBytes((decimal)value)),
        [typeof(List<IRecord>)] = DataSerializer.RecordListComposer, //(value, con) => (TransmissionTypeIdentifier.RecordList, DC.ToBytes((decimal)value)),
        [typeof(List<IRecord?>)] = DataSerializer.RecordListComposer, //(value, con) => (TransmissionTypeIdentifier.RecordList, DC.ToBytes((decimal)value)),
        [typeof(VarList<IRecord>)] = DataSerializer.RecordListComposer, //(value, con) => (TransmissionTypeIdentifier.RecordList, DC.ToBytes((decimal)value)),
        [typeof(VarList<IRecord?>)] = DataSerializer.RecordListComposer, //(value, con) => (TransmissionTypeIdentifier.RecordList, DC.ToBytes((decimal)value)),
        [typeof(Map<object, object>)] = DataSerializer.MapComposer,
        [typeof(Map<object?, object>)] = DataSerializer.MapComposer,
        [typeof(Map<object, object?>)] = DataSerializer.MapComposer,
        [typeof(Map<object?, object?>)] = DataSerializer.MapComposer,
        [typeof(PropertyValue[])] = DataSerializer.PropertyValueArrayComposer
        // Typed
        // [typeof(bool[])] = (value, con) => DataSerializer.TypedListComposer((IEnumerable)value, typeof(bool), con),
        // [typeof(bool?[])] = (value, con) => (TransmissionTypeIdentifier.TypedList, new byte[] { (byte)value }),
        // [typeof(List<bool>)] = (value, con) => (TransmissionTypeIdentifier.TypedList, new byte[] { (byte)value }),
        // [typeof(List<bool?>)] = (value, con) => (TransmissionTypeIdentifier.TypedList, new byte[] { (byte)value }),

        // [typeof(byte?[])] = (value, con) => (TransmissionTypeIdentifier.TypedList, new byte[] { (byte)value }),
        // [typeof(List<bool?>)] = (value, con) => (TransmissionTypeIdentifier.TypedList, new byte[] { (byte)value }),

    };



    /// <summary>
    /// Compose a variable
    /// </summary>
    /// <param name="value">Value to compose.</param>
    /// <param name="connection">DistributedConnection is required to check locality.</param>
    /// <param name="prependType">If True, prepend the DataType at the beginning of the output.</param>
    /// <returns>Array of bytes in the network byte order.</returns>
    public static byte[] Compose(object valueOrSource, DistributedConnection connection)//, bool prependType = true)
    {


        if (valueOrSource == null)
            return TransmissionType.Compose(TransmissionTypeIdentifier.Null, null);

        var type = valueOrSource.GetType();

        if (type.IsGenericType)
        {

            var genericType = type.GetGenericTypeDefinition();
            if (genericType == typeof(DistributedPropertyContext<>))
            {
                valueOrSource = ((IDistributedPropertyContext)valueOrSource).GetValue(connection);
            }
            else if (genericType == typeof(Func<>))
            {
                var args = genericType.GetGenericArguments();
                if (args.Length == 2 && args[0] == typeof(DistributedConnection))
                {
                    //Func<DistributedConnection, DistributedConnection> a;
                    //a.Invoke()
                }
            }
        }

        if (valueOrSource is IUserType)
            valueOrSource = (valueOrSource as IUserType).Get();

        //if (valueOrSource is Func<DistributedConnection, object>)
        //    valueOrSource = (valueOrSource as Func<DistributedConnection, object>)(connection);

        if (valueOrSource == null)
            return TransmissionType.Compose(TransmissionTypeIdentifier.Null, null);


        type = valueOrSource.GetType();


        if (Composers.ContainsKey(type))
        {
            var (hdr, data) = Composers[type](valueOrSource, connection);
            return TransmissionType.Compose(hdr, data);
        }
        else
        {
            if (Codec.ImplementsInterface(type, typeof(IResource)))
            {
                var (hdr, data) = DataSerializer.ResourceComposer(valueOrSource, connection);
                return TransmissionType.Compose(hdr, data);
            }
            else if (Codec.ImplementsInterface(type, typeof(IRecord)))
            {
                var (hdr, data) = DataSerializer.RecordComposer(valueOrSource, connection);
                return TransmissionType.Compose(hdr, data);
            }
            else if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(List<>) || genericType == typeof(VarList<>))
                {
                    var args = type.GetGenericArguments();
                    //if (Composers.ContainsKey(args[0]))
                    //{
                    var (hdr, data) = DataSerializer.TypedListComposer((IEnumerable)valueOrSource, args[0], connection);
                    return TransmissionType.Compose(hdr, data);
                    //}
                }
                else if (genericType == typeof(Map<,>))
                {
                    var args = type.GetGenericArguments();

                    var (hdr, data) = DataSerializer.TypedMapComposer(valueOrSource, args[0], args[1], connection);
                    return TransmissionType.Compose(hdr, data);

                }
                else if (genericType == typeof(ValueTuple<,>)
                      || genericType == typeof(ValueTuple<,,>)
                      || genericType == typeof(ValueTuple<,,,>)
                      || genericType == typeof(ValueTuple<,,,,>)
                      || genericType == typeof(ValueTuple<,,,,,>)
                      || genericType == typeof(ValueTuple<,,,,,,>)
                  )
                {
                    var (hdr, data) = DataSerializer.TupleComposer(valueOrSource, connection);
                    return TransmissionType.Compose(hdr, data);
                }
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();

                //if (Composers.ContainsKey(elementType))
                //{
                var (hdr, data) = DataSerializer.TypedListComposer((IEnumerable)valueOrSource, elementType, connection);
                return TransmissionType.Compose(hdr, data);

                //}
            } 
            else if (type.IsEnum)
            {
                var (hdr, data) = DataSerializer.EnumComposer(valueOrSource, connection);
                return TransmissionType.Compose(hdr, data);
            }

        }

        return TransmissionType.Compose(TransmissionTypeIdentifier.Null, null);

    }



    public static bool IsAnonymous(Type type)
    {
        // Detect anonymous types
        var info = type.GetTypeInfo();
        var hasCompilerGeneratedAttribute = info.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Count() > 0;
        var nameContainsAnonymousType = type.FullName.Contains("AnonymousType");
        return hasCompilerGeneratedAttribute && nameContainsAnonymousType;
    }


    public static Type GetGenericType(Type type, Type ifaceType, int argument = 0)
    {
        if (ifaceType.IsAssignableFrom(type))
        {
            var col = type.GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == ifaceType)
                                       .FirstOrDefault()?
                                       .GetGenericArguments()
                                       .FirstOrDefault() ?? null;

            return col;
        }
        else
            return null;
    }

    /// <summary>
    /// Check if a type implements an interface
    /// </summary>
    /// <param name="type">Sub-class type.</param>
    /// <param name="iface">Super-interface type.</param>
    /// <returns>True, if <paramref name="type"/> implements <paramref name="iface"/>.</returns>
    public static bool ImplementsInterface(Type type, Type iface)
    {
        while (type != null)
        {
            if (type == iface)
                return true;

#if NETSTANDARD
            if (type.GetTypeInfo().GetInterfaces().Contains(iface))
                return true;

            type = type.GetTypeInfo().BaseType;
#else
                if (type.GetInterfaces().Contains(iface))
                    return true;
                type = type.BaseType;
#endif
        }

        return false;
    }

    public static bool InheritsClass(Type type, Type parent)
        => type.IsSubclassOf(parent);

    /// <summary>
    /// Check if a type inherits another type.
    /// </summary>
    /// <param name="childType">Child type.</param>
    /// <param name="parentType">Parent type.</param>
    /// <returns>True, if <paramref name="childType"/> inherits <paramref name="parentType"/>.</returns>
    private static bool HasParentType(Type childType, Type parentType)
    {
        while (childType != null)
        {
            if (childType == parentType)
                return true;
#if NETSTANDARD
            childType = childType.GetTypeInfo().BaseType;
#else
                childType = childType.BaseType;
#endif
        }

        return false;
    }
}
