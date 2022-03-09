using Esiur.Resource.Template;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
 
    public class EventOccurredInfo
    {

        public readonly EventTemplate EventTemplate;

        public string Name => EventTemplate.Name;

        public readonly IResource Resource;
        public readonly object Value;

        public EventOccurredInfo(IResource resource, EventTemplate eventTemplate, object value)
        {
            Resource = resource;
            Value = value;
            EventTemplate = eventTemplate;
        }
    }
}
