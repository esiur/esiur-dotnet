using Esiur.Core;
using Esiur.Resource.Template;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public interface IDynamicResource
    {
        public PropertyValue[] SerializeResource();
        public Map<byte, PropertyValue> SerializeResourceAfter(ulong age);

        public object GetResourceProperty(byte index);
        public AsyncReply SetResourcePropertyAsync(byte index, object value);
        public void SetResourceProperty(byte index, object value);

        public TypeTemplate ResourceTemplate { get; }
    }
}
