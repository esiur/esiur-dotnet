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

    public string Annotation
    {
        get;
        set;
    }

    //public bool IsVoid
    //{
    //    get;
    //    set;
    //}

    public RepresentationType ReturnType { get; set; }

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


        if (Annotation != null)
        {
            var exp = DC.ToBytes(Annotation);
            bl.AddInt32(exp.Length)
            .AddUInt8Array(exp);
            bl.InsertUInt8(0, (byte)((Inherited ? (byte)0x90 : (byte)0x10) | (IsStatic ? 0x4 : 0)));
        }
        else
            bl.InsertUInt8(0, (byte)((Inherited ? (byte)0x80 : (byte)0x0) | (IsStatic ? 0x4 : 0)));

        return bl.ToArray();
    }

    public FunctionTemplate(TypeTemplate template, byte index, string name, bool inherited, bool isStatic, ArgumentTemplate[] arguments, RepresentationType returnType, string annotation = null)
       : base(template, index, name, inherited)
    {
        this.Arguments = arguments;
        this.ReturnType = returnType;
        this.Annotation = annotation;
        this.IsStatic = isStatic;
    }



    public static FunctionTemplate MakeFunctionTemplate(Type type, MethodInfo mi, byte index = 0, string customName = null, TypeTemplate typeTemplate = null)
    {

        var genericRtType = mi.ReturnType.IsGenericType ? mi.ReturnType.GetGenericTypeDefinition() : null;

        RepresentationType rtType;

        if (genericRtType == typeof(AsyncReply<>))
        {
            rtType = RepresentationType.FromType(mi.ReturnType.GetGenericArguments()[0]);
        }
        else if (genericRtType == typeof(IEnumerable<>) || genericRtType == typeof(IAsyncEnumerable<>))
        {
            // get export
            rtType = RepresentationType.FromType(mi.GetCustomAttribute<ExportAttribute>()?.ReturnType ?? typeof(object));
        }
        else
        {
            rtType = RepresentationType.FromType(mi.ReturnType);
        }

        //var rtType = genericRtType == typeof(AsyncReply<>) ?
        //        RepresentationType.FromType(mi.ReturnType.GetGenericArguments()[0]) :
        //        RepresentationType.FromType(mi.ReturnType);

        if (rtType == null)
            throw new Exception($"Unsupported type `{mi.ReturnType}` in method `{type.Name}.{mi.Name}` return");

        var annotationAttr = mi.GetCustomAttribute<AnnotationAttribute>(true);

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
            if (args.Last().ParameterType == typeof(DistributedConnection))
                args = args.Take(args.Count() - 1).ToArray();
        }

        var arguments = args.Select(x =>
        {
            var argType = RepresentationType.FromType(x.ParameterType);

            if (argType == null)
                throw new Exception($"Unsupported type `{x.ParameterType}` in method `{type.Name}.{mi.Name}` parameter `{x.Name}`");

            //argType.Nullable = nullabilityInfoContext.Create(x).WriteState is NullabilityState.Nullable;

            //var argNullableAttr = x.GetCustomAttribute<NullableAttribute>(true);
            //var argNullableContextAttr = x.GetCustomAttribute<NullableContextAttribute>(true) ?? nullableContextAttr;

            var argNullableAttr = x.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().FullName == "System.Runtime.CompilerServices.NullableAttribute");
            var argNullableContextAttr = x.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().FullName == "System.Runtime.CompilerServices.NullableContextAttr");

            //var argFlags = argNullableAttr?.Flags?.ToList() ?? new List<byte>();
            //var argFlags = ((byte[])argNullableAttr?.NullableFlags ?? new byte[0]).ToList();

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

            return new ArgumentTemplate()
            {
                Name = x.Name,
                Type = argType,
                ParameterInfo = x,
                Optional = x.IsOptional
            };
        })
        .ToArray();

        var fn = customName ?? mi.Name;

        var ft = new FunctionTemplate(typeTemplate, index, fn, mi.DeclaringType != type,
            mi.IsStatic,
            arguments, rtType);

        if (annotationAttr != null)
            ft.Annotation = annotationAttr.Annotation;
        else
            ft.Annotation = "(" + String.Join(",", mi.GetParameters().Where(x => x.ParameterType != typeof(DistributedConnection)).Select(x => "[" + x.ParameterType.Name + "] " + x.Name)) + ") -> " + mi.ReturnType.Name;

        ft.MethodInfo = mi;
        //    functions.Add(ft);

        return ft;
    }

}
