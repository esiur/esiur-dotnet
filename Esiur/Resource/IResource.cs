using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esiur.Data;
using Esiur.Engine;

namespace Esiur.Resource
{
    public delegate bool QueryFilter<T>(T value);

    public interface IResource : IDestructible
    {

        AsyncReply<bool> Trigger(ResourceTrigger trigger);
        
        Instance Instance
        {
            get;
            set;
        }
    }
}
