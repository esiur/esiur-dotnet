using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets;

struct IIPPacketAttachInfo
{
    public string Link;
    public ulong Age;
    public byte[] Content;
    public UUID ClassId;

    public IIPPacketAttachInfo(UUID classId, ulong age, string link, byte[] content)
    {
        ClassId = classId;
        Age = age;
        Content = content;
        Link = link;
    }
}
