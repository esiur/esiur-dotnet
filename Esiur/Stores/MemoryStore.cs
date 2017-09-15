using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Engine;

namespace Esiur.Stores
{
    public class MemoryStore : IStore
    {
        public Instance Instance { get; set; }

        public event DestroyedEvent OnDestroy;

        Dictionary<uint, IResource> resources = new Dictionary<uint, IResource>();

        public void Destroy()
        {

        }

        public string Link(IResource resource)
        {
            if (resource.Instance.Store == this)
                return this.Instance.Name + "/" + resource.Instance.Id;

            return null;
        }

        public AsyncReply<IResource> Get(string path)
        {
            return new AsyncReply<IResource>(null);
        }

        public bool Put(IResource resource)
        {
            resources.Add(resource.Instance.Id, resource);
            return true;
        }

        public AsyncReply<IResource> Retrieve(uint iid)
        {
            if (resources.ContainsKey(iid))
                return new AsyncReply<IResource>(resources[iid]);
            else
                return new AsyncReply<IResource>(null);
        }

        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            return new AsyncReply<bool>(true);
        }
    }
}
