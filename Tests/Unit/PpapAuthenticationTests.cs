using System.Buffers.Binary;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers.Ppap;

namespace Esiur.Tests.Unit;

public class PpapAuthenticationTests
{
    const string Domain = "example.test";
    const string InitiatorIdentity = "alice";
    const string ResponderIdentity = "server";
    const string InitiatorPassword = "correct horse battery staple";
    const string ResponderPassword = "server paper password";

    static readonly PpapKdfProfile TestKdf = new(
        PpapKdfProfile.Argon2Version13,
        memoryKiB: 8 * 1024,
        iterations: 1,
        parallelism: 1);

    [Theory]
    [InlineData(AuthenticationMode.InitializerIdentity)]
    [InlineData(AuthenticationMode.ResponderIdentity)]
    [InlineData(AuthenticationMode.DualIdentity)]
    public void PasswordHandshake_AllIdentityModesDeriveMatchingKeys(
        AuthenticationMode mode)
    {
        using var pair = CreatePair(mode);

        var result = CompleteHandshake(pair.Initiator, pair.Responder);

        var authenticatesInitiator = mode is AuthenticationMode.InitializerIdentity
            or AuthenticationMode.DualIdentity;
        var authenticatesResponder = mode is AuthenticationMode.ResponderIdentity
            or AuthenticationMode.DualIdentity;

        Assert.Equal(PpapProtocol.SessionKeyLength, result.Initiator.SessionKey.Length);
        Assert.Equal(result.Initiator.SessionKey, result.Responder.SessionKey);
        Assert.Equal(authenticatesInitiator ? InitiatorIdentity : null,
            result.Initiator.LocalIdentity);
        Assert.Equal(authenticatesResponder ? ResponderIdentity : null,
            result.Initiator.RemoteIdentity);
        Assert.Equal(authenticatesResponder ? ResponderIdentity : null,
            result.Responder.LocalIdentity);
        Assert.Equal(authenticatesInitiator ? InitiatorIdentity : null,
            result.Responder.RemoteIdentity);
    }

    [Fact]
    public void WrongPassword_FailsWithoutChangingRegistration()
    {
        using var pair = CreatePair(
            AuthenticationMode.InitializerIdentity,
            initiatorPassword: "not the registered password");
        var before = Snapshot(pair.ResponderStore.Get(Domain, InitiatorIdentity));

        var first = pair.Initiator.Process(null!);
        var second = pair.Responder.Process(Wire(first, PpapMessageType.ClientHello));
        var third = pair.Initiator.Process(Wire(second, PpapMessageType.ServerHello));
        var fourth = pair.Responder.Process(Wire(third, PpapMessageType.InitiatorProof));
        var failed = pair.Initiator.Process(Wire(fourth, PpapMessageType.ResponderProof));

        Assert.Equal(AuthenticationRuling.Failed, failed.Ruling);
        Assert.Null(failed.SessionKey);
        AssertSnapshot(before, pair.ResponderStore.Get(Domain, InitiatorIdentity));
    }

    [Fact]
    public void TamperedResponderFinished_FailsClosed()
    {
        using var pair = CreatePair(AuthenticationMode.InitializerIdentity);

        var first = pair.Initiator.Process(null!);
        var second = pair.Responder.Process(Wire(first, PpapMessageType.ClientHello));
        var third = pair.Initiator.Process(Wire(second, PpapMessageType.ServerHello));
        var fourth = pair.Responder.Process(Wire(third, PpapMessageType.InitiatorProof));
        var tampered = TamperLastByte(Wire(fourth, PpapMessageType.ResponderProof));

        var failed = pair.Initiator.Process(tampered);

        Assert.Equal(AuthenticationRuling.Failed, failed.Ruling);
        Assert.Null(failed.SessionKey);
    }

    [Fact]
    public void TamperedInitiatorFinished_FailsClosed()
    {
        using var pair = CreatePair(AuthenticationMode.InitializerIdentity);

        var first = pair.Initiator.Process(null!);
        var second = pair.Responder.Process(Wire(first, PpapMessageType.ClientHello));
        var third = pair.Initiator.Process(Wire(second, PpapMessageType.ServerHello));
        var fourth = pair.Responder.Process(Wire(third, PpapMessageType.InitiatorProof));
        var fifth = pair.Initiator.Process(Wire(fourth, PpapMessageType.ResponderProof));
        var tampered = TamperLastByte(Wire(fifth, PpapMessageType.InitiatorFinished));

        var failed = pair.Responder.Process(tampered);

        Assert.Equal(AuthenticationRuling.Failed, failed.Ruling);
        Assert.Null(failed.SessionKey);
    }

    [Fact]
    public void DualIdentityProofs_DoNotExposeRegistrationDescriptors()
    {
        using var pair = CreatePair(AuthenticationMode.DualIdentity);
        var initiatorRegistration = pair.ResponderStore.Get(
            Domain,
            InitiatorIdentity);
        var responderRegistration = pair.InitiatorStore.Get(
            Domain,
            ResponderIdentity);

        var first = pair.Initiator.Process(null!);
        var second = pair.Responder.Process(
            Wire(first, PpapMessageType.ClientHello));
        var third = pair.Initiator.Process(
            Wire(second, PpapMessageType.ServerHello));
        var initiatorProof = Wire(third, PpapMessageType.InitiatorProof);
        var fourth = pair.Responder.Process(initiatorProof);
        var responderProof = Wire(fourth, PpapMessageType.ResponderProof);

        AssertDescriptorNotVisible(initiatorProof, responderRegistration);
        AssertDescriptorNotVisible(responderProof, initiatorRegistration);
    }

    [Fact]
    public void TamperedEncryptedResponderDescriptor_FailsClosed()
    {
        using var pair = CreatePair(AuthenticationMode.DualIdentity);
        var first = pair.Initiator.Process(null!);
        var second = pair.Responder.Process(
            Wire(first, PpapMessageType.ClientHello));
        var third = pair.Initiator.Process(
            Wire(second, PpapMessageType.ServerHello));
        var tampered = TamperField(
            Wire(third, PpapMessageType.InitiatorProof),
            fieldIndex: 2);

        var failed = pair.Responder.Process(tampered);

        Assert.Equal(AuthenticationRuling.Failed, failed.Ruling);
        Assert.Null(failed.SessionKey);
    }

    [Fact]
    public void TamperedEncryptedInitiatorDescriptor_FailsClosed()
    {
        using var pair = CreatePair(AuthenticationMode.DualIdentity);
        var first = pair.Initiator.Process(null!);
        var second = pair.Responder.Process(
            Wire(first, PpapMessageType.ClientHello));
        var third = pair.Initiator.Process(
            Wire(second, PpapMessageType.ServerHello));
        var fourth = pair.Responder.Process(
            Wire(third, PpapMessageType.InitiatorProof));
        var tampered = TamperField(
            Wire(fourth, PpapMessageType.ResponderProof),
            fieldIndex: 1);

        var failed = pair.Initiator.Process(tampered);

        Assert.Equal(AuthenticationRuling.Failed, failed.Ruling);
        Assert.Null(failed.SessionKey);
    }

    [Fact]
    public void ProtectedDescriptor_DiffersAcrossFreshSessionsForSameRegistration()
    {
        using var pair = CreatePair(AuthenticationMode.DualIdentity);
        var firstProof = CreateInitiatorProof(pair.Initiator, pair.Responder);
        var initiatorProvider = Assert.IsType<PpapAuthenticationProvider>(
            pair.Initiator.Provider);
        var responderProvider = Assert.IsType<PpapAuthenticationProvider>(
            pair.Responder.Provider);
        using var secondInitiator = CreateHandler(
            initiatorProvider,
            AuthenticationDirection.Initiator,
            AuthenticationMode.DualIdentity);
        using var secondResponder = CreateHandler(
            responderProvider,
            AuthenticationDirection.Responder,
            AuthenticationMode.DualIdentity);
        var secondProof = CreateInitiatorProof(secondInitiator, secondResponder);

        var firstDescriptor = ExtractField(firstProof, fieldIndex: 2);
        var secondDescriptor = ExtractField(secondProof, fieldIndex: 2);

        Assert.Equal(144, firstDescriptor.Length);
        Assert.Equal(PpapProtocol.ProtectedDescriptorLength, firstDescriptor.Length);
        Assert.Equal(144, secondDescriptor.Length);
        Assert.False(firstDescriptor.SequenceEqual(secondDescriptor));
    }

    [Fact]
    public void InitializerPassword_RotationChangesVersionNonceAndKey()
    {
        using var pair = CreatePair(AuthenticationMode.InitializerIdentity);
        CompleteHandshake(pair.Initiator, pair.Responder);
        var before = Snapshot(pair.ResponderStore.Get(Domain, InitiatorIdentity));

        CompleteInitializerRotation(pair.Initiator, pair.Responder);

        var after = pair.ResponderStore.Get(Domain, InitiatorIdentity);
        Assert.Equal(before.Version + 1, after.Version);
        Assert.False(before.Nonce.SequenceEqual(after.Nonce));
        Assert.False(before.EncapsulationKey.SequenceEqual(after.EncapsulationKey));
    }

    [Fact]
    public void ResponderPassword_RotationChangesVerifierRecord()
    {
        using var pair = CreatePair(AuthenticationMode.ResponderIdentity);
        CompleteHandshake(pair.Initiator, pair.Responder);
        var before = Snapshot(pair.InitiatorStore.Get(Domain, ResponderIdentity));

        CompleteResponderRotation(pair.Initiator, pair.Responder);

        AssertRotated(
            before,
            pair.InitiatorStore.Get(Domain, ResponderIdentity));
    }

    [Fact]
    public void DualPassword_RotationChangesBothVerifierRecords()
    {
        using var pair = CreatePair(AuthenticationMode.DualIdentity);
        CompleteHandshake(pair.Initiator, pair.Responder);
        var initiatorBefore = Snapshot(
            pair.ResponderStore.Get(Domain, InitiatorIdentity));
        var responderBefore = Snapshot(
            pair.InitiatorStore.Get(Domain, ResponderIdentity));

        CompleteDualRotation(pair.Initiator, pair.Responder);

        AssertRotated(
            initiatorBefore,
            pair.ResponderStore.Get(Domain, InitiatorIdentity));
        AssertRotated(
            responderBefore,
            pair.InitiatorStore.Get(Domain, ResponderIdentity));
    }

    [Fact]
    public void TamperedRotationProof_FailsWithoutPersistingOffer()
    {
        using var pair = CreatePair(AuthenticationMode.InitializerIdentity);
        CompleteHandshake(pair.Initiator, pair.Responder);
        var before = Snapshot(pair.ResponderStore.Get(Domain, InitiatorIdentity));

        var offer = pair.Initiator.BeginKeyRotation();
        var challenge = pair.Responder.ProcessKeyRotation(
            RotationWire(offer, PpapMessageType.RotationOffer));
        var proof = pair.Initiator.ProcessKeyRotation(
            RotationWire(challenge, PpapMessageType.RotationChallenge));
        var tampered = TamperLastByte(
            RotationWire(proof, PpapMessageType.RotationProof));

        var failed = pair.Responder.ProcessKeyRotation(tampered);

        Assert.Equal(AuthenticationKeyRotationRuling.Failed, failed.Ruling);
        AssertSnapshot(before, pair.ResponderStore.Get(Domain, InitiatorIdentity));
    }

    [Fact]
    public void StaticDualIdentity_UsesEncryptedNoOpCompletionWithoutChangingRecords()
    {
        using var pair = CreateStaticPair();
        var initiatorBefore = Snapshot(
            pair.ResponderStore.Get(Domain, InitiatorIdentity));
        var responderBefore = Snapshot(
            pair.InitiatorStore.Get(Domain, ResponderIdentity));
        CompleteHandshake(pair.Initiator, pair.Responder);

        var done = pair.Initiator.BeginKeyRotation();
        Assert.Equal(AuthenticationKeyRotationRuling.Succeeded, done.Ruling);
        var completed = pair.Responder.ProcessKeyRotation(
            RotationWire(done, PpapMessageType.RotationDone));

        Assert.Equal(AuthenticationKeyRotationRuling.Succeeded, completed.Ruling);
        Assert.Null(completed.Data);
        AssertSnapshot(
            initiatorBefore,
            pair.ResponderStore.Get(Domain, InitiatorIdentity));
        AssertSnapshot(
            responderBefore,
            pair.InitiatorStore.Get(Domain, ResponderIdentity));
    }

    static HandshakeResult CompleteHandshake(
        PpapAuthenticationHandler initiator,
        PpapAuthenticationHandler responder)
    {
        var first = initiator.Process(null!);
        Assert.Equal(AuthenticationRuling.InProgress, first.Ruling);

        var second = responder.Process(Wire(first, PpapMessageType.ClientHello));
        Assert.Equal(AuthenticationRuling.InProgress, second.Ruling);

        var third = initiator.Process(Wire(second, PpapMessageType.ServerHello));
        Assert.Equal(AuthenticationRuling.InProgress, third.Ruling);

        var fourth = responder.Process(Wire(third, PpapMessageType.InitiatorProof));
        Assert.Equal(AuthenticationRuling.InProgress, fourth.Ruling);

        var fifth = initiator.Process(Wire(fourth, PpapMessageType.ResponderProof));
        Assert.Equal(AuthenticationRuling.Succeeded, fifth.Ruling);

        var sixth = responder.Process(Wire(fifth, PpapMessageType.InitiatorFinished));
        Assert.Equal(AuthenticationRuling.Succeeded, sixth.Ruling);

        return new HandshakeResult(fifth, sixth);
    }

    static byte[] CreateInitiatorProof(
        PpapAuthenticationHandler initiator,
        PpapAuthenticationHandler responder)
    {
        var first = initiator.Process(null!);
        var second = responder.Process(Wire(first, PpapMessageType.ClientHello));
        var third = initiator.Process(Wire(second, PpapMessageType.ServerHello));
        return Wire(third, PpapMessageType.InitiatorProof);
    }

    static PpapAuthenticationHandler CreateHandler(
        PpapAuthenticationProvider provider,
        AuthenticationDirection direction,
        AuthenticationMode mode)
        => Assert.IsType<PpapAuthenticationHandler>(
            provider.CreateAuthenticationHandler(new AuthenticationContext
            {
                Direction = direction,
                Mode = mode,
                Domain = Domain,
                InitiatorIdentity = InitiatorIdentity,
                ResponderIdentity = ResponderIdentity,
                HostName = ResponderIdentity,
            }));

    static void CompleteInitializerRotation(
        PpapAuthenticationHandler initiator,
        PpapAuthenticationHandler responder)
    {
        Assert.True(initiator.RequiresKeyRotation);
        Assert.True(responder.RequiresKeyRotation);

        var offer = initiator.BeginKeyRotation();
        Assert.Equal(AuthenticationKeyRotationRuling.InProgress, offer.Ruling);

        var challenge = responder.ProcessKeyRotation(
            RotationWire(offer, PpapMessageType.RotationOffer));
        Assert.Equal(AuthenticationKeyRotationRuling.InProgress, challenge.Ruling);

        var proof = initiator.ProcessKeyRotation(
            RotationWire(challenge, PpapMessageType.RotationChallenge));
        Assert.Equal(AuthenticationKeyRotationRuling.InProgress, proof.Ruling);

        var commit = responder.ProcessKeyRotation(
            RotationWire(proof, PpapMessageType.RotationProof));
        Assert.Equal(AuthenticationKeyRotationRuling.InProgress, commit.Ruling);

        var done = initiator.ProcessKeyRotation(
            RotationWire(commit, PpapMessageType.RotationCommit));
        Assert.Equal(AuthenticationKeyRotationRuling.Succeeded, done.Ruling);

        var completed = responder.ProcessKeyRotation(
            RotationWire(done, PpapMessageType.RotationDone));
        Assert.Equal(AuthenticationKeyRotationRuling.Succeeded, completed.Ruling);
        Assert.Null(completed.Data);
    }

    static void CompleteResponderRotation(
        PpapAuthenticationHandler initiator,
        PpapAuthenticationHandler responder)
    {
        var start = initiator.BeginKeyRotation();
        Assert.Equal(AuthenticationKeyRotationRuling.InProgress, start.Ruling);

        var offer = responder.ProcessKeyRotation(
            RotationWire(start, PpapMessageType.RotationStart));
        var challenge = initiator.ProcessKeyRotation(
            RotationWire(offer, PpapMessageType.RotationOffer));
        var proof = responder.ProcessKeyRotation(
            RotationWire(challenge, PpapMessageType.RotationChallenge));
        var commit = initiator.ProcessKeyRotation(
            RotationWire(proof, PpapMessageType.RotationProof));
        var acknowledgement = responder.ProcessKeyRotation(
            RotationWire(commit, PpapMessageType.RotationCommit));
        var done = initiator.ProcessKeyRotation(
            RotationWire(acknowledgement, PpapMessageType.RotationCommitAck));
        var completed = responder.ProcessKeyRotation(
            RotationWire(done, PpapMessageType.RotationDone));

        Assert.Equal(AuthenticationKeyRotationRuling.Succeeded, done.Ruling);
        Assert.Equal(AuthenticationKeyRotationRuling.Succeeded, completed.Ruling);
    }

    static void CompleteDualRotation(
        PpapAuthenticationHandler initiator,
        PpapAuthenticationHandler responder)
    {
        var initiatorOffer = initiator.BeginKeyRotation();
        var initiatorChallenge = responder.ProcessKeyRotation(
            RotationWire(initiatorOffer, PpapMessageType.RotationOffer));
        var initiatorProof = initiator.ProcessKeyRotation(
            RotationWire(initiatorChallenge, PpapMessageType.RotationChallenge));
        var initiatorCommit = responder.ProcessKeyRotation(
            RotationWire(initiatorProof, PpapMessageType.RotationProof));
        var responderStart = initiator.ProcessKeyRotation(
            RotationWire(initiatorCommit, PpapMessageType.RotationCommit));

        var responderOffer = responder.ProcessKeyRotation(
            RotationWire(responderStart, PpapMessageType.RotationStart));
        var responderChallenge = initiator.ProcessKeyRotation(
            RotationWire(responderOffer, PpapMessageType.RotationOffer));
        var responderProof = responder.ProcessKeyRotation(
            RotationWire(responderChallenge, PpapMessageType.RotationChallenge));
        var responderCommit = initiator.ProcessKeyRotation(
            RotationWire(responderProof, PpapMessageType.RotationProof));
        var acknowledgement = responder.ProcessKeyRotation(
            RotationWire(responderCommit, PpapMessageType.RotationCommit));
        var done = initiator.ProcessKeyRotation(
            RotationWire(acknowledgement, PpapMessageType.RotationCommitAck));
        var completed = responder.ProcessKeyRotation(
            RotationWire(done, PpapMessageType.RotationDone));

        Assert.Equal(AuthenticationKeyRotationRuling.Succeeded, done.Ruling);
        Assert.Equal(AuthenticationKeyRotationRuling.Succeeded, completed.Ruling);
    }

    static byte[] Wire(AuthenticationResult result, PpapMessageType expectedType)
    {
        var wire = Assert.IsType<byte[]>(result.AuthenticationData);
        Assert.True(wire.Length >= 10);
        Assert.Equal(expectedType, (PpapMessageType)wire[5]);
        return wire;
    }

    static byte[] RotationWire(
        AuthenticationKeyRotationResult result,
        PpapMessageType expectedType)
    {
        var wire = Assert.IsType<byte[]>(result.Data);
        Assert.True(wire.Length >= 10);
        Assert.Equal(expectedType, (PpapMessageType)wire[5]);
        return wire;
    }

    static byte[] TamperLastByte(byte[] wire)
    {
        var tampered = (byte[])wire.Clone();
        tampered[^1] ^= 0x80;
        return tampered;
    }

    static byte[] TamperField(byte[] wire, int fieldIndex)
    {
        var tampered = (byte[])wire.Clone();
        var offset = 10;
        for (var index = 0; index <= fieldIndex; index++)
        {
            Assert.True(tampered.Length - offset >= sizeof(int));
            var length = BinaryPrimitives.ReadInt32BigEndian(
                tampered.AsSpan(offset, sizeof(int)));
            offset += sizeof(int);
            Assert.InRange(length, 1, tampered.Length - offset);

            if (index == fieldIndex)
            {
                tampered[offset + (length / 2)] ^= 0x80;
                return tampered;
            }

            offset += length;
        }

        throw new InvalidOperationException("The requested PPAP field was not found.");
    }

    static byte[] ExtractField(byte[] wire, int fieldIndex)
    {
        var offset = 10;
        for (var index = 0; index <= fieldIndex; index++)
        {
            Assert.True(wire.Length - offset >= sizeof(int));
            var length = BinaryPrimitives.ReadInt32BigEndian(
                wire.AsSpan(offset, sizeof(int)));
            offset += sizeof(int);
            Assert.InRange(length, 1, wire.Length - offset);

            if (index == fieldIndex)
                return wire.AsSpan(offset, length).ToArray();

            offset += length;
        }

        throw new InvalidOperationException("The requested PPAP field was not found.");
    }

    static void AssertDescriptorNotVisible(
        byte[] wire,
        PpapRegistrationRecord registration)
    {
        Assert.False(ContainsSubsequence(wire, registration.Nonce),
            "The PPAP proof contains the plaintext registration nonce.");
        Assert.False(ContainsSubsequence(
                wire,
                EncodeDescriptorVersionPrefix(registration)),
            "The PPAP proof contains the plaintext registration version descriptor.");
        Assert.False(ContainsSubsequence(
                wire,
                EncodeDescriptorProfile(registration.KdfProfile)),
            "The PPAP proof contains the plaintext registration KDF descriptor.");
    }

    static byte[] EncodeDescriptorVersionPrefix(PpapRegistrationRecord registration)
    {
        var encoded = new byte[1 + sizeof(long) + sizeof(int)];
        encoded[0] = (byte)registration.Kind;
        BinaryPrimitives.WriteInt64BigEndian(
            encoded.AsSpan(1, sizeof(long)),
            registration.Version);
        BinaryPrimitives.WriteInt32BigEndian(
            encoded.AsSpan(1 + sizeof(long), sizeof(int)),
            registration.Nonce.Length);
        return encoded;
    }

    static byte[] EncodeDescriptorProfile(PpapKdfProfile profile)
    {
        Assert.NotNull(profile);
        var encoded = new byte[1 + (4 * sizeof(int))];
        encoded[0] = 1;
        BinaryPrimitives.WriteInt32BigEndian(
            encoded.AsSpan(1, sizeof(int)),
            profile.Version);
        BinaryPrimitives.WriteInt32BigEndian(
            encoded.AsSpan(1 + sizeof(int), sizeof(int)),
            profile.MemoryKiB);
        BinaryPrimitives.WriteInt32BigEndian(
            encoded.AsSpan(1 + (2 * sizeof(int)), sizeof(int)),
            profile.Iterations);
        BinaryPrimitives.WriteInt32BigEndian(
            encoded.AsSpan(1 + (3 * sizeof(int)), sizeof(int)),
            profile.Parallelism);
        return encoded;
    }

    static bool ContainsSubsequence(byte[] value, byte[] candidate)
    {
        if (candidate.Length == 0 || candidate.Length > value.Length)
            return false;

        for (var offset = 0; offset <= value.Length - candidate.Length; offset++)
        {
            if (value.AsSpan(offset, candidate.Length).SequenceEqual(candidate))
                return true;
        }

        return false;
    }

    static RegistrationSnapshot Snapshot(PpapRegistrationRecord record)
        => new(record.Version, record.Nonce, record.EncapsulationKey);

    static void AssertSnapshot(RegistrationSnapshot expected,
        PpapRegistrationRecord actual)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.Nonce, actual.Nonce);
        Assert.Equal(expected.EncapsulationKey, actual.EncapsulationKey);
    }

    static void AssertRotated(RegistrationSnapshot before,
        PpapRegistrationRecord after)
    {
        Assert.Equal(before.Version + 1, after.Version);
        Assert.False(before.Nonce.SequenceEqual(after.Nonce));
        Assert.False(before.EncapsulationKey.SequenceEqual(after.EncapsulationKey));
    }

    static PpapPair CreatePair(
        AuthenticationMode mode,
        string initiatorPassword = InitiatorPassword,
        string responderPassword = ResponderPassword)
    {
        var authenticatesInitiator = mode is AuthenticationMode.InitializerIdentity
            or AuthenticationMode.DualIdentity;
        var authenticatesResponder = mode is AuthenticationMode.ResponderIdentity
            or AuthenticationMode.DualIdentity;

        var initiatorStore = new InMemoryPpapRegistrationStore();
        var responderStore = new InMemoryPpapRegistrationStore();
        PpapLocalIdentity? initiatorLocal = null;
        PpapLocalIdentity? responderLocal = null;

        try
        {
            if (authenticatesInitiator)
            {
                initiatorLocal = PpapLocalIdentity.FromPassword(
                    InitiatorIdentity, initiatorPassword, TestKdf);
                using var registered = PpapLocalIdentity.FromPassword(
                    InitiatorIdentity, InitiatorPassword, TestKdf);
                Assert.True(responderStore.TryAdd(
                    Domain,
                    PpapRegistrationRecord.FromLocalIdentity(Domain, registered)));
            }

            if (authenticatesResponder)
            {
                responderLocal = PpapLocalIdentity.FromPassword(
                    ResponderIdentity, responderPassword, TestKdf);
                using var registered = PpapLocalIdentity.FromPassword(
                    ResponderIdentity, ResponderPassword, TestKdf);
                Assert.True(initiatorStore.TryAdd(
                    Domain,
                    PpapRegistrationRecord.FromLocalIdentity(Domain, registered)));
            }

            var initiatorProvider = new PpapAuthenticationProvider(
                initiatorLocal!, initiatorStore);
            var responderProvider = new PpapAuthenticationProvider(
                responderLocal!, responderStore);
            var initiator = Assert.IsType<PpapAuthenticationHandler>(
                initiatorProvider.CreateAuthenticationHandler(new AuthenticationContext
                {
                    Direction = AuthenticationDirection.Initiator,
                    Mode = mode,
                    Domain = Domain,
                    InitiatorIdentity = authenticatesInitiator ? InitiatorIdentity : null,
                    ResponderIdentity = authenticatesResponder ? ResponderIdentity : null,
                    HostName = ResponderIdentity,
                }));
            var responder = Assert.IsType<PpapAuthenticationHandler>(
                responderProvider.CreateAuthenticationHandler(new AuthenticationContext
                {
                    Direction = AuthenticationDirection.Responder,
                    Mode = mode,
                    Domain = Domain,
                    InitiatorIdentity = authenticatesInitiator ? InitiatorIdentity : null,
                    ResponderIdentity = authenticatesResponder ? ResponderIdentity : null,
                    HostName = ResponderIdentity,
                }));

            return new PpapPair(
                initiatorLocal,
                responderLocal,
                initiatorStore,
                responderStore,
                initiator,
                responder);
        }
        catch
        {
            initiatorLocal?.Dispose();
            responderLocal?.Dispose();
            throw;
        }
    }

    static PpapPair CreateStaticPair()
    {
        PpapCryptography.GenerateKeyPair(
            out var initiatorPrivateKey,
            out var initiatorPublicKey);
        PpapCryptography.GenerateKeyPair(
            out var responderPrivateKey,
            out var responderPublicKey);
        PpapLocalIdentity? initiatorLocal = null;
        PpapLocalIdentity? responderLocal = null;

        try
        {
            initiatorLocal = PpapLocalIdentity.FromStaticKey(
                InitiatorIdentity,
                initiatorPrivateKey);
            responderLocal = PpapLocalIdentity.FromStaticKey(
                ResponderIdentity,
                responderPrivateKey);
            var initiatorStore = new InMemoryPpapRegistrationStore();
            var responderStore = new InMemoryPpapRegistrationStore();
            Assert.True(responderStore.TryAdd(
                Domain,
                PpapRegistrationRecord.FromLocalIdentity(Domain, initiatorLocal)));
            Assert.True(initiatorStore.TryAdd(
                Domain,
                PpapRegistrationRecord.FromLocalIdentity(Domain, responderLocal)));

            var initiatorProvider = new PpapAuthenticationProvider(
                initiatorLocal!,
                initiatorStore);
            var responderProvider = new PpapAuthenticationProvider(
                responderLocal!,
                responderStore);
            var initiator = Assert.IsType<PpapAuthenticationHandler>(
                initiatorProvider.CreateAuthenticationHandler(new AuthenticationContext
                {
                    Direction = AuthenticationDirection.Initiator,
                    Mode = AuthenticationMode.DualIdentity,
                    Domain = Domain,
                    InitiatorIdentity = InitiatorIdentity,
                    ResponderIdentity = ResponderIdentity,
                    HostName = ResponderIdentity,
                }));
            var responder = Assert.IsType<PpapAuthenticationHandler>(
                responderProvider.CreateAuthenticationHandler(new AuthenticationContext
                {
                    Direction = AuthenticationDirection.Responder,
                    Mode = AuthenticationMode.DualIdentity,
                    Domain = Domain,
                    InitiatorIdentity = InitiatorIdentity,
                    ResponderIdentity = ResponderIdentity,
                    HostName = ResponderIdentity,
                }));

            return new PpapPair(
                initiatorLocal,
                responderLocal,
                initiatorStore,
                responderStore,
                initiator,
                responder);
        }
        catch
        {
            initiatorLocal?.Dispose();
            responderLocal?.Dispose();
            throw;
        }
        finally
        {
            PpapCryptography.Clear(initiatorPrivateKey);
            PpapCryptography.Clear(initiatorPublicKey);
            PpapCryptography.Clear(responderPrivateKey);
            PpapCryptography.Clear(responderPublicKey);
        }
    }

    readonly record struct RegistrationSnapshot(
        long Version,
        byte[] Nonce,
        byte[] EncapsulationKey);

    readonly record struct HandshakeResult(
        AuthenticationResult Initiator,
        AuthenticationResult Responder);

    sealed class PpapPair : IDisposable
    {
        readonly PpapLocalIdentity? _initiatorLocal;
        readonly PpapLocalIdentity? _responderLocal;

        public InMemoryPpapRegistrationStore InitiatorStore { get; }
        public InMemoryPpapRegistrationStore ResponderStore { get; }
        public PpapAuthenticationHandler Initiator { get; }
        public PpapAuthenticationHandler Responder { get; }

        public PpapPair(
            PpapLocalIdentity? initiatorLocal,
            PpapLocalIdentity? responderLocal,
            InMemoryPpapRegistrationStore initiatorStore,
            InMemoryPpapRegistrationStore responderStore,
            PpapAuthenticationHandler initiator,
            PpapAuthenticationHandler responder)
        {
            _initiatorLocal = initiatorLocal;
            _responderLocal = responderLocal;
            InitiatorStore = initiatorStore;
            ResponderStore = responderStore;
            Initiator = initiator;
            Responder = responder;
        }

        public void Dispose()
        {
            Initiator.Dispose();
            Responder.Dispose();
            _initiatorLocal?.Dispose();
            _responderLocal?.Dispose();
        }
    }
}
