using Esiur.Security.Permissions;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Esiur.Security.RateLimiting;

/// <summary>
/// Per-connection, per-member token-bucket policy with bounded delayed reservations.
/// </summary>
public sealed class BurstRatePolicy : RatePolicy
{
    sealed class Bucket
    {
        public readonly object Sync = new object();
        public double Tokens;
        public long LastTimestamp;
        public int Queued;

        public Bucket(double tokens, long timestamp)
        {
            Tokens = tokens;
            LastTimestamp = timestamp;
        }
    }

    sealed class ConnectionBuckets
    {
        public readonly ConcurrentDictionary<string, Bucket> Values
            = new ConcurrentDictionary<string, Bucket>(StringComparer.Ordinal);
    }

    readonly ConditionalWeakTable<object, ConnectionBuckets> _connections
        = new ConditionalWeakTable<object, ConnectionBuckets>();

    /// <summary>
    /// Number of permits replenished during each period.
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Replenishment period for <see cref="PermitLimit"/>.
    /// </summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Additional permits available for an immediate burst.
    /// </summary>
    public int BurstLimit { get; set; }

    /// <summary>
    /// Maximum number of delayed reservations per connection and member.
    /// Further requests are denied until queue positions become available.
    /// </summary>
    public int QueueLimit { get; set; }

    public BurstRatePolicy()
    {
    }

    public BurstRatePolicy(string name) : base(name)
    {
    }

    public override Ruling Applicable(RateControlContext context)
    {
        Validate();

        var capacity = checked(PermitLimit + BurstLimit);
        var now = Stopwatch.GetTimestamp();
        var key = $"{(int)context.Action}:{context.Member.Fullname}";
        var buckets = _connections.GetValue(context.Connection, _ => new ConnectionBuckets());
        var bucket = buckets.Values.GetOrAdd(key, _ => new Bucket(capacity, now));

        TimeSpan delay;

        lock (bucket.Sync)
        {
            Replenish(bucket, now, capacity);

            if (bucket.Tokens >= 1d)
            {
                bucket.Tokens -= 1d;
                return Ruling.Allowed;
            }

            if (QueueLimit == 0 || bucket.Queued >= QueueLimit)
                return Ruling.Denied;

            bucket.Tokens -= 1d;
            bucket.Queued++;

            var seconds = -bucket.Tokens * Period.TotalSeconds / PermitLimit;
            delay = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            context.Delay = delay;
        }

        _ = ReleaseQueuePositionAsync(bucket, delay);
        return Ruling.Allowed;
    }

    void Validate()
    {
        if (PermitLimit <= 0)
            throw new InvalidOperationException("PermitLimit must be greater than zero.");
        if (Period <= TimeSpan.Zero)
            throw new InvalidOperationException("Period must be greater than zero.");
        if (BurstLimit < 0)
            throw new InvalidOperationException("BurstLimit cannot be negative.");
        if (QueueLimit < 0)
            throw new InvalidOperationException("QueueLimit cannot be negative.");
    }

    void Replenish(Bucket bucket, long now, int capacity)
    {
        var elapsedTicks = now - bucket.LastTimestamp;
        if (elapsedTicks <= 0)
            return;

        var elapsedSeconds = (double)elapsedTicks / Stopwatch.Frequency;
        var replenished = elapsedSeconds * PermitLimit / Period.TotalSeconds;
        bucket.Tokens = Math.Min(capacity, bucket.Tokens + replenished);
        bucket.LastTimestamp = now;
    }

    static async Task ReleaseQueuePositionAsync(Bucket bucket, TimeSpan delay)
    {
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay).ConfigureAwait(false);

        lock (bucket.Sync)
        {
            if (bucket.Queued > 0)
                bucket.Queued--;
        }
    }
}
