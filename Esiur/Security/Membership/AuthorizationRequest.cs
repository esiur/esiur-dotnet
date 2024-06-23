using Esiur.Data;
using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Membership
{
    public class AuthorizationRequest
    {
        public uint Reference { get; set; }
        public IIPAuthPacketIAuthDestination Destination { get; set; }
        public string Clue { get; set; }
        public IIPAuthPacketIAuthFormat? RequiredFormat { get; set; }
        public IIPAuthPacketIAuthFormat? ContentFormat { get; set; }
        public object? Content { get; set; }

        public byte? Trials { get; set; }

        public DateTime? Issue { get; set; }
        public DateTime? Expire { get; set; }

        public int Timeout => Expire.HasValue && Issue.HasValue ? (int)(Expire.Value - Issue.Value).TotalSeconds : 0;

        public AuthorizationRequest(Map<IIPAuthPacketIAuthHeader, object> headers)
        {
            Reference = (uint)headers[IIPAuthPacketIAuthHeader.Reference];
            Destination = (IIPAuthPacketIAuthDestination)headers[IIPAuthPacketIAuthHeader.Destination];
            Clue = (string)headers[IIPAuthPacketIAuthHeader.Clue];

            if (headers.ContainsKey(IIPAuthPacketIAuthHeader.RequiredFormat))
                RequiredFormat = (IIPAuthPacketIAuthFormat)headers[IIPAuthPacketIAuthHeader.RequiredFormat];

            if (headers.ContainsKey(IIPAuthPacketIAuthHeader.ContentFormat))
                ContentFormat = (IIPAuthPacketIAuthFormat)headers[IIPAuthPacketIAuthHeader.ContentFormat];

            if (headers.ContainsKey(IIPAuthPacketIAuthHeader.Content))
                Content = headers[IIPAuthPacketIAuthHeader.Content];

            if (headers.ContainsKey(IIPAuthPacketIAuthHeader.Trials))
                Trials = (byte)headers[IIPAuthPacketIAuthHeader.Trials];

            if (headers.ContainsKey(IIPAuthPacketIAuthHeader.Issue))
                Issue = (DateTime)headers[IIPAuthPacketIAuthHeader.Issue];

            if (headers.ContainsKey(IIPAuthPacketIAuthHeader.Expire))
                Expire = (DateTime)headers[IIPAuthPacketIAuthHeader.Expire];
        }
    }
}
