namespace Esiur.Security.Authority;

/// <summary>
/// Optional authentication-handler capability for rotating authentication keys after
/// the initial session key has enabled authenticated encrypted transport protection.
/// Transport implementations MUST NOT call either exchange method or transmit their
/// data before that protection is active; rotation payloads can contain fresh verifier
/// material that must never appear on a plaintext channel.
/// </summary>
public interface IAuthenticationKeyRotationHandler
{
    bool RequiresKeyRotation { get; }

    /// <summary>
    /// Starts key rotation on the connection initiator. This is called only after the
    /// encrypted Established event has been received.
    /// </summary>
    AuthenticationKeyRotationResult BeginKeyRotation();

    /// <summary>
    /// Processes one encrypted key-rotation message from the peer.
    /// </summary>
    AuthenticationKeyRotationResult ProcessKeyRotation(object data);
}
