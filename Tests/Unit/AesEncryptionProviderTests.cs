using System.Reflection;
using System.Security.Cryptography;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;
using Esiur.Security.Cryptography;

namespace Esiur.Tests.Unit;

public class AesEncryptionProviderTests
{
    static readonly byte[] SharedKey = Enumerable.Range(1, 64).Select(x => (byte)x).ToArray();
    static readonly byte[] InitiatorNonce = Enumerable.Range(21, 32).Select(x => (byte)x).ToArray();
    static readonly byte[] ResponderNonce = Enumerable.Range(91, 32).Select(x => (byte)x).ToArray();
    static readonly byte[] InitiatorAddress = { 192, 0, 2, 10 };
    static readonly byte[] ResponderAddress = { 198, 51, 100, 20 };

    readonly AesEncryptionProvider _provider = new();

    [Fact]
    public void DefaultName_IsNegotiableProviderName()
        => Assert.Equal("aes-gcm", _provider.DefaultName);

    [Fact]
    public void FirstRecord_MatchesStableWireVector()
    {
        using var initiator = Create(AuthenticationDirection.Initiator);
        var record = initiator.Encrypt("Esiur AES-GCM vector"u8.ToArray());

        Assert.Equal(
            "0000000000000000D9538F565DFB97D51FE9AA53316A1FE2D5EEF3361A8958E4439C0BE21A6FC840A3DF42F9",
            Convert.ToHexString(record));
    }

    [Fact]
    public void Records_RoundTripInBothDirections()
    {
        using var initiator = Create(AuthenticationDirection.Initiator);
        using var responder = Create(AuthenticationDirection.Responder);

        var request = Enumerable.Range(0, 513).Select(x => (byte)x).ToArray();
        var response = Enumerable.Range(0, 97).Select(x => (byte)(255 - x)).ToArray();

        Assert.Equal(request, responder.Decrypt(initiator.Encrypt(request)));
        Assert.Equal(response, initiator.Decrypt(responder.Encrypt(response)));
        Assert.Empty(responder.Decrypt(initiator.Encrypt(Array.Empty<byte>())));
    }

    [Fact]
    public void RepeatedPlaintext_UsesDistinctSequencesAndCiphertext()
    {
        using var initiator = Create(AuthenticationDirection.Initiator);
        var plaintext = new byte[] { 1, 3, 3, 7 };

        var first = initiator.Encrypt(plaintext);
        var second = initiator.Encrypt(plaintext);

        Assert.False(first.SequenceEqual(second));
        Assert.Equal(new byte[8], first.Take(8).ToArray());
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, second.Take(8).ToArray());
    }

    [Fact]
    public void DirectionalKeys_RejectReflectedRecords()
    {
        using var initiator = Create(AuthenticationDirection.Initiator);
        var record = initiator.Encrypt(new byte[] { 4, 5, 6 });

        Assert.Throws<CryptographicException>(() => initiator.Decrypt(record));
    }

    [Fact]
    public void TamperedCiphertextOrTag_IsRejected()
    {
        using var initiator = Create(AuthenticationDirection.Initiator);
        using var responder = Create(AuthenticationDirection.Responder);
        var record = initiator.Encrypt(Enumerable.Range(0, 64).Select(x => (byte)x).ToArray());
        record[record.Length - 2] ^= 0x80;

        Assert.Throws<CryptographicException>(() => responder.Decrypt(record));
    }

    [Fact]
    public void ReplayedRecord_IsRejected()
    {
        using var initiator = Create(AuthenticationDirection.Initiator);
        using var responder = Create(AuthenticationDirection.Responder);
        var record = initiator.Encrypt(new byte[] { 7, 8, 9 });

        Assert.Equal(new byte[] { 7, 8, 9 }, responder.Decrypt(record));
        Assert.Throws<CryptographicException>(() => responder.Decrypt(record));
    }

    [Fact]
    public void OutOfOrderRecord_IsRejectedWithoutAdvancingReceiveSequence()
    {
        using var initiator = Create(AuthenticationDirection.Initiator);
        using var responder = Create(AuthenticationDirection.Responder);
        var first = initiator.Encrypt(new byte[] { 1 });
        var second = initiator.Encrypt(new byte[] { 2 });

        Assert.Throws<CryptographicException>(() => responder.Decrypt(second));
        Assert.Equal(new byte[] { 1 }, responder.Decrypt(first));
        Assert.Equal(new byte[] { 2 }, responder.Decrypt(second));
    }

    [Fact]
    public void WrongSharedKey_IsRejected()
    {
        using var initiator = Create(AuthenticationDirection.Initiator);
        var wrongKey = (byte[])SharedKey.Clone();
        wrongKey[0] ^= 0xFF;
        using var responder = Create(AuthenticationDirection.Responder, wrongKey);

        Assert.Throws<CryptographicException>(() =>
            responder.Decrypt(initiator.Encrypt(new byte[] { 10, 20, 30 })));
    }

    [Fact]
    public void NegotiatedProtocolName_IsBoundIntoKeyDerivation()
    {
        var offer = new[] { AesEncryptionProvider.Name, "aes-gcm-other" };
        using var initiator = Create(
            AuthenticationDirection.Initiator,
            offeredProtocols: offer);
        using var responder = Create(
            AuthenticationDirection.Responder,
            protocol: "aes-gcm-other",
            offeredProtocols: offer);

        Assert.Throws<CryptographicException>(() =>
            responder.Decrypt(initiator.Encrypt(new byte[] { 1, 2, 3 })));
    }

    [Fact]
    public void OriginalOfferedProtocolList_IsBoundIntoKeyDerivation()
    {
        using var initiator = Create(
            AuthenticationDirection.Initiator,
            offeredProtocols: new[] { "aes-gcm", "future-cipher" });
        using var responder = Create(
            AuthenticationDirection.Responder,
            offeredProtocols: new[] { "aes-gcm" });

        Assert.Throws<CryptographicException>(() =>
            responder.Decrypt(initiator.Encrypt(new byte[] { 1, 2, 3 })));
    }

    [Theory]
    [InlineData(AuthenticationMode.DualIdentity, "password-sha3-v1")]
    [InlineData(AuthenticationMode.InitializerIdentity, "password-sha3-v2")]
    public void AuthenticationNegotiation_IsBoundIntoKeyDerivation(
        AuthenticationMode responderMode,
        string responderProtocol)
    {
        using var initiator = Create(AuthenticationDirection.Initiator);
        using var responder = Create(
            AuthenticationDirection.Responder,
            authenticationMode: responderMode,
            authenticationProtocol: responderProtocol);

        Assert.Throws<CryptographicException>(() =>
            responder.Decrypt(initiator.Encrypt(new byte[] { 1, 2, 3 })));
    }

    [Fact]
    public void AuthenticationDomain_IsBoundIntoKeyDerivation()
    {
        using var initiator = Create(
            AuthenticationDirection.Initiator,
            domain: "realm-a.example");
        using var responder = Create(
            AuthenticationDirection.Responder,
            domain: "realm-b.example");

        Assert.Throws<CryptographicException>(() =>
            responder.Decrypt(initiator.Encrypt(new byte[] { 1, 2, 3 })));
    }

    [Fact]
    public void SetKey_AfterInitialization_IsRejectedForSameOrDifferentKey()
    {
        using var cipher = Create(AuthenticationDirection.Initiator);
        var differentKey = (byte[])SharedKey.Clone();
        differentKey[0] ^= 0xFF;

        Assert.Throws<InvalidOperationException>(() => cipher.SetKey(SharedKey));
        Assert.Throws<InvalidOperationException>(() => cipher.SetKey(differentKey));
    }

    [Fact]
    public void AddressBoundMode_BindsBothPeerAddresses()
    {
        using var initiator = Create(
            AuthenticationDirection.Initiator,
            mode: EncryptionMode.EncryptWithSessionKeyAndAddress);
        using var responder = Create(
            AuthenticationDirection.Responder,
            mode: EncryptionMode.EncryptWithSessionKeyAndAddress);

        Assert.Equal(new byte[] { 42 }, responder.Decrypt(initiator.Encrypt(new byte[] { 42 })));

        using var changedAddress = Create(
            AuthenticationDirection.Responder,
            mode: EncryptionMode.EncryptWithSessionKeyAndAddress,
            responderAddress: new byte[] { 203, 0, 113, 7 });

        var nextInitiator = Create(
            AuthenticationDirection.Initiator,
            mode: EncryptionMode.EncryptWithSessionKeyAndAddress);
        using (nextInitiator)
        {
            Assert.Throws<CryptographicException>(() =>
                changedAddress.Decrypt(nextInitiator.Encrypt(new byte[] { 42 })));
        }
    }

    [Fact]
    public void Dispose_ClearsDerivedSecretsAndRejectsFurtherUse()
    {
        var cipher = Create(AuthenticationDirection.Initiator);
        var secretFields = new[] { "_sendKey", "_receiveKey", "_sendNoncePrefix", "_receiveNoncePrefix" }
            .Select(name => typeof(AesGcmSymetricCipher).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)!)
            .ToArray();
        var secrets = secretFields.Select(field => (byte[])field.GetValue(cipher)!).ToArray();

        Assert.All(secrets, secret => Assert.Contains(secret, value => value != 0));

        cipher.Dispose();

        Assert.All(secrets, secret => Assert.All(secret, value => Assert.Equal((byte)0, value)));
        Assert.All(secretFields, field => Assert.Null(field.GetValue(cipher)));
        Assert.Throws<ObjectDisposedException>(() => cipher.Encrypt(new byte[] { 1 }));
    }

    AesGcmSymetricCipher Create(
        AuthenticationDirection direction,
        byte[]? key = null,
        EncryptionMode mode = EncryptionMode.EncryptWithSessionKey,
        byte[]? initiatorAddress = null,
        byte[]? responderAddress = null,
        string protocol = AesEncryptionProvider.Name,
        string[]? offeredProtocols = null,
        AuthenticationMode authenticationMode = AuthenticationMode.InitializerIdentity,
        string authenticationProtocol = PasswordAuthenticationProvider.ProtocolName,
        string domain = "example.test")
        => (AesGcmSymetricCipher)_provider.CreateCipher(new EncryptionContext
        {
            Key = key ?? SharedKey,
            Direction = direction,
            Mode = mode,
            Protocol = protocol,
            OfferedProtocols = offeredProtocols ?? new[] { AesEncryptionProvider.Name },
            AuthenticationMode = authenticationMode,
            AuthenticationProtocol = authenticationProtocol,
            Domain = domain,
            InitiatorNonce = InitiatorNonce,
            ResponderNonce = ResponderNonce,
            InitiatorAddress = initiatorAddress ?? InitiatorAddress,
            ResponderAddress = responderAddress ?? ResponderAddress,
        });
}
