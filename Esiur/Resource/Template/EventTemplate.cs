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

    public Map<string, string> Annotations
    {
        get;
        set;
    }

    public override string ToString()
    {
        return $"{Name}: {ArgumentType}";
    }

    public bool Subscribable { get; set; }

    public EventInfo EventInfo { get; set; }

    public TRU ArgumentType { get; set; }


    public static (uint, EventTemplate) Parse(byte[] data, uint offset, byte index, bool inherited)
    {
        var oOffset = offset;

        var hasAnnotation = ((data[offset] & 0x10) == 0x10);
        var subscribable = ((data[offset++] & 0x8) == 0x8);

        var name = data.GetString(offset + 1, data[offset]);
        offset += (uint)data[offset] + 1;

        var (dts, argType) = TRU.Parse(data, offset);

        offset += dts;

        // Annotation ?
        Map<string, string> annotations = null;

        if (hasAnnotation) 
        {
            var (len, anns) = Codec.ParseSync(data, offset, null);

            if (anns is Map<string, string> map)
                annotations = map;

            offset += len;
        }

        return (offset - oOffset, new EventTemplate()
        {
            Index = index,
            Name = name,
            Inherited = inherited,
            ArgumentType = argType,
            Subscribable = subscribable,
            Annotations = annotations
        });
    }

    public byte[] Compose()
    {
        var name = Name.ToBytes();

        var hdr = Inherited ? (byte)0x80 : (byte)0;

        if (Subscribable)
            hdr |= 0x8;

        if (Annotations != null)
        {
            var exp = Codec.Compose(Annotations, null, null); //(  DC.ToBytes(Annotation);
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

    //public EventTemplate(TypeTemplate template, byte index, string name, bool inherited, TRU argumentType, Map<string, string> annotations = null, bool subscribable = false)
    //   : base(template, index, name, inherited)
    //{
    //    this.Annotations = annotations;
    //    this.Subscribable = subscribable;
    //    this.ArgumentType = argumentType;
    //}

    public static EventTemplate MakeEventTemplate(Type type, EventInfo ei, byte index, string name, TypeTemplate typeTemplate)
    {

        if (!ei.EventHandlerType.IsGenericType)
            throw new Exception($"Unsupported event handler type in event `{type.Name}.{ei.Name}`");

        if (ei.EventHandlerType.GetGenericTypeDefinition() != typeof(ResourceEventHandler<>)
            && ei.EventHandlerType.GetGenericTypeDefinition() != typeof(CustomResourceEventHandler<>))
            throw new Exception($"Unsupported event handler type in event `{type.Name}.{ei.Name}`");


        var argType = ei.EventHandlerType.GenericTypeArguments[0];
        var evtType = TRU.FromType(argType);

        if (evtType == null)
            throw new Exception($"Unsupported type `{argType}` in event `{type.Name}.{ei.Name}`");

        var annotationAttrs = ei.GetCustomAttributes<AnnotationAttribute>(true);
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

        Map<string, string> annotations = null;

        if (annotationAttrs != null && annotationAttrs.Count() > 0)
        {
            annotations = new Map<string, string>();
            foreach (var attr in annotationAttrs)
                annotations.Add(attr.Key, attr.Value);
        }


        return new EventTemplate()
        {
            Name = name,
            ArgumentType = evtType,
            Index = index,
            Inherited = ei.DeclaringType != type,
            Annotations = annotations,
            EventInfo = ei,
            Subscribable = subscribableAttr != null
        };
    }

}
