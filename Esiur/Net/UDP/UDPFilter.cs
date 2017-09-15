using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Net;
using Esiur.Data;
using Esiur.Engine;
using Esiur.Resource;

namespace Esiur.Net.UDP
{
    public abstract class UDPFilter : IResource
    {
        public Instance Instance
        {
            get;
            set;
        }


        public event DestroyedEvent OnDestroy;

        public abstract AsyncReply<bool> Trigger(ResourceTrigger trigger);

        public abstract bool Execute(byte[] data, IPEndPoint sender);

        public void Destroy()
        {
            throw new NotImplementedException();
        }
    }
}