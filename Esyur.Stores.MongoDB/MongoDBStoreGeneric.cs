using Esyur.Core;
using Esyur.Data;
using Esyur.Proxy;
using Esyur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esyur.Stores.MongoDB
{
    public class MongoDBStore<T> : MongoDBStore where T:IResource
    {
        [ResourceFunction]
        public T Create(string name, Structure values)
        {
            return  Warehouse.New<T>(name, this, null, null, null, null, values);
        }

        [ResourceFunction]
        public async AsyncReply<IResource[]> Slice(int index, int limit)
        {
            var list = await this.Instance.Children<IResource>();
            return list.Skip(index).Take(limit).ToArray();
        }

    }
}
