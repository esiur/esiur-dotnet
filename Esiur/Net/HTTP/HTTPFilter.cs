using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using Esiur.Data;
using Esiur.Engine;
using Esiur.Resource;

namespace Esiur.Net.HTTP
{

    public abstract class HTTPFilter : IResource
    {
       public Instance Instance
       {
            get;
            set;
       }

        public event DestroyedEvent OnDestroy;

        public abstract AsyncReply<bool> Trigger(ResourceTrigger trigger);

        /*
        public virtual void SessionModified(HTTPSession session, string key, object oldValue, object newValue)
        {

        }

        public virtual void SessionExpired(HTTPSession session)
        {

        }
        */

        public abstract bool Execute(HTTPConnection sender);

        public virtual void ClientConnected(HTTPConnection HTTP)
        {
            //return false;
        }

        public virtual void ClientDisconnected(HTTPConnection HTTP)
        {
            //return false;
        }

        public void Destroy()
        {
            throw new NotImplementedException();
        }
    }
}