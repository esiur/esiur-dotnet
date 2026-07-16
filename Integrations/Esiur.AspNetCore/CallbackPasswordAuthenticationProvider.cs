using Esiur.Core;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Esiur.AspNetCore;

internal sealed class CallbackPasswordAuthenticationProvider
    : PasswordAuthenticationProvider
{
    private const int CredentialLength = 32;
    private const int SaltLength = 32;
    private const int MaximumIdentityBytes = 512;
    private const int MaximumDomainBytes = 512;

    private readonly Func<string, string, PasswordHash?> findCredential;
    private readonly byte[] dummyVerifier =
        RandomNumberGenerator.GetBytes(CredentialLength);
    private readonly byte[] dummySalt =
        RandomNumberGenerator.GetBytes(SaltLength);

    public CallbackPasswordAuthenticationProvider(
        Func<string, string, PasswordHash?> findCredential)
    {
        this.findCredential = findCredential;
    }

    public override PasswordHash GetHostedAccountCredential(
        string identity,
        string domain)
    {
        identity ??= string.Empty;
        domain ??= string.Empty;

        // Match PPAP's identity bounds before invoking application code or allocating
        // dummy-HMAC input for attacker-controlled handshake values.
        if (Encoding.UTF8.GetByteCount(identity) > MaximumIdentityBytes
            || Encoding.UTF8.GetByteCount(domain) > MaximumDomainBytes)
            return default;

        var credential = findCredential(identity, domain);
        if (credential is null)
            return CreateDummyCredential(identity, domain);

        if (credential.Value.Hash is null || credential.Value.Salt is null)
        {
            throw new InvalidOperationException(
                "A password credential must contain both a hash and a salt.");
        }

        if (credential.Value.Hash.Length != CredentialLength
            || credential.Value.Salt.Length != SaltLength)
        {
            throw new InvalidOperationException(
                $"A {ProtocolName} credential must contain a "
                + $"{CredentialLength}-byte hash and a {SaltLength}-byte salt.");
        }

        return new PasswordHash(
            (byte[])credential.Value.Hash.Clone(),
            (byte[])credential.Value.Salt.Clone());
    }

    private PasswordHash CreateDummyCredential(string identity, string domain)
    {
        // A single dummy salt shared by every missing account would itself identify
        // unknown users. Derive account-shaped material from provider-local random
        // secrets instead: the result is stable for retries of the same identity,
        // unlinkable across identities, and never aliases the provider-held secrets.
        var context = EncodeIdentity(identity, domain);
        try
        {
            return new PasswordHash(
                HMACSHA256.HashData(dummyVerifier, context),
                HMACSHA256.HashData(dummySalt, context));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(context);
        }
    }

    private static byte[] EncodeIdentity(string identity, string domain)
    {
        var identityBytes = Encoding.UTF8.GetBytes(identity ?? string.Empty);
        var domainBytes = Encoding.UTF8.GetBytes(domain ?? string.Empty);
        var context = new byte[
            sizeof(int) + domainBytes.Length + sizeof(int) + identityBytes.Length];

        BinaryPrimitives.WriteInt32LittleEndian(
            context.AsSpan(0, sizeof(int)),
            domainBytes.Length);
        domainBytes.CopyTo(context, sizeof(int));

        var identityLengthOffset = sizeof(int) + domainBytes.Length;
        BinaryPrimitives.WriteInt32LittleEndian(
            context.AsSpan(identityLengthOffset, sizeof(int)),
            identityBytes.Length);
        identityBytes.CopyTo(context, identityLengthOffset + sizeof(int));

        return context;
    }

    public override AsyncReply<bool> Login(Session session) => new(true);

    public override AsyncReply<bool> Logout(Session session) => new(true);
}
