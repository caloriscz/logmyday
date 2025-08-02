using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace LogMyDay.Api.Authentication;

public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _config;
    private readonly AuthAttemptTracker _attemptTracker;

    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration config,
        AuthAttemptTracker attemptTracker)
        : base(options, logger, encoder, clock)
    {
        _config = config;
        _attemptTracker = attemptTracker;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Logger.LogInformation("[BasicAuth] Handling request for {Path}", Request.Path);

        if (!Request.Headers.ContainsKey("Authorization"))
        {
            Logger.LogWarning("[BasicAuth] Missing Authorization header");
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization Header"));
        }

        try
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));

            var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
            var decodedBytes = Convert.FromBase64String(encodedCredentials);
            var credentials = Encoding.UTF8.GetString(decodedBytes).Split(':');

            if (credentials.Length != 2)
                return Task.FromResult(AuthenticateResult.Fail("Invalid Basic Authentication format"));

            var username = credentials[0];
            var password = credentials[1];
            var clientIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var identifier = $"{clientIp}:{username}";

            // Check if this IP/username combination is currently blocked
            if (_attemptTracker.IsBlocked(identifier))
            {
                Logger.LogWarning("[BasicAuth] Authentication blocked for {Identifier} due to too many failed attempts", identifier);
                return Task.FromResult(AuthenticateResult.Fail("Too many failed attempts. Please try again later."));
            }

            var expectedUsername = _config["Auth:Basic:Username"];
            var expectedPassword = _config["Auth:Basic:Password"];
            var userId = _config["Auth:Basic:UserId"] ?? Guid.Empty.ToString();

            if (username != expectedUsername || password != expectedPassword)
            {
                Logger.LogWarning("[BasicAuth] Invalid credentials for user: {User} from IP: {ClientIp}", username, clientIp);
                _attemptTracker.RecordFailedAttempt(identifier);
                return Task.FromResult(AuthenticateResult.Fail("Invalid Username or Password"));
            }

            // Successful authentication - clear any failed attempts
            _attemptTracker.RecordSuccessfulAttempt(identifier);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, username),
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            Logger.LogInformation("[BasicAuth] User '{User}' from IP '{ClientIp}' authenticated successfully", username, clientIp);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[BasicAuth] Exception during authentication");
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));
        }
    }
}