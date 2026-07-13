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

        if (((byte)data[offset] & 0xC7) == (byte)TduIdentifier.TypeDef)
        {
            var previousSequence = DataDeserializer.TypeDefRequestSequence.Value;
            try
            {
                DataDeserializer.TypeDefRequestSequence.Value = requestSequence;
                var parsed = await Codec.ParseAsync(data, offset, connection, null);
                if (parsed.Size != contentLength || parsed.Value is not TypeDefInfo info)
                    throw new Exception("Invalid TypeDefInfo payload.");

                ApplyInfo(od, domain, data, offset, contentLength, info);
                CompleteRegistration(od, connection);
                return od;
            }
            finally
            {
                DataDeserializer.TypeDefRequestSequence.Value = previousSequence;
            }
        }

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

#if VERBOSE
        if (od._proxyType == null)
            Console.WriteLine("Proxy type not found " + od.Name);
#endif

        if (od._proxyType != null)
        {
#if VERBOSE
            Console.WriteLine("Updating TypeDef Proxy: " + od.Name);
#endif
            // update PropertyInfo, MethodInfo, EventInfo, FieldInfo
            // @TODO Check signature match as well, not only name, to avoid conflicts

            foreach (var prop in od.Properties)            
                prop.PropertyInfo = od._proxyType.GetProperty(prop.Name);

            foreach (var func in od.Functions)
                func.MethodInfo = od._proxyType.GetMethod(func.Name);

            foreach (var evnt in od.Events)
                evnt.EventInfo = od._proxyType.GetEvent(evnt.Name);

            foreach(var cons in od.Constants)
                cons.FieldInfo = od._proxyType.GetField(cons.Name);

           
        }

        // register in warehouse
        // @TOOD check who is the initiator
        connection.Instance?.Warehouse?.TryRegisterRemoteTypeDef(connection.RemoteDomain, od);

        return od;
    }

    private static void ApplyInfo(RemoteTypeDef definition, string domain, byte[] data,
                                  uint offset, uint contentLength, TypeDefInfo info)
    {
        definition._domain = domain;
        definition._content = data.Clip(offset, contentLength);
        definition._typeId = info.Id;
        definition._parentTypeId = info.Parent;
        definition._typeName = string.IsNullOrEmpty(info.Namespace)
            ? info.Name
            : $"{info.Namespace}.{info.Name}";
        definition._typeDefKind = info.Kind;
        definition._version = info.Version;
        definition.Annotations = info.Annotations;

        definition._properties.Clear();
        definition._functions.Clear();
        definition._events.Clear();
        definition._constants.Clear();

        if (info.Properties != null)
            definition._properties.AddRange(info.Properties.Select(x => ToProperty(definition, x)));
        if (info.Functions != null)
            definition._functions.AddRange(info.Functions.Select(x => ToFunction(definition, x)));
        if (info.Events != null)
            definition._events.AddRange(info.Events.Select(x => ToEvent(definition, x)));
        if (info.Constants != null)
            definition._constants.AddRange(info.Constants.Select(x => ToConstant(definition, x)));
    }

    private static T ApplyMember<T>(RemoteTypeDef definition, MemberDefInfo info, T member)
        where T : MemberDef
    {
        member.Definition = definition;
        member.Index = info.Index;
        member.Name = info.Name;
        member.Inherited = (info.Flags & (byte)MemberDefFlags.Inherited) != 0;
        member.Description = info.Description;
        member.Usage = info.Usage;
        member.Examples = info.Examples;
        member.Tags = info.Tags;
        member.Unit = info.Unit;
        member.Minimum = info.Minimum;
        member.Maximum = info.Maximum;
        member.AllowedValues = info.AllowedValues;
        member.Pattern = info.Pattern;
        member.Format = info.Format;
        member.Preconditions = info.Preconditions;
        member.Postconditions = info.Postconditions;
        member.Effects = info.Effects;
        member.Warnings = info.Warnings;
        member.RelatedMembers = info.RelatedMembers;
        member.DeprecationMessage = info.DeprecationMessage;
        return member;
    }

    private static PropertyDef ToProperty(RemoteTypeDef definition, PropertyDefInfo info)
    {
        var flags = (PropertyDefFlags)info.Flags;
        return ApplyMember(definition, info, new PropertyDef
        {
            ValueType = info.ValueType,
            ReadOnly = flags.HasFlag(PropertyDefFlags.ReadOnly),
            Constant = flags.HasFlag(PropertyDefFlags.Constant),
            Historical = flags.HasFlag(PropertyDefFlags.Historical) || info.HistoryControl != 0,
        });
    }

    private static FunctionDef ToFunction(RemoteTypeDef definition, FunctionDefInfo info)
    {
        var flags = (FunctionDefFlags)info.Flags;
        return ApplyMember(definition, info, new FunctionDef
        {
            ReturnType = info.ReturnType,
            StreamMode = info.StreamMode,
            IsStatic = flags.HasFlag(FunctionDefFlags.Static),
            ReadOnly = flags.HasFlag(FunctionDefFlags.ReadOnly),
            Idempotent = flags.HasFlag(FunctionDefFlags.Idempotent),
            Cancellable = flags.HasFlag(FunctionDefFlags.Cancellable),
            Deprecated = flags.HasFlag(FunctionDefFlags.Deprecated),
            Arguments = info.Arguments?.Select(ToArgument).ToArray() ?? Array.Empty<ArgumentDef>(),
        });
    }

    private static ArgumentDef ToArgument(ArgumentDefInfo info)
    {
        var flags = (ArgumentDefFlags)info.Flags;
        return new ArgumentDef
        {
            Index = info.Index,
            Name = info.Name,
            Optional = flags.HasFlag(ArgumentDefFlags.Optional),
            Type = info.ValueType,
            DefaultValue = info.DefaultValue,
        };
    }

    private static EventDef ToEvent(RemoteTypeDef definition, EventDefInfo info)
    {
        var flags = (EventDefFlags)info.Flags;
        return ApplyMember(definition, info, new EventDef
        {
            ArgumentType = info.ArgumentType,
            AutoDelivered = flags.HasFlag(EventDefFlags.AutoDelivered),
            Deprecated = flags.HasFlag(EventDefFlags.Deprecated),
        });
    }

    private static ConstantDef ToConstant(RemoteTypeDef definition, ConstantDefInfo info)
        => ApplyMember(definition, info, new ConstantDef
        {
            ValueType = info.ValueType,
            Value = info.Value,
        });

    private static void CompleteRegistration(RemoteTypeDef definition, EpConnection connection)
    {
        definition._proxyType = connection.Instance?.Warehouse?.TryGetProxyType(
            definition.Kind, definition.Domain, definition.Name);

        if (definition._proxyType != null)
        {
            foreach (var property in definition.Properties)
                property.PropertyInfo = definition._proxyType.GetProperty(property.Name);
            foreach (var function in definition.Functions)
                function.MethodInfo = definition._proxyType.GetMethod(function.Name);
            foreach (var eventDefinition in definition.Events)
                eventDefinition.EventInfo = definition._proxyType.GetEvent(eventDefinition.Name);
            foreach (var constant in definition.Constants)
                constant.FieldInfo = definition._proxyType.GetField(constant.Name);
        }

        connection.Instance?.Warehouse?.TryRegisterRemoteTypeDef(connection.RemoteDomain, definition);
    }
}

