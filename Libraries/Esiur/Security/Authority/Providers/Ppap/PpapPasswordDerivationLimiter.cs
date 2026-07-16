using System;
using System.Threading;

namespace Esiur.Security.Authority.Providers.Ppap;

/// <summary>
/// Bounds concurrent Argon2id work performed by PPAP authentication handlers.
/// Saturated limiters reject new derivations instead of queuing socket threads.
/// </summary>
/// <remarks>
/// This bounds authentication-handler concurrency, not registration provisioning.
/// Operators must also choose KDF memory profiles appropriate for the host because
/// slots are not weighted by <see cref="PpapKdfProfile.MemoryKiB"/>.
/// </remarks>
public sealed class PpapPasswordDerivationLimiter
{
    sealed class Lease : IDisposable
    {
        PpapPasswordDerivationLimiter _owner;

        public Lease(PpapPasswordDerivationLimiter owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release();
        }
    }

    readonly object _sync = new object();
    int _active;

    /// <summary>
    /// Process-wide default shared by PPAP providers. Its upper bound limits the
    /// default Argon2id working set to four simultaneous 32-MiB derivations.
    /// </summary>
    public static PpapPasswordDerivationLimiter Shared { get; } =
        new PpapPasswordDerivationLimiter(
            Math.Max(1, Math.Min(Environment.ProcessorCount, 4)),
            Environment.ProcessorCount > 1 ? 1 : 0);

    public int MaximumConcurrency { get; }

    /// <summary>
    /// Slots unavailable to unauthenticated handshakes but available to the encrypted
    /// post-authentication rotation phase, preventing pre-authentication starvation.
    /// </summary>
    public int ReservedPostAuthenticationSlots { get; }

    public PpapPasswordDerivationLimiter(
        int maximumConcurrency,
        int reservedPostAuthenticationSlots = 0)
    {
        if (maximumConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
        if (reservedPostAuthenticationSlots < 0
            || reservedPostAuthenticationSlots >= maximumConcurrency)
            throw new ArgumentOutOfRangeException(
                nameof(reservedPostAuthenticationSlots));

        MaximumConcurrency = maximumConcurrency;
        ReservedPostAuthenticationSlots = reservedPostAuthenticationSlots;
    }

    internal IDisposable TryAcquire(bool postAuthentication)
    {
        lock (_sync)
        {
            var limit = postAuthentication
                ? MaximumConcurrency
                : MaximumConcurrency - ReservedPostAuthenticationSlots;
            if (_active >= limit)
                return null;

            _active++;
            return new Lease(this);
        }
    }

    void Release()
    {
        lock (_sync)
        {
            if (_active < 1)
                throw new InvalidOperationException("PPAP derivation limiter underflow.");
            _active--;
        }
    }
}
