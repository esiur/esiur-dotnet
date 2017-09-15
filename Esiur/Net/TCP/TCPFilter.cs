using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Esiur.Data;
using Esiur.Net.Sockets;
using Esiur.Engine;
using Esiur.Resource;

namespace Esiur.Net.TCP
{
    public abstract class TCPFilter: IResource
    {
        public Instance Instance
        {
            get;
            set;
        }

        public event DestroyedEvent OnDestroy;

        public abstract AsyncReply<bool> Trigger(ResourceTrigger trigger);

        public virtual bool Connected(TCPConnection sender)
        {
            return false;
        }

        public virtual bool Disconnected(TCPConnection sender)
        {
            return false;
        }

        public abstract bool Execute(byte[] msg, NetworkBuffer data, TCPConnection sender);

        public void Destroy()
        {
            throw new NotImplementedException();
        }
    }
}
