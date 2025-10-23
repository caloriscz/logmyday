using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace LogMyDay.Api.Authentication;

public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthAttemptTracker _attemptTracker;

    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserService userService,
        IPasswordHasher passwordHasher,
        AuthAttemptTracker attemptTracker)
        : base(options, logger, encoder)
    {
        _userService = userService;
        _passwordHasher = passwordHasher;
        _attemptTracker = attemptTracker;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Logger.LogInformation("[BasicAuth] Handling request for {Path}", Request.Path);

        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var headerValues)
            || StringValues.IsNullOrEmpty(headerValues))
        {
            Logger.LogDebug(
                "[BasicAuth] Authorization header missing, deferring to other authentication schemes for {Path}",
                Request.Path);

            return AuthenticateResult.NoResult();
        }

        try
        {
            var authHeader = headerValues.ToString();
            Logger.LogInformation(
                "[BasicAuth] Authorization header received: '{AuthHeader}'",
                authHeader);

            if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                return AuthenticateResult.Fail("Invalid Authorization Header");

            var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
            Logger.LogInformation(
                "[BasicAuth] Encoded credentials: '{EncodedCredentials}'",
                encodedCredentials);
            Logger.LogInformation(
                "[BasicAuth] Encoded credentials length: {Length}",
                encodedCredentials.Length);

            var decodedBytes = Convert.FromBase64String(encodedCredentials);
            var decodedString = Encoding.UTF8.GetString(decodedBytes);
            Logger.LogInformation(
                "[BasicAuth] Decoded credentials string: '{DecodedString}'",
                decodedString);
            Logger.LogInformation(
                "[BasicAuth] Decoded string length: {Length}",
                decodedString.Length);

            var credentials = decodedString.Split(':');
            Logger.LogInformation(
                "[BasicAuth] Split credentials count: {Count}",
                credentials.Length);

            if (credentials.Length != 2)
                return AuthenticateResult.Fail("Invalid Basic Authentication format");

            var email = credentials[0];
            var password = credentials[1];
            Logger.LogInformation(
                "[BasicAuth] Parsed email: '{Email}' (length: {Length})",
                email,
                email.Length);
            Logger.LogInformation(
                "[BasicAuth] Parsed password: '{Password}' (length: {Length})",
                password,
                password.Length);

            var clientIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var identifier = $"{clientIp}:{email}";

            // Check if this IP/email combination is currently blocked
            if (_attemptTracker.IsBlocked(identifier))
            {
                Logger.LogWarning(
                    "[BasicAuth] Authentication blocked for {Identifier} due to too many failed attempts",
                    identifier);
                return AuthenticateResult.Fail("Too many failed attempts. Please try again later.");
            }

            // Validate credentials against database
            var user = await _userService.FindByEmail(email, CancellationToken.None);
            if (user == null || !_passwordHasher.Verify(password, user.PasswordHash))
            {
                Logger.LogWarning(
                    "[BasicAuth] Invalid credentials for user: {Email} from IP: {ClientIp}",
                    email,
                    clientIp);
                _attemptTracker.RecordFailedAttempt(identifier);
                return AuthenticateResult.Fail("Invalid Email or Password");
            }

            // Successful authentication - clear any failed attempts
            _attemptTracker.RecordSuccessfulAttempt(identifier);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            Logger.LogInformation(
                "[BasicAuth] User '{Email}' (ID: {UserId}) from IP '{ClientIp}' authenticated successfully",
                user.Email,
                user.Id,
                clientIp);
            
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[BasicAuth] Exception during authentication");
            
            return AuthenticateResult.Fail("Invalid Authorization Header");
        }
    }
}
