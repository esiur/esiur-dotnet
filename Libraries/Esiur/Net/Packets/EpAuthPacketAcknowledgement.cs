using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum EpAuthPacketAcknowledgement : byte
    {
        Denied = 0x40, // no reason, terminate connection
        NotSupported = 0x41, // auth not supported, terminate connection
        TrySupported = 0x42, // auth not supported, but other auth methods in the reply are supported. connection is still open
        Retry = 0x43, // try another auth method, connection is still open
        ProceedToHandshake = 0x44, // auth method accepted, proceed to handshake, connection is still open
        ProceedToFinalHandshake = 0x45, // auth method accepted, proceed to final handshake, connection is still open
        ProceedToEstablishSession = 0x46, // auth method accepted, proceed to establish session, connection is still open
        SessionEstablished = 0x47, // session established, session Id provided, switch to session mode, connection is still open
    }
}
