using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource
{

    [AttributeUsage(AttributeTargets.Property)]
    public class ResourceProperty : System.Attribute
    {
        string readExpansion;
        string writeExpansion;


        public string ReadExpansion
        {
            get
            {
                return readExpansion;
            }
        }

        public string WriteExpansion
        {
            get
            {
                return writeExpansion;
            }
        }

        public ResourceProperty(string readExpansion = null, string writeExpansion = null)
        {
            this.readExpansion = readExpansion;
            this.writeExpansion = writeExpansion;
        }
    }
}
