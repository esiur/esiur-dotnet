namespace Esiur.Security.Authority;

/// <summary>
/// Describes the current state of a post-encryption authentication key rotation.
/// </summary>
public enum AuthenticationKeyRotationRuling
{
    Failed,
    InProgress,
    Succeeded,
}
