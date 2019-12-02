using Esyur.Core;
using Esyur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esyur.Net
{
    public class SendList : BinaryList
    {
        NetworkConnection connection;
        IAsyncReply<object[]> reply;

        public SendList(NetworkConnection connection, IAsyncReply<object[]> reply)
        {
            this.reply = reply;
            this.connection = connection;
        }

        public override IAsyncReply<object[]> Done()
        {
            connection.Send(this.ToArray());
            return reply;
        }
    }
}
