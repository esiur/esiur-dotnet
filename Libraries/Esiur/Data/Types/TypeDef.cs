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
using System.Runtime.CompilerServices;
using Esiur.Resource;
using Esiur.Protocol;

namespace Esiur.Data.Types;


public class TypeDef
{

    protected ulong _typeId;
    protected ulong? _parentTypeId;

    public Map<string, string> Annotations { get; set; }

    protected string _typeName;
    protected List<FunctionDef> _functions = new List<FunctionDef>();
    protected List<EventDef> _events = new List<EventDef>();
    protected List<PropertyDef> _properties = new List<PropertyDef>();
    protected List<AttributeDef> _attributes = new List<AttributeDef>();
    protected List<ConstantDef> _constants = new();
    protected int _version;
    protected TypeDefKind _typeDefKind;



    public override string ToString()
    {
        return _typeName;
    }

    
    protected byte[] _content;

    public ulong? ParentTypeId => _parentTypeId;

    public TypeDefKind Kind => _typeDefKind;

    public EventDef GetEventDefByName(string eventName)
    {
        foreach (var i in _events)
            if (i.Name == eventName)
                return i;
        return null;
    }

    public EventDef GetEventDefByIndex(byte index)
    {
        foreach (var i in _events)
            if (i.Index == index)
                return i;
        return null;
    }

    public FunctionDef GetFunctionDefByName(string functionName)
    {
        foreach (var i in _functions)
            if (i.Name == functionName)
                return i;
        return null;
    }
    public FunctionDef GetFunctionDefByIndex(byte index)
    {
        foreach (var i in _functions)
            if (i.Index == index)
                return i;
        return null;
    }

    public PropertyDef GetPropertyDefByIndex(byte index)
    {
        foreach (var i in _properties)
            if (i.Index == index)
                return i;
        return null;
    }

    public PropertyDef GetPropertyDefByName(string propertyName)
    {
        foreach (var i in _properties)
            if (i.Name == propertyName)
                return i;
        return null;
    }

    public AttributeDef GetAttributeDef(string attributeName)
    {
        foreach (var i in _attributes)
            if (i.Name == attributeName)
                return i;
        return null;
    }

    public ulong Id
    {
        get { return _typeId; }
        internal set { _typeId = value; }
    }


    public string Name
    {
        get { return _typeName; }
    }


    public FunctionDef[] Functions
    {
        get { return _functions.ToArray(); }
    }

    public EventDef[] Events
    {
        get { return _events.ToArray(); }
    }

    public PropertyDef[] Properties
    {
        get { return _properties.ToArray(); }
    }

    public ConstantDef[] Constants => _constants.ToArray();

    public TypeDef()
    {

    }

    public Map<byte, object> CastProperties(Map<string, object> properties)
    {
        var rt = new Map<byte, object>();
        foreach (var kv in properties)
        {
            var pt = GetPropertyDefByName(kv.Key);
            if (pt == null) continue;
            rt.Add(pt.Index, kv.Value);
        }

        return rt;
    }

    public virtual byte[] Compose(EpConnection connection)
    {
        return null;
    }

   
}

