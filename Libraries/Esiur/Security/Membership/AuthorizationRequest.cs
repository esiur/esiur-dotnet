using Esiur.Data;
using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace Esiur.Security.Membership
{
    public class AuthorizationRequest
    {
        public uint Reference { get; set; }
        public EpAuthPacketIAuthDestination Destination { get; set; }
        public string Clue { get; set; }
        public EpAuthPacketIAuthFormat? RequiredFormat { get; set; }
        public EpAuthPacketIAuthFormat? ContentFormat { get; set; }
        public object? Content { get; set; }

        public byte? Trials { get; set; }

        public DateTime? Issue { get; set; }
        public DateTime? Expire { get; set; }

        public int Timeout => Expire.HasValue && Issue.HasValue ? (int)(Expire.Value - Issue.Value).TotalSeconds : 0;

        public AuthorizationRequest(Map<EpAuthPacketIAuthHeader, object> headers)
        {
            Reference = (uint)headers[EpAuthPacketIAuthHeader.Reference];
            Destination =(EpAuthPacketIAuthDestination)headers[EpAuthPacketIAuthHeader.Destination];
            Clue = (string)headers[EpAuthPacketIAuthHeader.Clue];

            if (headers.ContainsKey(EpAuthPacketIAuthHeader.RequiredFormat))
                RequiredFormat = (EpAuthPacketIAuthFormat)headers[EpAuthPacketIAuthHeader.RequiredFormat];

            if (headers.ContainsKey(EpAuthPacketIAuthHeader.ContentFormat))
                ContentFormat = (EpAuthPacketIAuthFormat)headers[EpAuthPacketIAuthHeader.ContentFormat];

            if (headers.ContainsKey(EpAuthPacketIAuthHeader.Content))
                Content = headers[EpAuthPacketIAuthHeader.Content];

            if (headers.ContainsKey(EpAuthPacketIAuthHeader.Trials))
                Trials = (byte)headers[EpAuthPacketIAuthHeader.Trials];

            if (headers.ContainsKey(EpAuthPacketIAuthHeader.Issue))
                Issue = (DateTime)headers[EpAuthPacketIAuthHeader.Issue];

            if (headers.ContainsKey(EpAuthPacketIAuthHeader.Expire))
                Expire = (DateTime)headers[EpAuthPacketIAuthHeader.Expire];
        }
    }
}
