using System;
using System.Collections.Concurrent;
using System.Text;

namespace Esiur.Security.Authority.Providers.Ppap;

/// <summary>
/// The kind of long-lived identity authenticated by PPAP ML-KEM.
/// </summary>
public enum PpapIdentityKind : byte
{
    PasswordDerived = 1,
    StaticMlKem = 2,
}

/// <summary>
/// Immutable Argon2id parameters used to turn a password into an ML-KEM seed.
/// </summary>
public sealed class PpapKdfProfile : IEquatable<PpapKdfProfile>
{
    public const int Argon2Version13 = 0x13;

    public static PpapKdfProfile Default { get; } =
        new PpapKdfProfile(Argon2Version13, 32 * 1024, 3, 1);

    public int Version { get; }
    public int MemoryKiB { get; }
    public int Iterations { get; }
    public int Parallelism { get; }

    public PpapKdfProfile(int version, int memoryKiB, int iterations, int parallelism)
    {
        if (version != Argon2Version13)
            throw new ArgumentOutOfRangeException(nameof(version), "Only Argon2 version 1.3 is supported.");
        if (memoryKiB < 8 * 1024 || memoryKiB > 256 * 1024)
            throw new ArgumentOutOfRangeException(nameof(memoryKiB));
        if (iterations < 1 || iterations > 10)
            throw new ArgumentOutOfRangeException(nameof(iterations));
        if (parallelism < 1 || parallelism > 16)
            throw new ArgumentOutOfRangeException(nameof(parallelism));
        if (memoryKiB < parallelism * 8)
            throw new ArgumentOutOfRangeException(nameof(memoryKiB));

        Version = version;
        MemoryKiB = memoryKiB;
        Iterations = iterations;
        Parallelism = parallelism;
    }

    public bool Equals(PpapKdfProfile other)
        => other != null
        && Version == other.Version
        && MemoryKiB == other.MemoryKiB
        && Iterations == other.Iterations
        && Parallelism == other.Parallelism;

    public override bool Equals(object obj) => Equals(obj as PpapKdfProfile);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Version;
            hash = (hash * 397) ^ MemoryKiB;
            hash = (hash * 397) ^ Iterations;
            return (hash * 397) ^ Parallelism;
        }
    }
}

/// <summary>
/// Local PPAP identity. Secret material is cloned and is never exposed by a property.
/// </summary>
public sealed class PpapLocalIdentity : IDisposable
{
    readonly byte[] _secret;
    bool _disposed;

    public string Identity { get; }
    public PpapIdentityKind Kind { get; }
    public PpapKdfProfile KdfProfile { get; }

    PpapLocalIdentity(string identity, PpapIdentityKind kind, byte[] secret,
        PpapKdfProfile kdfProfile)
    {
        Identity = PpapCryptography.NormalizeIdentity(identity);
        Kind = kind;
        KdfProfile = kdfProfile;
        _secret = (byte[])secret.Clone();
    }

    public static PpapLocalIdentity FromPassword(string identity, string password,
        PpapKdfProfile kdfProfile = null)
    {
        if (password == null)
            throw new ArgumentNullException(nameof(password));

        var bytes = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));
        try
        {
            return FromPassword(identity, bytes, kdfProfile);
        }
        finally
        {
            PpapCryptography.Clear(bytes);
        }
    }

    public static PpapLocalIdentity FromPassword(string identity, byte[] password,
        PpapKdfProfile kdfProfile = null)
    {
        if (password == null || password.Length == 0)
            throw new ArgumentException("A password is required.", nameof(password));
        if (password.Length > PpapProtocol.MaximumPasswordBytes)
            throw new ArgumentException("The password is too long.", nameof(password));

        return new PpapLocalIdentity(identity, PpapIdentityKind.PasswordDerived,
            password, kdfProfile ?? PpapKdfProfile.Default);
    }

    public static PpapLocalIdentity FromStaticKey(string identity, byte[] privateKey)
    {
        PpapCryptography.ValidatePrivateKey(privateKey);
        return new PpapLocalIdentity(identity, PpapIdentityKind.StaticMlKem,
            privateKey, null);
    }

    /// <summary>
    /// Creates a new static ML-KEM-768 identity with a cryptographically random key.
    /// </summary>
    public static PpapLocalIdentity CreateStatic(string identity)
    {
        PpapCryptography.GenerateKeyPair(out var privateKey, out var publicKey);
        try
        {
            return FromStaticKey(identity, privateKey);
        }
        finally
        {
            PpapCryptography.Clear(privateKey);
            PpapCryptography.Clear(publicKey);
        }
    }

    /// <summary>
    /// Exports a copy of a static ML-KEM private key for durable secure storage.
    /// The caller owns the returned buffer and should erase it after persisting it.
    /// Password-derived identities cannot be exported.
    /// </summary>
    public byte[] ExportStaticPrivateKey()
    {
        ThrowIfDisposed();
        if (Kind != PpapIdentityKind.StaticMlKem)
            throw new InvalidOperationException(
                "Password-derived PPAP identities cannot export private key material.");
        return (byte[])_secret.Clone();
    }

    internal byte[] DerivePrivateKey(string domain, byte[] nonce,
        PpapKdfProfile profile)
    {
        ThrowIfDisposed();

        if (Kind == PpapIdentityKind.StaticMlKem)
        {
            if (nonce != null && nonce.Length != 0)
                throw new InvalidOperationException("A static identity cannot have a KDF nonce.");
            return (byte[])_secret.Clone();
        }

        if (profile == null || !KdfProfile.Equals(profile))
            throw new InvalidOperationException("The registration KDF profile is not accepted by the local identity.");

        return PpapCryptography.DerivePasswordPrivateKey(domain, Identity,
            _secret, nonce, profile);
    }

    internal byte[] GetStaticPublicKey()
    {
        ThrowIfDisposed();
        if (Kind != PpapIdentityKind.StaticMlKem)
            throw new InvalidOperationException("The identity is not backed by a static key.");
        return PpapCryptography.GetPublicKey(_secret);
    }

    void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PpapLocalIdentity));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        PpapCryptography.Clear(_secret);
    }
}

/// <summary>
/// Immutable verifier-side registration data for one PPAP identity.
/// </summary>
public sealed class PpapRegistrationRecord
{
    readonly byte[] _nonce;
    readonly byte[] _encapsulationKey;

    public long Version { get; }
    public string Identity { get; }
    public PpapIdentityKind Kind { get; }
    public PpapKdfProfile KdfProfile { get; }
    public byte[] Nonce => (byte[])_nonce.Clone();
    public byte[] EncapsulationKey => (byte[])_encapsulationKey.Clone();

    internal byte[] NonceBytes => (byte[])_nonce.Clone();
    internal byte[] EncapsulationKeyBytes => (byte[])_encapsulationKey.Clone();

    public PpapRegistrationRecord(long version, string identity, PpapIdentityKind kind,
        byte[] nonce, byte[] encapsulationKey, PpapKdfProfile kdfProfile = null)
    {
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version));
        if (!Enum.IsDefined(typeof(PpapIdentityKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        Identity = PpapCryptography.NormalizeIdentity(identity);
        Version = version;
        Kind = kind;
        PpapCryptography.ValidatePublicKey(encapsulationKey);

        if (kind == PpapIdentityKind.PasswordDerived)
        {
            if (nonce == null || nonce.Length != PpapProtocol.RegistrationNonceLength)
                throw new ArgumentException("A password registration requires a 32-byte nonce.", nameof(nonce));
            KdfProfile = kdfProfile ?? throw new ArgumentNullException(nameof(kdfProfile));
            _nonce = (byte[])nonce.Clone();
        }
        else
        {
            if (nonce != null && nonce.Length != 0)
                throw new ArgumentException("A static-key registration cannot contain a KDF nonce.", nameof(nonce));
            if (kdfProfile != null)
                throw new ArgumentException("A static-key registration cannot contain a KDF profile.", nameof(kdfProfile));
            _nonce = Array.Empty<byte>();
        }

        _encapsulationKey = (byte[])encapsulationKey.Clone();
    }

    public static PpapRegistrationRecord FromPassword(string domain, string identity,
        string password,
        long version = 1, byte[] nonce = null, PpapKdfProfile kdfProfile = null)
    {
        using (var local = PpapLocalIdentity.FromPassword(identity, password, kdfProfile))
            return FromLocalIdentity(domain, local, version, nonce);
    }

    public static PpapRegistrationRecord FromLocalIdentity(string domain,
        PpapLocalIdentity identity,
        long version = 1, byte[] nonce = null)
    {
        if (identity == null)
            throw new ArgumentNullException(nameof(identity));

        if (identity.Kind == PpapIdentityKind.StaticMlKem)
            return new PpapRegistrationRecord(version, identity.Identity, identity.Kind,
                Array.Empty<byte>(), identity.GetStaticPublicKey());

        var registrationNonce = nonce == null
            ? PpapCryptography.RandomBytes(PpapProtocol.RegistrationNonceLength)
            : (byte[])nonce.Clone();
        byte[] privateKey = null;
        byte[] publicKey = null;
        try
        {
            privateKey = identity.DerivePrivateKey(domain, registrationNonce,
                identity.KdfProfile);
            publicKey = PpapCryptography.GetPublicKey(privateKey);
            return new PpapRegistrationRecord(version, identity.Identity, identity.Kind,
                registrationNonce, publicKey, identity.KdfProfile);
        }
        finally
        {
            PpapCryptography.Clear(privateKey);
            PpapCryptography.Clear(publicKey);
            PpapCryptography.Clear(registrationNonce);
        }
    }
}

public interface IPpapRegistrationStore
{
    PpapRegistrationRecord Get(string domain, string identity);

    /// <summary>
    /// Resolves a per-handshake, keyed identity mask. Implementations must not
    /// persist <paramref name="maskKey"/> or use it as a stable lookup token.
    /// </summary>
    PpapRegistrationRecord ResolveMasked(string domain, byte[] mask,
        byte[] maskKey, byte[] maskedIdentity);

    /// <summary>
    /// Atomically replaces exactly the registration identified by
    /// <paramref name="domain"/>, <paramref name="identity"/>, and
    /// <paramref name="expectedVersion"/>. Implementations must perform a
    /// linearizable compare-and-swap, preserve identity, kind, and KDF policy,
    /// reject reused nonce or encapsulation-key material, and return false
    /// without mutation when any precondition fails. A database implementation
    /// must use a conditional update/transaction, not a separate read then write.
    /// </summary>
    bool TryRotate(string domain, string identity, long expectedVersion,
        PpapRegistrationRecord replacement);
}

/// <summary>
/// Thread-safe in-memory registration store intended for shortcuts, tests, and small deployments.
/// Masked resolution deliberately scans all records in a domain.
/// </summary>
public sealed class InMemoryPpapRegistrationStore : IPpapRegistrationStore
{
    struct RegistrationKey : IEquatable<RegistrationKey>
    {
        public readonly string Domain;
        public readonly string Identity;

        public RegistrationKey(string domain, string identity)
        {
            Domain = domain;
            Identity = identity;
        }

        public bool Equals(RegistrationKey other)
            => string.Equals(Domain, other.Domain, StringComparison.Ordinal)
            && string.Equals(Identity, other.Identity, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is RegistrationKey && Equals((RegistrationKey)obj);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Domain?.GetHashCode() ?? 0) * 397)
                    ^ (Identity?.GetHashCode() ?? 0);
            }
        }
    }

    readonly ConcurrentDictionary<RegistrationKey, PpapRegistrationRecord> _records = new();

    static RegistrationKey MakeKey(string domain, string identity)
        => new RegistrationKey(PpapCryptography.NormalizeDomain(domain),
            PpapCryptography.NormalizeIdentity(identity));

    public bool TryAdd(string domain, PpapRegistrationRecord record)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        return _records.TryAdd(MakeKey(domain, record.Identity), record);
    }

    public void AddOrUpdate(string domain, PpapRegistrationRecord record)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        var key = MakeKey(domain, record.Identity);
        _records.AddOrUpdate(key, record, (_, current) =>
        {
            if (RecordsEqual(current, record))
                return current;
            throw new InvalidOperationException(
                "An existing PPAP registration can only be changed through TryRotate.");
        });
    }

    public PpapRegistrationRecord Get(string domain, string identity)
    {
        _records.TryGetValue(MakeKey(domain, identity), out var record);
        return record;
    }

    public PpapRegistrationRecord ResolveMasked(string domain, byte[] mask,
        byte[] maskKey, byte[] maskedIdentity)
    {
        if (mask == null || mask.Length != PpapProtocol.IdentityMaskLength)
            return null;
        if (maskedIdentity == null || maskedIdentity.Length != PpapProtocol.HashLength)
            return null;
        if (maskKey == null || maskKey.Length != PpapProtocol.KemSecretLength)
            return null;

        var normalizedDomain = PpapCryptography.NormalizeDomain(domain);
        PpapRegistrationRecord match = null;
        var ambiguous = false;

        foreach (var pair in _records)
        {
            if (!string.Equals(pair.Key.Domain, normalizedDomain, StringComparison.Ordinal))
                continue;

            var candidate = PpapCryptography.MaskIdentity(
                normalizedDomain, pair.Value.Identity, mask, maskKey);
            var equal = PpapCryptography.FixedTimeEquals(candidate, maskedIdentity);
            PpapCryptography.Clear(candidate);

            if (!equal)
                continue;
            if (match != null)
                ambiguous = true;
            match = pair.Value;
        }

        return ambiguous ? null : match;
    }

    public bool TryRotate(string domain, string identity, long expectedVersion,
        PpapRegistrationRecord replacement)
    {
        if (replacement == null)
            throw new ArgumentNullException(nameof(replacement));
        var key = MakeKey(domain, identity);

        if (!string.Equals(key.Identity, replacement.Identity, StringComparison.Ordinal)
            || replacement.Version != expectedVersion + 1)
            return false;

        if (!_records.TryGetValue(key, out var current)
            || current.Version != expectedVersion
            || current.Kind != PpapIdentityKind.PasswordDerived
            || replacement.Kind != PpapIdentityKind.PasswordDerived)
            return false;

        if (!current.KdfProfile.Equals(replacement.KdfProfile)
            || PpapCryptography.FixedTimeEquals(current.NonceBytes,
                replacement.NonceBytes)
            || PpapCryptography.FixedTimeEquals(current.EncapsulationKeyBytes,
                replacement.EncapsulationKeyBytes))
            return false;

        return _records.TryUpdate(key, replacement, current);
    }

    static bool RecordsEqual(PpapRegistrationRecord left,
        PpapRegistrationRecord right)
        => left.Version == right.Version
        && left.Kind == right.Kind
        && string.Equals(left.Identity, right.Identity, StringComparison.Ordinal)
        && Equals(left.KdfProfile, right.KdfProfile)
        && PpapCryptography.FixedTimeEquals(left.NonceBytes, right.NonceBytes)
        && PpapCryptography.FixedTimeEquals(left.EncapsulationKeyBytes,
            right.EncapsulationKeyBytes);
}
