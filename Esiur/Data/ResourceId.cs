using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public struct ResourceId
    {
        public bool Local;
        public uint Id;

        public ResourceId(bool local, uint id)
        {
            this.Id = id;
            this.Local = local;
        }
    }
}
