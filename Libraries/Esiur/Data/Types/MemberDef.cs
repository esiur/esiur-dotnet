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
    

    public string Fullname => Definition.Name + "." + Name;

}

