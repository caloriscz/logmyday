using System.Threading.RateLimiting;
using LogMyDay.Api.Authentication;
using Microsoft.AspNetCore.RateLimiting;

namespace LogMyDay.App.Extensions;

internal static class RateLimitingExtensions
{
    internal const string McpPolicy = "mcp";
    internal const int McpPermitsPerMinute = 120;

    internal static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddSlidingWindowLimiter("api", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 100;
                opt.SegmentsPerWindow = 6;
            });

            options.AddSlidingWindowLimiter("auth", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(15);
                opt.PermitLimit = 10;
                opt.SegmentsPerWindow = 3;
            });

            options.AddSlidingWindowLimiter("ai", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 20;
                opt.SegmentsPerWindow = 4;
            });

            // The MCP endpoint gets its own budget per API key, so one busy agent neither starves
            // the web and mobile clients nor is starved by them. Requires the limiter to run after
            // authentication, or the claim is never there to partition on.
            options.AddPolicy(McpPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(McpPartitionKey(context), _ => new SlidingWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = McpPermitsPerMinute,
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));

            options.RejectionStatusCode = 429;
        });

        return services;
    }

    /// <summary>
    /// One partition per API key; unauthenticated callers (about to be refused anyway) share a
    /// per-address partition so they cannot exhaust a real key's budget.
    /// </summary>
    internal static string McpPartitionKey(HttpContext context)
    {
        var keyId = context.User.FindFirst(ApiKeyAuthDefaults.KeyIdClaim)?.Value;

        return keyId is { Length: > 0 }
            ? "key:" + keyId
            : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
