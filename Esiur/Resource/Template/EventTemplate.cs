using Esiur.Core;
using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template;
public class EventTemplate : MemberTemplate
{
    public string Annotation
    {
        get;
        set;
    }

    public bool Subscribable { get; set; }

    public EventInfo EventInfo { get; set; }

    public RepresentationType ArgumentType { get; set; }

    public override byte[] Compose()
    {
        var name = base.Compose();

        var hdr = Inherited ? (byte)0x80 : (byte)0;

        if (Subscribable)
            hdr |= 0x8;

        if (Annotation != null)
        {
            var exp = DC.ToBytes(Annotation);
            hdr |= 0x50;
            return new BinaryList()
                    .AddUInt8(hdr)
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(ArgumentType.Compose())
                    .AddInt32(exp.Length)
                    .AddUInt8Array(exp)
                    .ToArray();
        }
        else
            hdr |= 0x40;
        return new BinaryList()
                .AddUInt8(hdr)
                .AddUInt8((byte)name.Length)
                .AddUInt8Array(name)
                .AddUInt8Array(ArgumentType.Compose())
                .ToArray();
    }

    public EventTemplate(TypeTemplate template, byte index, string name, bool inherited, RepresentationType argumentType, string annotation = null, bool subscribable = false)
       : base(template, index, name, inherited)
    {
        this.Annotation = annotation;
        this.Subscribable = subscribable;
        this.ArgumentType = argumentType;
    }

    public static EventTemplate MakeEventTemplate(Type type, EventInfo ei, byte index = 0, string customName = null, TypeTemplate typeTemplate = null)
    {

        if (!ei.EventHandlerType.IsGenericType)
            throw new Exception($"Unsupported event handler type in event `{type.Name}.{ei.Name}`");

        if (ei.EventHandlerType.GetGenericTypeDefinition() != typeof(ResourceEventHandler<>)
            && ei.EventHandlerType.GetGenericTypeDefinition() != typeof(CustomResourceEventHandler<>))
            throw new Exception($"Unsupported event handler type in event `{type.Name}.{ei.Name}`");


        var argType = ei.EventHandlerType.GenericTypeArguments[0];
        var evtType = RepresentationType.FromType(argType);

        if (evtType == null)
            throw new Exception($"Unsupported type `{argType}` in event `{type.Name}.{ei.Name}`");

        var annotationAttr = ei.GetCustomAttribute<AnnotationAttribute>(true);
        var subscribableAttr = ei.GetCustomAttribute<SubscribableAttribute>(true);

        //evtType.Nullable =  new NullabilityInfoContext().Create(ei).ReadState is NullabilityState.Nullable;

        var nullableAttr = ei.GetCustomAttributes().FirstOrDefault(x => x.GetType().Name == "System.Runtime.CompilerServices.NullableAttribute");// .GetCustomAttribute<NullableAttribute>(true);
        var nullableContextAttr = ei.GetCustomAttributes().FirstOrDefault(x => x.GetType().Name == "System.Runtime.CompilerServices.NullableContextAttribute");// ei.GetCustomAttribute<NullableContextAttribute>(true);


        var nullableAttrFlags = (nullableAttr?.GetType().GetField("NullableFlags")?.GetValue(nullableAttr) as byte[] ?? new byte[0]).ToList();
        var nullableContextAttrFlag = (byte)(nullableContextAttr?.GetType().GetField("Flag")?.GetValue(nullableContextAttr) ?? (byte)0);

        //var flags = nullableAttr?.Flags?.ToList() ?? new List<byte>();
        //var flags = ((byte[])nullableAttr?.NullableFlags ?? new byte[0]).ToList();

        // skip the eventHandler class
        if (nullableAttrFlags.Count > 1)
            nullableAttrFlags = nullableAttrFlags.Skip(1).ToList();

        if (nullableContextAttrFlag == 2)
        {
            if (nullableAttrFlags.Count == 1)
                evtType.SetNotNull(nullableAttrFlags.FirstOrDefault());
            else
                evtType.SetNotNull(nullableAttrFlags);
        }
        else
        {
            if (nullableAttrFlags.Count == 1)
                evtType.SetNull(nullableAttrFlags.FirstOrDefault());
            else
                evtType.SetNull(nullableAttrFlags);
        }

        var et = new EventTemplate(typeTemplate, index, customName ?? ei.Name, ei.DeclaringType != type, evtType);
        et.EventInfo = ei;

        if (annotationAttr != null)
            et.Annotation = annotationAttr.Annotation;

        if (subscribableAttr != null)
            et.Subscribable = true;

        return et;
    }

}
