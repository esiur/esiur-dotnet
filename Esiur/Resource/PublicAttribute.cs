using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Event | AttributeTargets.Class)]

    public class PublicAttribute : Attribute
    {

      //  public StorageMode Storage { get; set; }

        //public bool Serialize { get; set; }

        public PublicAttribute()//StorageMode storage = StorageMode.NonVolatile, bool serialize = true)
        {
          //  Storage = storage;
            //Serialize = serialize;
        }
    }
}
