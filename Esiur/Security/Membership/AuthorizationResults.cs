using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Membership
{
    public class AuthorizationResults
    {
        public AuthorizationResultsResponse Response { get; set; }


        public uint Reference { get; set; }
        public IIPAuthPacketIAuthDestination Destination { get; set; }
        public string Clue { get; set; }
        public IIPAuthPacketIAuthFormat? RequiredFormat { get; set; }
        public IIPAuthPacketIAuthFormat? ContentFormat { get; set; }
        public object? Content { get; set; }

        public byte? Trials { get; set; }

        public DateTime? Issue { get; set; } = DateTime.UtcNow;
        public DateTime? Expire { get; set; }

        public int Timeout => Expire.HasValue && Issue.HasValue ? (int)(Expire.Value - Issue.Value).TotalSeconds : 0;

        public bool Expired => DateTime.Now > Expire;
    }
}
