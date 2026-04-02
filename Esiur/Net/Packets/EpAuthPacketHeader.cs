using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum EpAuthPacketHeader
    {
        Version,
        Domain,
        SupportedAuthentications ,
        SupportedHashAlgorithms,
        SupportedCiphers,
        SupportedCompression,
        SupportedMultiFactorAuthentications,
        CipherType,
        CipherKey,
        SoftwareIdentity,
        Referrer,
        Time,
        IPAddress,
        AuthenticationData,
    }
}
