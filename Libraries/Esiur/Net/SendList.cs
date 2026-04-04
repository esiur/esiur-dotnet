using Esiur.Core;
using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net;

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
        var s = this.ToArray();
        //Console.WriteLine($"Sending {s.Length} -> {DC.ToHex(s)}");
        connection.Send(s);
        return reply;
    }
}
