using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Core;
using Esiur.Data;
using Esiur.Resource.Template;

namespace Esiur.Stores;
public class TemporaryStore : IStore
{
    public Instance Instance { get; set; }

    public event DestroyedEvent OnDestroy;
    
    Dictionary<uint, WeakReference> resources = new Dictionary<uint, WeakReference>();

    public void Destroy()
    {
        OnDestroy?.Invoke(this);

    }

    public string Link(IResource resource)
    {
        if (resource.Instance.Store == this)
            return this.Instance.Name + "/" + resource.Instance.Id;

        return null;
    }

    public AsyncReply<IResource> Get(string path)
    {
        foreach (var r in resources)
            if (r.Value.IsAlive && (r.Value.Target as IResource).Instance.Name == path)
                return new AsyncReply<IResource>(r.Value.Target as IResource);

        return new AsyncReply<IResource>(null);
    }

    public AsyncReply<bool> Put(IResource resource)
    {
        resources.Add(resource.Instance.Id, new WeakReference(resource));
        return new AsyncReply<bool>(true);
    }

    public AsyncReply<IResource> Retrieve(uint iid)
    {
        if (resources.ContainsKey(iid))
        {
            if (resources.ContainsKey(iid) && resources[iid].IsAlive)// .TryGetTarget(out r))
                return new AsyncReply<IResource>(resources[iid].Target as IResource);
            else
                return new AsyncReply<IResource>(null);
        }
        else
            return new AsyncReply<IResource>(null);
    }

    public AsyncReply<bool> Trigger(ResourceTrigger trigger)
    {
        return new AsyncReply<bool>(true);
    }

    public bool Record(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime)
    {
        throw new NotImplementedException();
    }

    public AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> GetRecord(IResource resource, DateTime fromDate, DateTime toDate)
    {
        throw new NotImplementedException();
    }

   
    public bool Modify(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime)
    {
        return true;
    }

 
    public AsyncBag<T> Children<T>(IResource resource, string name) where T : IResource
    {
        throw new NotImplementedException();
    }
 

    AsyncReply<bool> IStore.Remove(IResource resource)
    {
        resources.Remove(resource.Instance.Id);
        return new AsyncReply<bool>(true);
    }

    public AsyncReply<bool> Remove(string path)
    {
        throw new NotImplementedException();
    }

    public AsyncReply<bool> Move(IResource resource, string newPath)
    {
        throw new NotImplementedException();
    }

    public AsyncBag<T> Parents<T>(IResource resource, string name) where T : IResource
    {
        throw new NotImplementedException();
    }
}
