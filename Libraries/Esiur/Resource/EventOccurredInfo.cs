using Esiur.Data.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
 
    public class EventOccurredInfo
    {

        public readonly EventDef Definition;

        public string Name => Definition.Name;

        public readonly IResource Resource;
        public readonly object Value;

        public EventOccurredInfo(IResource resource, EventDef eventDef, object value)
        {
            Resource = resource;
            Value = value;
            Definition = eventDef;
        }
    }
}
