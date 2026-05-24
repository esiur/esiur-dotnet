using Esiur.Core;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Protocol;

internal class FetchRequestInfo<TValue, TId>
{
    public AsyncReply<TValue> Reply { get; set; }
    public TId[] RequestSequence { get; set; }

    public FetchRequestInfo(AsyncReply<TValue> reply, TId[] requestSequence)
    {
        Reply = reply;
        RequestSequence = requestSequence;
    }
}
