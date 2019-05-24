using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template
{
    public class MemberTemplate
    {
        public enum MemberType
        {
            Function = 0,
            Property = 1,
            Event = 2,
        }

        public byte Index => index;
        public string Name => name;
        public MemberType Type => type;

        ResourceTemplate template;
        string name;
        MemberType type;
        byte index;

        public ResourceTemplate Template => template;

        public MemberTemplate(ResourceTemplate template, MemberType type, byte index, string name)
        {
            this.template = template;
            this.type = type;
            this.index = index;
            this.name = name;
        }

        public string Fullname => template.ClassName + "." + Name;

        public virtual byte[] Compose()
        {
            return DC.ToBytes(Name);
        }
    }

}
