using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPAuthPacketIAuthDestination
    {
        Self = 0,
        Device = 1, // logged in device
        Email = 2,
        SMS = 3,
        App = 4, // Authenticator app
        ThirdParty = 5, // usualy a second person
    }
}
