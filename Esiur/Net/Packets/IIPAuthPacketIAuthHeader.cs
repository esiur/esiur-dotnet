using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPAuthPacketIAuthHeader : byte
    {
        Reference = 0,
        Destination = 1,
        Clue = 2,
        RequiredFormat = 3,
        ContentFormat = 4,
        Content = 5,
        Trials = 6,
        Issue = 7,
        Expire = 8,
    }
}
