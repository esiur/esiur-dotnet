using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPAuthPacketIAuthFormat
    {
        None = 0,
        Number = 1,
        Text = 2,
        LowercaseText = 3,
        Choice = 4,
        Photo = 5,
        Signature = 6,
        Fingerprint = 7,
    }

}
