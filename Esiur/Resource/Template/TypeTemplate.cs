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

    protected UUID classId;
    protected UUID? parentId;

    public Map<string, string> Annotations { get; set; }

    string className;
    List<FunctionTemplate> functions = new List<FunctionTemplate>();
    List<EventTemplate> events = new List<EventTemplate>();
    List<PropertyTemplate> properties = new List<PropertyTemplate>();
    List<AttributeTemplate> attributes = new List<AttributeTemplate>();
    List<ConstantTemplate> constants = new();
    int version;
    TemplateType templateType;


    public override string ToString()
    {
        return className;
    }

    // protected TemplateType
    //bool isReady;

    protected byte[] content;

    public UUID? ParentId => parentId;

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

    public UUID ClassId
    {
        get { return classId; }
    }
    public string ClassName
    {
        get { return className; }
    }

    //public MemberTemplate[] Methods
    //{
    //    get { return members.ToArray(); }
    //}

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

    public static UUID GetTypeUUID(Type type)
    {
        var attr = type.GetCustomAttribute<ClassIdAttribute>();
        if (attr != null)
            return attr.ClassId;

        var tn = Encoding.UTF8.GetBytes(GetTypeClassName(type));
        var hash = SHA256.Create().ComputeHash(tn).Clip(0, 16);
        hash[6] = (byte)((hash[6] & 0xF) | 0x80);
        hash[8] = (byte)((hash[8] & 0xF) | 0x80);

        var rt = new UUID(hash);
        return rt;
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
                || genericType == typeof(PropertyContext<>)
                || genericType == typeof(AsyncReply<>)
                || genericType == typeof(ResourceLink<>))
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


    public static TypeTemplate[] GetDependencies(TypeTemplate template, Warehouse warehouse)
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
                var parentTemplate = warehouse.GetTemplateByType(parentType);
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
                    var functionReturnTemplate = warehouse.GetTemplateByType(functionReturnType);
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
                        var fpt = warehouse.GetTemplateByType(fpType);
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
                            var fpt = warehouse.GetTemplateByType(fpType);
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
                    var propertyTemplate = warehouse.GetTemplateByType(propertyType);
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
                    var eventTemplate = warehouse.GetTemplateByType(eventType);

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
        return list.Distinct().ToArray();
    }


    public static string GetTypeClassName(Type type, char separator = '.')
    {

        if (type.IsGenericType)
        {
            var index = type.Name.IndexOf("`");
            var name = $"{type.Namespace}{separator}{((index > -1) ? type.Name.Substring(0, index) : type.Name)}Of";
            foreach (var t in type.GenericTypeArguments)
                name += GetTypeClassName(t, '_');

            return name;
        }
        else
            return $"{type.Namespace?.Replace('.', separator) ?? "Global"}{separator}{type.Name}";
    }




 

    public bool IsWrapper { get; private set; }

    public TypeTemplate(Type type, Warehouse warehouse = null)
    {
        if (Codec.ImplementsInterface(type, typeof(IResource)))
            templateType = TemplateType.Resource;
        else if (Codec.ImplementsInterface(type, typeof(IRecord)))
            templateType = TemplateType.Record;
        else if (type.IsEnum)
            templateType = TemplateType.Enum;
        else
            throw new Exception("Type must implement IResource, IRecord or inherit from DistributedResource.");

        IsWrapper = Codec.InheritsClass(type, typeof(DistributedResource));

        type = ResourceProxy.GetBaseType(type);

        DefinedType = type;

        className = GetTypeClassName(type);

        // set guid
        classId = GetTypeUUID(type);

        if (warehouse != null)
            warehouse.PutTemplate(this);

        var hierarchy = GetHierarchy(type);

        if (hierarchy.ContainsKey(MemberTypes.Field))
        {
            foreach (var cd in hierarchy[MemberTypes.Field])
            {
                constants.Add(ConstantTemplate.MakeConstantTemplate
                    (type, (FieldInfo)cd.GetMemberInfo(), cd.Index, cd.Name, this));
            }
        }

        if (hierarchy.ContainsKey(MemberTypes.Property))
        {
            foreach (var pd in hierarchy[MemberTypes.Property])
            {
                properties.Add(PropertyTemplate.MakePropertyTemplate
                    (type, (PropertyInfo)pd.GetMemberInfo(), pd.Name, pd.Index, pd.PropertyPermission, this));
            }
        }

        if (templateType == TemplateType.Resource)
        {
            if (hierarchy.ContainsKey(MemberTypes.Method))
            {
                foreach (var fd in hierarchy[MemberTypes.Method])
                {
                    functions.Add(FunctionTemplate.MakeFunctionTemplate
                        (type, (MethodInfo)fd.GetMemberInfo(), fd.Index, fd.Name, this));
                }
            }

            if (hierarchy.ContainsKey(MemberTypes.Event))
            {
                foreach (var ed in hierarchy[MemberTypes.Event])
                {
                    events.Add(EventTemplate.MakeEventTemplate
                        (type, (EventInfo)ed.GetMemberInfo(), ed.Index, ed.Name, this));
                }
            }

        }

        // add attributes
        var attrs = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetCustomAttribute<AttributeAttribute>() != null);

        foreach (var attr in attrs)
        {
            var attrAttr = attr.GetCustomAttribute<AttributeAttribute>();

            attributes.Add(AttributeTemplate
                .MakeAttributeTemplate(type, attr, 0, attrAttr?.Name ?? attr.Name, this));
        }


        // bake it binarily
        var b = new BinaryList();

        // find the first parent type that implements IResource


        var hasParent = HasParent(type);
        var classAnnotations = type.GetCustomAttributes<AnnotationAttribute>(false);


        var hasClassAnnotation = (classAnnotations != null) && (classAnnotations.Count() > 0);

        var classNameBytes = DC.ToBytes(className);

        b.AddUInt8((byte)((hasParent ? 0x80 : 0) | (hasClassAnnotation ? 0x40 : 0x0) | (byte)templateType))
         .AddUUID(classId)
         .AddUInt8((byte)classNameBytes.Length)
         .AddUInt8Array(classNameBytes);

        if (hasParent)
        {
            // find the first parent type that implements IResource
            ParentDefinedType = ResourceProxy.GetBaseType(type.BaseType);
            var parentId = GetTypeUUID(ParentDefinedType);
            b.AddUUID(parentId);
        }

        if (hasClassAnnotation)
        {
            Annotations = new Map<string, string>();

            foreach (var ann in classAnnotations)
                Annotations.Add(ann.Key, ann.Value);

            var classAnnotationBytes = Codec.Compose (Annotations, null, null);

             b.AddUInt8Array(classAnnotationBytes);

        }

        b.AddInt32(version)
         .AddUInt16((ushort)(functions.Count + properties.Count + events.Count + constants.Count));

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


        while (parent != null)
        {
            if (parent == typeof(Resource)
                || parent == typeof(Record)
                || parent == typeof(EntryPoint))
                return false;

            if (parent.GetInterfaces().Contains(typeof(IResource))
                || parent.GetInterfaces().Contains(typeof(IRecord)))
                return true;

            parent = parent.BaseType;
        }

        return false;
    }



    public static Dictionary<MemberTypes, List<MemberData>> GetHierarchy(Type type)
    {
        var members = new List<MemberData>();

        var order = 0;

        while (type != null)
        {
            var classIsPublic = type.IsEnum || (type.GetCustomAttribute<ExportAttribute>() != null);

            if (classIsPublic)
            {
                // get public instance members only.
                var mis = type.GetMembers(BindingFlags.Public | BindingFlags.Instance
                                        | BindingFlags.DeclaredOnly | BindingFlags.Static)
                    .Where(x => x.MemberType == MemberTypes.Property || x.MemberType == MemberTypes.Field
                            || x.MemberType == MemberTypes.Event || x.MemberType == MemberTypes.Method)
                    .Where(x => !(x is FieldInfo c && !c.IsStatic))
                    .Where(x => x.GetCustomAttribute<IgnoreAttribute>() == null)
                    .Where(x => x.Name != "Instance")
                    .Where(x => x.Name != "Trigger")
                    .Where(x => !(x is MethodInfo m && m.IsSpecialName))
                    .Where(x => !(x is EventInfo e &&
                            !(e.EventHandlerType.IsGenericType &&
                                (e.EventHandlerType.GetGenericTypeDefinition() == typeof(ResourceEventHandler<>)
                                || e.EventHandlerType.GetGenericTypeDefinition() == typeof(CustomResourceEventHandler<>))
                             )
                          ))
                    .Select(x => new MemberData(
                        info: x,
                        order: order
                    ))
                    .OrderBy(x => x.Name);

                members.AddRange(mis.ToArray());

            }
            else
            {
                // allow private and public members that are marked with [Export] attribute.
                var mis = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    | BindingFlags.DeclaredOnly | BindingFlags.Static)
                    .Where(x => x.MemberType == MemberTypes.Property || x.MemberType == MemberTypes.Field
                            || x.MemberType == MemberTypes.Event || x.MemberType == MemberTypes.Method)
                    .Where(x => !(x is FieldInfo c && !c.IsStatic))
                    .Where(x => x.GetCustomAttribute<ExportAttribute>() != null)
                    .Where(x => !(x is MethodInfo m && m.IsSpecialName))
                    .Select(x => new MemberData(
                        info: x,
                        order: order
                    ))
                    .OrderBy(x => x.Name);

                members.AddRange(mis.ToArray());

            }

            type = type.BaseType;

            if (type == null
                || type == typeof(Resource)
                || type == typeof(Record)
                || type == typeof(EntryPoint))
                break;

            if (type.GetInterfaces().Contains(typeof(IResource))
                || type.GetInterfaces().Contains(typeof(IRecord)))
            {
                order++;
                continue;
            }

            break;
        }

        // round 2: check for duplicates
        for (var i = 0; i < members.Count; i++)
        {
            var mi = members[i];
            for (var j = i + 1; j < members.Count; j++)
            {
                var pi = members[j];
                if (pi.Info.MemberType != mi.Info.MemberType)
                    continue;

                //if (ci.Info.Name == mi.Info.Name && ci.Order == mi.Order)
                //    throw new Exception($"Method overload is not supported. Method '{ci.Info.Name}'.");

                if (pi.Name == mi.Name)
                {
                    if (pi.Order == mi.Order)
                        throw new Exception($"Duplicate definitions for members public name '{mi.Info.DeclaringType.Name}:{mi.Info.Name}' and '{pi.Info.DeclaringType.Name}:{pi.Info.Name}'.");
                    else
                    {
                        // @TODO: check for return type and parameters they must match
                        if (pi.Info.Name != mi.Info.Name)
                            throw new Exception($"Duplicate definitions for members public name '{mi.Info.DeclaringType.Name}:{mi.Info.Name}' and '{pi.Info.DeclaringType.Name}:{pi.Info.Name}'.");
                    }

                    mi.Parent = pi;
                    pi.Child = mi;
                }

            }
        }


        // assign indexes
        var groups = members.Where(x => x.Parent == null)
                            .OrderBy(x => x.Name).OrderByDescending(x => x.Order)
                            .GroupBy(x => x.Info.MemberType);

        foreach (var group in groups)
        {
            byte index = 0;
            foreach (var mi in group)
            {
                //if (mi.Parent == null)
                mi.Index = index++;
            }
        }

        var rt = groups.ToDictionary(g => g.Key, g => g.ToList());

        return rt;
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

        od.classId = data.GetUUID(offset);
        offset += 16;
        od.className = data.GetString(offset + 1, data[offset]);
        offset += (uint)data[offset] + 1;


        if (hasParent)
        {
            od.parentId = data.GetUUID(offset);
            offset += 16;
        }

        if (hasClassAnnotation)
        {
            var (len, anns) = Codec.ParseSync(data, offset, null);

            if (anns is Map<string, string> annotations)
                od.Annotations = annotations;

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
                var (len, ft) = FunctionTemplate.Parse(data, offset, functionIndex++, inherited);
                offset += len;
                od.functions.Add(ft);
            }
            else if (type == 1)    // property
            {
                var (len, pt) = PropertyTemplate.Parse(data, offset, propertyIndex++, inherited);
                offset += len;
                od.properties.Add(pt);

            }
            else if (type == 2) // Event
            {
                var (len, et) = EventTemplate.Parse(data, offset, propertyIndex++, inherited);
                offset += len;
                od.events.Add(et);
            }
            // constant
            else if (type == 3)
            {
                var (len, ct) = ConstantTemplate.Parse(data, offset, propertyIndex++, inherited);
                offset += len;
                od.constants.Add(ct);
            }
        }

        return od;
    }

    public Map<byte, object> CastProperties(Map<string, object> properties)
    {
        var rt = new Map<byte, object>();
        foreach (var kv in properties)
        {
            var pt = GetPropertyTemplateByName(kv.Key);
            if (pt == null) continue;
            rt.Add(pt.Index, kv.Value);
        }

        return rt;
    }
}

