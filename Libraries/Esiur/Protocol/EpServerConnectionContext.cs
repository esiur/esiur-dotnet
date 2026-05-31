using Esiur.Data;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Protocol
{
    public struct EpServerConnectionContext : IResourceContext
    {
        public Map<string, object> Attributes => null;

        public Map<string, object> Properties => null;

        public ulong Age => 0;

        public EpServer Server;
        public Warehouse Warehouse;
    }
}
