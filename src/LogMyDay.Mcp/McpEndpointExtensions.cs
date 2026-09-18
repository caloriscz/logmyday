using LogMyDay.Api.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace LogMyDay.Mcp;

public static class McpEndpointExtensions
{
    /// <summary>Name of the host's per-key rate-limit policy; defined in the App's rate-limiting setup.</summary>
    public const string RateLimitPolicy = "mcp";

    /// <summary>
    /// Mounts the MCP endpoint. McpRead requires an API-key principal, so a browser session cookie is
    /// refused here — the endpoint must not be a CSRF surface. Per-tool policies tighten further.
    /// </summary>
    public static IEndpointConventionBuilder MapLogMyDayMcp(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapMcp(McpSchemaVersion.EndpointPath)
            .RequireAuthorization(McpPolicies.Read)
            .RequireRateLimiting(RateLimitPolicy);
    }
}
