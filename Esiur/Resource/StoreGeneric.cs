using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Core;
using Esiur.Data;
using Esiur.Resource.Template;

namespace Esiur.Resource;
public abstract class Store<T> : IStore where T : IResource
{
    public Instance Instance { get; set; }

    public event DestroyedEvent OnDestroy;
    

 
    public abstract AsyncBag<T1> Children<T1>(IResource resource, string name) where T1 : IResource;

    public virtual void Destroy()
    {
        OnDestroy?.Invoke(this);
    }

    public abstract AsyncReply<IResource> Get(string path);

    public abstract AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> GetRecord(IResource resource, DateTime fromDate, DateTime toDate);


    public abstract string Link(IResource resource);

    public abstract bool Modify(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime);

 
    public abstract AsyncReply<bool> Put(IResource resource);

    public abstract bool Record(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime);

 
 
 
    public abstract AsyncReply<bool> Trigger(ResourceTrigger trigger);

    //public async AsyncReply<T> New(string name = null, object attributes = null, object properties = null)
    //{
    //    var resource = await Warehouse.New<T>(name, this, null, null, attributes, properties);
    //    resource.Instance.Managers.AddRange(this.Instance.Managers.ToArray());
    //    return resource;
    //}

    public abstract AsyncReply<bool> Remove(IResource resource);

    public abstract AsyncReply<bool> Remove(string path);

    public abstract AsyncReply<bool> Move(IResource resource, string newPath);

    public abstract AsyncBag<T1> Parents<T1>(IResource resource, string name) where T1 : IResource;
}

