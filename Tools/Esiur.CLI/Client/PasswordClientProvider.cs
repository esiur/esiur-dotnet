using Esiur.Core;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;

namespace Esiur.CLI.Client;

internal sealed class PasswordClientProvider(string identity, byte[] password) : PasswordAuthenticationProvider
{
    public override byte[] GetSelfCredential(string requestedIdentity, string domain, string hostname) =>
        string.Equals(requestedIdentity, identity, StringComparison.Ordinal) ? password : null!;

    public override IdentityPassword GetSelfIdentityAndCredential(string domain, string hostname) =>
        new() { Identity = identity, Password = password };

    public override AsyncReply<bool> Login(Session session) => new(true);
    public override AsyncReply<bool> Logout(Session session) => new(true);
}
