using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ResourceFunction : System.Attribute
    {
        private string expansion = null;
        
        public string Expansion
        {
            get
            {
                return expansion;
            }
        }


        public ResourceFunction(string expansion = null)
        {
            this.expansion = expansion;
        }
    }
}
