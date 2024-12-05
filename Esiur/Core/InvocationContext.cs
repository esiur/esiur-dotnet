using Esiur.Net.IIP;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Core
{
    public class InvocationContext
    {
        private uint CallbackId;

        public void Chunk(object value)
        {

        }

        public void Progress(int value) { 

        }

        public DistributedConnection Connection { get; internal set; }


        internal InvocationContext(DistributedConnection connection, uint callbackId)
        {
            Connection = connection;
            CallbackId = callbackId;

        }
    }
}
