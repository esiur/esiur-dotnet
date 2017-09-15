using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public class Source
    {

        string id;
        KeyList<SourceAttributeType, Structure> attributes;

        string Id { get { return id; } }

        KeyList<SourceAttributeType, Structure> Attributes
        {
            get { return attributes; }
        }

        public Source(string id, KeyList<SourceAttributeType, Structure> attributes)
        {
            this.id = id;
            this.attributes = attributes;
        }

    }
}
