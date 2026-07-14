namespace Esiur.Security.Cryptography;

/// <summary>
/// Creates a per-session symmetric cipher from authenticated session material.
/// Providers are registered and negotiated by <see cref="DefaultName"/>.
/// </summary>
public interface IEncryptionProvider
{
    /// <summary>
    /// Gets the stable protocol name advertised during connection establishment.
    /// </summary>
    string DefaultName { get; }

    /// <summary>
    /// Maximum number of bytes added by <see cref="ISymetricCipher.Encrypt(byte[])"/>
    /// to one plaintext record. The transport uses this before encryption so a rejected
    /// oversized send cannot consume a cipher sequence number.
    /// </summary>
    uint MaximumRecordOverhead { get; }

    /// <summary>
    /// Creates an independent authenticated record cipher for one session.
    /// Implementations must derive independent directional keys/nonces while binding
    /// every negotiation field in <paramref name="context"/>, authenticate each record,
    /// reject replay/reordering, and fail closed on authentication errors. A cipher and
    /// its nonce sequence must never be reused for another connection.
    /// </summary>
    ISymetricCipher CreateCipher(EncryptionContext context);
}
