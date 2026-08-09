using Esiur.Core;
using Esiur.Data.Types;
using Esiur.Protocol;
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

        public TypeDef ResourceDefinition { get; }
    }

    /// <summary>
    /// Optional server-side invocation hook for local resources whose type
    /// definition is assembled at runtime. Remote <see cref="EpResource"/>
    /// proxies remain unchanged; this hook gives their local counterpart the
    /// same dynamic function semantics.
    /// </summary>
    public interface IDynamicResourceFunctionHandler
    {
        public AsyncReply InvokeResourceFunctionAsync(
            byte index,
            object arguments,
            InvocationContext context);
    }

    public delegate void DynamicResourceEventHandler(byte index, object value);

    /// <summary>
    /// Optional event bridge for a local runtime-defined resource.
    /// </summary>
    public interface IDynamicResourceEventSource
    {
        public event DynamicResourceEventHandler ResourceEventOccurred;
    }
}
