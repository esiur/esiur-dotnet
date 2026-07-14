using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Data.Types;

public class FunctionDef : MemberDef
{

    //public bool IsVoid
    //{
    //    get;
    //    set;
    //}

    public Tru ReturnType { get; set; }

    public bool IsStatic { get; set; }
    public bool ReadOnly { get; set; }
    public bool Idempotent { get; set; }
    public bool Cancellable { get; set; }   
    public bool Pausable { get; set; }

    //public FunctionDefFlags Flags { get; set; }
    public StreamMode StreamMode { get; set; }

    public ArgumentDef[] Arguments { get; set; }

    public MethodInfo MethodInfo
    {
        get;
        set;
    }

    public static async AsyncReply<ParseResult<FunctionDef>> ParseAsync(
        byte[] data, uint offset, byte index, bool inherited,
        EpConnection connection, ulong[] requestSequence)
    {
        var originalOffset = offset;
        var isStatic = (data[offset] & 0x04) != 0;
        var hasAnnotations = (data[offset++] & 0x10) != 0;
        var name = data.GetString(offset + 1, data[offset]);
        offset += (uint)data[offset] + 1;

        var returnType = await Tru.ParseAsync(data, offset, connection, requestSequence);
        offset += returnType.Size;

        var argumentCount = data[offset++];
        var arguments = new List<ArgumentDef>(argumentCount);
        for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
        {
            var argument = await ArgumentDef.ParseAsync(
                data, offset, argumentIndex, connection, requestSequence);
            arguments.Add(argument.Value);
            offset += argument.Size;
        }

        Map<string, string> annotations = null;
        if (hasAnnotations)
        {
            var (size, value) = Codec.ParseSync(data, offset, null);
            annotations = value as Map<string, string>;
            offset += size;
        }

        return new ParseResult<FunctionDef>(new FunctionDef
        {
            Index = index,
            Name = name,
            Arguments = arguments.ToArray(),
            IsStatic = isStatic,
            Inherited = inherited,
            Annotations = annotations,
            ReturnType = returnType.Value,
        }, offset - originalOffset);
    }


    //public static async AsyncReply<ParseResult<FunctionDef>> ParseAsync(byte[] data, uint offset, byte index, bool inherited, EpConnection connection, ulong[] requestSequence)
    //{

    //    var oOffset = offset;

    //    var isStatic = ((data[offset] & 0x4) == 0x4);
    //    var hasAnnotation = ((data[offset++] & 0x10) == 0x10);

    //    var name = data.GetString(offset + 1, data[offset]);
    //    offset += (uint)data[offset] + 1;

    //    //Console.WriteLine("Parsing functionDef " + name);

    //    // return type
    //    var returnType = await Tru.ParseAsync(data, offset, connection, requestSequence);
    //    offset += returnType.Size;

    //    // arguments count
    //    var argsCount = data[offset++];
    //    List<ArgumentDef> arguments = new();

    //    for (var a = 0; a < argsCount; a++)
    //    {
    //        var argType = await ArgumentDef.ParseAsync(data, offset, a, connection, requestSequence);
    //        arguments.Add(argType.Value);
    //        offset += argType.Size;
    //    }

    //    Map<string, string> annotations = null;

    //    // arguments
    //    if (hasAnnotation) // Annotation ?
    //    {
    //        var (len, anns) = Codec.ParseSync(data, offset, null);

    //        if (anns is Map<string, string> map)
    //            annotations = map;

    //        offset += len;
    //    }

    //    return new ParseResult<FunctionDef>( new FunctionDef()
    //    {
    //        Index = index,
    //        Name = name,
    //        Arguments = arguments.ToArray(),
    //        IsStatic = isStatic,
    //        Inherited = inherited,
    //        Annotations = annotations,
    //        ReturnType = returnType.Value,
    //    }, offset - oOffset);
    //}

    //public byte[] Compose(EpConnection connection)
    //{

    //    var name = DC.ToBytes(Name);

    //    var bl = new BinaryList()
    //            .AddUInt8((byte)name.Length)
    //            .AddUInt8Array(name)
    //            .AddUInt8Array(ReturnType.Compose(connection))
    //            .AddUInt8((byte)Arguments.Length);

    //    for (var i = 0; i < Arguments.Length; i++)
    //        bl.AddUInt8Array(Arguments[i].Compose(connection));


    //    if (Annotations != null)
    //    {
    //        var exp = Codec.Compose(Annotations, connection.Instance.Warehouse , connection);// DC.ToBytes(Annotation);
    //        bl.AddUInt8Array(exp);
    //        bl.InsertUInt8(0, (byte)((Inherited ? (byte)0x90 : (byte)0x10) | (IsStatic ? 0x4 : 0)));
    //    }
    //    else
    //        bl.InsertUInt8(0, (byte)((Inherited ? (byte)0x80 : (byte)0x0) | (IsStatic ? 0x4 : 0)));

    //    return bl.ToArray();
    //}


    public static FunctionDef MakeFunctionDef(Warehouse warehouse, Type type, MethodInfo mi, byte index, string name, TypeDef schema)
    {

        var genericRtType = mi.ReturnType.IsGenericType ? mi.ReturnType.GetGenericTypeDefinition() : null;
        var streamAttribute = mi.GetCustomAttribute<StreamAttribute>(true);
        var streamMode = StreamMode.None;
        var pausable = false;
        Tru rtType;

        if (streamAttribute != null &&
            genericRtType != typeof(AsyncReply<>) &&
            genericRtType != typeof(AsyncStreamReply<>))
            throw new Exception($"Method `{type.Name}.{mi.Name}` uses StreamAttribute and must return AsyncReply<T> or AsyncStreamReply<T>.");

        if (genericRtType == typeof(IAsyncEnumerable<>))
        {
            streamMode = StreamMode.Pull;
            rtType = Tru.FromType(mi.ReturnType.GetGenericArguments()[0], warehouse);
        }
        else if (genericRtType == typeof(IEnumerable<>))
        {
            streamMode = StreamMode.Push;
            rtType = Tru.FromType(mi.ReturnType.GetGenericArguments()[0], warehouse);
        }
        else if (genericRtType == typeof(AsyncStreamReply<>))
        {
            if (streamAttribute == null)
                throw new Exception($"Method `{type.Name}.{mi.Name}` returning AsyncStreamReply<T> must declare StreamAttribute.");

            streamMode = streamAttribute.Mode;
            rtType = Tru.FromType(mi.ReturnType.GetGenericArguments()[0], warehouse);
        }
        else if (streamAttribute != null)
        {
            streamMode = streamAttribute.Mode;
            rtType = Tru.FromType(mi.ReturnType.GetGenericArguments()[0], warehouse);
        }
        else if (genericRtType == typeof(AsyncReply<>) || genericRtType == typeof(Task<>))
        {
            rtType = Tru.FromType(mi.ReturnType.GetGenericArguments()[0], warehouse);
        }
        else
        {
            rtType = mi.ReturnType == typeof(Task)
                ? Tru.FromType(null, warehouse)
                : Tru.FromType(mi.ReturnType, warehouse);
        }

        if (streamAttribute != null)
        {
            if (streamAttribute.Mode != StreamMode.Push && streamAttribute.Mode != StreamMode.Pull)
                throw new Exception($"Stream method `{type.Name}.{mi.Name}` must use Push or Pull mode.");

            if (streamMode != streamAttribute.Mode)
                throw new Exception($"Stream mode `{streamAttribute.Mode}` conflicts with return type `{mi.ReturnType}` in method `{type.Name}.{mi.Name}`.");

            pausable = streamAttribute.Pausable;
            if (pausable && streamMode != StreamMode.Push)
                throw new Exception($"Only push stream method `{type.Name}.{mi.Name}` can be pausable.");
        }

        if (rtType == null)
            throw new Exception($"Unsupported type `{mi.ReturnType}` in method `{type.Name}.{mi.Name}` return");

        var annotationAttrs = mi.GetCustomAttributes<AnnotationAttribute>(true);

        //var nullabilityInfoContext = new NullabilityInfoContext();
        //rtType.Nullable = nullabilityInfoContext.Create(mi.ReturnParameter).WriteState is NullabilityState.Nullable;


        var nullableAttr = mi.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().FullName == "System.Runtime.CompilerServices.NullableAttribute");
        var nullableContextAttr = mi.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().FullName == "System.Runtime.CompilerServices.NullableContextAttribute");

        var nullableAttrFlags = (nullableAttr?.GetType().GetField("NullableFlags")?.GetValue(nullableAttr) as byte[] ?? new byte[0]).ToList();
        var nullableContextAttrFlag = (byte)(nullableContextAttr?.GetType().GetField("Flag")?.GetValue(nullableContextAttr) ?? (byte)0);

        //var flags = ((byte[])nullableAttr?.NullableFlags ?? new byte[0]).ToList();

        //var rtNullableAttr = mi.ReturnTypeCustomAttributes.GetCustomAttributes(typeof(NullableAttribute), true).FirstOrDefault() as NullableAttribute;
        var rtNullableAttr = mi.ReturnTypeCustomAttributes.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().FullName == "System.Runtime.CompilerServices.NullableAttribute");



        // var rtNullableContextAttr = mi.ReturnTypeCustomAttributes
        //                                .GetCustomAttributes(typeof(NullableContextAttribute), true)
        //                                 .FirstOrDefault() as NullableContextAttribute
        //                                ?? nullableContextAttr;


        var rtNullableContextAttr = mi.ReturnTypeCustomAttributes
                                .GetCustomAttributes(true).FirstOrDefault(x => x.GetType().Name == "NullableContextAttribute")
                                ?? nullableContextAttr;

        var rtNullableAttrFlags = (rtNullableAttr?.GetType().GetField("NullableFlags")?.GetValue(rtNullableAttr) as byte[] ?? new byte[0]).ToList();
        var rtNullableContextAttrFlag = (byte)(rtNullableContextAttr?.GetType().GetField("Flag")?.GetValue(rtNullableContextAttr) ?? (byte)0);

        //var rtFlags = rtNullableAttr?.Flags?.ToList() ?? new List<byte>();
        //var rtFlags = ((byte[])rtNullableAttr?.NullableFlags ?? new byte[0]).ToList();

        if (rtNullableAttrFlags.Count > 0 &&
            (genericRtType == typeof(AsyncReply<>) ||
             genericRtType == typeof(Task<>) ||
             genericRtType == typeof(IEnumerable<>) ||
             genericRtType == typeof(IAsyncEnumerable<>) ||
             genericRtType == typeof(AsyncStreamReply<>)))
            rtNullableAttrFlags.RemoveAt(0);

        if (rtNullableContextAttrFlag == 2)
        {
            if (rtNullableAttrFlags.Count == 1)
                rtType.SetNotNull(rtNullableAttrFlags.FirstOrDefault());
            else
                rtType.SetNotNull(rtNullableAttrFlags);
        }
        else
        {
            if (rtNullableAttrFlags.Count == 1)
                rtType.SetNull(rtNullableAttrFlags.FirstOrDefault());
            else
                rtType.SetNull(rtNullableAttrFlags);
        }

        var args = mi.GetParameters();

        if (args.Length > 0)
        {
            if (args.Last().ParameterType == typeof(EpConnection)
                || args.Last().ParameterType == typeof(InvocationContext))
                args = args.Take(args.Count() - 1).ToArray();
        }

        var arguments = args.Select(x =>
        {
            var argType = Tru.FromType(x.ParameterType, warehouse);

            if (argType == null)
                throw new Exception($"Unsupported type `{x.ParameterType}` in method `{type.Name}.{mi.Name}` parameter `{x.Name}`");


            var argAnnotationAttrs = x.GetCustomAttributes<AnnotationAttribute>(true);


            var argNullableAttr = x.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().FullName == "System.Runtime.CompilerServices.NullableAttribute");
            var argNullableContextAttr = x.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().FullName == "System.Runtime.CompilerServices.NullableContextAttr");

            var argNullableAttrFlags = (argNullableAttr?.GetType().GetField("NullableFlags")?.GetValue(argNullableAttr) as byte[] ?? new byte[0]).ToList();
            var argNullableContextAttrFlag = (byte)(argNullableAttr?.GetType().GetField("Flag")?.GetValue(argNullableAttr) ?? (byte)0);

            if (argNullableContextAttrFlag == 2)
            {
                if (argNullableAttrFlags.Count == 1)
                    argType.SetNotNull(argNullableAttrFlags.FirstOrDefault());
                else
                    argType.SetNotNull(argNullableAttrFlags);
            }
            else
            {
                if (argNullableAttrFlags.Count == 1)
                    argType.SetNull(argNullableAttrFlags.FirstOrDefault());
                else
                    argType.SetNull(argNullableAttrFlags);
            }

            Map<string, string> argAnn = null;

            if (argAnnotationAttrs != null && argAnnotationAttrs.Count() > 0)
            {
                argAnn = new Map<string, string>();
                foreach (var attr in argAnnotationAttrs)
                    argAnn.Add(attr.Key, attr.Value);
            }

            return new ArgumentDef()
            {
                Name = x.Name,
                Type = argType,
                ParameterInfo = x,
                Optional = x.IsOptional,
                Annotations = argAnn
            };
        })
        .ToArray();

        Map<string, string> annotations = null;

        if (annotationAttrs != null && annotationAttrs.Count() > 0)
        {
            annotations = new Map<string, string>();
            foreach (var attr in annotationAttrs)
                annotations.Add(attr.Key, attr.Value);
        }
        else
        {
            annotations = new Map<string, string>();
            annotations.Add("", "(" + String.Join(",",
                mi.GetParameters().Where(x => x.ParameterType != typeof(EpConnection))
                .Select(x => "[" + x.ParameterType.Name + "] " + x.Name)) + ") -> " + mi.ReturnType.Name);

        }

        return DefinitionAttributeReader.Apply(mi, new FunctionDef()
        {
            Definition = schema,
            Name = name,
            Index = index,
            Inherited = mi.DeclaringType != type,
            IsStatic = mi.IsStatic,
            ReturnType = rtType,
            Arguments = arguments,
            MethodInfo = mi,
            Annotations = annotations,
            ReadOnly = mi.GetCustomAttribute<ReadOnlyAttribute>(true) != null,
            Idempotent = mi.GetCustomAttribute<IdempotentAttribute>(true) != null,
            Cancellable = mi.GetCustomAttribute<CancellableAttribute>(true) != null,
            RatePolicyName = mi.GetCustomAttribute<RateControlAttribute>(true)?.PolicyName,
            MemberPolicyAttributes = Attribute.GetCustomAttributes(mi, true),
            StreamMode = streamMode,
            Pausable = pausable,
        });

    }

    public override string ToString()
    {
        //return = "(" + String.Join(",", mi.GetParameters().Where(x => x.ParameterType != typeof(EpConnection)).Select(x => "[" + x.ParameterType.Name + "] " + x.Name)) + ") -> " + mi.ReturnType.Name;

        return $"{ReturnType} {Name}({string.Join(", ", Arguments.Select(a => a.ToString()))})";
    }

}
