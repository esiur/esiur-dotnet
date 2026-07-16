namespace Esiur.Security.Authority;

/// <summary>
/// Result returned by a post-encryption authentication key-rotation step.
/// </summary>
public sealed class AuthenticationKeyRotationResult
{
    public AuthenticationKeyRotationRuling Ruling { get; }
    public object Data { get; }
    public string Error { get; }

    public AuthenticationKeyRotationResult(
        AuthenticationKeyRotationRuling ruling,
        object data = null,
        string error = null)
    {
        Ruling = ruling;
        Data = data;
        Error = error;
    }
}
