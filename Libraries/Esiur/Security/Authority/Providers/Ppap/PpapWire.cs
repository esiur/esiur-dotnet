using System;
using System.IO;
using System.Text;

namespace Esiur.Security.Authority.Providers.Ppap;

/// <summary>
/// Stable message codes for the ppap-mlkem768-v1 authentication and rotation protocols.
/// Authentication payloads are canonical byte arrays, not runtime-specific object graphs.
/// </summary>
public enum PpapMessageType : byte
{
    ClientHello = 1,
    ServerHello = 2,
    InitiatorProof = 3,
    ResponderProof = 4,
    InitiatorFinished = 5,

    RotationStart = 16,
    RotationOffer = 17,
    RotationChallenge = 18,
    RotationProof = 19,
    RotationCommit = 20,
    RotationCommitAck = 21,
    RotationDone = 22,
}

public enum PpapIdentityRole : byte
{
    Initiator = 1,
    Responder = 2,
}

internal sealed class PpapRegistrationDescriptor
{
    public PpapIdentityKind Kind { get; }
    public long Version { get; }
    public byte[] Nonce { get; }
    public PpapKdfProfile KdfProfile { get; }

    public PpapRegistrationDescriptor(PpapIdentityKind kind, long version,
        byte[] nonce, PpapKdfProfile kdfProfile)
    {
        if (version < 1)
            throw new InvalidDataException("Invalid registration version.");
        Kind = kind;
        Version = version;
        KdfProfile = kdfProfile;

        if (kind == PpapIdentityKind.PasswordDerived)
        {
            if (nonce == null || nonce.Length != PpapProtocol.RegistrationNonceLength
                || kdfProfile == null)
                throw new InvalidDataException("Invalid password registration descriptor.");
            Nonce = (byte[])nonce.Clone();
        }
        else if (kind == PpapIdentityKind.StaticMlKem)
        {
            if ((nonce != null && nonce.Length != 0) || kdfProfile != null)
                throw new InvalidDataException("Invalid static-key registration descriptor.");
            Nonce = Array.Empty<byte>();
        }
        else
        {
            throw new InvalidDataException("Unknown PPAP identity kind.");
        }
    }

    public static PpapRegistrationDescriptor FromRecord(PpapRegistrationRecord record)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        return new PpapRegistrationDescriptor(record.Kind, record.Version,
            record.NonceBytes, record.KdfProfile);
    }
}

internal static partial class PpapWire
{
    static readonly byte[] Magic = { (byte)'P', (byte)'P', (byte)'A', (byte)'P' };

    internal sealed class ClientHello
    {
        public AuthenticationMode Mode;
        public string Domain;
        public byte[] EphemeralKey;
        public byte[] InitiatorMask;
    }

    internal sealed class ServerHello
    {
        public byte[] EphemeralCiphertext;
        public byte[] ResponderMask;
        public byte[] MaskedResponderIdentity;
    }

    internal sealed class InitiatorProof
    {
        public byte[] MaskedInitiatorIdentity;
        public byte[] ResponderCiphertext;
        public PpapRegistrationDescriptor ResponderRegistration;
    }

    internal sealed class ResponderProof
    {
        public byte[] InitiatorCiphertext;
        public PpapRegistrationDescriptor InitiatorRegistration;
        public byte[] Finished;
        public byte[] TranscriptCore;
    }

    internal static byte[] EncodeClientHello(AuthenticationMode mode, string domain,
        byte[] ephemeralKey, byte[] initiatorMask)
    {
        ValidateMode(mode);
        PpapCryptography.ValidatePublicKey(ephemeralKey);
        ValidateExact(initiatorMask, PpapProtocol.IdentityMaskLength, nameof(initiatorMask));
        var domainBytes = PpapCryptography.EncodeUtf8(
            PpapCryptography.NormalizeDomain(domain), PpapProtocol.MaximumDomainBytes,
            nameof(domain));
        try
        {
            return Encode(PpapMessageType.ClientHello, payload =>
            {
                payload.WriteByte((byte)mode);
                WriteField(payload, domainBytes);
                WriteField(payload, ephemeralKey);
                WriteField(payload, initiatorMask);
            });
        }
        finally
        {
            PpapCryptography.Clear(domainBytes);
        }
    }

    internal static ClientHello DecodeClientHello(object data)
    {
        var input = RequireWireBytes(data);
        var payload = DecodeEnvelope(input, PpapMessageType.ClientHello);
        var offset = 0;
        if (payload.Length == 0)
            throw new InvalidDataException("Missing authentication mode.");
        var mode = (AuthenticationMode)payload[offset++];
        ValidateMode(mode);
        var domainBytes = ReadField(payload, ref offset, PpapProtocol.MaximumDomainBytes);
        var key = ReadField(payload, ref offset, PpapProtocol.PublicKeyLength);
        var mask = ReadField(payload, ref offset, PpapProtocol.IdentityMaskLength);
        RequireEnd(payload, offset);
        PpapCryptography.ValidatePublicKey(key);
        ValidateExact(mask, PpapProtocol.IdentityMaskLength, nameof(mask));
        var domain = PpapCryptography.NormalizeDomain(PpapCryptography.DecodeUtf8(
            domainBytes, PpapProtocol.MaximumDomainBytes, "domain"));
        PpapCryptography.Clear(domainBytes);
        return new ClientHello
        {
            Mode = mode,
            Domain = domain,
            EphemeralKey = key,
            InitiatorMask = mask,
        };
    }

    internal static byte[] EncodeServerHello(byte[] ephemeralCiphertext,
        byte[] responderMask, byte[] maskedResponderIdentity)
    {
        ValidateExact(ephemeralCiphertext, PpapProtocol.CiphertextLength,
            nameof(ephemeralCiphertext));
        ValidateExact(responderMask, PpapProtocol.IdentityMaskLength,
            nameof(responderMask));
        ValidateOptional(maskedResponderIdentity, PpapProtocol.HashLength,
            nameof(maskedResponderIdentity));
        return Encode(PpapMessageType.ServerHello, payload =>
        {
            WriteField(payload, ephemeralCiphertext);
            WriteField(payload, responderMask);
            WriteField(payload, maskedResponderIdentity);
        });
    }

    internal static ServerHello DecodeServerHello(object data, bool responderAuthenticated)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data), PpapMessageType.ServerHello);
        var offset = 0;
        var ciphertext = ReadField(payload, ref offset, PpapProtocol.CiphertextLength);
        var mask = ReadField(payload, ref offset, PpapProtocol.IdentityMaskLength);
        var maskedIdentity = ReadField(payload, ref offset, PpapProtocol.HashLength);
        RequireEnd(payload, offset);
        ValidateExact(ciphertext, PpapProtocol.CiphertextLength, nameof(ciphertext));
        ValidateExact(mask, PpapProtocol.IdentityMaskLength, nameof(mask));
        ValidatePresence(maskedIdentity, responderAuthenticated, PpapProtocol.HashLength,
            nameof(maskedIdentity));
        return new ServerHello
        {
            EphemeralCiphertext = ciphertext,
            ResponderMask = mask,
            MaskedResponderIdentity = maskedIdentity,
        };
    }

    internal static byte[] EncodeInitiatorProof(byte[] maskedInitiatorIdentity,
        byte[] responderCiphertext, PpapRegistrationDescriptor responderRegistration,
        string domain, byte[] ephemeralSecret)
    {
        ValidateOptional(maskedInitiatorIdentity, PpapProtocol.HashLength,
            nameof(maskedInitiatorIdentity));
        ValidateOptional(responderCiphertext, PpapProtocol.CiphertextLength,
            nameof(responderCiphertext));
        var descriptor = ProtectDescriptor(responderRegistration,
            PpapIdentityRole.Responder, domain, ephemeralSecret);
        try
        {
            return Encode(PpapMessageType.InitiatorProof, payload =>
            {
                WriteField(payload, maskedInitiatorIdentity);
                WriteField(payload, responderCiphertext);
                WriteField(payload, descriptor);
            });
        }
        finally
        {
            PpapCryptography.Clear(descriptor);
        }
    }

    internal static InitiatorProof DecodeInitiatorProof(object data,
        bool initiatorAuthenticated, bool responderAuthenticated,
        string domain, byte[] ephemeralSecret)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data), PpapMessageType.InitiatorProof);
        var offset = 0;
        var maskedIdentity = ReadField(payload, ref offset, PpapProtocol.HashLength);
        var ciphertext = ReadField(payload, ref offset, PpapProtocol.CiphertextLength);
        var descriptorBytes = ReadField(payload, ref offset,
            PpapProtocol.ProtectedDescriptorLength);
        RequireEnd(payload, offset);
        ValidatePresence(maskedIdentity, initiatorAuthenticated, PpapProtocol.HashLength,
            nameof(maskedIdentity));
        ValidatePresence(ciphertext, responderAuthenticated, PpapProtocol.CiphertextLength,
            nameof(ciphertext));
        ValidatePresence(descriptorBytes, responderAuthenticated,
            PpapProtocol.ProtectedDescriptorLength, nameof(descriptorBytes));
        try
        {
            var descriptor = responderAuthenticated
                ? UnprotectDescriptor(descriptorBytes, PpapIdentityRole.Responder,
                    domain, ephemeralSecret)
                : null;
            return new InitiatorProof
            {
                MaskedInitiatorIdentity = maskedIdentity,
                ResponderCiphertext = ciphertext,
                ResponderRegistration = descriptor,
            };
        }
        finally
        {
            PpapCryptography.Clear(descriptorBytes);
        }
    }

    internal static byte[] EncodeResponderProofCore(byte[] initiatorCiphertext,
        PpapRegistrationDescriptor initiatorRegistration, string domain,
        byte[] ephemeralSecret, out byte[] protectedDescriptor)
    {
        ValidateOptional(initiatorCiphertext, PpapProtocol.CiphertextLength,
            nameof(initiatorCiphertext));
        protectedDescriptor = ProtectDescriptor(initiatorRegistration,
            PpapIdentityRole.Initiator, domain, ephemeralSecret);
        try
        {
            return EncodeResponderProofCoreProtected(initiatorCiphertext,
                protectedDescriptor);
        }
        catch
        {
            PpapCryptography.Clear(protectedDescriptor);
            protectedDescriptor = null;
            throw;
        }
    }

    internal static byte[] EncodeResponderProof(byte[] initiatorCiphertext,
        byte[] protectedInitiatorDescriptor, byte[] finished)
    {
        ValidateOptional(initiatorCiphertext, PpapProtocol.CiphertextLength,
            nameof(initiatorCiphertext));
        ValidateOptional(protectedInitiatorDescriptor,
            PpapProtocol.ProtectedDescriptorLength,
            nameof(protectedInitiatorDescriptor));
        ValidateExact(finished, PpapProtocol.FinishedTagLength, nameof(finished));
        return Encode(PpapMessageType.ResponderProof, payload =>
        {
            WriteField(payload, initiatorCiphertext);
            WriteField(payload, protectedInitiatorDescriptor);
            WriteField(payload, finished);
        });
    }

    internal static ResponderProof DecodeResponderProof(object data,
        bool initiatorAuthenticated, string domain, byte[] ephemeralSecret)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data), PpapMessageType.ResponderProof);
        var offset = 0;
        var ciphertext = ReadField(payload, ref offset, PpapProtocol.CiphertextLength);
        var descriptorBytes = ReadField(payload, ref offset,
            PpapProtocol.ProtectedDescriptorLength);
        var finished = ReadField(payload, ref offset, PpapProtocol.FinishedTagLength);
        RequireEnd(payload, offset);
        ValidatePresence(ciphertext, initiatorAuthenticated, PpapProtocol.CiphertextLength,
            nameof(ciphertext));
        ValidatePresence(descriptorBytes, initiatorAuthenticated,
            PpapProtocol.ProtectedDescriptorLength, nameof(descriptorBytes));
        ValidateExact(finished, PpapProtocol.FinishedTagLength, nameof(finished));
        try
        {
            var descriptor = initiatorAuthenticated
                ? UnprotectDescriptor(descriptorBytes, PpapIdentityRole.Initiator,
                    domain, ephemeralSecret)
                : null;
            var core = EncodeResponderProofCoreProtected(ciphertext,
                descriptorBytes);
            return new ResponderProof
            {
                InitiatorCiphertext = ciphertext,
                InitiatorRegistration = descriptor,
                Finished = finished,
                TranscriptCore = core,
            };
        }
        finally
        {
            PpapCryptography.Clear(descriptorBytes);
        }
    }

    static byte[] EncodeResponderProofCoreProtected(byte[] initiatorCiphertext,
        byte[] protectedInitiatorDescriptor)
    {
        ValidateOptional(initiatorCiphertext, PpapProtocol.CiphertextLength,
            nameof(initiatorCiphertext));
        ValidateOptional(protectedInitiatorDescriptor,
            PpapProtocol.ProtectedDescriptorLength,
            nameof(protectedInitiatorDescriptor));
        return Encode(PpapMessageType.ResponderProof, payload =>
        {
            WriteField(payload, initiatorCiphertext);
            WriteField(payload, protectedInitiatorDescriptor);
        });
    }

    internal static byte[] EncodeInitiatorFinished(byte[] finished)
    {
        ValidateExact(finished, PpapProtocol.FinishedTagLength, nameof(finished));
        return Encode(PpapMessageType.InitiatorFinished,
            payload => WriteField(payload, finished));
    }

    internal static byte[] DecodeInitiatorFinished(object data)
    {
        var payload = DecodeEnvelope(RequireWireBytes(data), PpapMessageType.InitiatorFinished);
        var offset = 0;
        var finished = ReadField(payload, ref offset, PpapProtocol.FinishedTagLength);
        RequireEnd(payload, offset);
        ValidateExact(finished, PpapProtocol.FinishedTagLength, nameof(finished));
        return finished;
    }

    internal static byte[] EncodeAuthenticationContext(AuthenticationMode mode,
        string domain, string initiatorIdentity, PpapIdentityKind? initiatorKind,
        string responderIdentity, PpapIdentityKind? responderKind)
    {
        ValidateMode(mode);
        var needsInitiator = mode == AuthenticationMode.InitializerIdentity
            || mode == AuthenticationMode.DualIdentity;
        var needsResponder = mode == AuthenticationMode.ResponderIdentity
            || mode == AuthenticationMode.DualIdentity;
        if (needsInitiator != initiatorKind.HasValue
            || needsInitiator != (initiatorIdentity != null)
            || needsResponder != responderKind.HasValue
            || needsResponder != (responderIdentity != null))
            throw new InvalidDataException("The PPAP authentication context is incomplete.");
        if (initiatorKind.HasValue
            && !Enum.IsDefined(typeof(PpapIdentityKind), initiatorKind.Value))
            throw new InvalidDataException("Invalid initiator identity kind.");
        if (responderKind.HasValue
            && !Enum.IsDefined(typeof(PpapIdentityKind), responderKind.Value))
            throw new InvalidDataException("Invalid responder identity kind.");

        var protocol = Encoding.ASCII.GetBytes(PpapProtocol.Name);
        var modeBytes = new[] { (byte)mode };
        var domainBytes = PpapCryptography.EncodeUtf8(
            PpapCryptography.NormalizeDomain(domain), PpapProtocol.MaximumDomainBytes,
            nameof(domain));
        var initiatorBytes = PpapCryptography.EncodeUtf8(
            initiatorIdentity == null ? string.Empty
                : PpapCryptography.NormalizeIdentity(initiatorIdentity),
            PpapProtocol.MaximumIdentityBytes, nameof(initiatorIdentity));
        var responderBytes = PpapCryptography.EncodeUtf8(
            responderIdentity == null ? string.Empty
                : PpapCryptography.NormalizeIdentity(responderIdentity),
            PpapProtocol.MaximumIdentityBytes, nameof(responderIdentity));
        var kinds = new[]
        {
            initiatorKind.HasValue ? (byte)initiatorKind.Value : (byte)0,
            responderKind.HasValue ? (byte)responderKind.Value : (byte)0,
        };
        try
        {
            return PpapCryptography.EncodeFrames(protocol, modeBytes, domainBytes,
                initiatorBytes, responderBytes, kinds);
        }
        finally
        {
            PpapCryptography.Clear(protocol);
            PpapCryptography.Clear(domainBytes);
            PpapCryptography.Clear(initiatorBytes);
            PpapCryptography.Clear(responderBytes);
            PpapCryptography.Clear(kinds);
        }
    }

    static byte[] ProtectDescriptor(PpapRegistrationDescriptor descriptor,
        PpapIdentityRole role, string domain, byte[] ephemeralSecret)
    {
        var plaintext = EncodeDescriptor(descriptor);
        if (plaintext.Length == 0)
            return plaintext;
        try
        {
            return PpapCryptography.ProtectRegistrationDescriptor(domain, role,
                ephemeralSecret, plaintext);
        }
        finally
        {
            PpapCryptography.Clear(plaintext);
        }
    }

    static PpapRegistrationDescriptor UnprotectDescriptor(byte[] descriptor,
        PpapIdentityRole role, string domain, byte[] ephemeralSecret)
    {
        var plaintext = PpapCryptography.UnprotectRegistrationDescriptor(domain,
            role, ephemeralSecret, descriptor);
        try
        {
            return DecodeDescriptor(plaintext);
        }
        finally
        {
            PpapCryptography.Clear(plaintext);
        }
    }

    static byte[] EncodeDescriptor(PpapRegistrationDescriptor descriptor)
    {
        if (descriptor == null)
            return Array.Empty<byte>();
        using (var output = new MemoryStream())
        {
            output.WriteByte((byte)descriptor.Kind);
            WriteInt64(output, descriptor.Version);
            WriteField(output, descriptor.Nonce);
            if (descriptor.Kind == PpapIdentityKind.PasswordDerived)
            {
                output.WriteByte(1);
                PpapCryptography.WriteInt32(output, descriptor.KdfProfile.Version);
                PpapCryptography.WriteInt32(output, descriptor.KdfProfile.MemoryKiB);
                PpapCryptography.WriteInt32(output, descriptor.KdfProfile.Iterations);
                PpapCryptography.WriteInt32(output, descriptor.KdfProfile.Parallelism);
            }
            else
            {
                output.WriteByte(0);
            }
            return output.ToArray();
        }
    }

    static PpapRegistrationDescriptor DecodeDescriptor(byte[] input)
    {
        if (input == null || input.Length < 10 || input.Length > 128)
            throw new InvalidDataException("Invalid registration descriptor.");
        var offset = 0;
        var kind = (PpapIdentityKind)input[offset++];
        var version = ReadInt64(input, ref offset);
        var nonce = ReadField(input, ref offset, PpapProtocol.RegistrationNonceLength);
        if (offset >= input.Length)
            throw new InvalidDataException("Truncated registration descriptor.");
        var hasProfile = input[offset++];
        PpapKdfProfile profile = null;
        if (hasProfile == 1)
        {
            var profileVersion = PpapCryptography.ReadInt32(input, ref offset);
            var memory = PpapCryptography.ReadInt32(input, ref offset);
            var iterations = PpapCryptography.ReadInt32(input, ref offset);
            var parallelism = PpapCryptography.ReadInt32(input, ref offset);
            profile = new PpapKdfProfile(profileVersion, memory, iterations, parallelism);
        }
        else if (hasProfile != 0)
        {
            throw new InvalidDataException("Invalid KDF profile marker.");
        }
        RequireEnd(input, offset);
        return new PpapRegistrationDescriptor(kind, version, nonce, profile);
    }

    static byte[] Encode(PpapMessageType type, Action<MemoryStream> writePayload)
    {
        using (var payload = new MemoryStream())
        {
            writePayload(payload);
            if (payload.Length > PpapProtocol.MaximumWireMessageBytes - 10)
                throw new InvalidDataException("PPAP payload is too large.");
            using (var message = new MemoryStream())
            {
                message.Write(Magic, 0, Magic.Length);
                message.WriteByte(PpapProtocol.WireVersion);
                message.WriteByte((byte)type);
                PpapCryptography.WriteInt32(message, (int)payload.Length);
                payload.Position = 0;
                payload.CopyTo(message);
                return message.ToArray();
            }
        }
    }

    static byte[] DecodeEnvelope(byte[] input, PpapMessageType expectedType)
    {
        if (input.Length < 10 || input.Length > PpapProtocol.MaximumWireMessageBytes)
            throw new InvalidDataException("Invalid PPAP message length.");
        for (var i = 0; i < Magic.Length; i++)
            if (input[i] != Magic[i])
                throw new InvalidDataException("Invalid PPAP message magic.");
        if (input[4] != PpapProtocol.WireVersion)
            throw new InvalidDataException("Unsupported PPAP wire version.");
        if (input[5] != (byte)expectedType)
            throw new InvalidDataException("Unexpected PPAP message type.");
        var offset = 6;
        var length = PpapCryptography.ReadInt32(input, ref offset);
        if (length != input.Length - offset)
            throw new InvalidDataException("Invalid PPAP payload length.");
        var payload = new byte[length];
        Buffer.BlockCopy(input, offset, payload, 0, length);
        return payload;
    }

    static byte[] RequireWireBytes(object data)
    {
        if (!(data is byte[] input))
            throw new InvalidDataException("PPAP authentication data must be a byte array.");
        return input;
    }

    static void WriteField(Stream output, byte[] value)
    {
        var field = value ?? Array.Empty<byte>();
        PpapCryptography.WriteInt32(output, field.Length);
        output.Write(field, 0, field.Length);
    }

    static byte[] ReadField(byte[] input, ref int offset, int maximumLength)
    {
        var length = PpapCryptography.ReadInt32(input, ref offset);
        if (length > maximumLength || input.Length - offset < length)
            throw new InvalidDataException("Invalid PPAP field length.");
        var field = new byte[length];
        Buffer.BlockCopy(input, offset, field, 0, length);
        offset += length;
        return field;
    }

    static void WriteInt64(Stream output, long value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        for (var shift = 56; shift >= 0; shift -= 8)
            output.WriteByte((byte)(value >> shift));
    }

    static long ReadInt64(byte[] input, ref int offset)
    {
        if (input.Length - offset < 8)
            throw new InvalidDataException("Truncated PPAP integer.");
        long value = 0;
        for (var i = 0; i < 8; i++)
            value = (value << 8) | input[offset++];
        if (value < 0)
            throw new InvalidDataException("Negative PPAP integer.");
        return value;
    }

    static void ValidateMode(AuthenticationMode mode)
    {
        if (mode != AuthenticationMode.InitializerIdentity
            && mode != AuthenticationMode.ResponderIdentity
            && mode != AuthenticationMode.DualIdentity)
            throw new InvalidDataException("Unsupported PPAP authentication mode.");
    }

    static void ValidateExact(byte[] value, int length, string name)
    {
        if (value == null || value.Length != length)
            throw new InvalidDataException($"{name} has an invalid length.");
    }

    static void ValidateOptional(byte[] value, int length, string name)
    {
        if (value != null && value.Length != 0 && value.Length != length)
            throw new InvalidDataException($"{name} has an invalid length.");
    }

    static void ValidatePresence(byte[] value, bool required, int length, string name)
    {
        if (required)
            ValidateExact(value, length, name);
        else if (value == null || value.Length != 0)
            throw new InvalidDataException($"{name} must be absent.");
    }

    static void RequireEnd(byte[] input, int offset)
    {
        if (offset != input.Length)
            throw new InvalidDataException("Trailing PPAP message data.");
    }
}
