using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Data.Types;
using Esiur.Resource;

namespace Esiur.Resource;

public struct PropertyModificationInfo
{
    public readonly IResource Resource;
    public readonly PropertyDef PropertyDef;
    public string Name => PropertyDef.Name;
    public readonly ulong Age;
    public ulong Revision => Cursor.Revision;
    public readonly ResourceCursor Cursor;
    public readonly DateTime RecordedAt;
    public object Value;

    public PropertyModificationInfo(
        IResource resource,
        PropertyDef propertyDef,
        object value,
        ResourceCursor cursor,
        DateTime recordedAt)
    {
        Resource = resource;
        PropertyDef = propertyDef;
        Cursor = cursor;
        Age = cursor.Revision;
        RecordedAt = recordedAt;
        Value = value;
    }

    public PropertyModificationInfo(IResource resource, PropertyDef propertyDef, object value, ulong age)
        : this(resource, propertyDef, value, new ResourceCursor(Guid.Empty, age), DateTime.UtcNow)
    {
    }
    
}

