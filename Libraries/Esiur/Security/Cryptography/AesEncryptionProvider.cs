using Esiur.Security.Authority;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Esiur.Security.Cryptography;

/// <summary>
/// Creates AES-256-GCM record ciphers. Session keys and nonce prefixes are
/// derived with HKDF-SHA256 and separated by protocol direction and purpose.
/// </summary>
public sealed class AesEncryptionProvider : IEncryptionProvider
{
    public const string Name = "aes-gcm";

    public string DefaultName => Name;

    public uint MaximumRecordOverhead => 8 + 16;

    public ISymetricCipher CreateCipher(EncryptionContext context)
        => new AesGcmSymetricCipher(context);
}

/// <summary>
/// AES-256-GCM session record cipher.
///
/// A record contains an eight-byte, big-endian sequence followed by ciphertext
/// and a sixteen-byte GCM tag. The transport's four-byte record length and the
/// sequence are authenticated as associated data. Sequence numbers are implicit
/// state as well as explicit record fields, so replay and reordering fail closed.
/// </summary>
public sealed class AesGcmSymetricCipher : ISymetricCipher, IDisposable
{
    const int KeySize = 32;
    const int NoncePrefixSize = 4;
    const int SequenceSize = 8;
    const int TagSize = 16;
    const int RecordOverhead = SequenceSize + TagSize;
    const int AadSize = 4 + SequenceSize;
    const int MinimumSharedKeySize = 16;
    const int MaximumSharedKeySize = 1024;
    const int MinimumPeerNonceSize = 16;
    const int MaximumPeerNonceSize = 64;

    static readonly byte[] ContextLabel = Encoding.ASCII.GetBytes("esiur/ep/aes-256-gcm/context/v3");
    static readonly byte[] InitiatorToResponderKeyLabel = Encoding.ASCII.GetBytes("esiur/ep/aes-256-gcm/v1/initiator-to-responder/key");
    static readonly byte[] ResponderToInitiatorKeyLabel = Encoding.ASCII.GetBytes("esiur/ep/aes-256-gcm/v1/responder-to-initiator/key");
    static readonly byte[] InitiatorToResponderNonceLabel = Encoding.ASCII.GetBytes("esiur/ep/aes-256-gcm/v1/initiator-to-responder/nonce");
    static readonly byte[] ResponderToInitiatorNonceLabel = Encoding.ASCII.GetBytes("esiur/ep/aes-256-gcm/v1/responder-to-initiator/nonce");

    readonly object _sendLock = new object();
    readonly object _receiveLock = new object();
    readonly byte[] _contextSalt;
    readonly byte[] _sendKeyLabel;
    readonly byte[] _receiveKeyLabel;
    readonly byte[] _sendNonceLabel;
    readonly byte[] _receiveNonceLabel;

    byte[] _sendKey;
    byte[] _receiveKey;
    byte[] _sendNoncePrefix;
    byte[] _receiveNoncePrefix;
    ulong _sendSequence;
    ulong _receiveSequence;
    bool _keyInitialized;
    bool _disposed;

    internal AesGcmSymetricCipher(EncryptionContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        ValidateContext(context);

        _contextSalt = ComposeContextSalt(context);

        if (context.Direction == AuthenticationDirection.Initiator)
        {
            _sendKeyLabel = InitiatorToResponderKeyLabel;
            _receiveKeyLabel = ResponderToInitiatorKeyLabel;
            _sendNonceLabel = InitiatorToResponderNonceLabel;
            _receiveNonceLabel = ResponderToInitiatorNonceLabel;
        }
        else
        {
            _sendKeyLabel = ResponderToInitiatorKeyLabel;
            _receiveKeyLabel = InitiatorToResponderKeyLabel;
            _sendNonceLabel = ResponderToInitiatorNonceLabel;
            _receiveNonceLabel = InitiatorToResponderNonceLabel;
        }

        var acceptedKey = SetKey(context.Key);
        Clear(acceptedKey);
    }

    /// <summary>Identifier retained for compatibility with <see cref="ISymetricCipher"/>.</summary>
    public ushort Identifier => (ushort)SymetricEncryptionAlgorithmType.AES;

    public byte[] Encrypt(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length > int.MaxValue - RecordOverhead)
            throw new ArgumentOutOfRangeException(nameof(data), "The encrypted record exceeds the 32-bit record length.");

        lock (_sendLock)
        {
            ThrowIfDisposed();

            if (_sendSequence == ulong.MaxValue)
                throw new CryptographicException("The AES-GCM send sequence is exhausted.");

            var sequence = _sendSequence;
            var recordLength = data.Length + RecordOverhead;
            var nonce = ComposeNonce(_sendNoncePrefix, sequence);
            var aad = ComposeAad((uint)recordLength, sequence);
            var cipherText = new byte[data.Length + TagSize];

            try
            {
                var cipher = new GcmBlockCipher(AesUtilities.CreateEngine());
                cipher.Init(true, new AeadParameters(new KeyParameter(_sendKey), TagSize * 8, nonce, aad));

                var written = cipher.ProcessBytes(data, 0, data.Length, cipherText, 0);
                written += cipher.DoFinal(cipherText, written);

                if (written != cipherText.Length)
                    throw new CryptographicException("AES-GCM produced an invalid record length.");

                var record = new byte[recordLength];
                WriteUInt64BigEndian(record, 0, sequence);
                Buffer.BlockCopy(cipherText, 0, record, SequenceSize, cipherText.Length);

                _sendSequence++;
                return record;
            }
            catch (CryptoException ex)
            {
                throw new CryptographicException("AES-GCM record encryption failed.", ex);
            }
            finally
            {
                Clear(nonce);
                Clear(aad);
                Clear(cipherText);
            }
        }
    }

    public byte[] Decrypt(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length < RecordOverhead)
            throw new CryptographicException("The AES-GCM record is truncated.");

        lock (_receiveLock)
        {
            ThrowIfDisposed();

            var sequence = ReadUInt64BigEndian(data, 0);
            if (sequence != _receiveSequence)
                throw new CryptographicException("The AES-GCM record sequence is invalid.");

            if (_receiveSequence == ulong.MaxValue)
                throw new CryptographicException("The AES-GCM receive sequence is exhausted.");

            var recordLength = checked((uint)data.Length);
            var cipherTextLength = data.Length - SequenceSize;
            var plainText = new byte[cipherTextLength - TagSize];
            var nonce = ComposeNonce(_receiveNoncePrefix, sequence);
            var aad = ComposeAad(recordLength, sequence);

            try
            {
                var cipher = new GcmBlockCipher(AesUtilities.CreateEngine());
                cipher.Init(false, new AeadParameters(new KeyParameter(_receiveKey), TagSize * 8, nonce, aad));

                var written = cipher.ProcessBytes(data, SequenceSize, cipherTextLength, plainText, 0);
                written += cipher.DoFinal(plainText, written);

                if (written != plainText.Length)
                    throw new CryptographicException("AES-GCM produced an invalid plaintext length.");

                _receiveSequence++;
                return plainText;
            }
            catch (CryptoException ex)
            {
                Clear(plainText);
                throw new CryptographicException("AES-GCM record authentication failed.", ex);
            }
            catch
            {
                Clear(plainText);
                throw;
            }
            finally
            {
                Clear(nonce);
                Clear(aad);
            }
        }
    }

    /// <summary>
    /// Initializes the cipher key. Session ciphers are deliberately immutable after
    /// construction because resetting a key would also reset the GCM nonce sequence.
    /// Create a new cipher with fresh peer nonces to use different key material.
    /// </summary>
    public byte[] SetKey(byte[] key)
    {
        ValidateSharedKey(key);

        lock (_sendLock)
        {
            lock (_receiveLock)
            {
                ThrowIfDisposed();

                if (_keyInitialized)
                    throw new InvalidOperationException(
                        "AES-GCM session keys are immutable; create a new cipher with fresh peer nonces.");

                byte[] sendKey = null;
                byte[] receiveKey = null;
                byte[] sendNoncePrefix = null;
                byte[] receiveNoncePrefix = null;

                try
                {
                    sendKey = Derive(key, _contextSalt, _sendKeyLabel, KeySize);
                    receiveKey = Derive(key, _contextSalt, _receiveKeyLabel, KeySize);
                    sendNoncePrefix = Derive(key, _contextSalt, _sendNonceLabel, NoncePrefixSize);
                    receiveNoncePrefix = Derive(key, _contextSalt, _receiveNonceLabel, NoncePrefixSize);

                    Clear(_sendKey);
                    Clear(_receiveKey);
                    Clear(_sendNoncePrefix);
                    Clear(_receiveNoncePrefix);

                    _sendKey = sendKey;
                    _receiveKey = receiveKey;
                    _sendNoncePrefix = sendNoncePrefix;
                    _receiveNoncePrefix = receiveNoncePrefix;
                    sendKey = null;
                    receiveKey = null;
                    sendNoncePrefix = null;
                    receiveNoncePrefix = null;
                    _sendSequence = 0;
                    _receiveSequence = 0;
                    _keyInitialized = true;

                    return (byte[])key.Clone();
                }
                finally
                {
                    Clear(sendKey);
                    Clear(receiveKey);
                    Clear(sendNoncePrefix);
                    Clear(receiveNoncePrefix);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_sendLock)
        {
            lock (_receiveLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                Clear(_sendKey);
                Clear(_receiveKey);
                Clear(_sendNoncePrefix);
                Clear(_receiveNoncePrefix);
                Clear(_contextSalt);
                _sendKey = null;
                _receiveKey = null;
                _sendNoncePrefix = null;
                _receiveNoncePrefix = null;
                _sendSequence = 0;
                _receiveSequence = 0;
            }
        }

        GC.SuppressFinalize(this);
    }

    static void ValidateContext(EncryptionContext context)
    {
        ValidateSharedKey(context.Key);
        ValidateNonce(context.InitiatorNonce, nameof(context.InitiatorNonce));
        ValidateNonce(context.ResponderNonce, nameof(context.ResponderNonce));

        if (context.Direction != AuthenticationDirection.Initiator &&
            context.Direction != AuthenticationDirection.Responder)
            throw new ArgumentOutOfRangeException(nameof(context.Direction));

        if (context.Mode == EncryptionMode.None)
            throw new ArgumentException("AES-GCM requires an encrypted session mode.", nameof(context));
        if (context.Mode != EncryptionMode.EncryptWithSessionKey &&
            context.Mode != EncryptionMode.EncryptWithSessionKeyAndAddress)
            throw new ArgumentOutOfRangeException(nameof(context.Mode));
        if (string.IsNullOrWhiteSpace(context.Protocol))
            throw new ArgumentException("A negotiated encryption protocol is required.", nameof(context));
        if (context.OfferedProtocols == null || context.OfferedProtocols.Length == 0)
            throw new ArgumentException("The initiator's encryption offer is required.", nameof(context));
        if (context.OfferedProtocols.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Encryption offers cannot contain empty protocol names.", nameof(context));
        if (!context.OfferedProtocols.Contains(context.Protocol, StringComparer.Ordinal))
            throw new ArgumentException("The selected encryption protocol was not offered.", nameof(context));
        if (context.AuthenticationMode == AuthenticationMode.None)
            throw new ArgumentException("AES-GCM requires an authenticated session.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.AuthenticationProtocol))
            throw new ArgumentException("A negotiated authentication protocol is required.", nameof(context));

        if (context.Mode == EncryptionMode.EncryptWithSessionKeyAndAddress)
        {
            ValidateAddress(context.InitiatorAddress, nameof(context.InitiatorAddress));
            ValidateAddress(context.ResponderAddress, nameof(context.ResponderAddress));

            if (IsUnspecifiedAddress(context.InitiatorAddress)
                || IsUnspecifiedAddress(context.ResponderAddress))
                throw new ArgumentException(
                    "Address-bound encryption requires concrete peer network addresses.",
                    nameof(context));
        }
    }

    static void ValidateSharedKey(byte[] key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        if (key.Length < MinimumSharedKeySize || key.Length > MaximumSharedKeySize)
            throw new ArgumentException(
                $"The shared key must contain between {MinimumSharedKeySize} and {MaximumSharedKeySize} bytes.",
                nameof(key));
    }

    static void ValidateNonce(byte[] nonce, string parameterName)
    {
        if (nonce == null)
            throw new ArgumentNullException(parameterName);
        if (nonce.Length < MinimumPeerNonceSize || nonce.Length > MaximumPeerNonceSize)
            throw new ArgumentException(
                $"A cipher nonce must contain between {MinimumPeerNonceSize} and {MaximumPeerNonceSize} bytes.",
                parameterName);
    }

    static void ValidateAddress(byte[] address, string parameterName)
    {
        if (address == null)
            throw new ArgumentNullException(parameterName);
        if (address.Length != 4 && address.Length != 16)
            throw new ArgumentException("An address must be an IPv4 or IPv6 byte sequence.", parameterName);
    }

    static byte[] ComposeContextSalt(EncryptionContext context)
    {
        var digest = new Sha256Digest();
        digest.BlockUpdate(ContextLabel, 0, ContextLabel.Length);
        digest.Update((byte)context.AuthenticationMode);
        DigestLengthPrefixed(digest, Encoding.UTF8.GetBytes(context.AuthenticationProtocol));
        DigestLengthPrefixed(digest, Encoding.UTF8.GetBytes(context.Domain ?? string.Empty));
        digest.Update((byte)context.Mode);
        DigestUInt32(digest, checked((uint)context.OfferedProtocols.Length));
        foreach (var offeredProtocol in context.OfferedProtocols)
            DigestLengthPrefixed(digest, Encoding.UTF8.GetBytes(offeredProtocol));
        DigestLengthPrefixed(digest, Encoding.UTF8.GetBytes(context.Protocol));
        DigestLengthPrefixed(digest, context.InitiatorNonce);
        DigestLengthPrefixed(digest, context.ResponderNonce);

        if (context.Mode == EncryptionMode.EncryptWithSessionKeyAndAddress)
        {
            DigestLengthPrefixed(digest, context.InitiatorAddress);
            DigestLengthPrefixed(digest, context.ResponderAddress);
        }

        var salt = new byte[digest.GetDigestSize()];
        digest.DoFinal(salt, 0);
        return salt;
    }

    static void DigestLengthPrefixed(Sha256Digest digest, byte[] value)
    {
        var length = new byte[4];
        WriteUInt32BigEndian(length, 0, checked((uint)value.Length));
        digest.BlockUpdate(length, 0, length.Length);
        digest.BlockUpdate(value, 0, value.Length);
        Clear(length);
    }

    static void DigestUInt32(Sha256Digest digest, uint value)
    {
        var encoded = new byte[4];
        WriteUInt32BigEndian(encoded, 0, value);
        digest.BlockUpdate(encoded, 0, encoded.Length);
        Clear(encoded);
    }

    static bool IsUnspecifiedAddress(byte[] address)
        => address.All(value => value == 0);

    static byte[] Derive(byte[] key, byte[] salt, byte[] label, int size)
    {
        var generator = new HkdfBytesGenerator(new Sha256Digest());
        generator.Init(new HkdfParameters(key, salt, label));
        var result = new byte[size];
        generator.GenerateBytes(result, 0, result.Length);
        return result;
    }

    static byte[] ComposeNonce(byte[] prefix, ulong sequence)
    {
        var nonce = new byte[NoncePrefixSize + SequenceSize];
        Buffer.BlockCopy(prefix, 0, nonce, 0, NoncePrefixSize);
        WriteUInt64BigEndian(nonce, NoncePrefixSize, sequence);
        return nonce;
    }

    static byte[] ComposeAad(uint recordLength, ulong sequence)
    {
        var aad = new byte[AadSize];
        WriteUInt32BigEndian(aad, 0, recordLength);
        WriteUInt64BigEndian(aad, 4, sequence);
        return aad;
    }

    static void WriteUInt32BigEndian(byte[] destination, int offset, uint value)
    {
        destination[offset] = (byte)(value >> 24);
        destination[offset + 1] = (byte)(value >> 16);
        destination[offset + 2] = (byte)(value >> 8);
        destination[offset + 3] = (byte)value;
    }

    static void WriteUInt64BigEndian(byte[] destination, int offset, ulong value)
    {
        destination[offset] = (byte)(value >> 56);
        destination[offset + 1] = (byte)(value >> 48);
        destination[offset + 2] = (byte)(value >> 40);
        destination[offset + 3] = (byte)(value >> 32);
        destination[offset + 4] = (byte)(value >> 24);
        destination[offset + 5] = (byte)(value >> 16);
        destination[offset + 6] = (byte)(value >> 8);
        destination[offset + 7] = (byte)value;
    }

    static ulong ReadUInt64BigEndian(byte[] source, int offset)
        => ((ulong)source[offset] << 56)
         | ((ulong)source[offset + 1] << 48)
         | ((ulong)source[offset + 2] << 40)
         | ((ulong)source[offset + 3] << 32)
         | ((ulong)source[offset + 4] << 24)
         | ((ulong)source[offset + 5] << 16)
         | ((ulong)source[offset + 6] << 8)
         | source[offset + 7];

    static void Clear(byte[] value)
    {
        if (value != null)
            Array.Clear(value, 0, value.Length);
    }

    void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AesGcmSymetricCipher));
    }
}
