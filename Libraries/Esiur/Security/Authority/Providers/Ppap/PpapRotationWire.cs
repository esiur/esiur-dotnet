using System;
using System.IO;

namespace Esiur.Security.Authority.Providers.Ppap;

internal static partial class PpapWire
{
    internal sealed class RotationOffer
    {
        public PpapIdentityRole Role;
        public string Identity;
        public long ExpectedVersion;
        public byte[] Nonce;
        public byte[] EncapsulationKey;
        public PpapKdfProfile KdfProfile;
    }

    internal sealed class RotationChallenge
    {
        public PpapIdentityRole Role;
        public byte[] Ciphertext;
    }

    internal sealed class RotationProof
    {
        public PpapIdentityRole Role;
        public byte[] Proof;
    }

    internal static byte[] EncodeRotationStart(PpapIdentityRole role)
        => Encode(PpapMessageType.RotationStart,
            payload => payload.WriteByte(ValidateRole(role)));

    internal static PpapIdentityRole DecodeRotationStart(object data)
        => DecodeRoleOnly(data, PpapMessageType.RotationStart);

    internal static byte[] EncodeRotationOffer(PpapIdentityRole role, string identity,
        long expectedVersion, byte[] nonce, byte[] encapsulationKey,
        PpapKdfProfile kdfProfile)
    {
        ValidateRole(role);
        if (expectedVersion < 1 || expectedVersion == long.MaxValue)
            throw new InvalidDataException("Invalid rotation version.");
        ValidateExact(nonce, PpapProtocol.RegistrationNonceLength, nameof(nonce));
        PpapCryptography.ValidatePublicKey(encapsulationKey);
        if (kdfProfile == null)
            throw new ArgumentNullException(nameof(kdfProfile));
        var identityBytes = PpapCryptography.EncodeUtf8(
            PpapCryptography.NormalizeIdentity(identity),
            PpapProtocol.MaximumIdentityBytes, nameof(identity));
        var descriptor = EncodeDescriptor(new PpapRegistrationDescriptor(
            PpapIdentityKind.PasswordDerived, expectedVersion + 1, nonce, kdfProfile));
        try
        {
            return Encode(PpapMessageType.RotationOffer, payload =>
            {
                payload.WriteByte((byte)role);
                WriteField(payload, identityBytes);
                WriteInt64(payload, expectedVersion);
                WriteField(payload, encapsulationKey);
                WriteField(payload, descriptor);
            });
        }
        finally
        {
            PpapCryptography.Clear(identityBytes);
            PpapCryptography.Clear(descriptor);
        }
    }

    internal static RotationOffer DecodeRotationOffer(object data)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data), PpapMessageType.RotationOffer);
        var offset = 0;
        var role = ReadRole(payload, ref offset);
        var identityBytes = ReadField(payload, ref offset,
            PpapProtocol.MaximumIdentityBytes);
        var expectedVersion = ReadInt64(payload, ref offset);
        var key = ReadField(payload, ref offset, PpapProtocol.PublicKeyLength);
        var descriptorBytes = ReadField(payload, ref offset, 128);
        RequireEnd(payload, offset);
        if (expectedVersion < 1 || expectedVersion == long.MaxValue)
            throw new InvalidDataException("Invalid rotation version.");
        PpapCryptography.ValidatePublicKey(key);
        var identity = PpapCryptography.NormalizeIdentity(
            PpapCryptography.DecodeUtf8(identityBytes,
                PpapProtocol.MaximumIdentityBytes, "identity"));
        var descriptor = DecodeDescriptor(descriptorBytes);
        if (descriptor.Kind != PpapIdentityKind.PasswordDerived
            || descriptor.Version != expectedVersion + 1)
            throw new InvalidDataException("Invalid rotated registration descriptor.");
        PpapCryptography.Clear(identityBytes);
        PpapCryptography.Clear(descriptorBytes);
        return new RotationOffer
        {
            Role = role,
            Identity = identity,
            ExpectedVersion = expectedVersion,
            Nonce = descriptor.Nonce,
            EncapsulationKey = key,
            KdfProfile = descriptor.KdfProfile,
        };
    }

    internal static byte[] EncodeRotationChallenge(PpapIdentityRole role,
        byte[] ciphertext)
    {
        ValidateRole(role);
        ValidateExact(ciphertext, PpapProtocol.CiphertextLength, nameof(ciphertext));
        return Encode(PpapMessageType.RotationChallenge, payload =>
        {
            payload.WriteByte((byte)role);
            WriteField(payload, ciphertext);
        });
    }

    internal static RotationChallenge DecodeRotationChallenge(object data)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data),
            PpapMessageType.RotationChallenge);
        var offset = 0;
        var role = ReadRole(payload, ref offset);
        var ciphertext = ReadField(payload, ref offset,
            PpapProtocol.CiphertextLength);
        RequireEnd(payload, offset);
        ValidateExact(ciphertext, PpapProtocol.CiphertextLength,
            nameof(ciphertext));
        return new RotationChallenge { Role = role, Ciphertext = ciphertext };
    }

    internal static byte[] EncodeRotationProof(PpapIdentityRole role, byte[] proof)
    {
        ValidateRole(role);
        ValidateExact(proof, PpapProtocol.HashLength, nameof(proof));
        return Encode(PpapMessageType.RotationProof, payload =>
        {
            payload.WriteByte((byte)role);
            WriteField(payload, proof);
        });
    }

    internal static RotationProof DecodeRotationProof(object data)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data), PpapMessageType.RotationProof);
        var offset = 0;
        var role = ReadRole(payload, ref offset);
        var proof = ReadField(payload, ref offset, PpapProtocol.HashLength);
        RequireEnd(payload, offset);
        ValidateExact(proof, PpapProtocol.HashLength, nameof(proof));
        return new RotationProof { Role = role, Proof = proof };
    }

    internal static byte[] EncodeRotationCommit(PpapIdentityRole role, long version)
        => EncodeRoleVersion(PpapMessageType.RotationCommit, role, version);

    internal static long DecodeRotationCommit(object data,
        PpapIdentityRole expectedRole)
        => DecodeRoleVersion(data, PpapMessageType.RotationCommit, expectedRole);

    internal static byte[] EncodeRotationCommitAck(PpapIdentityRole role, long version)
        => EncodeRoleVersion(PpapMessageType.RotationCommitAck, role, version);

    internal static long DecodeRotationCommitAck(object data,
        PpapIdentityRole expectedRole)
        => DecodeRoleVersion(data, PpapMessageType.RotationCommitAck, expectedRole);

    internal static byte[] EncodeRotationDone()
        => Encode(PpapMessageType.RotationDone, _ => { });

    internal static void DecodeRotationDone(object data)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data), PpapMessageType.RotationDone);
        RequireEnd(payload, 0);
    }

    internal static PpapMessageType PeekMessageType(object data)
    {
        var input = RequireWireBytes(data);
        if (input.Length < 10)
            throw new InvalidDataException("Invalid PPAP message length.");
        return (PpapMessageType)input[5];
    }

    static byte[] EncodeRoleVersion(PpapMessageType type, PpapIdentityRole role,
        long version)
    {
        ValidateRole(role);
        if (version < 2)
            throw new InvalidDataException("Invalid committed registration version.");
        return Encode(type, payload =>
        {
            payload.WriteByte((byte)role);
            WriteInt64(payload, version);
        });
    }

    static long DecodeRoleVersion(object data, PpapMessageType type,
        PpapIdentityRole expectedRole)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data), type);
        var offset = 0;
        var role = ReadRole(payload, ref offset);
        var version = ReadInt64(payload, ref offset);
        RequireEnd(payload, offset);
        if (role != expectedRole || version < 2)
            throw new InvalidDataException("Unexpected rotation commit.");
        return version;
    }

    static PpapIdentityRole DecodeRoleOnly(object data, PpapMessageType type)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data), type);
        var offset = 0;
        var role = ReadRole(payload, ref offset);
        RequireEnd(payload, offset);
        return role;
    }

    static PpapIdentityRole ReadRole(byte[] payload, ref int offset)
    {
        if (offset >= payload.Length)
            throw new InvalidDataException("Missing PPAP identity role.");
        var role = (PpapIdentityRole)payload[offset++];
        ValidateRole(role);
        return role;
    }

    static byte ValidateRole(PpapIdentityRole role)
    {
        if (role != PpapIdentityRole.Initiator
            && role != PpapIdentityRole.Responder)
            throw new InvalidDataException("Invalid PPAP identity role.");
        return (byte)role;
    }
}
