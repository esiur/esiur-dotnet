using Esiur.Data.Types;
using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource;

public class CustomEventOccurredInfo
{
    public readonly EventDef EventTemplate;
    public readonly IResource Resource;
    public readonly object Value;
    public readonly object Issuer;
    public readonly Func<Session, bool> Receivers;

    public string Name => EventTemplate.Name;

    public CustomEventOccurredInfo(IResource resource, EventDef eventTemplate, Func<Session, bool> receivers, object issuer, object value)
    {
        Resource = resource;
        EventTemplate = eventTemplate;
        Receivers = receivers;
        Issuer = issuer;
        Value = value;
    }
}
