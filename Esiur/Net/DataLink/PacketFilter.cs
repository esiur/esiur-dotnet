using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esiur.Engine;
using Esiur.Data;
using Esiur.Net.Packets;
using Esiur.Resource;

namespace Esiur.Net.DataLink
{
    public abstract class PacketFilter : IResource
    {

        public Instance Instance
        {
            get;
            set;
        }

        public event DestroyedEvent OnDestroy;

        public abstract AsyncReply<bool> Trigger(ResourceTrigger trigger);

        public abstract bool Execute(Packet packet);

        public void Destroy()
        {
            throw new NotImplementedException();
        }
    }
}
