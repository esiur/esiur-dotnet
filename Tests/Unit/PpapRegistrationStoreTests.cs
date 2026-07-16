using Esiur.Security.Authority.Providers.Ppap;

namespace Esiur.Tests.Unit;

public class PpapRegistrationStoreTests
{
    const string Domain = "example.test";
    const string Identity = "alice";

    static readonly PpapKdfProfile TestKdf = new(
        PpapKdfProfile.Argon2Version13,
        memoryKiB: 8 * 1024,
        iterations: 1,
        parallelism: 1);

    [Fact]
    public async Task TryRotate_ConcurrentCompareAndSwap_AllowsExactlyOneWinner()
    {
        var store = new InMemoryPpapRegistrationStore();
        var current = PpapRegistrationRecord.FromPassword(
            Domain, Identity, "correct horse battery staple", kdfProfile: TestKdf);
        Assert.True(store.TryAdd(Domain, current));

        var candidate = Replacement(current, marker: 1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable.Range(0, 16).Select(async _ =>
        {
            await start.Task;
            return store.TryRotate(Domain, Identity, current.Version, candidate);
        }).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, won => won);
        var stored = Assert.IsType<PpapRegistrationRecord>(store.Get(Domain, Identity));

        Assert.Same(candidate, stored);
        Assert.Equal(current.Version + 1, stored.Version);
        Assert.Equal(candidate.Nonce, stored.Nonce);
        Assert.Equal(candidate.EncapsulationKey, stored.EncapsulationKey);
        Assert.False(current.Nonce.SequenceEqual(stored.Nonce));
        Assert.False(current.EncapsulationKey.SequenceEqual(stored.EncapsulationKey));
    }

    [Fact]
    public void TryRotate_StaleVersionFailsWithoutChangingRecord()
    {
        var store = new InMemoryPpapRegistrationStore();
        var current = PpapRegistrationRecord.FromPassword(
            Domain, Identity, "correct horse battery staple", kdfProfile: TestKdf);
        var replacement = Replacement(current, marker: 99);
        Assert.True(store.TryAdd(Domain, current));

        Assert.False(store.TryRotate(Domain, Identity, current.Version - 1, replacement));

        var stored = Assert.IsType<PpapRegistrationRecord>(store.Get(Domain, Identity));
        Assert.Same(current, stored);
        Assert.Equal(current.Version, stored.Version);
        Assert.Equal(current.Nonce, stored.Nonce);
        Assert.Equal(current.EncapsulationKey, stored.EncapsulationKey);
    }

    [Fact]
    public void TryRotate_RejectsVersionBumpWithoutFreshNonceAndKey()
    {
        var store = new InMemoryPpapRegistrationStore();
        var current = PpapRegistrationRecord.FromPassword(
            Domain, Identity, "correct horse battery staple", kdfProfile: TestKdf);
        var noOp = new PpapRegistrationRecord(
            current.Version + 1,
            current.Identity,
            current.Kind,
            current.Nonce,
            current.EncapsulationKey,
            current.KdfProfile);
        Assert.True(store.TryAdd(Domain, current));

        Assert.False(store.TryRotate(Domain, Identity, current.Version, noOp));
        Assert.Same(current, store.Get(Domain, Identity));
    }

    [Fact]
    public void TryRotate_RejectsChangedKdfProfile()
    {
        var store = new InMemoryPpapRegistrationStore();
        var current = PpapRegistrationRecord.FromPassword(
            Domain, Identity, "correct horse battery staple", kdfProfile: TestKdf);
        var changedKdf = new PpapKdfProfile(
            PpapKdfProfile.Argon2Version13,
            memoryKiB: 8 * 1024,
            iterations: 2,
            parallelism: 1);
        var replacement = Replacement(
            current,
            marker: 7,
            kdfProfile: changedKdf);
        Assert.True(store.TryAdd(Domain, current));

        Assert.False(store.TryRotate(
            Domain,
            Identity,
            current.Version,
            replacement));
        Assert.Same(current, store.Get(Domain, Identity));
    }

    [Fact]
    public void PasswordRegistration_IsDomainSeparated()
    {
        var nonce = Enumerable.Range(1, PpapProtocol.RegistrationNonceLength)
            .Select(value => (byte)value)
            .ToArray();

        var first = PpapRegistrationRecord.FromPassword(
            "first.example",
            Identity,
            "correct horse battery staple",
            nonce: nonce,
            kdfProfile: TestKdf);
        var second = PpapRegistrationRecord.FromPassword(
            "second.example",
            Identity,
            "correct horse battery staple",
            nonce: nonce,
            kdfProfile: TestKdf);

        Assert.Equal(first.Nonce, second.Nonce);
        Assert.False(first.EncapsulationKey.SequenceEqual(second.EncapsulationKey));
    }

    [Fact]
    public void RegistrationRecord_DoesNotExposeMutableKeyMaterial()
    {
        var record = PpapRegistrationRecord.FromPassword(
            Domain,
            Identity,
            "correct horse battery staple",
            kdfProfile: TestKdf);
        var originalNonce = record.Nonce;
        var originalKey = record.EncapsulationKey;

        var exposedNonce = record.Nonce;
        var exposedKey = record.EncapsulationKey;
        exposedNonce[0] ^= 0xFF;
        exposedKey[0] ^= 0xFF;

        Assert.Equal(originalNonce, record.Nonce);
        Assert.Equal(originalKey, record.EncapsulationKey);
    }

    [Fact]
    public void ExportStaticPrivateKey_RoundTripsRegistrationAndReturnsDefensiveCopies()
    {
        using var original = PpapLocalIdentity.CreateStatic(Identity);
        var originalRegistration = PpapRegistrationRecord.FromLocalIdentity(
            Domain,
            original);

        var firstExport = original.ExportStaticPrivateKey();
        var secondExport = original.ExportStaticPrivateKey();
        Assert.NotSame(firstExport, secondExport);
        Assert.Equal(firstExport, secondExport);

        using var imported = PpapLocalIdentity.FromStaticKey(Identity, firstExport);
        var importedRegistration = PpapRegistrationRecord.FromLocalIdentity(
            Domain,
            imported);

        Assert.Equal(originalRegistration.Identity, importedRegistration.Identity);
        Assert.Equal(originalRegistration.Kind, importedRegistration.Kind);
        Assert.Equal(originalRegistration.Nonce, importedRegistration.Nonce);
        Assert.Equal(
            originalRegistration.EncapsulationKey,
            importedRegistration.EncapsulationKey);

        firstExport[0] ^= 0xFF;
        secondExport[1] ^= 0xFF;

        Assert.Equal(
            originalRegistration.EncapsulationKey,
            PpapRegistrationRecord.FromLocalIdentity(Domain, original).EncapsulationKey);
        Assert.Equal(
            importedRegistration.EncapsulationKey,
            PpapRegistrationRecord.FromLocalIdentity(Domain, imported).EncapsulationKey);

        var importedExport = imported.ExportStaticPrivateKey();
        Assert.NotSame(firstExport, importedExport);
        importedExport[2] ^= 0xFF;
        Assert.Equal(
            importedRegistration.EncapsulationKey,
            PpapRegistrationRecord.FromLocalIdentity(Domain, imported).EncapsulationKey);
    }

    [Fact]
    public void PasswordDerivationLimiter_RejectsImmediatelyWhenSaturated()
    {
        var limiter = new PpapPasswordDerivationLimiter(1);
        using var identity = PpapLocalIdentity.FromPassword(
            Identity,
            "correct horse battery staple",
            TestKdf);
        var provider = new PpapAuthenticationProvider(
            identity,
            passwordDerivationLimiter: limiter);
        var nonce = Enumerable.Range(0, PpapProtocol.RegistrationNonceLength)
            .Select(value => (byte)(value + 1))
            .ToArray();

        using var heldSlot = limiter.TryAcquire(postAuthentication: false);
        Assert.NotNull(heldSlot);
        Assert.Throws<InvalidOperationException>(() =>
            provider.DerivePrivateKey(identity, Domain, nonce, TestKdf));
    }

    [Fact]
    public void PasswordDerivationLimiter_ReservesPostAuthenticationCapacityAndReleases()
    {
        var limiter = new PpapPasswordDerivationLimiter(
            maximumConcurrency: 2,
            reservedPostAuthenticationSlots: 1);

        var preAuthentication = limiter.TryAcquire(postAuthentication: false);
        Assert.NotNull(preAuthentication);
        Assert.Null(limiter.TryAcquire(postAuthentication: false));

        using (var postAuthentication = limiter.TryAcquire(postAuthentication: true))
            Assert.NotNull(postAuthentication);

        Assert.Null(limiter.TryAcquire(postAuthentication: false));
        preAuthentication.Dispose();
        using var reacquired = limiter.TryAcquire(postAuthentication: false);
        Assert.NotNull(reacquired);
    }

    [Fact]
    public void PasswordDerivationLimiter_AllowsPostAuthenticationDerivationInReservedSlot()
    {
        var limiter = new PpapPasswordDerivationLimiter(
            maximumConcurrency: 2,
            reservedPostAuthenticationSlots: 1);
        using var identity = PpapLocalIdentity.FromPassword(
            Identity,
            "correct horse battery staple",
            TestKdf);
        var provider = new PpapAuthenticationProvider(
            identity,
            passwordDerivationLimiter: limiter);
        var nonce = Enumerable.Range(0, PpapProtocol.RegistrationNonceLength)
            .Select(value => (byte)(value + 1))
            .ToArray();

        using var occupiedPreAuthenticationSlot =
            limiter.TryAcquire(postAuthentication: false);
        Assert.NotNull(occupiedPreAuthenticationSlot);
        var privateKey = provider.DerivePrivateKey(
            identity,
            Domain,
            nonce,
            TestKdf,
            postAuthentication: true);
        try
        {
            Assert.Equal(PpapProtocol.PrivateKeyLength, privateKey.Length);
        }
        finally
        {
            PpapCryptography.Clear(privateKey);
        }
    }

    static PpapRegistrationRecord Replacement(
        PpapRegistrationRecord current,
        int marker,
        PpapKdfProfile? kdfProfile = null)
    {
        var nonce = current.Nonce;
        nonce[0] ^= (byte)marker;
        nonce[^1] ^= (byte)(marker * 17);
        PpapCryptography.GenerateKeyPair(out var privateKey, out var publicKey);
        try
        {
            return new PpapRegistrationRecord(
                current.Version + 1,
                current.Identity,
                current.Kind,
                nonce,
                publicKey,
                kdfProfile ?? current.KdfProfile);
        }
        finally
        {
            PpapCryptography.Clear(privateKey);
            PpapCryptography.Clear(publicKey);
        }
    }
}
