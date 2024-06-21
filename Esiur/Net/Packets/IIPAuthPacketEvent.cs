using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPAuthPacketEvent : byte
    {
        ErrorTerminate = 0xC0,
        ErrorMustEncrypt = 0xC1,
        ErrorRetry = 0xC2,

        IndicationEstablished = 0xC8,

        IAuthPlain = 0xD0,
        IAuthHashed = 0xD1,
        IAuthEncrypted = 0xD2
    }
}
