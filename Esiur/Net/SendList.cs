using Esiur.Core;
using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net
{
    public class SendList : BinaryList
    {
        NetworkConnection connection;
        AsyncReply<object[]> reply;

        public SendList(NetworkConnection connection, AsyncReply<object[]> reply)
        {
            this.reply = reply;
            this.connection = connection;
        }

        public override AsyncReply<object[]> Done()
        {
            connection.Send(this.ToArray());
            return reply;
        }
    }
}
