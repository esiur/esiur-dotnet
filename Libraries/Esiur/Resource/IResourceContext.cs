using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    public interface IResourceContext
    {
        public Map<string, object> Attributes { get; }
        public Map<string, object> Properties { get; }
        public ulong Age { get; }

    }
}
