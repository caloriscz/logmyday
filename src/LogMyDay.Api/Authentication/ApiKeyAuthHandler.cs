using System.Security.Claims;
using System.Text.Encodings.Web;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace LogMyDay.Api.Authentication;

/// <summary>
/// Authenticates "Authorization: Bearer lmd_…" with a per-user API key. Emits the same claim set
/// as the cookie and Basic schemes so every existing policy behaves identically, plus the
/// key-specific claims the MCP policies check. Every refusal is the same message, so a caller
/// learns nothing about which keys exist.
/// </summary>
public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string FailureMessage = "Invalid API key";

    private readonly IApiKeyService _apiKeys;
    private readonly AuthAttemptTracker _attemptTracker;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeys,
        AuthAttemptTracker attemptTracker)
        : base(options, logger, encoder)
    {
        _apiKeys = apiKeys;
        _attemptTracker = attemptTracker;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var headerValues)
            || StringValues.IsNullOrEmpty(headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var header = headerValues.ToString();

        // Only our own tokens. Any other bearer value belongs to some other scheme, or to nobody.
        if (!header.StartsWith(ApiKeyAuthDefaults.BearerTokenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header["Bearer ".Length..].Trim();
        var clientIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Lockout is per IP and visible prefix: a brute force against one key locks that prefix
        // from that address without touching the user's other keys or their password login.
        var prefix = token.Length >= ApiKeyService.PrefixLength ? token[..ApiKeyService.PrefixLength] : "apikey";
        var identifier = $"{clientIp}:{prefix}";

        if (_attemptTracker.IsBlocked(identifier))
        {
            Logger.LogWarning("[ApiKeyAuth] Authentication blocked for {Identifier} due to too many failed attempts", identifier);

            return AuthenticateResult.Fail(FailureMessage);
        }

        Context.Items[BasicAuthHandler.SchemeItemKey] = ApiKeyAuthDefaults.SchemeName;

        var validation = await _apiKeys.Validate(token, Context.RequestAborted);

        if (validation == null)
        {
            Logger.LogWarning("[ApiKeyAuth] Invalid API key {Prefix} from IP {ClientIp}", prefix, clientIp);
            _attemptTracker.RecordFailedAttempt(identifier);

            return AuthenticateResult.Fail(FailureMessage);
        }

        _attemptTracker.RecordSuccessfulAttempt(identifier);

        var user = validation.User;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
            new Claim("is_admin", user.IsAdmin.ToString().ToLowerInvariant()),
            new Claim(ApiKeyAuthDefaults.AuthSourceClaim, ApiKeyAuthDefaults.AuthSourceValue),
            new Claim(ApiKeyAuthDefaults.ScopeClaim, validation.Scope == ApiKeyScope.ReadWrite ? ApiKeyAuthDefaults.ScopeWrite : ApiKeyAuthDefaults.ScopeRead),
            new Claim(ApiKeyAuthDefaults.KeyIdClaim, validation.KeyId.ToString()),
            new Claim(ApiKeyAuthDefaults.KeyPrefixClaim, validation.Prefix),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        Logger.LogInformation(
            "[ApiKeyAuth] User '{Email}' (ID: {UserId}) authenticated with key {Prefix} ({Scope}) from IP '{ClientIp}'",
            user.Email, user.Id, validation.Prefix, validation.Scope, clientIp);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer realm=\"logmyday\"";

        return base.HandleChallengeAsync(properties);
    }
}
