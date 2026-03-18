using Esiur.Data.Types;
using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource;

public class CustomEventOccurredInfo
{
    public readonly EventDef EventDef;
    public readonly IResource Resource;
    public readonly object Value;
    public readonly object Issuer;
    public readonly Func<Session, bool> Receivers;

    public string Name => EventDef.Name;

    public CustomEventOccurredInfo(IResource resource, EventDef eventDef, Func<Session, bool> receivers, object issuer, object value)
    {
        Resource = resource;
        EventDef = eventDef;
        Receivers = receivers;
        Issuer = issuer;
        Value = value;
    }
}
