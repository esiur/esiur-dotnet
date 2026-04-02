using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum EpAuthPacketEncryptionMode
    {
        NoEncryption,
        EncryptWithSessionKey,
        EncryptWithSessionKeyAndAddress,
    }
}
