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
    public UUID TypeId;

    public EpPacketAttachInfo(UUID typeId, ulong age, string link, byte[] content)
    {
        TypeId = typeId;
        Age = age;
        Content = content;
        Link = link;
    }
}
