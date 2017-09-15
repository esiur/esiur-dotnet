using Esiur.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource
{
    public interface IStore:IResource
    {
        AsyncReply<IResource> Get(string path);
        AsyncReply<IResource> Retrieve(uint iid);
        bool Put(IResource resource);
        string Link(IResource resource);
    }
}
