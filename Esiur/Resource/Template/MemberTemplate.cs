using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template;
public class MemberTemplate
{

    public byte Index { get; set; }
    public string Name { get; set; }
    public bool Inherited { get; set; }
    public TypeTemplate Template { get; set; }

    //public MemberTemplate()
    //{
    //    Template = template;
    //    Index = index;
    //    Name = name;
    //    Inherited = inherited;
    //}

    public string Fullname => Template.ClassName + "." + Name;

    //public virtual byte[] Compose()
    //{
    //    return DC.ToBytes(Name);
    //}
}

