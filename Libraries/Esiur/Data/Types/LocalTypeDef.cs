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


public class LocalTypeDef:TypeDef
{
    Type _definedType { get; set; }
    Type _parentDefinedType { get; set; }


    public Type DefinedType => _definedType;
    public Type ParentDefinedType => _parentDefinedType;

    public TypeDef ParentTypeDef { get; private set; }

    public static Uuid GetTypeUUID(Type type)
    {
        var attr = type.GetCustomAttribute<TypeIdAttribute>();
        if (attr != null)
            return attr.Id;

        var tn = Encoding.UTF8.GetBytes(GetTypeName(type));
        var hash = SHA256.Create().ComputeHash(tn).Clip(0, 16);
        hash[6] = (byte)((hash[6] & 0xF) | 0x80);
        hash[8] = (byte)((hash[8] & 0xF) | 0x80);

        var rt = new Uuid(hash);
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


    public static TypeDef[] GetDependencies(LocalTypeDef typeDef, Warehouse warehouse)
    {

        var list = new List<TypeDef>();

        // Add self
        list.Add(typeDef);


        Action<LocalTypeDef> getDependenciesFunc = null;

        getDependenciesFunc = (LocalTypeDef td) =>
        {
            if (td.DefinedType == null)
                return;

            // Add parents
            var parentType = td.ParentDefinedType;

            // Get parents
            while (parentType != null)
            {
                var parentTypeDef = warehouse.GetLocalTypeDefByType(parentType);
                if (parentTypeDef != null)
                {
                    if (!list.Contains(parentTypeDef))
                    {
                        list.Add(parentTypeDef);

                        if (parentTypeDef is LocalTypeDef pltd)
                        {
                            parentType = pltd.DefinedType;
                        }
                    }
                }
            }

            // functions
            foreach (var f in td._functions)
            {
                var functionReturnTypes = GetDistributedTypes(f.MethodInfo.ReturnType);

                foreach (var functionReturnType in functionReturnTypes)
                {
                    var functionReturnTypeDef = warehouse.GetLocalTypeDefByType(functionReturnType);
                    if (functionReturnTypeDef != null)
                    {
                        if (!list.Contains(functionReturnTypeDef))
                        {
                            list.Add(functionReturnTypeDef);
                            if (functionReturnTypeDef is LocalTypeDef frtd)
                                getDependenciesFunc(frtd);
                        }
                    }
                }

                var args = f.MethodInfo.GetParameters();

                for (var i = 0; i < args.Length - 1; i++)
                {
                    var fpTypes = GetDistributedTypes(args[i].ParameterType);

                    foreach (var fpType in fpTypes)
                    {
                        var fpt = warehouse.GetLocalTypeDefByType(fpType);
                        if (fpt != null)
                        {
                            if (!list.Contains(fpt))
                            {
                                list.Add(fpt);
                                if (fpt is LocalTypeDef ltd)
                                    getDependenciesFunc(ltd);
                            }
                        }
                    }
                }

                // skip EpConnection argument
                if (args.Length > 0)
                {
                    var last = args.Last();
                    if (last.ParameterType != typeof(EpConnection))
                    {

                        var fpTypes = GetDistributedTypes(last.ParameterType);

                        foreach (var fpType in fpTypes)
                        {
                            var fpt = warehouse.GetLocalTypeDefByType(fpType);
                            if (fpt != null)
                            {
                                if (!list.Contains(fpt))
                                {
                                    list.Add(fpt);
                                    if (fpt is LocalTypeDef ltd)
                                        getDependenciesFunc(ltd);
                                }
                            }
                        }
                    }
                }

            }

            // properties
            foreach (var p in td._properties)
            {
                var propertyTypes = GetDistributedTypes(p.PropertyInfo.PropertyType);

                foreach (var propertyType in propertyTypes)
                {
                    var propertyTypeDef = warehouse.GetLocalTypeDefByType(propertyType);
                    if (propertyTypeDef != null)
                    {
                        if (!list.Contains(propertyTypeDef))
                        {
                            list.Add(propertyTypeDef);
                            if (propertyTypeDef is LocalTypeDef ltd)
                                getDependenciesFunc(ltd);
                        }
                    }
                }
            }

            // events
            foreach (var e in td._events)
            {
                var eventTypes = GetDistributedTypes(e.EventInfo.EventHandlerType.GenericTypeArguments[0]);

                foreach (var eventType in eventTypes)
                {
                    var eventTypeDef = warehouse.GetLocalTypeDefByType(eventType);

                    if (eventTypeDef != null)
                    {
                        if (!list.Contains(eventTypeDef))
                        {
                            list.Add(eventTypeDef);
                            if (eventTypeDef is LocalTypeDef ltd)
                                getDependenciesFunc(ltd);
                        }
                    }
                }
            }
        };

        getDependenciesFunc(typeDef);
        return list.Distinct().ToArray();
    }


    public static string GetTypeName(Type type, char separator = '.')
    {

        if (type.IsGenericType)
        {
            var index = type.Name.IndexOf("`");
            var name = $"{type.Namespace}{separator}{((index > -1) ? type.Name.Substring(0, index) : type.Name)}Of";
            foreach (var t in type.GenericTypeArguments)
                name += GetTypeName(t, '_');

            return name;
        }
        else
            return $"{type.Namespace?.Replace('.', separator) ?? "Global"}{separator}{type.Name}";
    }

    public LocalTypeDef(Type type, Warehouse warehouse)
    {
        if (Codec.ImplementsInterface(type, typeof(IResource)))
            _typeDefKind = TypeDefKind.Resource;
        else if (Codec.ImplementsInterface(type, typeof(IRecord)))
            _typeDefKind = TypeDefKind.Record;
        else if (type.IsEnum)
            _typeDefKind = TypeDefKind.Enum;
        else
            throw new Exception("Type must implement IResource, IRecord or inherit from DistributedResource.");

        //IsWrapper = Codec.InheritsClass(type, typeof(EpResource));

        type = ResourceProxy.GetBaseType(type);

        _definedType = type;

        _typeName = GetTypeName(type);


        warehouse.TryRegisterLocalTypeDef(this);

        var hierarchy = GetHierarchy(type);

        if (hierarchy.ContainsKey(MemberTypes.Field))
        {
            foreach (var cd in hierarchy[MemberTypes.Field])
            {
                _constants.Add(ConstantDef.MakeConstantDef
                    (warehouse, type, (FieldInfo)cd.GetMemberInfo(), cd.Index, cd.Name, this));
            }
        }

        if (hierarchy.ContainsKey(MemberTypes.Property))
        {
            foreach (var pd in hierarchy[MemberTypes.Property])
            {
                _properties.Add(PropertyDef.MakePropertyDef
                    ( warehouse, type, (PropertyInfo)pd.GetMemberInfo(), pd.Name, pd.Index, pd.PropertyPermission, this));
            }
        }

        if (_typeDefKind == TypeDefKind.Resource)
        {
            if (hierarchy.ContainsKey(MemberTypes.Method))
            {
                foreach (var fd in hierarchy[MemberTypes.Method])
                {
                    _functions.Add(FunctionDef.MakeFunctionDef
                        (warehouse, type, (MethodInfo)fd.GetMemberInfo(), fd.Index, fd.Name, this));
                }
            }

            if (hierarchy.ContainsKey(MemberTypes.Event))
            {
                foreach (var ed in hierarchy[MemberTypes.Event])
                {
                    _events.Add(EventDef.MakeEventDef
                        ( warehouse, type, (EventInfo)ed.GetMemberInfo(), ed.Index, ed.Name, this));
                }
            }

        }

        // add attributes
        var attrs = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetCustomAttribute<AttributeAttribute>() != null);

        foreach (var attr in attrs)
        {
            var attrAttr = attr.GetCustomAttribute<AttributeAttribute>();

            _attributes.Add(AttributeDef
                .MakeAttributeDef(type, attr, 0, attrAttr?.Name ?? attr.Name, this));
        }



        // find the first parent type that implements IResource
        var hasParent = HasParent(type);
        var classAnnotations = type.GetCustomAttributes<AnnotationAttribute>(false);
        var hasClassAnnotation = (classAnnotations != null) && (classAnnotations.Count() > 0);


        if (hasParent)
        {
            // find the first parent type that implements IResource
            _parentDefinedType = ResourceProxy.GetBaseType(type.BaseType);

            var parentTypeDef = warehouse.GetLocalTypeDefByType(_parentDefinedType);

            if (parentTypeDef == null)
                throw new Exception("Can't find parent TypeDef.");

            ParentTypeDef = parentTypeDef;
        }

        if (hasClassAnnotation)
        {
            Annotations = new Map<string, string>();

            foreach (var ann in classAnnotations)
                Annotations.Add(ann.Key, ann.Value);
        }

    }

    public override byte[] Compose(EpConnection connection)
    {
        // bake it binarily
        var b = new BinaryList();

        // find the first parent type that implements IResource


        var hasParent = ParentTypeDef != null;
        var hasClassAnnotation = Annotations != null && Annotations.Count() > 0;

        var typeNameBytes = DC.ToBytes(_typeName);

        b.AddUInt8((byte)((hasParent ? 0x80 : 0) | (hasClassAnnotation ? 0x40 : 0x0) | (byte)_typeDefKind))
         .AddUInt64(_typeId)
         .AddUInt8((byte)typeNameBytes.Length)
         .AddUInt8Array(typeNameBytes);

        if (hasParent)
        {
            b.AddUInt64(ParentTypeDef.Id);
        }

        if (hasClassAnnotation)
        {

            //foreach (var ann in Annotations)
            //    Annotations.Add(ann.Key, ann.Value);

            var classAnnotationBytes = Codec.Compose(Annotations, null, null);

            b.AddUInt8Array(classAnnotationBytes);

        }

        b.AddInt32(_version)
         .AddUInt16((ushort)(_functions.Count + _properties.Count + _events.Count + _constants.Count));

        foreach (var ft in _functions)
            b.AddUInt8Array(ft.Compose(connection   ));
        foreach (var pt in _properties)
            b.AddUInt8Array(pt.Compose(connection));
        foreach (var et in _events)
            b.AddUInt8Array(et.Compose(connection));
        foreach (var ct in _constants)
            b.AddUInt8Array(ct.Compose(connection));

        return b.ToArray();

    }

    public static bool HasParent(Type type)
    {
        var parent = type.BaseType;


        while (parent != null)
        {
            if (parent == typeof(Esiur.Resource.Resource)
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
                || type == typeof(Esiur.Resource.Resource)
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


    //public Map<byte, object> CastProperties(Map<string, object> properties)
    //{
    //    var rt = new Map<byte, object>();
    //    foreach (var kv in properties)
    //    {
    //        var pt = GetPropertyDefByName(kv.Key);
    //        if (pt == null) continue;
    //        rt.Add(pt.Index, kv.Value);
    //    }

    //    return rt;
    //}
}

