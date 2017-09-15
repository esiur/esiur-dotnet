using Esiur.Resource;
using System;
using Esiur.Engine;
using MongoDB.Driver.Core;
using MongoDB.Driver;
using MongoDB.Bson;
using Esiur.Data;
using System.Collections.Generic;
using System.Reflection;

namespace Esiur.Stores.MongoDB
{
    public class MongoDBStore : IStore
    {
        public Instance Instance { get; set; }

        public event DestroyedEvent OnDestroy;
        MongoClient client;
        IMongoDatabase database;

        Dictionary<string, IResource> resources = new Dictionary<string, IResource>();


        public int Count
        {
            get { return resources.Count; }
        }
        public void Destroy()
        {

        }

        public MongoDBStore()
        {
            client = new MongoClient();
            this.database = client.GetDatabase("esiur");
        }

        public MongoDBStore(string connectionString, string database)
        {
            client = new MongoClient(connectionString);
            this.database = client.GetDatabase(database);
        }


        AsyncReply<IResource> Fetch(string id)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonObjectId(new ObjectId(id)));
            var list = this.database.GetCollection<BsonDocument>("resources").Find(filter).ToList();
            if (list.Count == 0)
                return new AsyncReply<IResource>(null);
            var document = list[0];


            IResource resource = (IResource)Activator.CreateInstance(Type.GetType(document["classname"].AsString));
            resources.Add(document["_id"].AsObjectId.ToString(), resource);

            Warehouse.Put(resource, document["name"].AsString, this);


            var parents = document["parents"].AsBsonArray;
            var children = document["children"].AsBsonArray;

            var bag = new AsyncBag<object>();

            foreach (var p in parents)
            {
                var ap = Warehouse.Get(p.AsString);
                bag.Add(ap);
                ap.Then((x) =>
                {
                    resource.Instance.Parents.Add(x);
                });
            }

            foreach (var c in children)
            {

                var ac = Warehouse.Get(c.AsString);
                bag.Add(ac);
                ac.Then((x) =>
                {
                    resource.Instance.Children.Add(x);
                });
            }

            // Load values
            var values = document["values"].AsBsonDocument;


            foreach (var v in values)
            {
#if NETSTANDARD1_5
                var pi = resource.GetType().GetTypeInfo().GetProperty(v.Name);
#else
                var pi = resource.GetType().GetProperty(pt.Name);
#endif

                var av = Parse(v.Value);
                bag.Add(av);
                av.Then((x) =>
                {
                    if (pi.CanWrite)
                        pi.SetValue(resource, DC.CastConvert(x, pi.PropertyType));
                });
            }

            bag.Seal();

            var rt = new AsyncReply<IResource>();

            bag.Then((x) =>
            {
                rt.Trigger(resource);
            });

            return rt;
        }

        AsyncReply Parse(BsonValue value)
        {
            if (value.BsonType == BsonType.Document)
            {
                var doc = value.AsBsonDocument;
                if (doc["type"] == 0)
                {
                    return Warehouse.Get(doc["link"].AsString);
                } // structure
                else if (doc["type"] == 1)
                {
                    var bag = new AsyncBag<object>();
                    var rt = new AsyncReply<Structure>();

                    var bs = (BsonDocument)doc["values"].AsBsonDocument;
                    var s = new Structure();

                    foreach (var v in bs)
                        bag.Add(Parse(v.Value));

                    bag.Seal();
                    bag.Then((x) =>
                    {
                        for (var i = 0; i < x.Length; i++)
                            s[bs.GetElement(i).Name] = x[i];

                        rt.Trigger(s);
                    });

                    return rt;
                }
                else
                    return new AsyncReply(null);
            }
            else if (value.BsonType == BsonType.Array)
            {
                var array = value.AsBsonArray;
                var bag = new AsyncBag<object>();

                foreach (var v in array)
                    bag.Add(Parse(v));

                bag.Seal();

                return bag;
            }
            else
            {
                return new AsyncReply(value.RawValue);
            }
        }

        public AsyncReply<IResource> Get(string path)
        {
            var p = path.Split('/');
            if (p.Length == 2)
                if (p[0] == "id")
                {
                    // load from Id

                    if (resources.ContainsKey(p[1]))
                        return new AsyncReply<IResource>(resources[p[1]]);
                    else
                        return Fetch(p[1]);
                }

            return new AsyncReply<IResource>(null);
        }

        public string Link(IResource resource)
        {
            return this.Instance.Name + "/id/" + (string)resource.Instance.Attributes["objectId"];
        }

        public bool Put(IResource resource)
        {

            foreach (var kv in resources)
                if (kv.Value == resource)
                {
                    resource.Instance.Attributes.Add("objectId", kv.Key);
                    return true;
                }

            var parents = new BsonArray();
            var children = new BsonArray();
            var template = resource.Instance.Template;

            foreach (IResource c in resource.Instance.Children)
                children.Add(c.Instance.Link);

            foreach (IResource p in resource.Instance.Parents)
                parents.Add(p.Instance.Link);

            var document = new BsonDocument
            {
                { "parents", parents },
                { "children", children },
                { "classname", resource.GetType().AssemblyQualifiedName },
                { "name", resource.Instance.Name }
            };


            var col = this.database.GetCollection<BsonDocument>("resources");

            col.InsertOne(document);

            resource.Instance.Attributes["objectId"] = document["_id"].ToString();

            var values = new BsonDocument();

            foreach (var pt in template.Properties)
            {
#if NETSTANDARD1_5
                var pi = resource.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                var pi = resource.GetType().GetProperty(pt.Name);
#endif
                var rt = pi.GetValue(resource, null);

                values.Add(pt.Name, Compose(rt));
            }

            var filter = Builders<BsonDocument>.Filter.Eq("_id", document["_id"]);
            var update = Builders<BsonDocument>.Update
                .Set("values", values);

            col.UpdateOne(filter, update);

            //document.Add("values", values);

            //col.ReplaceOne(document, document);
            return true;
        }


        public BsonDocument ComposeStructure(Structure value)
        {
            var rt = new BsonDocument { { "type", 1 } };

            var values = new BsonDocument();
            foreach (var i in value)
                values.Add(i.Key, Compose(i.Value));

            rt.Add("values", values);
            return rt;
        }

        public BsonArray ComposeVarArray(object[] array)
        {
            var rt = new BsonArray();

            for (var i = 0; i < array.Length; i++)
                rt.Add(Compose(array[i]));

            return rt;
        }

        BsonArray ComposeStructureArray(Structure[] structures)
        {
            var rt = new BsonArray();

            if (structures == null || structures?.Length == 0)
                return rt;

            foreach (var s in structures)
                rt.Add(ComposeStructure(s));

            return rt;
        }

        BsonArray ComposeResourceArray(IResource[] array)
        {
            var rt = new BsonArray();
            foreach (var r in array)
            {
                rt.Add(new BsonDocument { { "type", 0 }, { "link", r.Instance.Link } });

                //if (r.Instance.Attributes.ContainsKey("objectId"))

                //rt.Add(new BsonObjectId(new ObjectId((string)r.Instance.Attributes["objectId"])));
            }

            return rt;
        }

        private BsonValue Compose(object value)
        {
            var type = Codec.GetDataType(value, null);

            switch (type)
            {
                case DataType.Void:
                    // nothing to do;
                    return BsonNull.Value;

                case DataType.String:
                    return new BsonString((string)value);

                case DataType.Resource:
                case DataType.DistributedResource:
                    return new BsonDocument { { "type", 0 }, { "link", (value as IResource).Instance.Link } };

                //return new BsonObjectId(new ObjectId((string)(value as IResource).Instance.Attributes["objectId"]));

                case DataType.Structure:
                    return ComposeStructure((Structure)value);

                case DataType.VarArray:
                    return ComposeVarArray((object[])value);

                case DataType.ResourceArray:
                    if (value is IResource[])
                        return ComposeResourceArray((IResource[])value);
                    else
                        return ComposeResourceArray((IResource[])DC.CastConvert(value, typeof(IResource[])));


                case DataType.StructureArray:
                    return ComposeStructureArray((Structure[])value);


                default:
                    return BsonValue.Create(value);
            }
        }

        public AsyncReply<IResource> Retrieve(uint iid)
        {
            throw new NotImplementedException();
        }

        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {

            if (trigger == ResourceTrigger.Initialize)
            {
                var filter = new BsonDocument();

                var list = this.database.GetCollection<BsonDocument>("resources").Find(filter).ToList();


                // if (list.Count == 0)
                //   return new AsyncBag<IResource>(new IResource[0]);

                var bag = new AsyncBag<IResource>();

                foreach (var r in list)
                {
                    bag.Add(Get("id/" + r["_id"].AsObjectId.ToString()));
                }

                bag.Seal();

                var rt = new AsyncReply<bool>();

                bag.Then((x) => { rt.Trigger(true); });

                return rt;
            }
            else if (trigger == ResourceTrigger.Terminate)
            {
                // save all resources
                foreach (var resource in resources.Values)
                    SaveResource(resource);

                return new AsyncReply<bool>(true);
            }
            else
                return new AsyncReply<bool>(true);
        }


        public void SaveResource(IResource resource)
        {
            var parents = new BsonArray();
            var children = new BsonArray();
            var template = resource.Instance.Template;

            foreach (IResource c in resource.Instance.Children)
                children.Add(c.Instance.Link);

            foreach (IResource p in resource.Instance.Parents)
                parents.Add(p.Instance.Link);

            var values = new BsonDocument();

            foreach (var pt in template.Properties)
            {
#if NETSTANDARD1_5
                var pi = resource.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                var pi = resource.GetType().GetProperty(pt.Name);
#endif
                var rt = pi.GetValue(resource, null);

                values.Add(pt.Name, Compose(rt));
            }

            var document = new BsonDocument
            {
                { "parents", parents },
                { "children", children },
                { "classname", resource.GetType().AssemblyQualifiedName },
                { "name", resource.Instance.Name },
                { "_id", new BsonObjectId(new ObjectId(resource.Instance.Attributes["objectId"].ToString())) },
                {"values", values }
            };


            var col = this.database.GetCollection<BsonDocument>("resources");

            var filter = Builders<BsonDocument>.Filter.Eq("_id", document["_id"]);
            var update = Builders<BsonDocument>.Update
                .Set("values", values);

            col.UpdateOne(filter, update);

        }
    }
}
