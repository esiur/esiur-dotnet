/*
 
Copyright (c) 2020 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esiur.Core;
using Esiur.Data;
using Esiur.Proxy;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Stores.MongoDB
{
    public class MongoDBStore<T> : MongoDBStore where T:IResource
    {
        [Export]
        public async AsyncReply<T> New(string name = null, object properties = null)
        {
            var resource = Instance.Warehouse.Create<T>(properties);
            await Instance.Warehouse.Put(this.Instance.Name + "/" + name, resource);
            resource.Instance.Managers.AddRange(this.Instance.Managers.ToArray());
            return resource;
        }

        [Export]
        public async AsyncReply<IResource[]> Slice(int index, int limit)
        {
            var list = await this.Instance.Children<IResource>();
            return list.Skip(index).Take(limit).ToArray();
        }

    }
}
