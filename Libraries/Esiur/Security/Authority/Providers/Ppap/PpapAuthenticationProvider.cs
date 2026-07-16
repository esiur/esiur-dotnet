using Esiur.Core;
using System;

namespace Esiur.Security.Authority.Providers.Ppap;

public delegate PpapLocalIdentity PpapLocalIdentityResolver(
    AuthenticationContext context);

/// <summary>
/// Fixed-suite PPAP ML-KEM-768 authentication provider.
/// </summary>
public sealed class PpapAuthenticationProvider : IAuthenticationProvider
{
    public string DefaultName => PpapProtocol.Name;
    public PpapLocalIdentity LocalIdentity { get; }
    public IPpapRegistrationStore Registrations { get; }

    /// <summary>
    /// Admission control for password-based Argon2id work performed during live
    /// authentication and protected rotation exchanges.
    /// </summary>
    public PpapPasswordDerivationLimiter PasswordDerivationLimiter { get; }
    readonly PpapLocalIdentityResolver _localIdentityResolver;

    public PpapAuthenticationProvider(PpapLocalIdentity localIdentity = null,
        IPpapRegistrationStore registrations = null,
        PpapPasswordDerivationLimiter passwordDerivationLimiter = null)
    {
        LocalIdentity = localIdentity;
        Registrations = registrations ?? new InMemoryPpapRegistrationStore();
        PasswordDerivationLimiter = passwordDerivationLimiter
            ?? PpapPasswordDerivationLimiter.Shared;
    }

    /// <summary>
    /// Advanced constructor for hosts serving multiple domains or local identities.
    /// The selected identity is resolved and snapshotted when a handler is created.
    /// </summary>
    public PpapAuthenticationProvider(IPpapRegistrationStore registrations,
        PpapLocalIdentityResolver localIdentityResolver,
        PpapPasswordDerivationLimiter passwordDerivationLimiter = null)
    {
        Registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
        _localIdentityResolver = localIdentityResolver
            ?? throw new ArgumentNullException(nameof(localIdentityResolver));
        PasswordDerivationLimiter = passwordDerivationLimiter
            ?? PpapPasswordDerivationLimiter.Shared;
    }

    internal PpapLocalIdentity ResolveLocalIdentity(AuthenticationContext context)
        => _localIdentityResolver == null
            ? LocalIdentity
            : _localIdentityResolver(context);

    internal byte[] DerivePrivateKey(PpapLocalIdentity identity, string domain,
        byte[] nonce, PpapKdfProfile profile,
        bool postAuthentication = false)
    {
        if (identity == null)
            throw new ArgumentNullException(nameof(identity));

        if (identity.Kind != PpapIdentityKind.PasswordDerived)
            return identity.DerivePrivateKey(domain, nonce, profile);

        using (var lease = PasswordDerivationLimiter.TryAcquire(postAuthentication))
        {
            if (lease == null)
                throw new InvalidOperationException(
                    "PPAP password-derivation capacity is temporarily exhausted.");

            return identity.DerivePrivateKey(domain, nonce, profile);
        }
    }

    public IAuthenticationHandler CreateAuthenticationHandler(AuthenticationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        return new PpapAuthenticationHandler(this, context);
    }

    public AsyncReply<bool> Login(Session session) => new AsyncReply<bool>(true);

    public AsyncReply<bool> Logout(Session session) => new AsyncReply<bool>(true);
}
