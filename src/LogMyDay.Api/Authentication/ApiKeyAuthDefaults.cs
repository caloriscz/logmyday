namespace LogMyDay.Api.Authentication;

/// <summary>
/// Names shared by <see cref="ApiKeyAuthHandler"/>, the authorization policies and any consumer
/// that reads the resulting principal. Defined once so the pieces cannot drift apart.
/// </summary>
public static class ApiKeyAuthDefaults
{
    public const string SchemeName = "api-key";

    /// <summary>The header value prefix that selects this scheme: "Bearer lmd_…".</summary>
    public const string BearerTokenPrefix = "Bearer lmd_";

    /// <summary>Claim naming the mechanism that authenticated the request; keys always carry <see cref="AuthSourceValue"/>.</summary>
    public const string AuthSourceClaim = "lmd_auth";
    public const string AuthSourceValue = "api-key";

    public const string ScopeClaim = "lmd_scope";
    public const string ScopeRead = "read";
    public const string ScopeWrite = "write";

    public const string KeyIdClaim = "lmd_api_key_id";
    public const string KeyPrefixClaim = "lmd_api_key_prefix";
}

/// <summary>
/// Authorization policies for the MCP endpoint. All three require the request to have been
/// authenticated by an API key — a browser session cookie must never reach /mcp, because a
/// JSON-RPC POST from a hostile page would ride it and antiforgery does not cover that path.
/// </summary>
public static class McpPolicies
{
    public const string Read = "McpRead";
    public const string Write = "McpWrite";
    public const string Admin = "McpAdmin";
}
