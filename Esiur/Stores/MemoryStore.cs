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

public class MemoryStore : IStore
{
    public Instance Instance { get; set; }

    public event DestroyedEvent OnDestroy;
    

    KeyList<uint, IResource> resources = new KeyList<uint, IResource>();

    public void Destroy()
    {
        OnDestroy?.Invoke(this);
    }

    public string Link(IResource resource)
    {
        if (resource.Instance.Store == this)
            return this.Instance.Name + "/$" + resource.Instance.Id;

        return null;
    }

    public AsyncReply<IResource> Get(string path)
    {

        if (path.StartsWith("$"))
        {
            uint id;
            if (uint.TryParse(path.Substring(1), out id))
            {
                foreach (var r in resources)
                    if (r.Value.Instance.Id == id)
                        return new AsyncReply<IResource>(r.Value);
            }
        }
        else
        {
            foreach (var r in resources)
                if (r.Value.Instance.Name == path)
                    return new AsyncReply<IResource>(r.Value);
        }

        return new AsyncReply<IResource>(null);
    }

    public AsyncReply<bool> Put(IResource resource)
    {

        resources.Add(resource.Instance.Id, resource);//  new WeakReference<IResource>(resource));
        resource.Instance.Variables["children"] = new AutoList<IResource, Instance>(resource.Instance);
        resource.Instance.Variables["parents"] = new AutoList<IResource, Instance>(resource.Instance);

        return new AsyncReply<bool>(true);
    }

    //public AsyncReply<IResource> Retrieve(uint iid)
    //{
    //    if (resources.ContainsKey(iid))
    //    {
    //        if (resources.ContainsKey(iid))// .TryGetTarget(out r))
    //            return new AsyncReply<IResource>(resources[iid]);
    //        else
    //            return new AsyncReply<IResource>(null);
    //    }
    //    else
    //        return new AsyncReply<IResource>(null);
    //}

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

    public bool Remove(IResource resource)
    {
        resources.Remove(resource.Instance.Id);
        return true;
    }

    public bool Modify(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime)
    {
        return true;
    }

    //public AsyncReply<bool> AddChild(IResource parent, IResource child)
    //{
    //    if (parent.Instance.Store == this)
    //    {
    //        (parent.Instance.Variables["children"] as AutoList<IResource, Instance>).Add(child);
    //        return new AsyncReply<bool>(true);
    //    }
    //    else
    //        return new AsyncReply<bool>(false);
    //}
 

    //public AsyncReply<bool> AddParent(IResource resource, IResource parent)
    //{

    //    if (resource.Instance.Store == this)
    //    {
    //        (resource.Instance.Variables["parents"] as AutoList<IResource, Instance>).Add(parent);
    //        return new AsyncReply<bool>(true);
    //    }
    //    else
    //        return new AsyncReply<bool>(false);
    //}

    public AsyncReply<bool> RemoveParent(IResource child, IResource parent)
    {
        throw new NotImplementedException();
    }

    public AsyncBag<T> Children<T>(IResource resource, string name) where T : IResource
    {
        var children = (resource.Instance.Variables["children"] as AutoList<IResource, Instance>);

        if (name == null)
            return new AsyncBag<T>(children.Where(x => x is T).Select(x => (T)x).ToArray());
        else
            return new AsyncBag<T>(children.Where(x => x is T && x.Instance.Name == name).Select(x => (T)x).ToArray());

    }

    public AsyncBag<T> Parents<T>(IResource resource, string name) where T : IResource
    {
        var parents = (resource.Instance.Variables["parents"] as AutoList<IResource, Instance>);

        if (name == null)
            return new AsyncBag<T>(parents.Where(x => x is T).Select(x => (T)x).ToArray());
        else
            return new AsyncBag<T>(parents.Where(x => x is T && x.Instance.Name == name).Select(x => (T)x).ToArray());
    }

    AsyncReply<bool> IStore.Remove(IResource resource)
    {
        throw new NotImplementedException();
    }

    public AsyncReply<bool> Remove(string path)
    {
        throw new NotImplementedException();
    }

    public AsyncReply<bool> Move(IResource resource, string newPath)
    {
        throw new NotImplementedException();
    }

}
