using Esiur.Core;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Protocol;
using Esiur.Proxy;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Esiur.Data.Types;


public class RemoteTypeDef:TypeDef
{
    Type _proxyType { get; set; }

    public Type ProxyType => _proxyType;

    string _domain;

    public string Domain => _domain;


    public uint _localTypeDefId;

    public uint LocalTypeDefId
    {
        get => _localTypeDefId;
        internal set => _localTypeDefId = value;
    }

    public static AsyncReply<RemoteTypeDef> Parse(RemoteTypeDef od, string domain, byte[] data, EpConnection connection, ulong[] requestSequence)
    {
        return Parse(od, domain, data, 0, (uint)data.Length, connection, requestSequence);
    }

    public static async AsyncReply<RemoteTypeDef> Parse(RemoteTypeDef od, string domain, byte[] data, uint offset, uint contentLength, EpConnection connection, ulong[] requestSequence)
    {
        uint ends = offset + contentLength;

        uint oOffset = offset;

        // start parsing...


        od._domain = domain;

        od._content = data.Clip(offset, contentLength);

        var hasParent = (data[offset] & 0x80) > 0;
        var hasClassAnnotation = (data[offset] & 0x40) > 0;

        od._typeDefKind = (TypeDefKind)(data[offset++] & 0xF);

        od._typeId = data.GetUInt64(offset, Endian.Little);
        offset += 8;
        od._typeName = data.GetString(offset + 1, data[offset]);
        offset += (uint)data[offset] + 1;


        if (hasParent)
        {
            od._parentTypeId = data.GetUInt64(offset, Endian.Little);
            offset += 8;
        }

        if (hasClassAnnotation)
        {
            var (len, anns) = Codec.ParseSync(data, offset, null);

            if (anns is Map<string, string> annotations)
                od.Annotations = annotations;

            offset += len;
        }

        od._version = data.GetInt32(offset, Endian.Little);
        offset += 4;

        ushort methodsCount = data.GetUInt16(offset, Endian.Little);
        offset += 2;

        byte functionIndex = 0;
        byte propertyIndex = 0;
        byte eventIndex = 0;
        byte constantIndex = 0;

        for (int i = 0; i < methodsCount; i++)
        {
            var inherited = (data[offset] & 0x80) > 0;
            var type = (data[offset] >> 5) & 0x3;

            if (type == 0) // function
            {
                var ft = await FunctionDef.ParseAsync(data, offset, functionIndex++, inherited, connection, requestSequence);
                offset += ft.Size;
                od._functions.Add(ft.Value);
            }
            else if (type == 1)    // property
            {
                var pt = await PropertyDef.ParseAsync(data, offset, propertyIndex++, inherited, connection, requestSequence);
                offset += pt.Size;
                od._properties.Add(pt.Value);

            }
            else if (type == 2) // Event
            {
                var et = await EventDef.ParseAsync(data, offset, eventIndex++, inherited, connection, requestSequence);
                offset += et.Size;
                od._events.Add(et.Value);
            }
            // constant
            else if (type == 3)
            {
                var ct = await ConstantDef.ParseAsync(data, offset, constantIndex++, inherited, connection, requestSequence);
                offset += ct.Size;
                od._constants.Add(ct.Value);
            }
        }

        // try to get proxy type
        od._proxyType = connection.Instance?.Warehouse?.TryGetProxyType(od.Kind, od.Domain, od.Name);

        return od;
    }
}

