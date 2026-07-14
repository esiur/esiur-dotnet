/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/
using Esiur.Data;
using Esiur.Core;
using Esiur.Net;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Security.Cryptography;
using Esiur.Net.Packets;

namespace Esiur.Security.Authority;

/// <summary>
/// Authentication metadata exchanged while establishing a session.
/// Members retain the indexes defined by <see cref="EpAuthPacketHeader"/>, so this
/// structure has the same wire representation as the former header map.
/// </summary>
public sealed class SessionHeaders : IndexedStructure
{
    [Index((int)EpAuthPacketHeader.Version)]
    public object? Version { get; set; }

    [Index((int)EpAuthPacketHeader.Domain)]
    public string? Domain { get; set; }

    [Index((int)EpAuthPacketHeader.SupportedAuthentications)]
    public object? SupportedAuthentications { get; set; }

    [Index((int)EpAuthPacketHeader.SupportedHashAlgorithms)]
    public object? SupportedHashAlgorithms { get; set; }

    [Index((int)EpAuthPacketHeader.SupportedCiphers)]
    public object? SupportedCiphers { get; set; }

    [Index((int)EpAuthPacketHeader.SupportedCompression)]
    public object? SupportedCompression { get; set; }

    [Index((int)EpAuthPacketHeader.SupportedMultiFactorAuthentications)]
    public object? SupportedMultiFactorAuthentications { get; set; }

    [Index((int)EpAuthPacketHeader.CipherType)]
    public object? CipherType { get; set; }

    [Index((int)EpAuthPacketHeader.CipherKey)]
    public byte[]? CipherKey { get; set; }

    [Index((int)EpAuthPacketHeader.SoftwareIdentity)]
    public string? SoftwareIdentity { get; set; }

    [Index((int)EpAuthPacketHeader.Referrer)]
    public string? Referrer { get; set; }

    [Index((int)EpAuthPacketHeader.Time)]
    public DateTime? Time { get; set; }

    [Index((int)EpAuthPacketHeader.IPAddress)]
    public byte[]? IPAddress { get; set; }

    [Index((int)EpAuthPacketHeader.Identity)]
    public string? Identity { get; set; }

    [Index((int)EpAuthPacketHeader.AuthenticationProtocol)]
    public string? AuthenticationProtocol { get; set; }

    [Index((int)EpAuthPacketHeader.AuthenticationData)]
    public object? AuthenticationData { get; set; }

    [Index((int)EpAuthPacketHeader.ErrorMessage)]
    public string? ErrorMessage { get; set; }

    internal SessionHeaders Copy() => (SessionHeaders)MemberwiseClone();
}

public class Session
{
    public byte[] Id { get; set; }
    public DateTime Creation { get; }
    public DateTime Modification { get; }
    public KeyList<string, object> Variables { get; } = new KeyList<string, object>();



    //public IKeyExchanger KeyExchanger { get; set; } = null;
    public ISymetricCipher SymetricCipher { get; set; } = null;


    public SessionHeaders LocalHeaders { get; set; } = new SessionHeaders();
    public SessionHeaders RemoteHeaders { get; set; } = new SessionHeaders();

    //public AuthenticationMethod AuthenticationMethod { get; set; }
    //public AuthenticationMethod RemoteMethod { get; set; }

    public AuthenticationMode AuthenticationMode { get; set; }
    public EncryptionMode EncryptionMode { get; set; }

    public IAuthenticationHandler AuthenticationHandler { get; set; }
    //public IAuthenticationHandler AuthenticationResponder { get; set; }

    public string LocalIdentity { get; set; }
    public string RemoteIdentity { get; set; }

    public bool Authenticated { get; set; } = false;

    public byte[] Key { get; set; }
}
