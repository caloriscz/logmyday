using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// A second, tighter budget for tools that destroy data, on top of the endpoint's per-key limit.
/// An agent that has gone wrong in a loop can delete at most this many things a minute.
/// </summary>
public sealed class DestructiveBudget
{
    public const int PermitsPerMinute = 10;

    private readonly ConcurrentDictionary<int, SlidingWindowRateLimiter> _limiters = new();

    public bool TryAcquire(int apiKeyId)
    {
        var limiter = _limiters.GetOrAdd(apiKeyId, _ => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = PermitsPerMinute,
            SegmentsPerWindow = 6,
            QueueLimit = 0
        }));

        using var lease = limiter.AttemptAcquire();

        return lease.IsAcquired;
    }
}
