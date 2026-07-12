using Esiur.Data.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    /// <summary>
    /// Marks an exported function as streaming and specifies its delivery mode.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class StreamAttribute : Attribute
    {
        /// <summary>
        /// Gets the stream delivery mode.
        /// </summary>
        public StreamMode Mode { get; }

        /// <summary>
        /// Indicates whether a push stream may be paused and resumed remotely.
        /// This should only be true when Mode is Push.
        /// </summary>
        public bool Pausable { get; set; }

        public StreamAttribute(StreamMode mode = StreamMode.Push)
        {
            Mode = mode;
        }
    }

}
