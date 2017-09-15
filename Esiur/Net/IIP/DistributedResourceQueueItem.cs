using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Net.IIP
{
    public class DistributedResourceQueueItem
    {
        public enum DistributedResourceQueueItemType
        {
            Propery,
            Event
        }

        DistributedResourceQueueItemType type;
        byte index;
        object value;
        DistributedResource resource;

        public DistributedResourceQueueItem(DistributedResource resource, DistributedResourceQueueItemType type, object value, byte index)
        {
            this.resource = resource;
            this.index = index;
            this.type = type;
            this.value = value;
        }

        public DistributedResource Resource
        {
            get { return resource; }
        }
        public DistributedResourceQueueItemType Type
        {
            get { return type; }
        }

        public byte Index
        {
            get { return index; }
        }

        public object Value
        {
            get { return value; }
        }
    }
}
