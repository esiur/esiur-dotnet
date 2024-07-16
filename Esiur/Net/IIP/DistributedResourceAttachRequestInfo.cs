using Esiur.Core;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.IIP
{
    internal class DistributedResourceAttachRequestInfo
    {
        public AsyncReply<DistributedResource> Reply { get; set; }
        public uint[] RequestSequence { get; set; }

          public DistributedResourceAttachRequestInfo(AsyncReply<DistributedResource> reply, uint[] requestSequence)
        {
            Reply = reply;
            RequestSequence = requestSequence;
        }   
    }
}
