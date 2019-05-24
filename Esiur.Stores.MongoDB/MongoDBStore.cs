using Esiur.Resource;
using System;
using Esiur.Engine;
using MongoDB.Driver.Core;
using MongoDB.Driver;
using MongoDB.Bson;
using Esiur.Data;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Esiur.Resource.Template;
using System.Linq;
using Esiur.Security.Permissions;

namespace Esiur.Stores.MongoDB
{
    public class MongoDBStore : IStore
    {
        public Instance Instance { get; set; }

        public event DestroyedEvent OnDestroy;
        MongoClient client;
        IMongoDatabase database;
        IMongoCollection<BsonDocument> resourcesCollection;

        Dictionary<string, IResource> resources = new Dictionary<string, IResource>();


        public int Count
        {
            get { return resources.Count; }
        }

        public void Destroy()
        {

        }

        
        public bool Record(IResource resource, string propertyName, object value, ulong age, DateTime date)
        {
            var objectId = resource.Instance.Attributes["objectId"].ToString();
            //var bsonObjectId = new BsonObjectId(new ObjectId(objectId));

            var record = this.database.GetCollection<BsonDocument>("record_" + objectId);

            record.InsertOne(new BsonDocument()
            {
                {"property", propertyName}, {"age", BsonValue.Create(age) }, {"date", date}, {"value", Compose(value) }
            });

            var col = this.database.GetCollection<BsonDocument>("resources");



            var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonObjectId(new ObjectId(objectId)));
            var update = Builders<BsonDocument>.Update
                .Set("values." + propertyName, new BsonDocument { { "age", BsonValue.Create(age) },
                                     { "modification", date },
                                     { "value", Compose(value) } });

            col.UpdateOne(filter, update);

            return true;
        }

        public MongoDBStore()
        {
            client = new MongoClient();
            this.database = client.GetDatabase("esiur");
            this.resourcesCollection = this.database.GetCollection<BsonDocument>("resources");

        }

        public MongoDBStore(string connectionString, string database)
        {
            client = new MongoClient(connectionString);
            this.database = client.GetDatabase(database);
            this.resourcesCollection = this.database.GetCollection<BsonDocument>("resources");
        }

        public bool Remove(IResource resource)
        {
            var objectId = resource.Instance.Attributes["objectId"].ToString();
            var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonObjectId(new ObjectId(objectId)));

            this.database.DropCollection("record_" + objectId);
            resourcesCollection.DeleteOne(filter);

            return true;
        }

        AsyncReply<IResource> Fetch(string id)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonObjectId(new ObjectId(id)));
            var list = resourcesCollection.Find(filter).ToList();
            if (list.Count == 0)
                return new AsyncReply<IResource>(null);
            var document = list[0];


            IResource resource = (IResource)Activator.CreateInstance(Type.GetType(document["classname"].AsString));
            resources.Add(document["_id"].AsObjectId.ToString(), resource);

            Warehouse.Put(resource, document["name"].AsString, this);


            var parents = document["parents"].AsBsonArray;
            var children = document["children"].AsBsonArray;
            //var managers = document["managers"].AsBsonArray;

            var attributes = Parse(document["attributes"]).Then(x=> {
                resource.Instance.SetAttributes(x as Structure);
            });

            var bag = new AsyncBag<object>();

            foreach (var p in parents)
            {
                var ap = Warehouse.Get(p.AsString);
                bag.Add(ap);
                ap.Then((x) =>
                {
                    if (!resource.Instance.Parents.Contains(x))
                        resource.Instance.Parents.Add(x);
                });
            }

            foreach (var c in children)
            {

                var ac = Warehouse.Get(c.AsString);
                bag.Add(ac);
                ac.Then((x) =>
                {
                    if (!resource.Instance.Children.Contains(x))
                        resource.Instance.Children.Add(x);
                });
            }

            /*
            // load managers
            foreach(var m in managers)
            {
                IPermissionsManager pm = (IPermissionsManager)Activator.CreateInstance(Type.GetType(m["classname"].AsString));
                var sr = Parse(m["settings"]);
                bag.Add(sr);
                sr.Then((x) =>
                {
                    pm.Initialize((Structure)x, resource);
                    resource.Instance.Managers.Add(pm);
                });
            }
            */

            // Load values
            var values = document["values"].AsBsonDocument;


            foreach (var v in values)
            {
 

                var valueInfo = v.Value as BsonDocument;

                var av = Parse(valueInfo["value"]);
                bag.Add(av);
                av.Then((x) =>
                {
                    resource.Instance.LoadProperty(v.Name, (ulong)valueInfo["age"].AsInt64, valueInfo["modification"].ToUniversalTime(), x);
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
            else if (value.BsonType == BsonType.DateTime)
            {
                return new AsyncReply(value.ToUniversalTime());
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

            var attrs = resource.Instance.GetAttributes();

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
            

            var attrsDoc = ComposeStructure(attrs);

           

            
            var values = new BsonDocument();

            foreach (var pt in template.Properties)
            {
#if NETSTANDARD1_5
                var pi = resource.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                var pi = resource.GetType().GetProperty(pt.Name);
#endif
                var rt = pi.GetValue(resource, null);
 
                values.Add(pt.Name,
                  new BsonDocument { { "age", BsonValue.Create(resource.Instance.GetAge(pt.Index)) },
                                     { "modification", resource.Instance.GetModificationDate(pt.Index) },
                                     { "value", Compose(rt) } });
            }

//            var filter = Builders<BsonDocument>.Filter.Eq("_id", document["_id"]);
//            var update = Builders<BsonDocument>.Update
//                .Set("values", values);
//            col.UpdateOne(filter, update);


            var document = new BsonDocument
            {
                { "parents", parents },
                { "children", children },
                { "attributes", attrsDoc },
                { "classname", resource.GetType().FullName + "," + resource.GetType().GetTypeInfo().Assembly.GetName().Name },
                { "name", resource.Instance.Name },
                { "values", values }
            };


            resourcesCollection.InsertOne(document);

            resource.Instance.Attributes["objectId"] = document["_id"].ToString();

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

                var list = resourcesCollection.Find(filter).ToList();

                Console.WriteLine(list.Count);

                // if (list.Count == 0)
                //   return new AsyncBag<IResource>(new IResource[0]);

                var bag = new AsyncBag<IResource>();

                for(var i = 0; i < list.Count; i++)
                {
                    Console.WriteLine("Loading {0}/{1}", i, list.Count);
                    bag.Add(Get("id/" + list[i]["_id"].AsObjectId.ToString()));
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
            var attrs = resource.Instance.GetAttributes();

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

                values.Add(pt.Name,
                                      new BsonDocument { { "age", BsonValue.Create(resource.Instance.GetAge(pt.Index)) },
                                     { "modification", resource.Instance.GetModificationDate(pt.Index) },
                                     { "value", Compose(rt) } });

            }

            var attrsDoc = ComposeStructure(attrs);

            var document = new BsonDocument
            {
                { "parents", parents },
                { "children", children },
                {"attributes", attrsDoc },
                { "classname", resource.GetType().FullName + "," + resource.GetType().GetTypeInfo().Assembly.GetName().Name },
                { "name", resource.Instance.Name },
                { "_id", new BsonObjectId(new ObjectId(resource.Instance.Attributes["objectId"].ToString())) },
                {"values", values }
            };



            var filter = Builders<BsonDocument>.Filter.Eq("_id", document["_id"]);

            /*
            var update = Builders<BsonDocument>.Update
                .Set("values", values);

            var update = Builders<BsonDocument>.Update.Set("values", values).Set("parents", parents;
                        col.UpdateOne(filter, update);

            */

            resourcesCollection.ReplaceOne(filter, document);
        }

        public AsyncReply<PropertyValue[]> GetPropertyRecordByAge(IResource resource, string propertyName, ulong fromAge, ulong toAge)
        {
            var objectId = resource.Instance.Attributes["objectId"].ToString();

            var record = this.database.GetCollection<BsonDocument>("record_" + objectId);
            var builder = Builders<BsonDocument>.Filter;

            var filter = builder.Gte("age", fromAge) & builder.Lte("age", toAge) & builder.Eq("property", propertyName);

            var reply = new AsyncReply<PropertyValue[]>();

            record.FindAsync(filter).ContinueWith((x) =>
            {
                var values = ((Task<IAsyncCursor<BsonDocument>>)x).Result.ToList();

                var bag = new AsyncBag<object>();

                foreach(var v in values)
                    bag.Add(Parse(v["value"]));
                
                bag.Seal();

                bag.Then((results) =>
                {
                    var list = new List<PropertyValue>();
                    for(var i = 0; i < results.Length; i++)
                        list.Add(new PropertyValue(results[i], (ulong)values[i]["age"].AsInt64, values[i]["date"].ToUniversalTime()));

                    reply.Trigger(list.ToArray());
                });

            });

            return reply;
        }

        public AsyncReply<PropertyValue[]> GetPropertyRecordByDate(IResource resource, string propertyName, DateTime fromDate, DateTime toDate)
        {
            var objectId = resource.Instance.Attributes["objectId"].ToString();

            var record = this.database.GetCollection<BsonDocument>("record_" + objectId);
            var builder = Builders<BsonDocument>.Filter;

            var filter = builder.Gte("date", fromDate) & builder.Lte("date", toDate) & builder.Eq("property", propertyName);

            var reply = new AsyncReply<PropertyValue[]>();

            record.FindAsync(filter).ContinueWith((x) =>
            {
                var values = ((Task<IAsyncCursor<BsonDocument>>)x).Result.ToList();

                var bag = new AsyncBag<object>();

                foreach (var v in values)
                    bag.Add(Parse(v["value"]));

                bag.Seal();

                bag.Then((results) =>
                {
                    var list = new List<PropertyValue>();
                    for (var i = 0; i < results.Length; i++)
                        list.Add(new PropertyValue(results[i], (ulong)values[i]["age"].AsInt64, values[i]["date"].ToUniversalTime()));

                    reply.Trigger(list.ToArray());
                });

            });

            return reply;
        }

        AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> GetRecordByAge(IResource resource, ulong fromAge, ulong toAge)
        {
            var properties = resource.Instance.Template.Properties.Where(x => x.Storage == StorageMode.Recordable).ToList();

            var reply = new AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>>();

            AsyncBag<PropertyValue> bag = new AsyncBag<PropertyValue>();

            foreach (var p in properties)
                bag.Add(GetPropertyRecordByAge(resource, p.Name, fromAge, toAge));

            bag.Seal();

            bag.Then(x =>
            {
                var list = new KeyList<PropertyTemplate, PropertyValue>();

                for (var i = 0; i < x.Length; i++)
                    list.Add(properties[i], x[i]);

                reply.Trigger(list);
            });
            
            return reply;
        }

        public AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> GetRecord(IResource resource, DateTime fromDate, DateTime toDate)
        {
            var properties = resource.Instance.Template.Properties.Where(x => x.Storage == StorageMode.Recordable).ToList();

            var reply = new AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>>();

            AsyncBag<PropertyValue[]> bag = new AsyncBag<PropertyValue[]>();

            foreach (var p in properties)
                bag.Add(GetPropertyRecordByDate(resource, p.Name, fromDate, toDate));

            bag.Seal();

            bag.Then(x =>
            {
                var list = new KeyList<PropertyTemplate, PropertyValue[]>();

                for (var i = 0; i < x.Length; i++)
                    list.Add(properties[i], x[i]);

                reply.Trigger(list);
            });

            return reply;
        }

        public bool Modify(IResource resource, string propertyName, object value, ulong age, DateTime dateTime)
        {
            

            var objectId = resource.Instance.Attributes["objectId"].ToString();

            var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonObjectId(new ObjectId(objectId)));
            var update = Builders<BsonDocument>.Update
                .Set("values." + propertyName, new BsonDocument { { "age", BsonValue.Create(age) },
                                     { "modification", dateTime },
                                     { "value", Compose(value) } });

            resourcesCollection.UpdateOne(filter, update);

            return true;

        }
    }
}
