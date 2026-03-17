using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Data.Types;
public class MemberDef
{

    public byte Index { get; set; }
    public string Name { get; set; }
    public bool Inherited { get; set; }
    public TypeDef Definition { get; set; }

    //public MemberTemplate()
    //{
    //    Template = template;
    //    Index = index;
    //    Name = name;
    //    Inherited = inherited;
    //}

    public string Fullname => Definition.Name + "." + Name;

    //public virtual byte[] Compose()
    //{
    //    return DC.ToBytes(Name);
    //}
}

