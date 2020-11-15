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

namespace Esiur.Data
{
    public static class Codec
    {
        /// <summary>
        /// Check if a DataType is an array
        /// </summary>
        /// <param name="type">DataType to check</param>
        /// <returns>True if DataType is an array, otherwise false</returns>
        public static bool IsArray(this DataType type)
        {
            return (((byte)type & 0x80) == 0x80) && (type != DataType.NotModified);
        }

        /// <summary>
        /// Get the element DataType
        /// </summary>
        /// <example>
        /// Passing UInt8Array will return UInt8 
        /// </example>
        /// <param name="type">DataType to get its element DataType</param>
        public static DataType GetElementType(this DataType type)
        {
            return (DataType)((byte)type & 0x7F);
        }

        /// <summary>
        /// Get DataType array of a given Structure
        /// </summary>
        /// <param name="structure">Structure to get its DataTypes</param>
        /// <param name="connection">Distributed connection is required in case a type is at the other end</param>
        private static DataType[] GetStructureDateTypes(Structure structure, DistributedConnection connection)
        {
            var keys = structure.GetKeys();
            var types = new DataType[keys.Length];

            for (var i = 0; i < keys.Length; i++)
            {
                types[i] = Codec.GetDataType(structure[keys[i]], connection).type;
            }
            return types;
        }

        /// <summary>
        /// Compare two structures
        /// </summary>
        /// <param name="initial">Initial structure to compare with</param>
        /// <param name="next">Next structure to compare with the initial</param>
        /// <param name="connection">DistributedConnection is required in case a structure holds items at the other end</param>
        public static StructureComparisonResult Compare(Structure initial, Structure next, DistributedConnection connection)
        {
            if (next == null)
                return StructureComparisonResult.Null;

            if (initial == null)
                return StructureComparisonResult.Structure;

            if (next == initial)
                return StructureComparisonResult.Same;

            if (initial.Length != next.Length)
                return StructureComparisonResult.Structure;

            var previousKeys = initial.GetKeys();
            var nextKeys = next.GetKeys();

            for (var i = 0; i < previousKeys.Length; i++)
                if (previousKeys[i] != nextKeys[i])
                    return StructureComparisonResult.Structure;

            var previousTypes = GetStructureDateTypes(initial, connection);
            var nextTypes = GetStructureDateTypes(next, connection);

            for (var i = 0; i < previousTypes.Length; i++)
                if (previousTypes[i] != nextTypes[i])
                    return StructureComparisonResult.StructureSameKeys;

            return StructureComparisonResult.StructureSameTypes;
        }

        /// <summary>
        /// Compose an array of structures into an array of bytes
        /// </summary>
        /// <param name="structures">Array of Structure to compose</param>
        /// <param name="connection">DistributedConnection is required in case a structure in the array holds items at the other end</param>
        /// <param name="prependLength">If true, prepend the length as UInt32 at the beginning of the returned bytes array</param>
        /// <returns>Array of bytes in the network byte order</returns>
        public static byte[] ComposeStructureArray(Structure[] structures, DistributedConnection connection, bool prependLength = false)
        {
            if (structures == null || structures?.Length == 0)
                return prependLength ? new byte[] { 0, 0, 0, 0 } : new byte[0];

            var rt = new BinaryList();
            var comparsion = StructureComparisonResult.Structure;

            rt.AddUInt8((byte)comparsion)
              .AddUInt8Array(ComposeStructure(structures[0], connection, true, true, true));

            for (var i = 1; i < structures.Length; i++)
            {
                comparsion = Compare(structures[i - 1], structures[i], connection);
                rt.AddUInt8((byte)comparsion);

                if (comparsion == StructureComparisonResult.Structure)
                    rt.AddUInt8Array(ComposeStructure(structures[i], connection, true, true, true));
                else if (comparsion == StructureComparisonResult.StructureSameKeys)
                    rt.AddUInt8Array(ComposeStructure(structures[i], connection, false, true, true));
                else if (comparsion == StructureComparisonResult.StructureSameTypes)
                    rt.AddUInt8Array(ComposeStructure(structures[i], connection, false, false, true));
            }

            if (prependLength)
                rt.InsertInt32(0, rt.Length);

            return rt.ToArray();
        }

        /// <summary>
        /// Parse an array of structures
        /// </summary>
        /// <param name="data">Bytes array</param>
        /// <param name="offset">Zero-indexed offset</param>
        /// <param name="length">Number of bytes to parse</param>
        /// <param name="connection">DistributedConnection is required in case a structure in the array holds items at the other end</param>
        /// <returns>Array of structures</returns>
        public static AsyncBag<Structure> ParseStructureArray(byte[] data, uint offset, uint length, DistributedConnection connection)
        {
            var reply = new AsyncBag<Structure>();
            if (length == 0)
            {
                reply.Seal();
                return reply;
            }

            var end = offset + length;

            var result = (StructureComparisonResult)data[offset++];

            AsyncReply<Structure> previous = null;
            // string[] previousKeys = null;
            // DataType[] previousTypes = null;

            Structure.StructureMetadata metadata = new Structure.StructureMetadata();


            if (result == StructureComparisonResult.Null)
                previous = new AsyncReply<Structure>(null);
            else if (result == StructureComparisonResult.Structure)
            {
                uint cs = data.GetUInt32(offset);
                offset += 4;
                previous = ParseStructure(data, offset, cs, connection, out metadata);
                offset += cs;
            }

            reply.Add(previous);


            while (offset < end)
            {
                result = (StructureComparisonResult)data[offset++];

                if (result == StructureComparisonResult.Null)
                    previous = new AsyncReply<Structure>(null);
                else if (result == StructureComparisonResult.Structure)
                {
                    uint cs = data.GetUInt32(offset);
                    offset += 4;
                    previous = ParseStructure(data, offset, cs, connection, out metadata);// out previousKeys, out previousTypes);
                    offset += cs;
                }
                else if (result == StructureComparisonResult.StructureSameKeys)
                {
                    uint cs = data.GetUInt32(offset);
                    offset += 4;
                    previous = ParseStructure(data, offset, cs, connection, out metadata, metadata.Keys);
                    offset += cs;
                }
                else if (result == StructureComparisonResult.StructureSameTypes)
                {
                    uint cs = data.GetUInt32(offset);
                    offset += 4;
                    previous = ParseStructure(data, offset, cs, connection, out metadata, metadata.Keys, metadata.Types);
                    offset += cs;
                }

                reply.Add(previous);
            }

            reply.Seal();
            return reply;
        }

        /// <summary>
        /// Compose a structure into an array of bytes
        /// </summary>
        /// <param name="value">Structure to compose</param>
        /// <param name="connection">DistributedConnection is required in case an item in the structure is at the other end</param>
        /// <param name="includeKeys">Whether to include the structure keys</param>
        /// <param name="includeTypes">Whether to include each item DataType</param>
        /// <param name="prependLength">If true, prepend the length as UInt32 at the beginning of the returned bytes array</param>
        /// <returns>Array of bytes in the network byte order</returns>
        public static byte[] ComposeStructure(Structure value, DistributedConnection connection, bool includeKeys = true, bool includeTypes = true, bool prependLength = false)
        {
            var rt = new BinaryList();

            if (includeKeys)
            {
                foreach (var i in value)
                {
                    var key = DC.ToBytes(i.Key);
                    rt.AddUInt8((byte)key.Length)
                      .AddUInt8Array(key)
                      .AddUInt8Array(Compose(i.Value, connection));
                }
            }
            else
            {
                foreach (var i in value)
                    rt.AddUInt8Array(Compose(i.Value, connection, includeTypes));
            }

            if (prependLength)
                rt.InsertInt32(0, rt.Length);

            return rt.ToArray();
        }

        /// <summary>
        /// Parse a structure
        /// </summary>
        /// <param name="data">Bytes array</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="length">Number of bytes to parse.</param>
        /// <param name="connection">DistributedConnection is required in case a structure in the array holds items at the other end.</param>
        /// <returns>Value</returns>
        public static AsyncReply<Structure> ParseStructure(byte[] data, uint offset, uint contentLength, DistributedConnection connection)
        {
            Structure.StructureMetadata metadata;
            return ParseStructure(data, offset, contentLength, connection, out metadata);
        }

        /// <summary>
        /// Parse a structure
        /// </summary>
        /// <param name="data">Bytes array</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="length">Number of bytes to parse.</param>
        /// <param name="connection">DistributedConnection is required in case a structure in the array holds items at the other end.</param>
        /// <param name="parsedKeys">Array to store keys in.</param>
        /// <param name="parsedTypes">Array to store DataTypes in.</param>
        /// <param name="keys">Array of keys, in case the data doesn't include keys</param>
        /// <param name="types">Array of DataTypes, in case the data doesn't include DataTypes</param>
        /// <returns>Structure</returns>
        public static AsyncReply<Structure> ParseStructure(byte[] data, uint offset, uint length, DistributedConnection connection, out Structure.StructureMetadata metadata, string[] keys = null, DataType[] types = null)// out string[] parsedKeys, out DataType[] parsedTypes, string[] keys = null, DataType[] types = null)
        {
            var reply = new AsyncReply<Structure>();
            var bag = new AsyncBag<object>();
            var keylist = new List<string>();
            var typelist = new List<DataType>();

            if (keys == null)
            {
                while (length > 0)
                {
                    var len = data[offset++];
                    keylist.Add(data.GetString(offset, len));
                    offset += len;

                    typelist.Add((DataType)data[offset]);

                    uint rt;
                    bag.Add(Codec.Parse(data, offset, out rt, connection));
                    length -= rt + len + 1;
                    offset += rt;
                }
            }
            else if (types == null)
            {
                keylist.AddRange(keys);

                while (length > 0)
                {
                    typelist.Add((DataType)data[offset]);

                    uint rt;
                    bag.Add(Codec.Parse(data, offset, out rt, connection));
                    length -= rt;
                    offset += rt;
                }
            }
            else
            {
                keylist.AddRange(keys);
                typelist.AddRange(types);

                var i = 0;
                while (length > 0)
                {
                    uint rt;
                    bag.Add(Codec.Parse(data, offset, out rt, connection, types[i]));
                    length -= rt;
                    offset += rt;
                    i++;
                }
            }

            bag.Seal();

            bag.Then((res) =>
            {
                // compose the list
                var s = new Structure();
                for (var i = 0; i < keylist.Count; i++)
                    s[keylist[i]] = res[i];
                reply.Trigger(s);
            });

            metadata = new Structure.StructureMetadata() { Keys = keylist.ToArray(), Types = typelist.ToArray() };

            return reply;
        }

        /// <summary>
        /// Parse a value
        /// </summary>
        /// <param name="data">Bytes array</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="connection">DistributedConnection is required in case a structure in the array holds items at the other end.</param>
        /// <param name="dataType">DataType, in case the data is not prepended with DataType</param>
        /// <returns>Structure</returns>
        public static AsyncReply Parse(byte[] data, uint offset, DistributedConnection connection, DataType dataType = DataType.Unspecified)
        {
            uint size;
            return Parse(data, offset, out size, connection);
        }

        /// <summary>
        /// Parse a value
        /// </summary>
        /// <param name="data">Bytes array</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="size">Output the number of bytes parsed</param>
        /// <param name="connection">DistributedConnection is required in case a structure in the array holds items at the other end.</param>
        /// <param name="dataType">DataType, in case the data is not prepended with DataType</param>
        /// <returns>Value</returns>
        public static AsyncReply Parse(byte[] data, uint offset, out uint size, DistributedConnection connection, DataType dataType = DataType.Unspecified)
        {

            bool isArray;
            DataType t;

            if (dataType == DataType.Unspecified)
            {
                size = 1;
                dataType = (DataType)data[offset++];
            }
            else
                size = 0;

            t = (DataType)((byte)dataType & 0x7F);

            isArray = ((byte)dataType & 0x80) == 0x80;

            var payloadSize = dataType.Size();// SizeOf();


            uint contentLength = 0;

            // check if we have the enough data
            if (payloadSize == -1)
            {
                contentLength = data.GetUInt32(offset);
                offset += 4;
                size += 4 + contentLength;
            }
            else
                size += (uint)payloadSize;

            if (isArray)
            {
                switch (t)
                {
                    // VarArray ?
                    case DataType.Void:
                        return ParseVarArray(data, offset, contentLength, connection);

                    case DataType.Bool:
                        return new AsyncReply<bool[]>(data.GetBooleanArray(offset, contentLength));

                    case DataType.UInt8:
                        return new AsyncReply<byte[]>(data.GetUInt8Array(offset, contentLength));

                    case DataType.Int8:
                        return new AsyncReply<sbyte[]>(data.GetInt8Array(offset, contentLength));

                    case DataType.Char:
                        return new AsyncReply<char[]>(data.GetCharArray(offset, contentLength));

                    case DataType.Int16:
                        return new AsyncReply<short[]>(data.GetInt16Array(offset, contentLength));

                    case DataType.UInt16:
                        return new AsyncReply<ushort[]>(data.GetUInt16Array(offset, contentLength));

                    case DataType.Int32:
                        return new AsyncReply<int[]>(data.GetInt32Array(offset, contentLength));

                    case DataType.UInt32:
                        return new AsyncReply<uint[]>(data.GetUInt32Array(offset, contentLength));

                    case DataType.Int64:
                        return new AsyncReply<long[]>(data.GetInt64Array(offset, contentLength));

                    case DataType.UInt64:
                        return new AsyncReply<ulong[]>(data.GetUInt64Array(offset, contentLength));

                    case DataType.Float32:
                        return new AsyncReply<float[]>(data.GetFloat32Array(offset, contentLength));

                    case DataType.Float64:
                        return new AsyncReply<double[]>(data.GetFloat64Array(offset, contentLength));

                    case DataType.String:
                        return new AsyncReply<string[]>(data.GetStringArray(offset, contentLength));

                    case DataType.Resource:
                    case DataType.DistributedResource:
                        return ParseResourceArray(data, offset, contentLength, connection);

                    case DataType.DateTime:
                        return new AsyncReply<DateTime[]>(data.GetDateTimeArray(offset, contentLength));

                    case DataType.Structure:
                        return ParseStructureArray(data, offset, contentLength, connection);
                }
            }
            else
            {
                switch (t)
                {
                    case DataType.NotModified:
                        return new AsyncReply<object>(new NotModified());

                    case DataType.Void:
                        return new AsyncReply<object>(null);

                    case DataType.Bool:
                        return new AsyncReply<object>(data.GetBoolean(offset));

                    case DataType.UInt8:
                        return new AsyncReply<object>(data[offset]);

                    case DataType.Int8:
                        return new AsyncReply<object>((sbyte)data[offset]);

                    case DataType.Char:
                        return new AsyncReply<object>(data.GetChar(offset));

                    case DataType.Int16:
                        return new AsyncReply<object>(data.GetInt16(offset));

                    case DataType.UInt16:
                        return new AsyncReply<object>(data.GetUInt16(offset));

                    case DataType.Int32:
                        return new AsyncReply<object>(data.GetInt32(offset));

                    case DataType.UInt32:
                        return new AsyncReply<object>(data.GetUInt32(offset));

                    case DataType.Int64:
                        return new AsyncReply<object>(data.GetInt64(offset));

                    case DataType.UInt64:
                        return new AsyncReply<object>(data.GetUInt64(offset));

                    case DataType.Float32:
                        return new AsyncReply<object>(data.GetFloat32(offset));

                    case DataType.Float64:
                        return new AsyncReply<object>(data.GetFloat64(offset));

                    case DataType.String:
                        return new AsyncReply<string>(data.GetString(offset, contentLength));

                    case DataType.Resource:
                        return ParseResource(data, offset);

                    case DataType.DistributedResource:
                        return ParseDistributedResource(data, offset, connection);

                    case DataType.DateTime:
                        return new AsyncReply<object>(data.GetDateTime(offset));

                    case DataType.Structure:
                        return ParseStructure(data, offset, contentLength, connection);
                }
            }


            return null;
        }

        /// <summary>
        /// Parse a resource
        /// </summary>
        /// <param name="data">Bytes array</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <returns>Resource</returns>
        public static AsyncReply<IResource> ParseResource(byte[] data, uint offset)
        {
            return Warehouse.GetById(data.GetUInt32(offset));
        }

        /// <summary>
        /// Parse a DistributedResource
        /// </summary>
        /// <param name="data">Bytes array</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="connection">DistributedConnection is required.</param>
        /// <returns>DistributedResource</returns>
        public static AsyncReply<DistributedResource> ParseDistributedResource(byte[] data, uint offset, DistributedConnection connection)
        {
            //var g = data.GetGuid(offset);
            //offset += 16;

            // find the object
            var iid = data.GetUInt32(offset);

            return connection.Fetch(iid);// Warehouse.Get(iid);
        }

        public enum ResourceComparisonResult
        {
            Null,
            Distributed,
            Local,
            Same
        }

        public enum StructureComparisonResult : byte
        {
            Null,
            Structure,
            StructureSameKeys,
            StructureSameTypes,
            Same
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
                if ((resource as DistributedResource).Connection == connection)
                    return true;

            return false;
        }

        /// <summary>
        /// Compare two resources
        /// </summary>
        /// <param name="initial">Initial resource to make comparison with.</param>
        /// <param name="next">Next resource to compare with the initial.</param>
        /// <param name="connection">DistributedConnection is required to check locality.</param>
        /// <returns>Null, same, local, distributed or same class distributed.</returns>

        public static ResourceComparisonResult Compare(IResource initial, IResource next, DistributedConnection connection)
        {
            if (next == null)
                return ResourceComparisonResult.Null;
            else if (next == initial)
                return ResourceComparisonResult.Same;
            else if (IsLocalResource(next, connection))
                return ResourceComparisonResult.Local;
            else
                return ResourceComparisonResult.Distributed;
        }

        /// <summary>
        /// Compose a resource
        /// </summary>
        /// <param name="resource">Resource to compose.</param>
        /// <param name="connection">DistributedConnection is required to check locality.</param>
        /// <returns>Array of bytes in the network byte order.</returns>
        public static byte[] ComposeResource(IResource resource, DistributedConnection connection)
        {
            if (IsLocalResource(resource, connection))
                return DC.ToBytes((resource as DistributedResource).Id);
            else
            {
                return new BinaryList().AddGuid(resource.Instance.Template.ClassId).AddUInt32(resource.Instance.Id).ToArray();
                //return BinaryList.ToBytes(resource.Instance.Template.ClassId, resource.Instance.Id);
            }
        }

        /// <summary>
        /// Compose an array of resources
        /// </summary>
        /// <param name="resources">Array of resources.</param>
        /// <param name="connection">DistributedConnection is required to check locality.</param>
        /// <param name="prependLength">If True, prepend the length of the output at the beginning.</param>
        /// <returns>Array of bytes in the network byte order.</returns>

        public static byte[] ComposeResourceArray(IResource[] resources, DistributedConnection connection, bool prependLength = false)
        {
            if (resources == null || resources?.Length == 0)
                return prependLength ? new byte[] { 0, 0, 0, 0 } : new byte[0];

            var rt = new BinaryList();
            var comparsion = Compare(null, resources[0], connection);

            rt.AddUInt8((byte)comparsion);

            if (comparsion == ResourceComparisonResult.Local)
                rt.AddUInt32((resources[0] as DistributedResource).Id);
            else if (comparsion == ResourceComparisonResult.Distributed)
                rt.AddUInt32(resources[0].Instance.Id);

            for (var i = 1; i < resources.Length; i++)
            {
                comparsion = Compare(resources[i - 1], resources[i], connection);
                rt.AddUInt8((byte)comparsion);
                if (comparsion == ResourceComparisonResult.Local)
                    rt.AddUInt32((resources[i] as DistributedResource).Id);
                else if (comparsion == ResourceComparisonResult.Distributed)
                    rt.AddUInt32(resources[i].Instance.Id);
            }

            if (prependLength)
                rt.InsertInt32(0, rt.Length);

            return rt.ToArray();
        }

        /// <summary>
        /// Parse an array of bytes into array of resources
        /// </summary>
        /// <param name="data">Array of bytes.</param>
        /// <param name="length">Number of bytes to parse.</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="connection">DistributedConnection is required to fetch resources.</param>
        /// <returns>Array of resources.</returns>
        public static AsyncBag<IResource> ParseResourceArray(byte[] data, uint offset, uint length, DistributedConnection connection)
        {
            var reply = new AsyncBag<IResource>();
            if (length == 0)
            {
                reply.Seal();
                return reply;
            }

            var end = offset + length;

            // 
            var result = (ResourceComparisonResult)data[offset++];

            AsyncReply previous = null;

            if (result == ResourceComparisonResult.Null)
                previous = new AsyncReply<IResource>(null);
            else if (result == ResourceComparisonResult.Local)
            {
                previous = Warehouse.GetById(data.GetUInt32(offset));
                offset += 4;
            }
            else if (result == ResourceComparisonResult.Distributed)
            {
                previous = connection.Fetch(data.GetUInt32(offset));
                offset += 4;
            }

            reply.Add(previous);


            while (offset < end)
            {
                result = (ResourceComparisonResult)data[offset++];

                AsyncReply current = null;

                if (result == ResourceComparisonResult.Null)
                {
                    current = new AsyncReply<IResource>(null);
                }
                else if (result == ResourceComparisonResult.Same)
                {
                    current = previous;
                }
                else if (result == ResourceComparisonResult.Local)
                {
                    current = Warehouse.GetById(data.GetUInt32(offset));
                    offset += 4;
                }
                else if (result == ResourceComparisonResult.Distributed)
                {
                    current = connection.Fetch(data.GetUInt32(offset));
                    offset += 4;
                }

                reply.Add(current);

                previous = current;
            }

            reply.Seal();
            return reply;
        }

        /// <summary>
        /// Compose an array of variables
        /// </summary>
        /// <param name="array">Variables.</param>
        /// <param name="connection">DistributedConnection is required to check locality.</param>
        /// <param name="prependLength">If True, prepend the length as UInt32 at the beginning of the output.</param>
        /// <returns>Array of bytes in the network byte order.</returns>
        public static byte[] ComposeVarArray(Array array, DistributedConnection connection, bool prependLength = false)
        {
            var rt = new List<byte>();

            for (var i = 0; i < array.Length; i++)
                rt.AddRange(Compose(array.GetValue(i), connection));
            if (prependLength)
                rt.InsertRange(0, DC.ToBytes(rt.Count));
            return rt.ToArray();
        }
        /// <summary>
        /// Parse an array of variables.
        /// </summary>
        /// <param name="data">Array of bytes.</param>
        /// <param name="connection">DistributedConnection is required to fetch resources.</param>
        /// <returns>Array of variables.</returns>
        public static AsyncBag<object> ParseVarArray(byte[] data, DistributedConnection connection)
        {
            return ParseVarArray(data, 0, (uint)data.Length, connection);
        }

        /// <summary>
        /// Parse an array of bytes into an array of varialbes.
        /// </summary>
        /// <param name="data">Array of bytes.</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="length">Number of bytes to parse.</param>
        /// <param name="connection">DistributedConnection is required to fetch resources.</param>
        /// <returns>Array of variables.</returns>
        public static AsyncBag<object> ParseVarArray(byte[] data, uint offset, uint length, DistributedConnection connection)
        {
            var rt = new AsyncBag<object>();

            while (length > 0)
            {
                uint cs;

                rt.Add(Parse(data, offset, out cs, connection));

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

        /// <summary>
        /// Compose an array of property values.
        /// </summary>
        /// <param name="array">PropertyValue array.</param>
        /// <param name="connection">DistributedConnection is required to check locality.</param>
        /// <param name="prependLength">If True, prepend the length as UInt32 at the beginning of the output.</param>
        /// <returns>Array of bytes in the network byte order.</returns>
        /// //, bool includeAge = true
        public static byte[] ComposePropertyValueArray(PropertyValue[] array, DistributedConnection connection, bool prependLength = false)
        {
            var rt = new List<byte>();
            for (var i = 0; i < array.Length; i++)
                rt.AddRange(ComposePropertyValue(array[i], connection));
            if (prependLength)
                rt.InsertRange(0, DC.ToBytes(rt.Count));
            return rt.ToArray();
        }

        /// <summary>
        /// Compose a property value.
        /// </summary>
        /// <param name="propertyValue">Property value</param>
        /// <param name="connection">DistributedConnection is required to check locality.</param>
        /// <returns>Array of bytes in the network byte order.</returns>
        public static byte[] ComposePropertyValue(PropertyValue propertyValue, DistributedConnection connection)//, bool includeAge = true)
        {

            return new BinaryList()
                .AddUInt64(propertyValue.Age)
                .AddDateTime(propertyValue.Date)
                .AddUInt8Array(Compose(propertyValue.Value, connection))
                .ToArray();

            // age, date, value
            //if (includeAge)
            // return BinaryList.ToBytes(propertyValue.Age, propertyValue.Date, Compose(propertyValue.Value, connection));
            //else
            //  return BinaryList.ToBytes(propertyValue.Date, Compose(propertyValue.Value, connection));

        }


        /// <summary>
        /// Parse property value.
        /// </summary>
        /// <param name="data">Array of bytes.</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="connection">DistributedConnection is required to fetch resources.</param>
        /// <param name="cs">Output content size.</param>
        /// <returns>PropertyValue.</returns>
        public static AsyncReply<PropertyValue> ParsePropertyValue(byte[] data, uint offset, out uint cs, DistributedConnection connection)//, bool ageIncluded = true)
        {
            var reply = new AsyncReply<PropertyValue>();

            var age = data.GetUInt64(offset);
            offset += 8;

            DateTime date = data.GetDateTime(offset);
            offset += 8;

            uint valueSize;

            Parse(data, offset, out valueSize, connection).Then(value =>
            {
                reply.Trigger(new PropertyValue(value, age, date));
            });

            cs = 16 + valueSize;
            return reply;
        }

        /// <summary>
        /// Parse an array of PropertyValue.
        /// </summary>
        /// <param name="data">Array of bytes.</param>
        /// <param name="connection">DistributedConnection is required to fetch resources.</param>
        /// <returns>Array of variables.</returns>
        public static AsyncBag<PropertyValue> ParsePropertyValueArray(byte[] data, DistributedConnection connection)
        {
            return ParsePropertyValueArray(data, 0, (uint)data.Length, connection);
        }

        /// <summary>
        /// Parse resource history
        /// </summary>
        /// <param name="data">Array of bytes.</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="length">Number of bytes to parse.</param>
        /// <param name="resource">Resource</param>
        /// <param name="fromAge">Starting age.</param>
        /// <param name="toAge">Ending age.</param>
        /// <param name="connection">DistributedConnection is required to fetch resources.</param>
        /// <returns></returns>
        public static AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> ParseHistory(byte[] data, uint offset, uint length, IResource resource, DistributedConnection connection)
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
                var cs = DC.GetUInt32(data, offset);
                offset += 4;
                bagOfBags.Add(ParsePropertyValueArray(data, offset, cs, connection));
                offset += cs;
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

        /// <summary>
        /// Compose resource history
        /// </summary>
        /// <param name="history">History</param>
        /// <param name="connection">DistributedConnection is required to fetch resources.</param>
        /// <returns></returns>
        public static byte[] ComposeHistory(KeyList<PropertyTemplate, PropertyValue[]> history,
                                            DistributedConnection connection, bool prependLength = false)
        {
            var rt = new BinaryList();

            for (var i = 0; i < history.Count; i++)
                rt.AddUInt8(history.Keys.ElementAt(i).Index)
                  .AddUInt8Array(ComposePropertyValueArray(history.Values.ElementAt(i), connection, true));

            //    rt.Append((byte)history.Keys.ElementAt(i).Index, 
            //            ComposePropertyValueArray(history.Values.ElementAt(i), connection, true));

            if (prependLength)
                rt.InsertInt32(0, rt.Length);

            return rt.ToArray();
        }

        /// <summary>
        /// Parse an array of PropertyValue.
        /// </summary>
        /// <param name="data">Array of bytes.</param>
        /// <param name="offset">Zero-indexed offset.</param>
        /// <param name="length">Number of bytes to parse.</param>
        /// <param name="connection">DistributedConnection is required to fetch resources.</param>
        /// <param name="ageIncluded">Whether property age is represented in the data.</param>
        /// <returns></returns>
        public static AsyncBag<PropertyValue> ParsePropertyValueArray(byte[] data, uint offset, uint length, DistributedConnection connection)//, bool ageIncluded = true)
        {
            var rt = new AsyncBag<PropertyValue>();

            while (length > 0)
            {
                uint cs;

                rt.Add(ParsePropertyValue(data, offset, out cs, connection));//, ageIncluded));

                if (cs > 0)
                {
                    offset += (uint)cs;
                    length -= (uint)cs;
                }
                else
                    throw new Exception("Error while parsing ValueInfo structured data");
            }

            rt.Seal();
            return rt;
        }

        /// <summary>
        /// Compose a variable
        /// </summary>
        /// <param name="value">Value to compose.</param>
        /// <param name="connection">DistributedConnection is required to check locality.</param>
        /// <param name="prependType">If True, prepend the DataType at the beginning of the output.</param>
        /// <returns>Array of bytes in the network byte order.</returns>
        public static byte[] Compose(object valueOrSource, DistributedConnection connection, bool prependType = true)
        {

            var (type, value) = GetDataType(valueOrSource, connection);
            var rt = new BinaryList();

            switch (type)
            {
                case DataType.Void:
                    // nothing to do;
                    break;

                case DataType.String:
                    var st = DC.ToBytes((string)value);
                    rt.AddInt32(st.Length).AddUInt8Array(st);
                    break;

                case DataType.Resource:
                    rt.AddUInt32((value as DistributedResource).Id);
                    break;

                case DataType.DistributedResource:
                    //rt.Append((value as IResource).Instance.Template.ClassId, (value as IResource).Instance.Id);
                    rt.AddUInt32((value as IResource).Instance.Id);

                    break;

                case DataType.Structure:
                    rt.AddUInt8Array(ComposeStructure((Structure)value, connection, true, true, true));
                    break;

                case DataType.VarArray:
                    rt.AddUInt8Array(ComposeVarArray((Array)value, connection, true));
                    break;

                case DataType.ResourceArray:
                    if (value is IResource[])
                        rt.AddUInt8Array(ComposeResourceArray((IResource[])value, connection, true));
                    else
                        rt.AddUInt8Array(ComposeResourceArray((IResource[])DC.CastConvert(value, typeof(IResource[])), connection, true));
                    break;

                case DataType.StructureArray:
                    rt.AddUInt8Array(ComposeStructureArray((Structure[])value, connection, true));
                    break;

                default:
                    rt.Add(type, value);
                    if (type.IsArray())
                        rt.InsertInt32(0, rt.Length);
                    break;
            }

            if (prependType)
                rt.InsertUInt8(0, (byte)type);

            return rt.ToArray();
        }


        public static bool IsAnonymous(Type type)
        {
            // Detect anonymous types
            var info = type.GetTypeInfo();
            var hasCompilerGeneratedAttribute = info.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Count() > 0;
            var nameContainsAnonymousType = type.FullName.Contains("AnonymousType");
            return hasCompilerGeneratedAttribute && nameContainsAnonymousType;
        }

        /// <summary>
        /// Check if a type implements an interface
        /// </summary>
        /// <param name="type">Sub-class type.</param>
        /// <param name="iface">Super-interface type.</param>
        /// <returns>True, if <paramref name="type"/> implements <paramref name="iface"/>.</returns>
        public static bool ImplementsInterface(Type type, Type iface)
        {
            /*
            if (iface.GetTypeInfo().IsGenericType)
            {
                //var x = (type.GetTypeInfo().GetInterfaces().Any(x => x.GetTypeInfo().IsGenericType Contains(iface))

                iface = iface.GetTypeInfo().GetGenericTypeDefinition();

                //if (type.GetTypeInfo().IsGenericType)
                //    type = 
                while (type != null)
                {
                    if (type == iface)
                        return true;

#if NETSTANDARD
                    if (type.GetTypeInfo().GetInterfaces().Contains(iface))// (x=>x.GetTypeInfo().IsGenericType (iface))
                        return true;

                    type = type.GetTypeInfo().BaseType;
#else
                if (type.GetInterfaces().Contains(iface))
                    return true;
                type = type.BaseType;
#endif
                }


            }
            else
                */
            //{

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

            //}
            return false;
        }

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

        /// <summary>
        /// Get the DataType of a given value.
        /// This function is needed to compose a value.
        /// </summary>
        /// <param name="value">Value to find its DataType.</param>
        /// <param name="connection">DistributedConnection is required to check locality of resources.</param>
        /// <returns>DataType.</returns>
        public static (DataType type, object value) GetDataType(object value, DistributedConnection connection)
        {
            
            if (value == null)
                return (DataType.Void, null);

            if (value is IUserType)
                value = (value as IUserType).Get();


            //  value = (List<>)value.ToArray();

            if (value is Func<DistributedConnection, object>)
                //if (connection != null)
                value = (value as Func<DistributedConnection, object>)(connection);
            //else
            //    return (DataType.Void, null);
            else if (value is DistributedPropertyContext)
            {
                try
                {
                    //if (connection != null)
                    value = (value as DistributedPropertyContext).Method(connection);
                    //else
                }
                catch(Exception ex)
                {
                    Global.Log(ex);
                    return (DataType.Void, null);
                }
                //    return (DataType.Void, null);
            }
            
            if (value == null)
                return (DataType.Void, null);


            var t = value.GetType();

            // Convert ICollection<T> to Array<T>
            if (!t.IsArray && typeof(ICollection).IsAssignableFrom(t))
            {
                var col = t.GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));
                if (col.Count() == 0)
                    return (DataType.Void, null);

                var elementType = col.First().GetGenericArguments()[0];
                
                value = new ArrayList((ICollection)value).ToArray(elementType);
                t = value.GetType();
            }

            var isArray = t.IsArray;

            if (isArray)
            {
                t = t.GetElementType();
                if (t.IsEnum)
                {
                    var src = value as Array;
                    t = t.GetEnumUnderlyingType();
                    var dst = Array.CreateInstance(t, src.Length);
                    src.CopyTo(dst, 0);
                    value = dst;
                }
            }
            else if (t.IsEnum)
            {
                t = t.GetEnumUnderlyingType();
                value = Convert.ChangeType(value, t);
            }

            DataType type;


            
            if (t == typeof(bool))
                type = DataType.Bool;
            else if (t == typeof(char))
                type = DataType.Char;
            else if (t == typeof(byte))
                type = DataType.UInt8;
            else if (t == typeof(sbyte))
                type = DataType.Int8;
            else if (t == typeof(short))
                type = DataType.Int16;
            else if (t == typeof(ushort))
                type = DataType.UInt16;
            else if (t == typeof(int))
                type = DataType.Int32;
            else if (t == typeof(uint))
                type = DataType.UInt32;
            else if (t == typeof(long))
                type = DataType.Int64;
            else if (t == typeof(ulong))
                type = DataType.UInt64;
            else if (t == typeof(float))
                type = DataType.Float32;
            else if (t == typeof(double))
                type = DataType.Float64;
            else if (t == typeof(decimal))
                type = DataType.Decimal;
            else if (t == typeof(string))
                type = DataType.String;
            else if (t == typeof(DateTime))
                type = DataType.DateTime;
            else if (typeof(Structure).IsAssignableFrom(t))
                type = DataType.Structure;
            //else if (t == typeof(DistributedResource))
            //  type = DataType.DistributedResource;
            else if (ImplementsInterface(t, typeof(IResource)))
            {
                if (isArray)
                    return (DataType.ResourceArray, value);
                else
                {
                    return (IsLocalResource((IResource)value, connection) ? DataType.Resource : DataType.DistributedResource, value);
                }
            }
            else
                type = DataType.Void;


            if (isArray)
                return ((DataType)((byte)type | 0x80), value);
            else
                return (type, value);

        }

    }
}
