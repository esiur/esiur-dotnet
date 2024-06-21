using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Membership
{
    public class AuthorizationResults
    {
        public AuthorizationResultsResponse Response { get; set; }
        public IIPAuthPacketIAuthDestination Destination { get; set; }
        public IIPAuthPacketIAuthFormat RequiredFormat { get; set; }
        public string Clue { get; set; }

        public ushort Timeout { get; set; } // 0 means no timeout
        public uint Reference { get; set; }

        public DateTime Issue { get; set; } = DateTime.UtcNow;

        public bool Expired => Timeout == 0 ? false : (DateTime.UtcNow - Issue).TotalSeconds > Timeout;
    }
}
