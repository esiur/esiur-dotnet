using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Esiur.Misc;
using Esiur.Data;
using Esiur.Core;
using System.Security.Cryptography;
using Esiur.Proxy;
using Esiur.Net.IIP;

namespace Esiur.Resource.Template
{
    //public enum TemplateType
    //{
    //    Resource,
    //    Record
    //}

    public class TypeTemplate
    {

        protected Guid classId;
        protected string className;
        protected List<MemberTemplate> members = new List<MemberTemplate>();
        protected List<FunctionTemplate> functions = new List<FunctionTemplate>();
        protected List<EventTemplate> events = new List<EventTemplate>();
        protected List<PropertyTemplate> properties = new List<PropertyTemplate>();
        protected List<AttributeTemplate> attributes = new List<AttributeTemplate>();
        protected int version;
        protected TemplateType templateType;

       // protected TemplateType
        //bool isReady;

        protected byte[] content;

        public byte[] Content
        {
            get { return content; }
        }

        public TemplateType Type => templateType;


        public Type DefinedType { get; set; }



        
        //public MemberTemplate GetMemberTemplate(MemberInfo member)
        //{
        //    if (member is MethodInfo)
        //        return GetFunctionTemplateByName(member.Name);
        //    else if (member is EventInfo)
        //        return GetEventTemplateByName(member.Name);
        //    else if (member is PropertyInfo)
        //        return GetPropertyTemplateByName(member.Name);
        //    else
        //        return null;
        //}

        public EventTemplate GetEventTemplateByName(string eventName)
        {
            foreach (var i in events)
                if (i.Name == eventName)
                    return i;
            return null;
        }

        public EventTemplate GetEventTemplateByIndex(byte index)
        {
            foreach (var i in events)
                if (i.Index == index)
                    return i;
            return null;
        }

        public FunctionTemplate GetFunctionTemplateByName(string functionName)
        {
            foreach (var i in functions)
                if (i.Name == functionName)
                    return i;
            return null;
        }
        public FunctionTemplate GetFunctionTemplateByIndex(byte index)
        {
            foreach (var i in functions)
                if (i.Index == index)
                    return i;
            return null;
        }

        public PropertyTemplate GetPropertyTemplateByIndex(byte index)
        {
            foreach (var i in properties)
                if (i.Index == index)
                    return i;
            return null;
        }

        public PropertyTemplate GetPropertyTemplateByName(string propertyName)
        {
            foreach (var i in properties)
                if (i.Name == propertyName)
                    return i;
            return null;
        }

        public AttributeTemplate GetAttributeTemplate(string attributeName)
        {
            foreach (var i in attributes)
                if (i.Name == attributeName)
                    return i;
            return null;
        }

        public Guid ClassId
        {
            get { return classId; }
        }
        public string ClassName
        {
            get { return className; }
        }

        public MemberTemplate[] Methods
        {
            get{return members.ToArray();}
        }

        public FunctionTemplate[] Functions
        {
            get { return functions.ToArray(); }
        }

        public EventTemplate[] Events
        {
            get { return events.ToArray(); }
        }

        public PropertyTemplate[] Properties
        {
            get { return properties.ToArray(); }
        }



        public TypeTemplate()
        {

        }


        public static Guid GetTypeGuid(Type type) => GetTypeGuid(type.FullName);

        public static Guid GetTypeGuid(string typeName)
        {
            var tn = Encoding.UTF8.GetBytes(typeName);
            var hash = SHA256.Create().ComputeHash(tn).Clip(0, 16);

            return new Guid(hash);
        }

        static Type GetElementType(Type type) => type switch
            {
                { IsArray: true } => type.GetElementType(),
                { IsEnum: true } => type.GetEnumUnderlyingType(),
                (_) => type
            };
        


        public static TypeTemplate[] GetDependencies(TypeTemplate template)
        {

            var list = new List<TypeTemplate>();

            list.Add(template);

            Action<TypeTemplate, List<TypeTemplate>> getDependenciesFunc = null;

            getDependenciesFunc = (TypeTemplate tmp, List<TypeTemplate> bag) =>
            {
                if (template.DefinedType == null)
                    return;

                // functions
                foreach (var f in tmp.functions)
                {
                    var frtt = Warehouse.GetTemplateByType(GetElementType(f.MethodInfo.ReturnType));
                    if (frtt != null)
                    {
                        if (!bag.Contains(frtt))
                        {
                            list.Add(frtt);
                            getDependenciesFunc(frtt, bag);
                        }
                    }

                    var args = f.MethodInfo.GetParameters();

                    for(var i = 0; i < args.Length - 1; i++)
                    {
                        var fpt = Warehouse.GetTemplateByType(GetElementType(args[i].ParameterType));
                        if (fpt != null)
                        {
                            if (!bag.Contains(fpt))
                            {
                                bag.Add(fpt);
                                getDependenciesFunc(fpt, bag);
                            }
                        }
                    }

                    // skip DistributedConnection argument
                    if (args.Length > 0)
                    {
                        var last = args.Last();
                        if (last.ParameterType != typeof(DistributedConnection))
                        {
                            var fpt = Warehouse.GetTemplateByType(GetElementType(last.ParameterType));
                            if (fpt != null)
                            {
                                if (!bag.Contains(fpt))
                                {
                                    bag.Add(fpt);
                                    getDependenciesFunc(fpt, bag);
                                }
                            }
                        }
                    }

                }

                // properties
                foreach (var p in tmp.properties)
                {
                    var pt = Warehouse.GetTemplateByType(GetElementType(p.PropertyInfo.PropertyType));
                    if (pt != null)
                    {
                        if (!bag.Contains(pt))
                        {
                            bag.Add(pt);
                            getDependenciesFunc(pt, bag);
                        }
                    }
                }

                // events
                foreach (var e in tmp.events)
                {
                    var et = Warehouse.GetTemplateByType(GetElementType(e.EventInfo.EventHandlerType.GenericTypeArguments[0]));

                    if (et != null)
                    {
                        if (!bag.Contains(et))
                        {
                            bag.Add(et);
                            getDependenciesFunc(et, bag);
                        }
                    }
                }
            };

            getDependenciesFunc(template, list);
            return list.ToArray();
        }

        public TypeTemplate(Type type, bool addToWarehouse = false)
        {
            if (Codec.InheritsClass(type, typeof(DistributedResource)))
                templateType = TemplateType.Wrapper;
            else if (Codec.ImplementsInterface(type, typeof(IResource)))
                templateType = TemplateType.Resource;
            else if (Codec.ImplementsInterface(type, typeof(IRecord)))
                templateType = TemplateType.Record;
            else
                throw new Exception("Type must implement IResource, IRecord or inherit from DistributedResource.");

            //if (isRecord && isResource)
            //  throw new Exception("Type can't have both IResource and IRecord interfaces");

            //if (!(isResource || isRecord))
            //  throw new Exception("Type is neither a resource nor a record.");

            type = ResourceProxy.GetBaseType(type);

            DefinedType = type;

            className = type.FullName;

            //Console.WriteLine($"Creating {className}");

            // set guid
            classId = GetTypeGuid(className);

            if (addToWarehouse)
                Warehouse.PutTemplate(this);

#if NETSTANDARD
            PropertyInfo[] propsInfo = type.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance);// | BindingFlags.DeclaredOnly);
            EventInfo[] eventsInfo = type.GetTypeInfo().GetEvents(BindingFlags.Public | BindingFlags.Instance);// | BindingFlags.DeclaredOnly);
            MethodInfo[] methodsInfo = type.GetTypeInfo().GetMethods(BindingFlags.Public | BindingFlags.Instance); // | BindingFlags.DeclaredOnly);

#else
            PropertyInfo[] propsInfo = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);// | BindingFlags.DeclaredOnly);
            EventInfo[] eventsInfo = type.GetEvents(BindingFlags.Public | BindingFlags.Instance);// | BindingFlags.DeclaredOnly);
            MethodInfo[] methodsInfo = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);// | BindingFlags.DeclaredOnly);
#endif


            bool classIsPublic = type.GetCustomAttribute<PublicAttribute>() != null;

            byte i = 0;

            if (classIsPublic)
            {
                foreach (var pi in propsInfo)
                {
                    var privateAttr = pi.GetCustomAttribute<PrivateAttribute>(true);

                    if (privateAttr == null)
                    {
                        var annotationAttr = pi.GetCustomAttribute<AnnotationAttribute>(true);
                        var storageAttr = pi.GetCustomAttribute<StorageAttribute>(true);
                        
                        var pt = new PropertyTemplate(this, i++, pi.Name, TemplateDataType.FromType(pi.PropertyType));

                        if (storageAttr != null)
                            pt.Recordable = storageAttr.Mode == StorageMode.Recordable;

                        if (annotationAttr != null)
                            pt.ReadExpansion = annotationAttr.Annotation;
                        else
                            pt.ReadExpansion = pi.PropertyType.Name;
 
                        pt.PropertyInfo = pi;
                        //pt.Serilize = publicAttr.Serialize;
                        properties.Add(pt);
                    }
                    else
                    {
                        var attributeAttr = pi.GetCustomAttribute<AttributeAttribute>(true);
                        if (attributeAttr != null)
                        {
                            var an = attributeAttr.Name ?? pi.Name;
                            var at = new AttributeTemplate(this, 0, an);
                            at.PropertyInfo = pi;
                            attributes.Add(at);
                        }
                    }
                }

                if (templateType == TemplateType.Resource)
                {
                    i = 0;

                    foreach (var ei in eventsInfo)
                    {
                        var privateAttr = ei.GetCustomAttribute<PrivateAttribute>(true);
                        if (privateAttr == null)
                        {
                            var annotationAttr = ei.GetCustomAttribute<AnnotationAttribute>(true);
                            var listenableAttr = ei.GetCustomAttribute<ListenableAttribute>(true);

                            var argType = ei.EventHandlerType.GenericTypeArguments[0];
                            var et = new EventTemplate(this, i++, ei.Name, TemplateDataType.FromType(argType));
                            et.EventInfo = ei;

                            if (annotationAttr != null)
                                et.Expansion = annotationAttr.Annotation;

                            if (listenableAttr != null)
                                et.Listenable = true;

                            events.Add(et);
                        }
                    }

                    i = 0;
                    foreach (MethodInfo mi in methodsInfo)
                    {
                        var privateAttr = mi.GetCustomAttribute<PrivateAttribute>(true);
                        if (privateAttr == null)
                        {
                            var annotationAttr = mi.GetCustomAttribute<AnnotationAttribute>(true);

                            var returnType = TemplateDataType.FromType(mi.ReturnType);

                            var args = mi.GetParameters();

                            if (args.Length > 0)
                            {
                                if (args.Last().ParameterType == typeof(DistributedConnection))
                                    args = args.Take(args.Count() - 1).ToArray();
                            }

                            var arguments = args.Select(x => new ArgumentTemplate()
                            {
                                Name = x.Name,
                                Type = TemplateDataType.FromType(x.ParameterType),
                                ParameterInfo = x
                            }).ToArray();

                            var ft = new FunctionTemplate(this, i++, mi.Name, arguments, returnType);// mi.ReturnType == typeof(void));

                            if (annotationAttr != null)
                                ft.Expansion = annotationAttr.Annotation;
                            else
                                ft.Expansion = "(" + String.Join(",", mi.GetParameters().Where(x => x.ParameterType != typeof(DistributedConnection)).Select(x => "[" + x.ParameterType.Name + "] " + x.Name)) + ") -> " + mi.ReturnType.Name;

                            ft.MethodInfo = mi;
                            functions.Add(ft);
                        }
                    }
                }
            }
            else
            {

                foreach (var pi in propsInfo)
                {
                    var publicAttr = pi.GetCustomAttribute<PublicAttribute>(true);

                    if (publicAttr != null)
                    {
                        var annotationAttr = pi.GetCustomAttribute<AnnotationAttribute>(true);
                        var storageAttr = pi.GetCustomAttribute<StorageAttribute>(true);
                        var valueType = TemplateDataType.FromType(pi.PropertyType);
                        var pn = publicAttr.Name ?? pi.Name;

                        var pt = new PropertyTemplate(this, i++, pn, valueType);//, rp.ReadExpansion, rp.WriteExpansion, rp.Storage);
                        if (storageAttr != null)
                            pt.Recordable = storageAttr.Mode == StorageMode.Recordable;
                        
                        if (annotationAttr != null)
                            pt.ReadExpansion = annotationAttr.Annotation;
                        else
                            pt.ReadExpansion = pi.PropertyType.Name;

                        pt.PropertyInfo = pi;
                        //pt.Serilize = publicAttr.Serialize;
                        properties.Add(pt);
                    }
                    else
                    {
                        var attributeAttr = pi.GetCustomAttribute<AttributeAttribute>(true);
                        if (attributeAttr != null)
                        {
                            var pn = attributeAttr.Name ?? pi.Name;
                            var at = new AttributeTemplate(this, 0, pn);
                            at.PropertyInfo = pi;
                            attributes.Add(at);
                        }
                    }
                }

                if (templateType == TemplateType.Resource)
                {
                    i = 0;

                    foreach (var ei in eventsInfo)
                    {
                        var publicAttr = ei.GetCustomAttribute<PublicAttribute>(true);
                        if (publicAttr != null)
                        {
                            var annotationAttr = ei.GetCustomAttribute<AnnotationAttribute>(true);
                            var listenableAttr = ei.GetCustomAttribute<ListenableAttribute>(true);

                            var argType = ei.EventHandlerType.GenericTypeArguments[0];

                            var en = publicAttr.Name ?? ei.Name;

                            var et = new EventTemplate(this, i++, en, TemplateDataType.FromType(argType));
                            et.EventInfo = ei;

                            if (annotationAttr != null)
                                et.Expansion = annotationAttr.Annotation;

                            if (listenableAttr != null)
                                et.Listenable = true;

                            events.Add(et);
                        }
                    }

                    i = 0;
                    foreach (MethodInfo mi in methodsInfo)
                    {
                        var publicAttr = mi.GetCustomAttribute<PublicAttribute>(true);
                        if (publicAttr != null)
                        {
                            var annotationAttr = mi.GetCustomAttribute<AnnotationAttribute>(true);
                            var returnType = TemplateDataType.FromType(mi.ReturnType);

                            var args = mi.GetParameters();

                            if (args.Length > 0)
                            {
                                if (args.Last().ParameterType == typeof(DistributedConnection))
                                    args = args.Take(args.Count() - 1).ToArray();
                            }

                            var arguments = args.Select(x => new ArgumentTemplate()
                            {
                                Name = x.Name,
                                Type = TemplateDataType.FromType(x.ParameterType),
                                ParameterInfo = x
                            })
                                             .ToArray();

                            var fn = publicAttr.Name ?? mi.Name;

                            var ft = new FunctionTemplate(this, i++, fn, arguments, returnType);// mi.ReturnType == typeof(void));

                            if (annotationAttr != null)
                                ft.Expansion = annotationAttr.Annotation;
                            else
                                ft.Expansion = "(" + String.Join(",", mi.GetParameters().Where(x => x.ParameterType != typeof(DistributedConnection)).Select(x => "[" + x.ParameterType.Name + "] " + x.Name)) + ") -> " + mi.ReturnType.Name;

                            ft.MethodInfo = mi;
                            functions.Add(ft);
                        }
                    }
                }
            }

            // append signals
            for (i = 0; i < events.Count; i++)
                members.Add(events[i]);
            // append slots
            for (i = 0; i < functions.Count; i++)
                members.Add(functions[i]);
            // append properties
            for (i = 0; i < properties.Count; i++)
                members.Add(properties[i]);

            // bake it binarily
            var b = new BinaryList();
            b.AddUInt8((byte)templateType)
             .AddGuid(classId)
             .AddUInt8((byte)className.Length)
             .AddString(className)
             .AddInt32(version)
             .AddUInt16((ushort)members.Count);


            foreach (var ft in functions)
                b.AddUInt8Array(ft.Compose());
            foreach (var pt in properties)
                b.AddUInt8Array(pt.Compose());
            foreach (var et in events)
                b.AddUInt8Array(et.Compose());

            content = b.ToArray();

        }

        public static TypeTemplate Parse(byte[] data)
        {
            return Parse(data, 0, (uint)data.Length);
        }

        
        public static TypeTemplate Parse(byte[] data, uint offset, uint contentLength)
        {

            uint ends = offset + contentLength;

            uint oOffset = offset;

            // start parsing...
           
            var od = new TypeTemplate();
            od.content = data.Clip(offset, contentLength);

            od.templateType = (TemplateType)data[offset++];

            od.classId = data.GetGuid(offset);
            offset += 16;
            od.className = data.GetString(offset + 1, data[offset]);
            offset += (uint)data[offset] + 1;

            od.version = data.GetInt32(offset);
            offset += 4;

            ushort methodsCount = data.GetUInt16(offset);
            offset += 2;
                
            byte functionIndex = 0;
            byte propertyIndex = 0;
            byte eventIndex = 0;

            for (int i = 0; i < methodsCount; i++)
            {
                var type = data[offset] >> 5;

                if (type == 0) // function
                {
                    string expansion = null;
                    var hasExpansion = ((data[offset++] & 0x10) == 0x10);

                    var name = data.GetString(offset + 1, data[offset]);
                    offset += (uint)data[offset] + 1;

                    // return type
                    var (rts, returnType) = TemplateDataType.Parse(data, offset);
                    offset += rts;

                    // arguments count
                    var argsCount = data[offset++];
                    List<ArgumentTemplate> arguments = new();

                    for (var a = 0; a < argsCount; a++)
                    {
                        var (cs, argType) = ArgumentTemplate.Parse(data, offset);
                        arguments.Add(argType);
                        offset += cs;
                    }

                    // arguments
                    if (hasExpansion) // expansion ?
                    {
                        var cs = data.GetUInt32(offset);
                        offset += 4;
                        expansion = data.GetString(offset, cs);  
                        offset += cs;
                    }

                    var ft = new FunctionTemplate(od, functionIndex++, name, arguments.ToArray(), returnType, expansion);

                    od.functions.Add(ft);
                }
                else if (type == 1)    // property
                {

                    string readExpansion = null, writeExpansion = null;

                    var hasReadExpansion = ((data[offset] & 0x8) == 0x8);
                    var hasWriteExpansion = ((data[offset] & 0x10) == 0x10);
                    var recordable = ((data[offset] & 1) == 1);
                    var permission = (PropertyTemplate.PropertyPermission)((data[offset++] >> 1) & 0x3);
                    var name = data.GetString(offset + 1, data[offset]);// Encoding.ASCII.GetString(data, (int)offset + 1, data[offset]);
                    
                    offset += (uint)data[offset] + 1;

                    var (dts, valueType) = TemplateDataType.Parse(data, offset);

                    offset += dts;

                    if (hasReadExpansion) // expansion ?
                    {
                        var cs = data.GetUInt32(offset);
                        offset += 4;
                        readExpansion = data.GetString(offset, cs);
                        offset += cs;
                    }

                    if (hasWriteExpansion) // expansion ?
                    {
                        var cs = data.GetUInt32(offset);
                        offset += 4;
                        writeExpansion = data.GetString(offset, cs);
                        offset += cs;
                    }

                    var pt = new PropertyTemplate(od, propertyIndex++, name, valueType, readExpansion, writeExpansion, recordable);

                    od.properties.Add(pt);
                }
                else if (type == 2) // Event
                {

                    string expansion = null;
                    var hasExpansion = ((data[offset] & 0x10) == 0x10);
                    var listenable = ((data[offset++] & 0x8) == 0x8);

                    var name = data.GetString(offset + 1, data[offset]);// Encoding.ASCII.GetString(data, (int)offset + 1, (int)data[offset]);
                    offset += (uint)data[offset] + 1;

                    var (dts, argType) = TemplateDataType.Parse(data, offset);
                    
                    offset += dts;

                    if (hasExpansion) // expansion ?
                    {
                        var cs = data.GetUInt32(offset);
                        offset += 4;
                        expansion = data.GetString(offset, cs);
                        offset += cs;
                    }

                    var et = new EventTemplate(od, eventIndex++, name, argType, expansion, listenable);

                    od.events.Add(et);

                }
            }

            // append signals
            for (int i = 0; i < od.events.Count; i++)
                od.members.Add(od.events[i]);
            // append slots
            for (int i = 0; i < od.functions.Count; i++)
                od.members.Add(od.functions[i]);
            // append properties
            for (int i = 0; i < od.properties.Count; i++)
                od.members.Add(od.properties[i]);


            //od.isReady = true;
            /*
            var oo = owner.Socket.Engine.GetObjectDescription(od.GUID);
            if (oo != null)
            {
                Console.WriteLine("Already there ! description");
                return oo;
            }
            else
            {
                owner.Socket.Engine.AddObjectDescription(od);
                return od;
            }
            */

            return od;
        }
    }

}
