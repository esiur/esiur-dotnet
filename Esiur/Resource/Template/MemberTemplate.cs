using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template;
public class MemberTemplate
{

    public readonly byte Index;
    public readonly string Name;
    public readonly bool Inherited;
    public readonly TypeTemplate Template;

    public MemberTemplate(TypeTemplate template, byte index, string name, bool inherited)
    {
        Template = template;
        Index = index;
        Name = name;
        Inherited = inherited;
    }

    public string Fullname => Template.ClassName + "." + Name;

    public virtual byte[] Compose()
    {
        return DC.ToBytes(Name);
    }
}

