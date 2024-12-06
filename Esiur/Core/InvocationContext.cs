using Esiur.Net.IIP;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Core
{
    public class InvocationContext
    {
        private uint CallbackId;

        internal bool Ended;

        public void Chunk(object value)
        {
            if (Ended)
                throw new Exception("Execution has ended.");

            Connection.SendChunk(CallbackId, value);
        }

        public void Progress(int value, int max) {

            if (Ended)
                throw new Exception("Execution has ended.");

            Connection.SendProgress(CallbackId, value, max);
        }

        public DistributedConnection Connection { get; internal set; }


        internal InvocationContext(DistributedConnection connection, uint callbackId)
        {
            Connection = connection;
            CallbackId = callbackId;

        }
    }
}
