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

        var rtType = genericRtType == typeof(AsyncReply<>) ?
                RepresentationType.FromType(mi.ReturnType.GetGenericArguments()[0]) :
                RepresentationType.FromType(mi.ReturnType);

        if (rtType == null)
            throw new Exception($"Unsupported type `{mi.ReturnType}` in method `{type.Name}.{mi.Name}` return");

        var annotationAttr = mi.GetCustomAttribute<AnnotationAttribute>(true);
        var nullableAttr = mi.GetCustomAttribute<NullableAttribute>(true);
        var nullableContextAttr = mi.GetCustomAttribute<NullableContextAttribute>(true);

        var flags = nullableAttr?.Flags?.ToList() ?? new List<byte>();

        var rtNullableAttr = mi.ReturnTypeCustomAttributes.GetCustomAttributes(typeof(NullableAttribute), true).FirstOrDefault() as NullableAttribute;
        var rtNullableContextAttr = mi.ReturnTypeCustomAttributes
                                        .GetCustomAttributes(typeof(NullableContextAttribute), true)
                                        .FirstOrDefault() as NullableContextAttribute
                                        ?? nullableContextAttr;

        var rtFlags = rtNullableAttr?.Flags?.ToList() ?? new List<byte>();

        if (rtFlags.Count > 0 && genericRtType == typeof(AsyncReply<>))
            rtFlags.RemoveAt(0);

        if (rtNullableContextAttr?.Flag == 2)
        {
            if (rtFlags.Count == 1)
                rtType.SetNotNull(rtFlags.FirstOrDefault());
            else
                rtType.SetNotNull(rtFlags);
        }
        else
        {
            if (rtFlags.Count == 1)
                rtType.SetNull(rtFlags.FirstOrDefault());
            else
                rtType.SetNull(rtFlags);
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


            var argNullableAttr = x.GetCustomAttribute<NullableAttribute>(true);
            var argNullableContextAttr = x.GetCustomAttribute<NullableContextAttribute>(true) ?? nullableContextAttr;

            var argFlags = argNullableAttr?.Flags?.ToList() ?? new List<byte>();


            if (argNullableContextAttr?.Flag == 2)
            {
                if (argFlags.Count == 1)
                    argType.SetNotNull(argFlags.FirstOrDefault());
                else
                    argType.SetNotNull(argFlags);
            }
            else
            {
                if (rtFlags.Count == 1)
                    argType.SetNull(argFlags.FirstOrDefault());
                else
                    argType.SetNull(argFlags);
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

        var fn =  customName ?? mi.Name;

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
