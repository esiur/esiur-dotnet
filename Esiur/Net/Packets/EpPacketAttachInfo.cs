using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets;

struct EpPacketAttachInfo
{
    public string Link;
    public ulong Age;
    public byte[] Content;
    public Uuid TypeId;

    public EpPacketAttachInfo(Uuid typeId, ulong age, string link, byte[] content)
    {
        TypeId = typeId;
        Age = age;
        Content = content;
        Link = link;
    }
}
