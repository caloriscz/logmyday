using System.Security.Claims;
using LogMyDay.Api.Authentication;
using LogMyDay.Domain.Enums;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;

namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// Who the current tool call acts as, read once from the authenticated principal. Tools take this
/// instead of touching claims, so the claim names live in exactly one place.
/// </summary>
public sealed class McpUserContext
{
    public McpUserContext(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User;

        // The endpoint requires the McpRead policy, so an unauthenticated principal here means a
        // wiring mistake, not a caller mistake — fail loudly rather than act as nobody.
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new McpException("Unauthenticated");
        }

        UserId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        Email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        IsAdmin = string.Equals(user.FindFirstValue("is_admin"), "true", StringComparison.OrdinalIgnoreCase);
        Scope = user.FindFirstValue(ApiKeyAuthDefaults.ScopeClaim) == ApiKeyAuthDefaults.ScopeWrite
            ? ApiKeyScope.ReadWrite
            : ApiKeyScope.ReadOnly;
        ApiKeyId = int.TryParse(user.FindFirstValue(ApiKeyAuthDefaults.KeyIdClaim), out var id) ? id : 0;
        KeyPrefix = user.FindFirstValue(ApiKeyAuthDefaults.KeyPrefixClaim) ?? string.Empty;
    }

    public Guid UserId { get; }
    public string Email { get; }
    public bool IsAdmin { get; }
    public ApiKeyScope Scope { get; }
    public int ApiKeyId { get; }
    public string KeyPrefix { get; }

    public bool CanWrite => Scope == ApiKeyScope.ReadWrite;
}
