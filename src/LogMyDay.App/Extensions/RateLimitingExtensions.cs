using System.Security.Claims;
using System.Threading.RateLimiting;
using LogMyDay.Api.Authentication;
using Microsoft.AspNetCore.RateLimiting;

namespace LogMyDay.App.Extensions;

internal static class RateLimitingExtensions
{
    internal const string ApiPolicy = "api";
    internal const string AuthPolicy = "auth";
    internal const string AiPolicy = "ai";
    internal const string McpPolicy = "mcp";
    internal const int ApiPermitsPerMinute = 100;
    internal const int AuthPermitsPerWindow = 10;
    internal const int AiPermitsPerMinute = 20;
    internal const int McpPermitsPerMinute = 120;

    internal static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Each caller gets their own budget: the authenticated user per client address for the
            // API and AI policies, the client address for the sign-in endpoints (nobody is signed
            // in yet).
            // AddSlidingWindowLimiter(name, …) would key on the policy name — one bucket shared by
            // every web tab, phone, script and agent, so a single busy client 429s everyone.
            options.AddPolicy(ApiPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(UserOrAddressPartitionKey(context), _ => new SlidingWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = ApiPermitsPerMinute,
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));

            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(AddressPartitionKey(context), _ => new SlidingWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(15),
                    PermitLimit = AuthPermitsPerWindow,
                    SegmentsPerWindow = 3,
                    QueueLimit = 0
                }));

            options.AddPolicy(AiPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(UserOrAddressPartitionKey(context), _ => new SlidingWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = AiPermitsPerMinute,
                    SegmentsPerWindow = 4,
                    QueueLimit = 0
                }));

            // The MCP endpoint gets its own budget per API key, so one busy agent neither starves
            // the web and mobile clients nor is starved by them. All of these require the limiter
            // to run after authentication, or the claims are never there to partition on.
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

    /// <summary>
    /// The signed-in user's id combined with the client address, or the address alone for
    /// anonymous requests. The address matters even for a known user: the web app's own calls
    /// all arrive from the loopback address, so a phone on the same account gets a budget of its
    /// own instead of sharing one with every open dashboard tab.
    /// </summary>
    internal static string UserOrAddressPartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var address = AddressPartitionKey(context);

        return userId is { Length: > 0 } ? "user:" + userId + "|" + address : address;
    }

    internal static string AddressPartitionKey(HttpContext context)
    {
        return "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
