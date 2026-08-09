using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Data;
using System.Runtime.CompilerServices;
using System.Reflection;
using Esiur.Misc;
using Esiur.Security.Permissions;
using Esiur.Security.Management;
using Esiur.Security.Authority;
using Esiur.Proxy;
using Esiur.Core;
using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Emit;
using Esiur.Data.Types;
using Esiur.Protocol;

namespace Esiur.Resource;

[NotMapped]
public class Instance
{
	string name;

	// public int IntVal { get; set; }


	WeakReference<IResource> resource;
	IStore store;
	TypeDef definition;
	AutoList<IResourceManager, Instance> managers;


	public event PropertyModifiedEvent PropertyModified;



	public event EventOccurredEvent EventOccurred;
	public event CustomEventOccurredEvent CustomEventOccurred;
	public event ResourceDestroyedEvent Destroyed;

	bool loading = false;

	//KeyList<string, object> attributes;

	List<ulong?> ages = new();
	List<DateTime?> modificationDates = new();
	private ulong instanceAge;
	private Guid streamGeneration;
	private readonly object journalSync = new();
	private byte hops;
	private DateTime instanceModificationDate;

	uint id;

	public KeyList<string, object> Variables { get; } = new KeyList<string, object>();

	/// <summary>
	/// Instance attributes are custom properties associated with the instance, a place to store information by IStore.
	/// </summary>
	//public KeyList<string, object> Attributes
	//{
	//    get
	//    {
	//        return attributes;
	//    }
	//}

	public override string ToString()
	{
		return name + " (" + Link + ")";
	}

	public bool RemoveAttributes(string[] attributes = null)
	{

		return false;

		/*
        IResource res;

        if (!resource.TryGetTarget(out res))
            return false;

        return store.RemoveAttributes(res, attributes);
        */

		/*
        if (attributes == null)
            this.attributes.Clear();
        else
        {
            foreach (var attr in attributes)
                this.attributes.Remove(attr);
        }

        return true;
        */
	}

	public Map<string, object> GetAttributes(string[] attributes = null)
	{
		// @TODO
		var rt = new Map<string, object>();

		if (attributes != null)
		{
			for (var i = 0; i < attributes.Length; i++)
			{
				var at = definition.GetAttributeDef(attributes[i]);
				if (at != null)
				{

				}
			}
		}

		return rt;
	}

	public bool SetAttributes(Map<string, object> attributes, bool clearAttributes = false)
	{

		// @ TODO
		IResource res;

		if (resource.TryGetTarget(out res))
		{
			foreach (var kv in attributes)
			{
				var at = definition.GetAttributeDef(kv.Key);

				if (at != null)
					if (at.PropertyInfo.CanWrite)
						at.PropertyInfo.SetValue(res, RuntimeCaster.Cast(kv.Value, at.PropertyInfo.PropertyType));

			}
		}

		return true;


		/*
        try
        {

            if (clearAttributes)
                this.attributes.Clear();

            foreach (var attr in attributes)
                if (attr.Key == "name")
                    this.name = attr.Value as string;
                else if (attr.Key == "managers")
                {
                    this.managers.Clear();

                    var mngrs = attr.Value as object[];

                    foreach (var mngr in mngrs)
                    {
                        var m = mngr as Structure;
                        var type = Type.GetType(m["type"] as string);
                        if (Codec.ImplementsInterface(type, typeof(IPermissionsManager)))
                        {
                            var settings = m["settings"] as Structure;
                            var manager = Activator.CreateInstance(type) as IPermissionsManager;

                            IResource res;
                            if (this.resource.TryGetTarget(out res))
                            {
                                manager.Initialize(settings, res);
                                this.managers.Add(manager);
                            }
                        }
                        else
                            return false;
                    }
                }
                else
                {
                    this.attributes[attr.Key] = attr.Value;
                }

        }
        catch
        {
            return false;
        }

        return true;
        */
	}

	/*
    public Structure GetAttributes()
    {
        var st = new Structure();
        foreach (var a in attributes.Keys)
            st[a] = attributes[a];

        st["name"] = name;

        var mngrs = new List<Structure>();

        foreach (var manager in managers)
        {
            var mngr = new Structure();
            mngr["settings"] = manager.Settings;
            mngr["type"] = manager.GetType().FullName;
            mngrs.Add(mngr);
        }

        st["managers"] = mngrs;

        return st;
    }*/

	/// <summary>
	/// Get the age of a given property index.
	/// </summary>
	/// <param name="index">Zero-based property index.</param>
	/// <returns>Age.</returns>
	public ulong? GetAge(byte index)
	{
		if (index < ages.Count)
			return ages[index];
		else
			return 0;
	}

	/// <summary>
	/// Set the age of a property.
	/// </summary>
	/// <param name="index">Zero-based property index.</param>
	/// <param name="value">Age.</param>
	public void SetAge(byte index, ulong? value)
	{
		if (index < ages.Count)
		{
			ages[index] = value;
			if (value > instanceAge)
				instanceAge = (ulong)value;
		}
	}

	/// <summary>
	/// Set the modification date of a property.
	/// </summary>
	/// <param name="index">Zero-based property index.</param>
	/// <param name="value">Modification date.</param>
	public void SetModificationDate(byte index, DateTime? value)
	{
		if (index < modificationDates.Count)
		{
			modificationDates[index] = value;
			if (value > instanceModificationDate)
				instanceModificationDate = (DateTime)value;
		}
	}

	/// <summary>
	/// Get modification date of a specific property.
	/// </summary>
	/// <param name="index">Zero-based property index</param>
	/// <returns>Modification date.</returns>
	public DateTime? GetModificationDate(byte index)
	{
		if (index < modificationDates.Count)
			return modificationDates[index];
		else
			return DateTime.MinValue;
	}


	/// <summary>
	/// Load property value (used by stores)
	/// </summary>
	/// <param name="name">Property name</param>
	/// <param name="age">Property age</param>
	/// <param name="value">Property value</param>
	/// <returns></returns>
	public bool LoadProperty(string name, ulong? age, DateTime? modificationDate, object value)
	{

		IResource res;

		if (!resource.TryGetTarget(out res))
			return false;

		var pt = definition.GetPropertyDefByName(name);

		if (pt == null)
			return false;

		/*
#if NETSTANDARD
        var pi = resource.GetType().GetTypeInfo().GetProperty(name, new[] { resource.GetType() });
#else
        var pi = resource.GetType().GetProperty(pt.Name);
#endif
*/

		if (pt.PropertyInfo.PropertyType.IsGenericType
			&& pt.PropertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(PropertyContext<>))
			return false;


		if (pt.PropertyInfo.CanWrite)
		{
			try
			{
				loading = true;

				pt.PropertyInfo.SetValue(res, RuntimeCaster.Cast(value, pt.PropertyInfo.PropertyType));
			}
			catch (Exception ex)
			{
				//Console.WriteLine(resource.ToString() + " " + name);
				Global.Log(ex);
			}

			loading = false;
		}


		SetAge(pt.Index, age);
		SetModificationDate(pt.Index, modificationDate);

		return true;
	}

	/// <summary>
	/// Legacy alias for the resource-wide revision. It advances for every
	/// property modification and event occurrence.
	/// </summary>
	public ulong Age
	{
		get { lock (journalSync) return instanceAge; }
		internal set { lock (journalSync) instanceAge = value; }
	}

	/// <summary>
	/// Current resource revision. Unlike the legacy property-only age, this
	/// advances for every property modification and event occurrence.
	/// </summary>
	public ulong Revision
	{
		get { lock (journalSync) return instanceAge; }
	}

	/// <summary>Generation of the current resource change stream.</summary>
	public Guid Generation
	{
		get { lock (journalSync) return streamGeneration; }
	}

	/// <summary>Current replay cursor for the resource.</summary>
	public ResourceCursor Cursor
	{
		get { lock (journalSync) return new ResourceCursor(streamGeneration, instanceAge); }
	}

	/// <summary>
	/// Number of nodes to reach the original resource.
	/// </summary>
	public byte Hops
	{
		get { return hops; }
		internal set { hops = value; }
	}


	/// <summary>
	/// Last modification date.
	/// </summary>
	public DateTime? ModificationDate
	{
		get
		{
			return instanceModificationDate;
		}
	}

	/// <summary>
	/// Instance Id.
	/// </summary>
	public uint Id
	{
		get { return id; }
	}

	/// <summary>
	/// Import properties from bytes array.
	/// </summary>
	/// <param name="properties"></param>
	/// <returns></returns>
	public bool Deserialize(PropertyValue[] properties)
	{
		for (byte i = 0; i < properties.Length; i++)
		{
			var pt = this.definition.GetPropertyDefByIndex(i);
			if (pt != null)
			{
				var pv = properties[i];
				LoadProperty(pt.Name, pv.Age, pv.Date, pv.Value);
			}
		}

		return true;
	}

	public string ToJson()
	{
		IResource res;
		if (resource.TryGetTarget(out res))
			return JsonSerializer.Serialize(res, Global.SerializeOptions);
		else
			return null;
	}

	/// <summary>
	/// Export all properties with ResourceProperty attributed as bytes array.
	/// </summary>
	/// <returns></returns>
	public PropertyValue[] Serialize()
	{
		IResource res;

		if (!resource.TryGetTarget(out res))
			throw new Exception("Resource no longer available.");


		if (res is IDynamicResource dynamicResource)
			return dynamicResource.SerializeResource();

		var props = new List<PropertyValue>();

		foreach (var pt in definition.Properties)
		{
			var rt = pt.PropertyInfo.GetValue(res, null);
			props.Add(new PropertyValue(rt, ages[pt.Index], modificationDates[pt.Index]));
		}

		return props.ToArray();
	}

	/// <summary>
	/// Export all properties with ResourceProperty attributed as bytes array after a specific age.
	/// </summary>
	/// <returns></returns>
	public Map<byte, PropertyValue> SerializeAfter(ulong age = 0)
	{
		IResource res;

		if (!resource.TryGetTarget(out res))
			throw new Exception("Resource no longer available.");

		if (res is IDynamicResource dynamicResource)
			return dynamicResource.SerializeResourceAfter(age);

		var props = new Map<byte, PropertyValue>();

		foreach (var pt in definition.Properties)
		{
			if (res.Instance.GetAge(pt.Index) > age)
			{
				var rt = pt.PropertyInfo.GetValue(res, null);
				props.Add(pt.Index,
					new PropertyValue(rt,
					ages[pt.Index],
					modificationDates[pt.Index]));
			}
		}

		return props;
	}

	/// <summary>Exports every property keyed by its member index.</summary>
	public Map<byte, PropertyValue> SerializeMap()
	{
		var values = Serialize();
		var map = new Map<byte, PropertyValue>();
		for (byte index = 0; index < values.Length; index++)
			map.Add(index, values[index]);
		return map;
	}


	/// <summary>
	/// If True, the instance can be stored to disk.
	/// </summary>
	/// <returns></returns>
	public bool IsStorable()
	{
#if NETSTANDARD
		var attrs = resource.GetType().GetTypeInfo().GetCustomAttributes(typeof(Storable), true).ToArray();
#else
            var attrs = resource.GetType().GetCustomAttributes(typeof(Storable), true);
#endif
		return attrs.Length > 0;

	}


	internal void EmitModification(PropertyDef pt, object value)
	{

		IResource res;
		if (this.resource.TryGetTarget(out res))
		{
			lock (journalSync)
			{
				var cursor = NextCursor();
				var now = DateTime.UtcNow;

				ages[pt.Index] = cursor.Revision;
				modificationDates[pt.Index] = now;
				instanceModificationDate = now;

				store.Modify(res, pt, value, cursor.Revision, now);
				CommitJournal(res, new ResourceJournalEntry(
					cursor,
					now,
					ResourceJournalEntryKind.PropertyModified,
					pt.Index,
					value), pt.Historical);

				PropertyModified?.Invoke(new PropertyModificationInfo(res, pt, value, cursor, now));
			}
		}
	}

	/// <summary>
	/// Notify listeners that a property was modified.
	/// </summary>
	/// <param name="propertyName"></param>
	/// <param name="newValue"></param>
	/// <param name="oldValue"></param>
	public void Modified([CallerMemberName] string propertyName = "")
	{
		if (loading)
			return;

		object value;
		if (TryGetPropertyValue(propertyName, out value))
		{
			var pt = definition.GetPropertyDefByName(propertyName);
			EmitModification(pt, value);
		}
	}



	//        internal void EmitResourceEvent(string name, string[] users, EpConnection[] connections, object[] args)

	internal void EmitCustomResourceEvent(object issuer, Func<Session, bool> receivers, EventDef eventDef, object value)
	{
		IResource res;
		if (this.resource.TryGetTarget(out res))
		{
			lock (journalSync)
			{
				var cursor = NextCursor();
				var now = DateTime.UtcNow;
				instanceModificationDate = now;
				// Receiver predicates are session-specific and cannot be replayed safely.
				CommitJournal(res, new ResourceJournalEntry(
					cursor,
					now,
					ResourceJournalEntryKind.EventOccurred,
					eventDef.Index,
					value), false);
				CustomEventOccurred?.Invoke(new CustomEventOccurredInfo(
					res, eventDef, receivers, issuer, value, cursor, now));
			}
		}
	}

	internal void EmitResourceEvent(EventDef eventDef, object value)
	{
		IResource res;
		if (this.resource.TryGetTarget(out res))
		{
			EmitResourceEventCore(res, eventDef, value);
		}
	}

	internal void EmitResourceEventByIndex(byte eventIndex, object value)
	{
		IResource res;
		if (this.resource.TryGetTarget(out res))
		{
			var eventDef = definition.GetEventDefByIndex(eventIndex);
			EmitResourceEventCore(res, eventDef, value);
		}
	}

	internal void EmitCustomResourceEventByIndex(object issuer, Func<Session, bool> receivers, byte eventIndex, object value)
	{
		IResource res;
		if (this.resource.TryGetTarget(out res))
		{
			var eventDef = definition.GetEventDefByIndex(eventIndex);
			EmitCustomResourceEvent(issuer, receivers, eventDef, value);
		}
	}

	void EmitResourceEventCore(IResource res, EventDef eventDef, object value)
	{
		if (eventDef == null)
			return;

		lock (journalSync)
		{
			var cursor = NextCursor();
			var now = DateTime.UtcNow;
			instanceModificationDate = now;
			CommitJournal(res, new ResourceJournalEntry(
				cursor,
				now,
				ResourceJournalEntryKind.EventOccurred,
				eventDef.Index,
				value), eventDef.Historical);
			EventOccurred?.Invoke(new EventOccurredInfo(res, eventDef, value, cursor, now));
		}
	}

	ResourceCursor NextCursor()
	{
		instanceAge++;
		return new ResourceCursor(streamGeneration, instanceAge);
	}

	void CommitJournal(IResource res, ResourceJournalEntry entry, bool retain)
	{
		if (store is IResourceJournalStore journal &&
			!journal.AppendJournalEntry(res, entry, retain))
			throw new InvalidOperationException(
				$"The store rejected resource journal revision {entry.Cursor} for `{Link}`.");
	}

	/// <summary>Reads retained changes from this resource's owning store.</summary>
	public ResourceJournalPage QueryJournal(ResourceJournalQuery query)
	{
		lock (journalSync)
		{
			if (resource.TryGetTarget(out var res) && store is IResourceJournalStore journal)
			{
				var page = journal.QueryJournal(res, query ?? new ResourceJournalQuery());
				var cursor = new ResourceCursor(streamGeneration, instanceAge);
				if (page.HighWatermark == cursor)
					return page;

				return new ResourceJournalPage(
					page.OldestAvailable,
					cursor,
					page.Next,
					page.CursorExpired,
					page.HasMore,
					page.Entries);
			}

			var currentCursor = new ResourceCursor(streamGeneration, instanceAge);
			return new ResourceJournalPage(currentCursor, currentCursor, query?.After ?? currentCursor,
				false, false, Array.Empty<ResourceJournalEntry>());
		}
	}

	/// <summary>
	/// Runs an attach/replay operation against an atomic resource high-watermark.
	/// Emission uses the same lock, so notifications after the callback are live
	/// changes strictly newer than the captured cursor.
	/// </summary>
	internal T SynchronizeJournal<T>(Func<ResourceCursor, T> action)
	{
		lock (journalSync)
			return action(new ResourceCursor(streamGeneration, instanceAge));
	}

	internal void ObserveRemoteCursor(ResourceCursor cursor, DateTime recordedAt)
	{
		lock (journalSync)
		{
			if (streamGeneration != cursor.Generation)
			{
				streamGeneration = cursor.Generation;
				instanceAge = cursor.Revision;
			}
			else if (cursor.Revision > instanceAge)
				instanceAge = cursor.Revision;

			if (recordedAt > instanceModificationDate)
				instanceModificationDate = recordedAt;
		}
	}

	internal bool IsNewerPropertyRevision(byte index, ResourceCursor cursor)
	{
		lock (journalSync)
		{
			if (streamGeneration != cursor.Generation)
				return true;
			return index >= ages.Count || !ages[index].HasValue || ages[index].Value < cursor.Revision;
		}
	}

	internal void ApplyRemotePropertyModification(
		PropertyDef propertyDef,
		object value,
		ResourceCursor cursor,
		DateTime recordedAt)
	{
		if (!resource.TryGetTarget(out var res))
			return;

		lock (journalSync)
		{
			ObserveRemoteCursor(cursor, recordedAt);
			ages[propertyDef.Index] = cursor.Revision;
			modificationDates[propertyDef.Index] = recordedAt;
			PropertyModified?.Invoke(new PropertyModificationInfo(
				res, propertyDef, value, cursor, recordedAt));
		}
	}

	internal void ApplyRemoteEvent(
		EventDef eventDef,
		object value,
		ResourceCursor cursor,
		DateTime recordedAt)
	{
		if (!resource.TryGetTarget(out var res))
			return;

		lock (journalSync)
		{
			ObserveRemoteCursor(cursor, recordedAt);
			EventOccurred?.Invoke(new EventOccurredInfo(res, eventDef, value, cursor, recordedAt));
		}
	}


	/// <summary>
	/// Get the value of a given property by name.
	/// </summary>
	/// <param name="name">Property name</param>
	/// <param name="value">Output value</param>
	/// <returns>True, if the resource has the property.</returns>
	public bool TryGetPropertyValue(string name, out object value)
	{
		var pt = definition.GetPropertyDefByName(name);

		IResource res;
		if (resource.TryGetTarget(out res))
		{
			if (res is IDynamicResource dynamicResource)
			{
				value = dynamicResource.GetResourceProperty(pt.Index);
				return true;
			}
			else if (pt != null && pt.PropertyInfo != null)
			{
				value = pt.PropertyInfo.GetValue(res, null);
				return true;
			}
		}

		value = null;
		return false;
	}

	public object GetPropertyValueOrDefault(string name, object defaultValue = null)
	{
		object value;
		if (TryGetPropertyValue(name, out value))
			return value;
		else
			return defaultValue;
	}

	/// <summary>
	/// Store responsible for creating and keeping the resource.
	/// </summary>
	public IStore Store
	{
		get { return store; }
	}

	public bool IsDestroyed { get; private set; }

	/// <summary>
	/// The unique and permanent link to the resource.
	/// </summary>
	public string Link
	{
		get
		{
			IResource res;
			if (this.resource.TryGetTarget(out res))
			{
				if (res == res.Instance.store)
					return name; // root store
				else
					return store.Instance.name + "/" + store.Link(res);
			}
			else
				return null;
		}
	}

	public AsyncBag<T> Children<T>(string name = null) where T : IResource
	{
		IResource res;
		if (this.resource.TryGetTarget(out res))
		{
			return store.Children<T>(res, name);
		}
		else
			return new AsyncBag<T>(null);
	}

	public AsyncBag<T> Parents<T>(string name = null) where T : IResource
	{
		IResource res;
		if (this.resource.TryGetTarget(out res))
		{
			return store.Parents<T>(res, name);
		}
		else
			return new AsyncBag<T>(default(T[]));
	}


	/// <summary>
	/// Instance name.
	/// </summary>
	public string Name
	{
		get { return name; }
		set { name = value; }
	}


	/// <summary>
	/// Resource managed by this instance.
	/// </summary>
	public IResource Resource
	{
		get
		{
			IResource res;
			if (this.resource.TryGetTarget(out res))
			{
				return res;
			}
			else
				return null;
		}
	}

	/// <summary>
	/// Resource TypeDef describes the properties, functions and events of the resource.
	/// </summary>
	public TypeDef Definition
	{
		get { return definition; }
 
	}

	/// <summary>
	/// Check for permission.
	/// </summary>
	/// <param name="session">Caller sessions.</param>
	/// <param name="action">Action type</param>
	/// <param name="member">Function, property or event to check for permission.</param>
	/// <param name="inquirer">Permission inquirer.</param>
	/// <returns>Ruling.</returns>
	public Ruling Applicable(Session session, ActionType action, MemberDef member, object inquirer = null)
	{
		var context = MakeManagerContext(session, action, member, inquirer);
		return Warehouse.EvaluatePermissions(context, managers.ToArray());
	}

	/// <summary>
	/// Evaluates the Warehouse defaults and this resource's manager snapshot.
	/// </summary>
	public ResourceManagerEvaluation EvaluateManagers(
		Session session,
		ActionType action,
		MemberDef member,
		object inquirer = null)
	{
		var context = MakeManagerContext(session, action, member, inquirer);
		return Warehouse.EvaluateManagers(context, managers.ToArray());
	}

	ResourceManagerContext MakeManagerContext(
		Session session,
		ActionType action,
		MemberDef member,
		object inquirer)
	{
		resource.TryGetTarget(out var res);
		return new ResourceManagerContext(
			Warehouse,
			inquirer as EpConnection,
			session,
			res,
			member,
			action,
			inquirer,
			member?.MemberPolicyAttributes);
	}

	/// <summary>
	/// Execution managers.
	/// </summary>
	public AutoList<IResourceManager, Instance> Managers => managers;

	void ManagerAdded(Instance instance, IResourceManager manager)
	{
		if (manager != null && Warehouse.IsRegisteredManager(manager))
			return;

		managers.Remove(manager);
		throw new InvalidOperationException(
			$"Resource manager `{manager?.GetType()}` must be registered with this Warehouse before it is attached.");
	}

	public readonly Warehouse Warehouse;
	/// <summary>
	/// Create new instance.
	/// </summary>
	/// <param name="id">Instance Id.</param>
	/// <param name="name">Name of the instance.</param>
	/// <param name="resource">Resource to manage.</param>
	/// <param name="store">Store responsible for the resource.</param>
	public Instance(
		Warehouse warehouse,
		uint id,
		string name,
		IResource resource,
		IStore store,
		ulong age = 0,
		string resourceKey = null)
	{
		this.Warehouse = warehouse;
		this.store = store;
		this.resource = new WeakReference<IResource>(resource);
		this.id = id;
		this.name = name ?? "";
		this.instanceAge = age;
		this.streamGeneration = Guid.NewGuid();

		if (store is IResourceJournalStore journal)
		{
			var cursor = journal.OpenJournal(
				resource,
				resourceKey ?? name ?? string.Empty,
				new ResourceCursor(streamGeneration, instanceAge));
			streamGeneration = cursor.Generation;
			instanceAge = Math.Max(instanceAge, cursor.Revision);
		}

		//this.attributes = new KeyList<string, object>(this);
		//children = new AutoList<IResource, Instance>(this);
		//parents = new AutoList<IResource, Instance>(this);
		managers = new AutoList<IResourceManager, Instance>(this);
		managers.OnAdd += ManagerAdded;
		//children.OnAdd += Children_OnAdd;
		//children.OnRemoved += Children_OnRemoved;
		//parents.OnAdd += Parents_OnAdd;
		//parents.OnRemoved += Parents_OnRemoved;

		resource.OnDestroy += Resource_OnDestroy;

		if (resource is IDynamicResource dynamicResource)
		{
			this.definition = dynamicResource.ResourceDefinition;
			warehouse.RegisterDynamicTypeDef(this.definition);
		}
		else
		{
			this.definition = warehouse.GetLocalTypeDefByType(resource.GetType());
		}

		// set ages
		for (byte i = 0; i < definition.Properties.Length; i++)
		{
			ages.Add(0);
			modificationDates.Add(DateTime.MinValue);
		}


		// connect events
		if (!(resource is EpResource))
		{
			if (resource is IDynamicResourceEventSource dynamicEventSource)
				dynamicEventSource.ResourceEventOccurred += EmitResourceEventByIndex;

			Type t = ResourceProxy.GetBaseType(resource);

			var events = t.GetTypeInfo().GetEvents(BindingFlags.Public | BindingFlags.Instance);

			var emitEventByIndexMethod = GetType().GetMethod("EmitResourceEventByIndex", BindingFlags.Instance | BindingFlags.NonPublic);
			var emitCustomEventByIndexMethod = GetType().GetMethod("EmitCustomResourceEventByIndex", BindingFlags.Instance | BindingFlags.NonPublic);

			foreach (var evt in definition.Events)
			{

				if (evt.EventInfo == null)
					continue;

				var eventGenericType = evt.EventInfo.EventHandlerType.GetGenericTypeDefinition();

				if (eventGenericType == typeof(ResourceEventHandler<>))
				{

					var dm = new DynamicMethod("_", null,
					   new Type[] { typeof(Instance), evt.EventInfo.EventHandlerType.GenericTypeArguments[0] },
					   typeof(Instance).Module, true);


					var il = dm.GetILGenerator();
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Ldc_I4, (int)evt.Index);
					il.Emit(OpCodes.Ldarg_1);
					il.Emit(OpCodes.Box, evt.EventInfo.EventHandlerType.GenericTypeArguments[0]);
					il.Emit(OpCodes.Callvirt, emitEventByIndexMethod);
					il.Emit(OpCodes.Nop);
					il.Emit(OpCodes.Ret);


					var proxyDelegate = dm.CreateDelegate(evt.EventInfo.EventHandlerType, this);

					//ResourceEventHandler<object> proxyDelegate = new ResourceEventHandler<object>((args) => EmitResourceEvent(evt, args));
					evt.EventInfo.AddEventHandler(resource, proxyDelegate);


				}
				else if (eventGenericType == typeof(CustomResourceEventHandler<>))
				{
					var dm = new DynamicMethod("_", null,
					   new Type[] { typeof(Instance), typeof(object), typeof(Func<Session, bool>),
					   evt.EventInfo.EventHandlerType.GenericTypeArguments[0] },
					   typeof(Instance).Module, true);


					var il = dm.GetILGenerator();
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Ldarg_1);
					il.Emit(OpCodes.Ldarg_2);
					il.Emit(OpCodes.Ldc_I4, (int)evt.Index);
					il.Emit(OpCodes.Ldarg_3);
					il.Emit(OpCodes.Box, evt.EventInfo.EventHandlerType.GenericTypeArguments[0]);
					il.Emit(OpCodes.Callvirt, emitCustomEventByIndexMethod);
					il.Emit(OpCodes.Nop);
					il.Emit(OpCodes.Ret);


					var proxyDelegate = dm.CreateDelegate(evt.EventInfo.EventHandlerType, this);

					evt.EventInfo.AddEventHandler(resource, proxyDelegate);
				}

			}

		}
	}

	private void Resource_OnDestroy(object sender)
	{
		IsDestroyed = true;
		Destroyed?.Invoke((IResource)sender);
	}
}
