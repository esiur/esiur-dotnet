using Esiur.Core;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Protocol;

internal class EpResourceAttachRequestInfo
{
    public AsyncReply<EpResource> Reply { get; set; }
    public uint[] RequestSequence { get; set; }

    public EpResourceAttachRequestInfo(AsyncReply<EpResource> reply, uint[] requestSequence)
    {
        Reply = reply;
        RequestSequence = requestSequence;
    }
}
