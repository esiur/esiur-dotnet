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
        public readonly ResourceCursor Cursor;
        public ulong Revision => Cursor.Revision;
        public readonly DateTime RecordedAt;

        public EventOccurredInfo(
            IResource resource,
            EventDef eventDef,
            object value,
            ResourceCursor cursor,
            DateTime recordedAt)
        {
            Resource = resource;
            Value = value;
            Definition = eventDef;
            Cursor = cursor;
            RecordedAt = recordedAt;
        }

        public EventOccurredInfo(IResource resource, EventDef eventDef, object value)
            : this(resource, eventDef, value, new ResourceCursor(Guid.Empty, 0), DateTime.UtcNow)
        {
        }
    }
}
