using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Kems;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Esiur.Security.Authority.Providers.Ppap;

public static class PpapProtocol
{
    public const string Name = "ppap-mlkem768-v1";
    public const int WireVersion = 1;
    public const int PublicKeyLength = 1184;
    public const int PrivateKeyLength = 2400;
    public const int CiphertextLength = 1088;
    public const int KemSecretLength = 32;
    public const int SeedLength = 64;
    public const int HashLength = 32;
    public const int FinishedTagLength = 32;
    public const int IdentityMaskLength = 32;
    public const int RegistrationNonceLength = 32;
    public const int SessionKeyLength = 32;
    public const int DescriptorPlaintextLength = 128;
    public const int DescriptorKeyLength = 32;
    public const int DescriptorNonceLength = 12;
    public const int DescriptorTagLength = 16;
    public const int ProtectedDescriptorLength =
        DescriptorPlaintextLength + DescriptorTagLength;
    public const int MaximumIdentityBytes = 512;
    public const int MaximumDomainBytes = 512;
    public const int MaximumPasswordBytes = 4096;
    public const int MaximumWireMessageBytes = 8192;
}

internal static class PpapCryptography
{
    static readonly MLKemParameters KemParameters = MLKemParameters.ml_kem_768;
    static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
    static readonly byte[] PasswordSeedLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/password-seed");
    static readonly byte[] IdentityMaskLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/masked-identity");
    static readonly byte[] DescriptorSaltLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/descriptor-salt");
    static readonly byte[] DescriptorKeyLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/descriptor-key");
    static readonly byte[] DescriptorNonceLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/descriptor-nonce");
    static readonly byte[] DescriptorAadLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/descriptor-aad");
    static readonly byte[] InitiatorDescriptorLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/initiator-registration");
    static readonly byte[] ResponderDescriptorLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/responder-registration");
    static readonly byte[] TranscriptLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/transcript");
    static readonly byte[] KeyScheduleLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/key-schedule");
    static readonly byte[] SessionKeyLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/session-key");
    static readonly byte[] InitiatorFinishedKeyLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/initiator-finished-key");
    static readonly byte[] ResponderFinishedKeyLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/responder-finished-key");
    static readonly byte[] InitiatorFinishedLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/initiator-finished");
    static readonly byte[] ResponderFinishedLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/responder-finished");
    static readonly byte[] RotationProofLabel = Encoding.ASCII.GetBytes(
        "esiur/ppap-mlkem768-v1/rotation-proof");

    internal static string NormalizeIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            throw new ArgumentException("An identity is required.", nameof(identity));
        var normalized = identity.Normalize(NormalizationForm.FormC);
        var encoded = EncodeUtf8(normalized, PpapProtocol.MaximumIdentityBytes, nameof(identity));
        Clear(encoded);
        return normalized;
    }

    internal static string NormalizeDomain(string domain)
    {
        var normalized = (domain ?? string.Empty).Normalize(NormalizationForm.FormC);
        var encoded = EncodeUtf8(normalized, PpapProtocol.MaximumDomainBytes, nameof(domain));
        Clear(encoded);
        return normalized;
    }

    internal static byte[] EncodeUtf8(string value, int maximumBytes, string parameterName)
    {
        try
        {
            var encoded = StrictUtf8.GetBytes(value ?? string.Empty);
            if (encoded.Length > maximumBytes)
            {
                Clear(encoded);
                throw new ArgumentException("The UTF-8 value is too long.", parameterName);
            }
            return encoded;
        }
        catch (EncoderFallbackException ex)
        {
            throw new ArgumentException("The value contains invalid Unicode.", parameterName, ex);
        }
    }

    internal static string DecodeUtf8(byte[] value, int maximumBytes, string parameterName)
    {
        if (value == null || value.Length > maximumBytes)
            throw new ArgumentException("The UTF-8 value has an invalid length.", parameterName);
        try
        {
            return StrictUtf8.GetString(value).Normalize(NormalizationForm.FormC);
        }
        catch (DecoderFallbackException ex)
        {
            throw new ArgumentException("The value is not valid UTF-8.", parameterName, ex);
        }
    }

    internal static byte[] RandomBytes(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        var value = new byte[length];
        using (var random = RandomNumberGenerator.Create())
            random.GetBytes(value);
        return value;
    }

    internal static byte[] Hash(params byte[][] values)
    {
        var digest = new Sha3Digest(256);
        foreach (var value in values)
        {
            if (value != null && value.Length != 0)
                digest.BlockUpdate(value, 0, value.Length);
        }
        var output = new byte[PpapProtocol.HashLength];
        digest.DoFinal(output, 0);
        return output;
    }

    internal static byte[] HashFramed(params byte[][] values)
    {
        var encoded = EncodeFrames(values);
        try
        {
            return Hash(encoded);
        }
        finally
        {
            Clear(encoded);
        }
    }

    internal static byte[] MaskIdentity(string domain, string identity, byte[] mask,
        byte[] maskKey)
    {
        if (mask == null || mask.Length != PpapProtocol.IdentityMaskLength)
            throw new ArgumentException("The identity mask has an invalid length.", nameof(mask));
        ValidateKemSecret(maskKey, nameof(maskKey));

        var domainBytes = EncodeUtf8(NormalizeDomain(domain),
            PpapProtocol.MaximumDomainBytes, nameof(domain));
        var identityBytes = EncodeUtf8(NormalizeIdentity(identity),
            PpapProtocol.MaximumIdentityBytes, nameof(identity));
        try
        {
            var input = EncodeFrames(IdentityMaskLabel, domainBytes, identityBytes, mask);
            try
            {
                return ComputeMac(maskKey, input);
            }
            finally
            {
                Clear(input);
            }
        }
        finally
        {
            Clear(domainBytes);
            Clear(identityBytes);
        }
    }

    // Protected descriptor v1 is:
    //   uint32be descriptorLength || canonicalDescriptor || zeroPadding
    // padded to DescriptorPlaintextLength, followed by a 128-bit GCM tag.
    // HKDF-SHA3 derives a role/domain/message-specific AES-256 key and 96-bit
    // nonce from the fresh ephemeral ML-KEM secret; the nonce is not sent.
    // Protocol implementations MUST encrypt at most one descriptor for each
    // role under a given ephemeral secret. ResponderProof therefore reuses its
    // single protected field when constructing the transcript core and packet.
    internal static byte[] ProtectRegistrationDescriptor(string domain,
        PpapIdentityRole role, byte[] ephemeralSecret, byte[] descriptor)
    {
        ValidateKemSecret(ephemeralSecret, nameof(ephemeralSecret));
        if (descriptor == null || descriptor.Length == 0
            || descriptor.Length > PpapProtocol.DescriptorPlaintextLength - 4)
            throw new ArgumentException("The registration descriptor has an invalid length.",
                nameof(descriptor));

        var framed = EncodeFrames(descriptor);
        var plaintext = new byte[PpapProtocol.DescriptorPlaintextLength];
        var output = new byte[PpapProtocol.ProtectedDescriptorLength];
        byte[] key = null;
        byte[] nonce = null;
        byte[] aad = null;
        try
        {
            Buffer.BlockCopy(framed, 0, plaintext, 0, framed.Length);
            DeriveDescriptorProtection(domain, role, ephemeralSecret,
                out key, out nonce, out aad);

            var cipher = new GcmBlockCipher(AesUtilities.CreateEngine());
            cipher.Init(true, new AeadParameters(new KeyParameter(key),
                PpapProtocol.DescriptorTagLength * 8, nonce, aad));
            var written = cipher.ProcessBytes(plaintext, 0, plaintext.Length,
                output, 0);
            written += cipher.DoFinal(output, written);
            if (written != output.Length)
                throw new CryptographicException(
                    "AES-GCM produced an invalid protected descriptor length.");
            return output;
        }
        catch (CryptoException ex)
        {
            Clear(output);
            throw new CryptographicException(
                "Registration descriptor protection failed.", ex);
        }
        catch
        {
            Clear(output);
            throw;
        }
        finally
        {
            Clear(framed);
            Clear(plaintext);
            Clear(key);
            Clear(nonce);
            Clear(aad);
        }
    }

    internal static byte[] UnprotectRegistrationDescriptor(string domain,
        PpapIdentityRole role, byte[] ephemeralSecret, byte[] protectedDescriptor)
    {
        ValidateKemSecret(ephemeralSecret, nameof(ephemeralSecret));
        if (protectedDescriptor == null
            || protectedDescriptor.Length != PpapProtocol.ProtectedDescriptorLength)
            throw new InvalidDataException(
                "The protected registration descriptor has an invalid length.");

        var plaintext = new byte[PpapProtocol.DescriptorPlaintextLength];
        byte[] key = null;
        byte[] nonce = null;
        byte[] aad = null;
        try
        {
            DeriveDescriptorProtection(domain, role, ephemeralSecret,
                out key, out nonce, out aad);
            var cipher = new GcmBlockCipher(AesUtilities.CreateEngine());
            cipher.Init(false, new AeadParameters(new KeyParameter(key),
                PpapProtocol.DescriptorTagLength * 8, nonce, aad));
            var written = cipher.ProcessBytes(protectedDescriptor, 0,
                protectedDescriptor.Length, plaintext, 0);
            written += cipher.DoFinal(plaintext, written);
            if (written != plaintext.Length)
                throw new InvalidDataException(
                    "AES-GCM produced an invalid registration descriptor length.");

            var offset = 0;
            var descriptorLength = ReadInt32(plaintext, ref offset);
            if (descriptorLength == 0
                || descriptorLength > PpapProtocol.DescriptorPlaintextLength - offset)
                throw new InvalidDataException(
                    "The protected registration descriptor is malformed.");
            var descriptor = new byte[descriptorLength];
            Buffer.BlockCopy(plaintext, offset, descriptor, 0, descriptorLength);
            offset += descriptorLength;
            for (var i = offset; i < plaintext.Length; i++)
            {
                if (plaintext[i] != 0)
                {
                    Clear(descriptor);
                    throw new InvalidDataException(
                        "The protected registration descriptor is not canonical.");
                }
            }
            return descriptor;
        }
        catch (CryptoException ex)
        {
            throw new InvalidDataException(
                "Protected registration descriptor authentication failed.", ex);
        }
        finally
        {
            Clear(plaintext);
            Clear(key);
            Clear(nonce);
            Clear(aad);
        }
    }

    static void DeriveDescriptorProtection(string domain, PpapIdentityRole role,
        byte[] ephemeralSecret, out byte[] key, out byte[] nonce, out byte[] aad)
    {
        var roleLabel = GetDescriptorRoleLabel(role, out var messageType);
        var domainBytes = EncodeUtf8(NormalizeDomain(domain),
            PpapProtocol.MaximumDomainBytes, nameof(domain));
        var binding = new[]
        {
            (byte)PpapProtocol.WireVersion,
            (byte)role,
            (byte)messageType,
        };
        var salt = HashFramed(DescriptorSaltLabel, roleLabel, domainBytes, binding);
        key = null;
        nonce = null;
        aad = null;
        try
        {
            key = Expand(ephemeralSecret, salt,
                EncodeFrames(DescriptorKeyLabel, roleLabel, domainBytes, binding),
                PpapProtocol.DescriptorKeyLength);
            nonce = Expand(ephemeralSecret, salt,
                EncodeFrames(DescriptorNonceLabel, roleLabel, domainBytes, binding),
                PpapProtocol.DescriptorNonceLength);
            aad = EncodeFrames(DescriptorAadLabel, roleLabel, domainBytes, binding);
        }
        catch
        {
            Clear(key);
            Clear(nonce);
            Clear(aad);
            key = null;
            nonce = null;
            aad = null;
            throw;
        }
        finally
        {
            Clear(domainBytes);
            Clear(binding);
            Clear(salt);
        }
    }

    static byte[] GetDescriptorRoleLabel(PpapIdentityRole role,
        out PpapMessageType messageType)
    {
        if (role == PpapIdentityRole.Initiator)
        {
            messageType = PpapMessageType.ResponderProof;
            return InitiatorDescriptorLabel;
        }
        if (role == PpapIdentityRole.Responder)
        {
            messageType = PpapMessageType.InitiatorProof;
            return ResponderDescriptorLabel;
        }
        throw new ArgumentOutOfRangeException(nameof(role));
    }

    internal static byte[] DerivePasswordPrivateKey(string domain, string identity,
        byte[] password, byte[] nonce, PpapKdfProfile profile)
    {
        if (password == null || password.Length == 0
            || password.Length > PpapProtocol.MaximumPasswordBytes)
            throw new ArgumentException("The password has an invalid length.", nameof(password));
        if (nonce == null || nonce.Length != PpapProtocol.RegistrationNonceLength)
            throw new ArgumentException("The registration nonce has an invalid length.", nameof(nonce));
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        var identityBytes = EncodeUtf8(NormalizeIdentity(identity),
            PpapProtocol.MaximumIdentityBytes, nameof(identity));
        var domainBytes = EncodeUtf8(NormalizeDomain(domain),
            PpapProtocol.MaximumDomainBytes, nameof(domain));
        var input = EncodeFrames(PasswordSeedLabel, domainBytes, identityBytes,
            password, nonce);
        var seed = new byte[PpapProtocol.SeedLength];
        try
        {
            var parameters = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
                .WithVersion(profile.Version)
                .WithSalt((byte[])nonce.Clone())
                .WithMemoryAsKB(profile.MemoryKiB)
                .WithIterations(profile.Iterations)
                .WithParallelism(profile.Parallelism)
                .Build();
            var argon2 = new Argon2BytesGenerator();
            argon2.Init(parameters);
            argon2.GenerateBytes(input, seed);

            var privateKey = MLKemPrivateKeyParameters.FromSeed(KemParameters, seed);
            return privateKey.GetEncoded();
        }
        finally
        {
            Clear(identityBytes);
            Clear(domainBytes);
            Clear(input);
            Clear(seed);
        }
    }

    internal static void GenerateKeyPair(out byte[] privateKey, out byte[] publicKey)
    {
        var seed = RandomBytes(PpapProtocol.SeedLength);
        try
        {
            var parameters = MLKemPrivateKeyParameters.FromSeed(KemParameters, seed);
            privateKey = parameters.GetEncoded();
            publicKey = parameters.GetPublicKeyEncoded();
        }
        finally
        {
            Clear(seed);
        }
    }

    internal static byte[] GetPublicKey(byte[] privateKey)
    {
        ValidatePrivateKey(privateKey);
        return MLKemPrivateKeyParameters.FromEncoding(KemParameters, privateKey)
            .GetPublicKeyEncoded();
    }

    internal static void ValidatePrivateKey(byte[] privateKey)
    {
        if (privateKey == null || privateKey.Length != PpapProtocol.PrivateKeyLength)
            throw new ArgumentException("The ML-KEM-768 private key has an invalid length.", nameof(privateKey));
        MLKemPrivateKeyParameters.FromEncoding(KemParameters, privateKey);
    }

    internal static void ValidatePublicKey(byte[] publicKey)
    {
        if (publicKey == null || publicKey.Length != PpapProtocol.PublicKeyLength)
            throw new ArgumentException("The ML-KEM-768 public key has an invalid length.", nameof(publicKey));
        MLKemPublicKeyParameters.FromEncoding(KemParameters, publicKey);
    }

    internal static void Encapsulate(byte[] publicKey, out byte[] ciphertext,
        out byte[] secret)
    {
        ValidatePublicKey(publicKey);
        var encapsulator = new MLKemEncapsulator(KemParameters);
        encapsulator.Init(MLKemPublicKeyParameters.FromEncoding(KemParameters, publicKey));
        ciphertext = new byte[encapsulator.EncapsulationLength];
        secret = new byte[encapsulator.SecretLength];
        try
        {
            encapsulator.Encapsulate(ciphertext, 0, ciphertext.Length,
                secret, 0, secret.Length);
        }
        catch
        {
            Clear(ciphertext);
            Clear(secret);
            ciphertext = null;
            secret = null;
            throw;
        }
    }

    internal static byte[] Decapsulate(byte[] privateKey, byte[] ciphertext)
    {
        ValidatePrivateKey(privateKey);
        if (ciphertext == null || ciphertext.Length != PpapProtocol.CiphertextLength)
            throw new ArgumentException("The ML-KEM-768 ciphertext has an invalid length.", nameof(ciphertext));
        var decapsulator = new MLKemDecapsulator(KemParameters);
        decapsulator.Init(MLKemPrivateKeyParameters.FromEncoding(KemParameters, privateKey));
        var secret = new byte[decapsulator.SecretLength];
        decapsulator.Decapsulate(ciphertext, 0, ciphertext.Length,
            secret, 0, secret.Length);
        return secret;
    }

    internal static byte[] ComputeTranscriptHash(IList<byte[]> messages,
        byte[] authenticationContext)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));
        var frames = new byte[messages.Count + 2][];
        frames[0] = TranscriptLabel;
        frames[1] = authenticationContext ?? Array.Empty<byte>();
        for (var i = 0; i < messages.Count; i++)
            frames[i + 2] = messages[i];
        return HashFramed(frames);
    }

    internal static void DeriveHandshakeKeys(byte[] ephemeralSecret,
        byte[] initiatorIdentitySecret, byte[] responderIdentitySecret,
        byte[] transcriptHash, byte[] authenticationContext,
        out byte[] sessionKey, out byte[] initiatorFinishedKey,
        out byte[] responderFinishedKey)
    {
        ValidateKemSecret(ephemeralSecret, nameof(ephemeralSecret));
        ValidateOptionalKemSecret(initiatorIdentitySecret, nameof(initiatorIdentitySecret));
        ValidateOptionalKemSecret(responderIdentitySecret, nameof(responderIdentitySecret));
        if (transcriptHash == null || transcriptHash.Length != PpapProtocol.HashLength)
            throw new ArgumentException("The transcript hash has an invalid length.", nameof(transcriptHash));

        var input = EncodeFrames(KeyScheduleLabel, ephemeralSecret,
            initiatorIdentitySecret ?? Array.Empty<byte>(),
            responderIdentitySecret ?? Array.Empty<byte>());
        try
        {
            sessionKey = Expand(input, transcriptHash,
                EncodeFrames(SessionKeyLabel, authenticationContext),
                PpapProtocol.SessionKeyLength);
            initiatorFinishedKey = Expand(input, transcriptHash,
                EncodeFrames(InitiatorFinishedKeyLabel, authenticationContext),
                PpapProtocol.HashLength);
            responderFinishedKey = Expand(input, transcriptHash,
                EncodeFrames(ResponderFinishedKeyLabel, authenticationContext),
                PpapProtocol.HashLength);
        }
        finally
        {
            Clear(input);
        }
    }

    static byte[] Expand(byte[] input, byte[] salt, byte[] info, int length)
    {
        try
        {
            var hkdf = new HkdfBytesGenerator(new Sha3Digest(256));
            hkdf.Init(new HkdfParameters(input, salt, info));
            var output = new byte[length];
            hkdf.GenerateBytes(output, 0, output.Length);
            return output;
        }
        finally
        {
            Clear(info);
        }
    }

    internal static byte[] ComputeFinished(bool initiator, byte[] finishedKey,
        byte[] transcriptHash)
    {
        if (finishedKey == null || finishedKey.Length != PpapProtocol.HashLength)
            throw new ArgumentException("The Finished key has an invalid length.", nameof(finishedKey));
        if (transcriptHash == null || transcriptHash.Length != PpapProtocol.HashLength)
            throw new ArgumentException("The transcript hash has an invalid length.", nameof(transcriptHash));
        var input = EncodeFrames(initiator ? InitiatorFinishedLabel : ResponderFinishedLabel,
            transcriptHash);
        try
        {
            return ComputeMac(finishedKey, input);
        }
        finally
        {
            Clear(input);
        }
    }

    internal static byte[] ComputeMac(byte[] key, byte[] input)
    {
        if (key == null || key.Length == 0)
            throw new ArgumentException("A MAC key is required.", nameof(key));
        var hmac = new HMac(new Sha3Digest(256));
        hmac.Init(new KeyParameter(key));
        hmac.BlockUpdate(input, 0, input.Length);
        var output = new byte[hmac.GetMacSize()];
        hmac.DoFinal(output, 0);
        return output;
    }

    internal static byte[] ComputeRotationProof(byte[] challengeSecret,
        byte[] sessionKey, byte[] offer, byte[] challenge)
    {
        ValidateKemSecret(challengeSecret, nameof(challengeSecret));
        if (sessionKey == null || sessionKey.Length != PpapProtocol.SessionKeyLength)
            throw new ArgumentException("The session key has an invalid length.", nameof(sessionKey));
        var context = HashFramed(RotationProofLabel, sessionKey, offer, challenge);
        try
        {
            return ComputeMac(challengeSecret, context);
        }
        finally
        {
            Clear(context);
        }
    }

    internal static bool FixedTimeEquals(byte[] left, byte[] right)
    {
        return left != null && right != null && left.Length == right.Length
            && Arrays.FixedTimeEquals(left, right);
    }

    internal static byte[] EncodeFrames(params byte[][] values)
    {
        using (var output = new MemoryStream())
        {
            foreach (var value in values)
            {
                var field = value ?? Array.Empty<byte>();
                WriteInt32(output, field.Length);
                output.Write(field, 0, field.Length);
            }
            return output.ToArray();
        }
    }

    internal static void WriteInt32(Stream output, int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        output.WriteByte((byte)(value >> 24));
        output.WriteByte((byte)(value >> 16));
        output.WriteByte((byte)(value >> 8));
        output.WriteByte((byte)value);
    }

    internal static int ReadInt32(byte[] input, ref int offset)
    {
        if (input == null || offset < 0 || input.Length - offset < 4)
            throw new InvalidDataException("Truncated PPAP integer.");
        var value = (input[offset] << 24)
            | (input[offset + 1] << 16)
            | (input[offset + 2] << 8)
            | input[offset + 3];
        offset += 4;
        if (value < 0)
            throw new InvalidDataException("Negative PPAP length.");
        return value;
    }

    static void ValidateKemSecret(byte[] value, string parameterName)
    {
        if (value == null || value.Length != PpapProtocol.KemSecretLength)
            throw new ArgumentException("The KEM secret has an invalid length.", parameterName);
    }

    static void ValidateOptionalKemSecret(byte[] value, string parameterName)
    {
        if (value != null && value.Length != PpapProtocol.KemSecretLength)
            throw new ArgumentException("The KEM secret has an invalid length.", parameterName);
    }

    internal static void Clear(byte[] value)
    {
        if (value != null)
            Array.Clear(value, 0, value.Length);
    }
}
