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
using System.Runtime.CompilerServices;

namespace Esiur.Resource.Template;

//public enum TemplateType
//{
//    Resource,
//    Record
//}

public class TypeTemplate
{

    protected Guid classId;
    protected Guid? parentId;

    public string Annotation { get; set; }

    string className;
    List<MemberTemplate> members = new List<MemberTemplate>();
    List<FunctionTemplate> functions = new List<FunctionTemplate>();
    List<EventTemplate> events = new List<EventTemplate>();
    List<PropertyTemplate> properties = new List<PropertyTemplate>();
    List<AttributeTemplate> attributes = new List<AttributeTemplate>();
    List<ConstantTemplate> constants = new();
    int version;
    TemplateType templateType;


    // protected TemplateType
    //bool isReady;

    protected byte[] content;

    public Guid? ParentId => parentId;

    public byte[] Content
    {
        get { return content; }
    }

    public TemplateType Type => templateType;


    public Type DefinedType { get; set; }
    public Type ParentDefinedType { get; set; }

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
        get { return members.ToArray(); }
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

    public ConstantTemplate[] Constants => constants.ToArray();

    public TypeTemplate()
    {

    }

    public static Guid GetTypeGuid(Type type) => GetTypeGuid(GetTypeClassName(type));

    public static Guid GetTypeGuid(string typeName)
    {
        var tn = Encoding.UTF8.GetBytes(typeName);
        var hash = SHA256.Create().ComputeHash(tn).Clip(0, 16);

        return new Guid(hash);
    }

    static Type[] GetDistributedTypes(Type type)
    {
        if (type.IsArray)
            return GetDistributedTypes(type.GetElementType());
        else if (type.IsEnum)
            return new Type[] { type };
        else if (Codec.ImplementsInterface(type, typeof(IRecord))
                || Codec.ImplementsInterface(type, typeof(IResource)))
        {
            return new Type[] { type };
        }
        else if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            var genericTypeArgs = type.GetGenericArguments();

            if (genericType == typeof(List<>)
                || genericType == typeof(DistributedPropertyContext<>))
            {
                return GetDistributedTypes(genericTypeArgs[0]);
            }
            else if (genericType == typeof(Tuple<>)
                  || genericType == typeof(Map<,>))
            {
                var rt = new List<Type>();
                for (var i = 0; i < genericTypeArgs.Length; i++)
                {
                    var depTypes = GetDistributedTypes(genericTypeArgs[i]);
                    foreach (var depType in depTypes)
                        if (!rt.Contains(depType))
                            rt.Add(depType);
                }

                return rt.ToArray();
            }
        }


        return new Type[0];
    }


    public static TypeTemplate[] GetDependencies(TypeTemplate template)
    {

        var list = new List<TypeTemplate>();

        // Add self
        list.Add(template);


        Action<TypeTemplate, List<TypeTemplate>> getDependenciesFunc = null;

        getDependenciesFunc = (TypeTemplate tmp, List<TypeTemplate> bag) =>
        {
            if (template.DefinedType == null)
                return;

            // Add parents
            var parentType = tmp.ParentDefinedType;

            // Get parents
            while (parentType != null)
            {
                var parentTemplate = Warehouse.GetTemplateByType(parentType);
                if (parentTemplate != null)
                {
                    list.Add(parentTemplate);
                    parentType = parentTemplate.ParentDefinedType;
                }
            }

            // functions
            foreach (var f in tmp.functions)
            {
                var functionReturnTypes = GetDistributedTypes(f.MethodInfo.ReturnType);
                //.Select(x => Warehouse.GetTemplateByType(x))
                //.Where(x => x != null && !bag.Contains(x))

                foreach (var functionReturnType in functionReturnTypes)
                {
                    var functionReturnTemplate = Warehouse.GetTemplateByType(functionReturnType);
                    if (functionReturnTemplate != null)
                    {
                        if (!bag.Contains(functionReturnTemplate))
                        {
                            list.Add(functionReturnTemplate);
                            getDependenciesFunc(functionReturnTemplate, bag);
                        }
                    }
                }

                var args = f.MethodInfo.GetParameters();

                for (var i = 0; i < args.Length - 1; i++)
                {
                    var fpTypes = GetDistributedTypes(args[i].ParameterType);

                    foreach (var fpType in fpTypes)
                    {
                        var fpt = Warehouse.GetTemplateByType(fpType);
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

                // skip DistributedConnection argument
                if (args.Length > 0)
                {
                    var last = args.Last();
                    if (last.ParameterType != typeof(DistributedConnection))
                    {

                        var fpTypes = GetDistributedTypes(last.ParameterType);

                        foreach (var fpType in fpTypes)
                        {
                            var fpt = Warehouse.GetTemplateByType(fpType);
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

            }

            // properties
            foreach (var p in tmp.properties)
            {
                var propertyTypes = GetDistributedTypes(p.PropertyInfo.PropertyType);

                foreach (var propertyType in propertyTypes)
                {
                    var propertyTemplate = Warehouse.GetTemplateByType(propertyType);
                    if (propertyTemplate != null)
                    {
                        if (!bag.Contains(propertyTemplate))
                        {
                            bag.Add(propertyTemplate);
                            getDependenciesFunc(propertyTemplate, bag);
                        }
                    }
                }
            }

            // events
            foreach (var e in tmp.events)
            {
                var eventTypes = GetDistributedTypes(e.EventInfo.EventHandlerType.GenericTypeArguments[0]);

                foreach (var eventType in eventTypes)
                {
                    var eventTemplate = Warehouse.GetTemplateByType(eventType);

                    if (eventTemplate != null)
                    {
                        if (!bag.Contains(eventTemplate))
                        {
                            bag.Add(eventTemplate);
                            getDependenciesFunc(eventTemplate, bag);
                        }
                    }
                }
            }
        };

        getDependenciesFunc(template, list);
        return list.ToArray();
    }

    public string GetTypeAnnotationName(Type type)
    {
        var nullType = Nullable.GetUnderlyingType(type);
        if (nullType == null)
            return type.Name;
        else
            return type.Name + "?";
    }

    public static string GetTypeClassName(Type type, string separator = ".")
    {

        if (type.IsGenericType)
        {
            var index = type.Name.IndexOf("`");
            var name = $"{type.Namespace}{separator}{((index > -1) ? type.Name.Substring(0, index) : type.Name)}Of";
            foreach (var t in type.GenericTypeArguments)
                name += GetTypeClassName(t, "_");

            return name;
        }
        else
            return $"{type.Namespace}{separator}{type.Name}";
    }

    public TypeTemplate(Type type, bool addToWarehouse = false)
    {
        if (Codec.InheritsClass(type, typeof(DistributedResource)))
            templateType = TemplateType.Wrapper;
        else if (Codec.ImplementsInterface(type, typeof(IResource)))
            templateType = TemplateType.Resource;
        else if (Codec.ImplementsInterface(type, typeof(IRecord)))
            templateType = TemplateType.Record;
        else if (type.IsEnum)
            templateType = TemplateType.Enum;
        else
            throw new Exception("Type must implement IResource, IRecord or inherit from DistributedResource.");

        //if (isRecord && isResource)
        //  throw new Exception("Type can't have both IResource and IRecord interfaces");

        //if (!(isResource || isRecord))
        //  throw new Exception("Type is neither a resource nor a record.");

        type = ResourceProxy.GetBaseType(type);

        DefinedType = type;

        className = GetTypeClassName(type);

        // set guid
        classId = GetTypeGuid(className);

        if (addToWarehouse)
            Warehouse.PutTemplate(this);



        PropertyInfo[] propsInfo = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);// | BindingFlags.DeclaredOnly);
        EventInfo[] eventsInfo = type.GetEvents(BindingFlags.Public | BindingFlags.Instance);// | BindingFlags.DeclaredOnly);
        MethodInfo[] methodsInfo = type.GetMethods(BindingFlags.Public | BindingFlags.Instance); // | BindingFlags.DeclaredOnly);
        FieldInfo[] constantsInfo = type.GetFields(BindingFlags.Public | BindingFlags.Static);


        bool classIsPublic = type.IsEnum || (type.GetCustomAttribute<PublicAttribute>() != null);

        var addConstant = (FieldInfo ci, PublicAttribute publicAttr) =>
        {

            var annotationAttr = ci.GetCustomAttribute<AnnotationAttribute>(true);
            var nullableAttr = ci.GetCustomAttribute<NullableAttribute>(true);

            var valueType = RepresentationType.FromType(ci.FieldType);//, nullable != null && nullable.NullableFlags[0] == 2);

            if (valueType == null)
                throw new Exception($"Unsupported type `{ci.FieldType}` in constant `{type.Name}.{ci.Name}`");

            var value = ci.GetValue(null);

            if (templateType == TemplateType.Enum)
                value = Convert.ChangeType(value, ci.FieldType.GetEnumUnderlyingType());

            var ct = new ConstantTemplate(this, (byte)constants.Count, publicAttr?.Name ?? ci.Name, ci.DeclaringType != type, valueType, value, annotationAttr?.Annotation);

            constants.Add(ct);
        };

        var addProperty = (PropertyInfo pi, PublicAttribute publicAttr) =>
        {

            var genericPropType = pi.PropertyType.IsGenericType ? pi.PropertyType.GetGenericTypeDefinition() : null;

            var propType = genericPropType == typeof(DistributedPropertyContext<>) ?
                    RepresentationType.FromType(pi.PropertyType.GetGenericArguments()[0]) :
                    RepresentationType.FromType(pi.PropertyType);

            //var propType = RepresentationType.FromType(pi.PropertyType);//, nullableAttr != null && nullableAttr.Flag == 2);

            if (propType == null)
                throw new Exception($"Unsupported type `{pi.PropertyType}` in property `{type.Name}.{pi.Name}`");

            var annotationAttr = pi.GetCustomAttribute<AnnotationAttribute>(true);
            var storageAttr = pi.GetCustomAttribute<StorageAttribute>(true);

            var nullableContextAttr = pi.GetCustomAttribute<NullableContextAttribute>(true);
            var nullableAttr = pi.GetCustomAttribute<NullableAttribute>(true);

            var flags = nullableAttr?.Flags?.ToList() ?? new List<byte>();

            if (flags.Count > 0 && genericPropType == typeof(DistributedPropertyContext<>))
                flags.RemoveAt(0);

            if (nullableContextAttr?.Flag == 2)
            {
                if (flags.Count == 1)
                    propType.SetNotNull(flags.FirstOrDefault());
                else
                    propType.SetNotNull(flags);
            }
            else
            {
                if (flags.Count == 1)
                    propType.SetNull(flags.FirstOrDefault());
                else
                    propType.SetNull(flags);
            }

            var pt = new PropertyTemplate(this, (byte)properties.Count, publicAttr?.Name ?? pi.Name, pi.DeclaringType != type, propType);

            if (storageAttr != null)
                pt.Recordable = storageAttr.Mode == StorageMode.Recordable;

            if (annotationAttr != null)
                pt.ReadAnnotation = annotationAttr.Annotation;
            else
                pt.ReadAnnotation = GetTypeAnnotationName(pi.PropertyType);

            pt.PropertyInfo = pi;

            properties.Add(pt);

        };

        var addEvent = (EventInfo ei, PublicAttribute publicAttr) =>
        {
            var argType = ei.EventHandlerType.GenericTypeArguments[0];
            var evtType = RepresentationType.FromType(argType);//, argIsNull);

            if (evtType == null)
                throw new Exception($"Unsupported type `{argType}` in event `{type.Name}.{ei.Name}`");

            var annotationAttr = ei.GetCustomAttribute<AnnotationAttribute>(true);
            var listenableAttr = ei.GetCustomAttribute<ListenableAttribute>(true);
            var nullableAttr = ei.GetCustomAttribute<NullableAttribute>(true);
            var nullableContextAttr = ei.GetCustomAttribute<NullableContextAttribute>(true);

            var flags = nullableAttr?.Flags?.ToList() ?? new List<byte>();

            // skip the eventHandler class
            if (flags.Count > 1)
                flags = flags.Skip(1).ToList();

            if (nullableContextAttr?.Flag == 2)
            {
                if (flags.Count == 1)
                    evtType.SetNotNull(flags.FirstOrDefault());
                else
                    evtType.SetNotNull(flags);
            }
            else
            {
                if (flags.Count == 1)
                    evtType.SetNull(flags.FirstOrDefault());
                else
                    evtType.SetNull(flags);
            }

            var et = new EventTemplate(this, (byte)events.Count, publicAttr?.Name ?? ei.Name, ei.DeclaringType != type, evtType);
            et.EventInfo = ei;

            if (annotationAttr != null)
                et.Annotation = annotationAttr.Annotation;

            if (listenableAttr != null)
                et.Listenable = true;

            events.Add(et);
        };

        var addAttribute = (PropertyInfo pi, AttributeAttribute attributeAttr) =>
        {
            var an = attributeAttr.Name ?? pi.Name;
            var at = new AttributeTemplate(this, 0, an, pi.DeclaringType != type);
            at.PropertyInfo = pi;
            attributes.Add(at);
        };


        var addFunction = (MethodInfo mi, PublicAttribute publicAttr) =>
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

            var fn = publicAttr.Name ?? mi.Name;

            var ft = new FunctionTemplate(this, (byte)functions.Count, fn, mi.DeclaringType != type, arguments, rtType);

            if (annotationAttr != null)
                ft.Annotation = annotationAttr.Annotation;
            else
                ft.Annotation = "(" + String.Join(",", mi.GetParameters().Where(x => x.ParameterType != typeof(DistributedConnection)).Select(x => "[" + x.ParameterType.Name + "] " + x.Name)) + ") -> " + mi.ReturnType.Name;

            ft.MethodInfo = mi;
            functions.Add(ft);

        };



        if (classIsPublic)
        {

            foreach (var ci in constantsInfo)
            {
                var privateAttr = ci.GetCustomAttribute<PrivateAttribute>(true);

                if (privateAttr != null)
                    continue;

                var publicAttr = ci.GetCustomAttribute<PublicAttribute>(true);

                addConstant(ci, publicAttr);
            }


            foreach (var pi in propsInfo)
            {
                var privateAttr = pi.GetCustomAttribute<PrivateAttribute>(true);

                if (privateAttr == null)
                {
                    var publicAttr = pi.GetCustomAttribute<PublicAttribute>(true);
                    addProperty(pi, publicAttr);
                }
                else
                {
                    var attributeAttr = pi.GetCustomAttribute<AttributeAttribute>(true);
                    if (attributeAttr != null)
                    {
                        addAttribute(pi, attributeAttr);
                    }
                }
            }

            if (templateType == TemplateType.Resource
                || templateType == TemplateType.Wrapper)
            {

                foreach (var ei in eventsInfo)
                {
                    var privateAttr = ei.GetCustomAttribute<PrivateAttribute>(true);
                    if (privateAttr != null)
                        continue;

                    var publicAttr = ei.GetCustomAttribute<PublicAttribute>(true);

                    addEvent(ei, publicAttr);
                }

                foreach (MethodInfo mi in methodsInfo)
                {
                    var privateAttr = mi.GetCustomAttribute<PrivateAttribute>(true);
                    if (privateAttr != null)
                        continue;

                    var publicAttr = mi.GetCustomAttribute<PublicAttribute>(true);
                    addFunction(mi, publicAttr);
                }

            }
        }
        else
        {
            foreach (var ci in constantsInfo)
            {
                var publicAttr = ci.GetCustomAttribute<PublicAttribute>(true);

                if (publicAttr == null)
                    continue;

                addConstant(ci, publicAttr);
            }


            foreach (var pi in propsInfo)
            {
                var publicAttr = pi.GetCustomAttribute<PublicAttribute>(true);

                if (publicAttr != null)
                {
                    addProperty(pi, publicAttr);
                }
                else
                {
                    var attributeAttr = pi.GetCustomAttribute<AttributeAttribute>(true);
                    if (attributeAttr != null)
                    {
                        addAttribute(pi, attributeAttr);
                    }
                }
            }

            if (templateType == TemplateType.Resource
                || templateType == TemplateType.Wrapper)
            {

                foreach (var ei in eventsInfo)
                {
                    var publicAttr = ei.GetCustomAttribute<PublicAttribute>(true);

                    if (publicAttr == null)
                        continue;

                    addEvent(ei, publicAttr);
                }

                foreach (MethodInfo mi in methodsInfo)
                {
                    var publicAttr = mi.GetCustomAttribute<PublicAttribute>(true);
                    if (publicAttr == null)
                        continue;

                    addFunction(mi, publicAttr);
                }
            }
        }

        // append signals
        for (var i = 0; i < events.Count; i++)
            members.Add(events[i]);
        // append slots
        for (var i = 0; i < functions.Count; i++)
            members.Add(functions[i]);
        // append properties
        for (var i = 0; i < properties.Count; i++)
            members.Add(properties[i]);

        // append constants
        for (var i = 0; i < constants.Count; i++)
            members.Add(constants[i]);

        // bake it binarily
        var b = new BinaryList();

        // find the first parent type that implements IResource


        var hasParent = HasParent(type);
        var classAnnotation = type.GetCustomAttribute<AnnotationAttribute>(false);
        var hasClassAnnotation = classAnnotation != null && classAnnotation.Annotation != null;

        var classNameBytes = DC.ToBytes(className);

        b.AddUInt8((byte)((hasParent ? 0x80 : 0) | (hasClassAnnotation ? 0x40 : 0x0) | (byte)templateType))
         .AddGuid(classId)
         .AddUInt8((byte)classNameBytes.Length)
         .AddUInt8Array(classNameBytes);

        if (hasParent)
        {
            // find the first parent type that implements IResource
            ParentDefinedType = ResourceProxy.GetBaseType(type.BaseType);
            var parentId = GetTypeGuid(ParentDefinedType);
            b.AddGuid(parentId);
        }

        if (hasClassAnnotation)
        {
            var classAnnotationBytes = DC.ToBytes(classAnnotation.Annotation);
            b.AddUInt16((ushort)classAnnotationBytes.Length)
             .AddUInt8Array(classAnnotationBytes);

            Annotation = classAnnotation.Annotation;
        }

        b.AddInt32(version)
         .AddUInt16((ushort)members.Count);

        foreach (var ft in functions)
            b.AddUInt8Array(ft.Compose());
        foreach (var pt in properties)
            b.AddUInt8Array(pt.Compose());
        foreach (var et in events)
            b.AddUInt8Array(et.Compose());
        foreach (var ct in constants)
            b.AddUInt8Array(ct.Compose());

        content = b.ToArray();

    }

    public static bool HasParent(Type type)
    {
        var parent = type.BaseType;

        if (parent == typeof(Resource)
            || parent == typeof(Record)
            || parent == typeof(EntryPoint))
            return false;

        while (parent != null)
        {
            if (parent.GetInterfaces().Contains(typeof(IResource))
                || parent.GetInterfaces().Contains(typeof(IRecord)))
                return true;
            parent = parent.BaseType;
        }

        return false;
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

        var hasParent = (data[offset] & 0x80) > 0;
        var hasClassAnnotation = (data[offset] & 0x40) > 0;

        od.templateType = (TemplateType)(data[offset++] & 0xF);

        od.classId = data.GetGuid(offset);
        offset += 16;
        od.className = data.GetString(offset + 1, data[offset]);
        offset += (uint)data[offset] + 1;


        if (hasParent)
        {
            od.parentId = data.GetGuid(offset);
            offset += 16;
        }

        if (hasClassAnnotation)
        {
            var len = data.GetUInt16(offset, Endian.Little);
            offset += 2;
            od.Annotation = data.GetString(offset, len);
            offset += len;
        }

        od.version = data.GetInt32(offset, Endian.Little);
        offset += 4;

        ushort methodsCount = data.GetUInt16(offset, Endian.Little);
        offset += 2;

        byte functionIndex = 0;
        byte propertyIndex = 0;
        byte eventIndex = 0;

        for (int i = 0; i < methodsCount; i++)
        {
            var inherited = (data[offset] & 0x80) > 0;
            var type = (data[offset] >> 5) & 0x3;

            if (type == 0) // function
            {
                string annotation = null;
                var hasAnnotation = ((data[offset++] & 0x10) == 0x10);

                var name = data.GetString(offset + 1, data[offset]);
                offset += (uint)data[offset] + 1;

                // return type
                var (rts, returnType) = RepresentationType.Parse(data, offset);
                offset += rts;

                // arguments count
                var argsCount = data[offset++];
                List<ArgumentTemplate> arguments = new();

                for (var a = 0; a < argsCount; a++)
                {
                    var (cs, argType) = ArgumentTemplate.Parse(data, offset, a);
                    arguments.Add(argType);
                    offset += cs;
                }

                // arguments
                if (hasAnnotation) // Annotation ?
                {
                    var cs = data.GetUInt32(offset, Endian.Little);
                    offset += 4;
                    annotation = data.GetString(offset, cs);
                    offset += cs;
                }

                var ft = new FunctionTemplate(od, functionIndex++, name, inherited, arguments.ToArray(), returnType, annotation);

                od.functions.Add(ft);
            }
            else if (type == 1)    // property
            {

                string readAnnotation = null, writeAnnotation = null;

                var hasReadAnnotation = ((data[offset] & 0x8) == 0x8);
                var hasWriteAnnotation = ((data[offset] & 0x10) == 0x10);
                var recordable = ((data[offset] & 1) == 1);
                var permission = (PropertyTemplate.PropertyPermission)((data[offset++] >> 1) & 0x3);
                var name = data.GetString(offset + 1, data[offset]);// Encoding.ASCII.GetString(data, (int)offset + 1, data[offset]);

                offset += (uint)data[offset] + 1;

                var (dts, valueType) = RepresentationType.Parse(data, offset);

                offset += dts;

                if (hasReadAnnotation) // annotation ?
                {
                    var cs = data.GetUInt32(offset, Endian.Little);
                    offset += 4;
                    readAnnotation = data.GetString(offset, cs);
                    offset += cs;
                }

                if (hasWriteAnnotation) // annotation ?
                {
                    var cs = data.GetUInt32(offset, Endian.Little);
                    offset += 4;
                    writeAnnotation = data.GetString(offset, cs);
                    offset += cs;
                }

                var pt = new PropertyTemplate(od, propertyIndex++, name, inherited, valueType, readAnnotation, writeAnnotation, recordable);

                od.properties.Add(pt);
            }
            else if (type == 2) // Event
            {

                string annotation = null;
                var hasAnnotation = ((data[offset] & 0x10) == 0x10);
                var listenable = ((data[offset++] & 0x8) == 0x8);

                var name = data.GetString(offset + 1, data[offset]);// Encoding.ASCII.GetString(data, (int)offset + 1, (int)data[offset]);
                offset += (uint)data[offset] + 1;

                var (dts, argType) = RepresentationType.Parse(data, offset);

                offset += dts;

                if (hasAnnotation) // annotation ?
                {
                    var cs = data.GetUInt32(offset, Endian.Little);
                    offset += 4;
                    annotation = data.GetString(offset, cs);
                    offset += cs;
                }

                var et = new EventTemplate(od, eventIndex++, name, inherited, argType, annotation, listenable);

                od.events.Add(et);

            }
            // constant
            else if (type == 3)
            {
                string annotation = null;
                var hasAnnotation = ((data[offset++] & 0x10) == 0x10);

                var name = data.GetString(offset + 1, data[offset]);
                offset += (uint)data[offset] + 1;

                var (dts, valueType) = RepresentationType.Parse(data, offset);

                offset += dts;

                (dts, var value) = Codec.Parse(data, offset, null, null);

                offset += dts;

                if (hasAnnotation) // annotation ?
                {
                    var cs = data.GetUInt32(offset, Endian.Little);
                    offset += 4;
                    annotation = data.GetString(offset, cs);
                    offset += cs;
                }

                var ct = new ConstantTemplate(od, eventIndex++, name, inherited, valueType, value.Result, annotation);

                od.constants.Add(ct);
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
        // append constants
        for (int i = 0; i < od.constants.Count; i++)
            od.members.Add(od.constants[i]);

        return od;
    }
}

