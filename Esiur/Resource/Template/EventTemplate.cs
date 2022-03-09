using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template;
public class EventTemplate : MemberTemplate
{
    public string Expansion
    {
        get;
        set;
    }

    public bool Listenable { get; set; }

    public EventInfo EventInfo { get; set; }

    public RepresentationType ArgumentType { get; set; }

    public override byte[] Compose()
    {
        var name = base.Compose();

        var hdr = Inherited ? (byte)0x80 : (byte)0;
        
        if (Listenable)
            hdr |= 0x8;

        if (Expansion != null)
        {
            var exp = DC.ToBytes(Expansion);
            hdr |= 0x50;
            return new BinaryList()
                    .AddUInt8(hdr)
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(ArgumentType.Compose())
                    .AddInt32(exp.Length)
                    .AddUInt8Array(exp)
                    .ToArray();
        }
        else
            hdr |= 0x40;
            return new BinaryList()
                    .AddUInt8(hdr)
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(ArgumentType.Compose())
                    .ToArray();
    }

     public EventTemplate(TypeTemplate template, byte index, string name,bool inherited, RepresentationType argumentType, string expansion = null, bool listenable = false)
        : base(template, index, name, inherited)
    {
        this.Expansion = expansion;
        this.Listenable = listenable;
        this.ArgumentType = argumentType;
    }
}
