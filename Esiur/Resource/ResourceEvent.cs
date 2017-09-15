using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource
{

    [AttributeUsage(AttributeTargets.Event)]
    public class ResourceEvent : System.Attribute
    {

        string expansion;

        public string Expansion
        {
            get
            {
                return expansion;
            }
        }
        

        public ResourceEvent(string expansion = null)
        {
            this.expansion = expansion;
        }
    }
}
