/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

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

using Esiur.Data;
using Esiur.Core;
using Esiur.Resource.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Security.Permissions;
using Esiur.Security.Authority;

namespace Esiur.Resource;
public interface IStore : IResource
{
    AsyncReply<IResource> Get(string path);//, Func<IResource, bool> filter = null);
                                           //AsyncReply<IResource> Retrieve(uint iid);
    AsyncReply<bool> Put(IResource resource);
    string Link(IResource resource);
    bool Record(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime);
    bool Modify(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime);
    bool Remove(IResource resource);

    //bool RemoveAttributes(IResource resource, string[] attributes = null);

    //Structure GetAttributes(IResource resource, string[] attributes = null);

    //bool SetAttributes(IResource resource, Structure attributes, bool clearAttributes = false);



    AsyncReply<bool> AddChild(IResource parent, IResource child);
    AsyncReply<bool> RemoveChild(IResource parent, IResource child);

    AsyncReply<bool> AddParent(IResource child, IResource parent);
    AsyncReply<bool> RemoveParent(IResource child, IResource parent);


    AsyncBag<T> Children<T>(IResource resource, string name) where T : IResource;
    AsyncBag<T> Parents<T>(IResource resource, string name) where T : IResource;



    //AsyncReply<PropertyValue[]> GetPropertyRecord(IResource resource, string propertyName, ulong fromAge, ulong toAge);
    //AsyncReply<PropertyValue[]> GetPropertyRecordByDate(IResource resource, string propertyName, DateTime fromDate, DateTime toDate);

    //AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> GetRecord(IResource resource, ulong fromAge, ulong toAge);
    // AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> GetRecordByDate(IResource resource, DateTime fromDate, DateTime toDate);

    AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> GetRecord(IResource resource, DateTime fromDate, DateTime toDate);
}
