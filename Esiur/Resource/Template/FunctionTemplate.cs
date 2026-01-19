using Esiur.Core;
using Esiur.Data;
using Esiur.Net.IIP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template;
public class FunctionTemplate : MemberTemplate
{

    public Map<string, string> Annotations
    {
        get;
        set;
    }

    //public bool IsVoid
    //{
    //    get;
    //    set;
    //}

    public TRU ReturnType { get; set; }

    public bool IsStatic { get; set; }

    public ArgumentTemplate[] Arguments { get; set; }

    public MethodInfo MethodInfo
    {
        get;
        set;
    }


    public override byte[] Compose()
    {

        var name = base.Compose();

        var bl = new BinaryList()
                .AddUInt8((byte)name.Length)
                .AddUInt8Array(name)
                .AddUInt8Array(ReturnType.Compose())
                .AddUInt8((byte)Arguments.Length);

        for (var i = 0; i < Arguments.Length; i++)
            bl.AddUInt8Array(Arguments[i].Compose());


        if (Annotations != null)
        {
            var exp = Codec.Compose(Annotations, null, null);// DC.ToBytes(Annotation);
            bl.AddInt32(exp.Length)
            .AddUInt8Array(exp);
            bl.InsertUInt8(0, (byte)((Inherited ? (byte)0x90 : (byte)0x10) | (IsStatic ? 0x4 : 0)));
        }
        else
            bl.InsertUInt8(0, (byte)((Inherited ? (byte)0x80 : (byte)0x0) | (IsStatic ? 0x4 : 0)));

        return bl.ToArray();
    }

    public FunctionTemplate(TypeTemplate template, byte index, string name, bool inherited, bool isStatic, ArgumentTemplate[] arguments, TRU returnType, Map<string, string> annotations = null)
       : base(template, index, name, inherited)
    {
        this.Arguments = arguments;
        this.ReturnType = returnType;
        this.Annotations = annotations;
        this.IsStatic = isStatic;
    }



    public static FunctionTemplate MakeFunctionTemplate(Type type, MethodInfo mi, byte index = 0, string customName = null, TypeTemplate typeTemplate = null)
    {

        var genericRtType = mi.ReturnType.IsGenericType ? mi.ReturnType.GetGenericTypeDefinition() : null;

        TRU rtType;

        if (genericRtType == typeof(AsyncReply<>))
        {
            rtType = TRU.FromType(mi.ReturnType.GetGenericArguments()[0]);
        }
        else if (genericRtType == typeof(Task<>))
        {
            rtType = TRU.FromType(mi.ReturnType.GetGenericArguments()[0]);
        }
        else if (genericRtType == typeof(IEnumerable<>))// || genericRtType == typeof(IAsyncEnumerable<>))
        {
            // get export
            rtType = TRU.FromType(mi.ReturnType.GetGenericArguments()[0]);
        }
        else
        {
            rtType = TRU.FromType(mi.ReturnType);
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

        if (rtNullableAttrFlags.Count > 0 && genericRtType == typeof(AsyncReply<>))
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
            if (args.Last().ParameterType == typeof(DistributedConnection) 
                || args.Last().ParameterType == typeof(InvocationContext))
                args = args.Take(args.Count() - 1).ToArray();
        }

        var arguments = args.Select(x =>
        {
            var argType = TRU.FromType(x.ParameterType);

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

            return new ArgumentTemplate()
            {
                Name = x.Name,
                Type = argType,
                ParameterInfo = x,
                Optional = x.IsOptional,
                Annotations = argAnn
            };
        })
        .ToArray();

        var fn = customName ?? mi.Name;

        var ft = new FunctionTemplate(typeTemplate, index, fn, mi.DeclaringType != type,
            mi.IsStatic,
            arguments, rtType);


        if (annotationAttrs != null && annotationAttrs.Count() > 0)
        {
            ft.Annotations = new Map<string, string>();
            foreach (var attr in annotationAttrs)
                ft.Annotations.Add(attr.Key, attr.Value);
        }
        else
        {
            ft.Annotations = new Map<string, string>();
            ft.Annotations.Add(null, "(" + String.Join(",", 
                mi.GetParameters().Where(x => x.ParameterType != typeof(DistributedConnection))
                .Select(x => "[" + x.ParameterType.Name + "] " + x.Name)) + ") -> " + mi.ReturnType.Name);

        }

        ft.MethodInfo = mi;
        //    functions.Add(ft);

        return ft;
    }

    public override string ToString()
    {
        //return = "(" + String.Join(",", mi.GetParameters().Where(x => x.ParameterType != typeof(DistributedConnection)).Select(x => "[" + x.ParameterType.Name + "] " + x.Name)) + ") -> " + mi.ReturnType.Name;

        return $"{ReturnType} {Name}({string.Join(", ", Arguments.Select(a => a.ToString()))})";
    }

}
