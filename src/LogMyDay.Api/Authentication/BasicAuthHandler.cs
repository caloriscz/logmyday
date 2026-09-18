using System.Diagnostics;
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
    // Diagnostic timings stashed for the request-logging middleware. Basic auth re-derives the
    // password on every request (unlike the cookie scheme, which verifies once at sign-in), so the
    // split between the user lookup and the Argon2 verify is what explains mobile latency.
    public const string SchemeItemKey = "lmd.auth.scheme";
    public const string LookupMsItemKey = "lmd.auth.lookupMs";
    public const string VerifyMsItemKey = "lmd.auth.verifyMs";
    public const string ParamsItemKey = "lmd.auth.params";
    public const string CacheHitItemKey = "lmd.auth.cacheHit";

    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthAttemptTracker _attemptTracker;
    private readonly PasswordVerificationCache _verificationCache;

    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserService userService,
        IPasswordHasher passwordHasher,
        AuthAttemptTracker attemptTracker,
        PasswordVerificationCache verificationCache)
        : base(options, logger, encoder)
    {
        _userService = userService;
        _passwordHasher = passwordHasher;
        _attemptTracker = attemptTracker;
        _verificationCache = verificationCache;
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
            Logger.LogDebug("[BasicAuth] Processing Basic authentication header");

            if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                return AuthenticateResult.Fail("Invalid Authorization Header");

            var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
            var decodedBytes = Convert.FromBase64String(encodedCredentials);
            var decodedString = Encoding.UTF8.GetString(decodedBytes);

            var credentials = decodedString.Split(':');

            if (credentials.Length != 2)
                return AuthenticateResult.Fail("Invalid Basic Authentication format");

            var email = credentials[0];
            var password = credentials[1];

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

            Context.Items[SchemeItemKey] = "basic";

            // Validate credentials against database
            var lookupStart = Stopwatch.GetTimestamp();
            var user = await _userService.FindByEmail(email, CancellationToken.None);
            Context.Items[LookupMsItemKey] = Stopwatch.GetElapsedTime(lookupStart).TotalMilliseconds;

            var verified = false;
            if (user != null)
            {
                // Hash format: argon2id$v=19$m=<kib>,t=<iters>,p=<lanes>$<salt>$<hash>. The lane
                // count is whatever the machine that hashed the password had, so record it —
                // a p far above the server's core count makes every verify slower still.
                var hashParts = user.PasswordHash.Split('$');
                Context.Items[ParamsItemKey] = hashParts.Length == 5
                    ? $"{hashParts[2]},cores={Environment.ProcessorCount}"
                    : $"unparsed,cores={Environment.ProcessorCount}";

                // Basic re-sends credentials on every request, so without a cache every request
                // pays a full Argon2id derivation. Only successful verifications are ever cached,
                // so a wrong password still costs an attacker the full derivation each time.
                if (_verificationCache.IsVerified(user.Id, user.PasswordHash, password))
                {
                    verified = true;
                    Context.Items[CacheHitItemKey] = true;
                    Context.Items[VerifyMsItemKey] = 0d;
                }
                else
                {
                    var verifyStart = Stopwatch.GetTimestamp();
                    verified = _passwordHasher.Verify(password, user.PasswordHash);
                    Context.Items[VerifyMsItemKey] = Stopwatch.GetElapsedTime(verifyStart).TotalMilliseconds;
                    Context.Items[CacheHitItemKey] = false;

                    if (verified)
                    {
                        // Keyed on the stored hash, so a password change invalidates this entry
                        // on the next request without any explicit eviction.
                        _verificationCache.Record(user.Id, user.PasswordHash, password);
                    }
                }
            }

            // `user is null` is redundant with `verified` but keeps null-state analysis happy
            // for the claim block below.
            if (user is null || !verified)
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

            // Mirror the cookie AuthService claim set so the AdminOnly policy (and any other
            // claim-based check) behaves identically across both authentication schemes.
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
                new Claim("is_admin", user.IsAdmin.ToString().ToLowerInvariant()),
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
