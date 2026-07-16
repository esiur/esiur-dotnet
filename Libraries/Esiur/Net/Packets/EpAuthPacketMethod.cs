using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum EpAuthPacketMethod
    {
        // Initialize
        Initialize = 0x00,
        //InitializeEncrypted = 0x01,
        //InitializeNoAuth = 0x02,
        //InitializeNoAuthEncrypted = 0x3,

        // Actions
        Handshake = 0x80,
        FinalHandshake = 0x81,
        KeyRotation = 0x82,

        // Acks
        Denied = 0x40, // no reason, terminate connection
        NotSupported = 0x41, // auth not supported, terminate connection
        TrySupported = 0x42, // auth not supported, but other auth methods in the reply are supported. connection is still open
        Retry = 0x43, // try another auth method, connection is still open
        ProceedToHandshake = 0x44, // auth method accepted, proceed to handshake, connection is still open
        ProceedToFinalHandshake = 0x45, // auth method accepted, proceed to final handshake, connection is still open
        ProceedToEstablishSession = 0x46, // auth method accepted, proceed to establish session, connection is still open
        SessionEstablished = 0x47, // session established, session Id provided, switch to session mode, connection is still open

        // Events
        Established = 0xC0,
        ErrorTerminate = 0xC1,
        ErrorMustEncrypt = 0xC2,
        ErrorRetry = 0xC3,

        IndicationEstablished = 0xC8,
        KeyRotationEstablished = 0xC9,

        IAuthPlain = 0xD0,
        IAuthHashed = 0xD1,
        IAuthEncrypted = 0xD2

    }
}
