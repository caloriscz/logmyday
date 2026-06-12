using LogMyDay.Api.Application.Helpers;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Security;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Preferences;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAntiforgery _antiforgery;
    private readonly AuthAttemptTracker _attemptTracker;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        IAuthService authService,
        IPasswordHasher passwordHasher,
        IAntiforgery antiforgery,
        AuthAttemptTracker attemptTracker,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _authService = authService;
        _passwordHasher = passwordHasher;
        _antiforgery = antiforgery;
        _attemptTracker = attemptTracker;
        _logger = logger;
    }

    // Same identifier shape as BasicAuthHandler so lockout state is shared across both auth schemes.
    private string BuildAttemptIdentifier(string email)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return $"{clientIp}:{email}";
    }

    [HttpPost("register-first")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterFirstAdmin([FromBody] RegisterFirstDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !EmailValidator.IsValidEmail(request.Email))
            {
                return BadRequest("Valid email is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10)
            {
                return BadRequest("Password must be at least 10 characters long.");
            }

            var user = await _userService.CreateFirstAdmin(request.Email, request.Password, request.DisplayName, cancellationToken);
            
            return CreatedAtAction(nameof(GetCurrentUser), new { }, new { message = "First admin user created successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login attempt for email: {Email}", request.Email);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Login attempt with invalid model state for email: {Email}", request.Email);

            return BadRequest(ModelState);
        }

        var identifier = BuildAttemptIdentifier(request.Email);
        if (_attemptTracker.IsBlocked(identifier))
        {
            _logger.LogWarning("Login blocked for email {Email} due to too many failed attempts", request.Email);

            return StatusCode(429, "Too many failed attempts. Please try again later.");
        }

        var user = await _userService.FindByEmail(request.Email, cancellationToken);
        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login attempt with invalid credentials for email: {Email}", request.Email);
            _attemptTracker.RecordFailedAttempt(identifier);

            return StatusCode(401, "Invalid email or password.");
        }

        _attemptTracker.RecordSuccessfulAttempt(identifier);
        await _authService.SignInAsync(HttpContext, user);
        _logger.LogInformation("User {UserId} ({Email}) signed in successfully with cookie authentication", user.Id, user.Email);

        // Log cookie information for debugging
        var cookieValue = HttpContext.Response.Headers["Set-Cookie"].FirstOrDefault();
        _logger.LogDebug("Set-Cookie header: {CookieHeader}", cookieValue);

        return NoContent();
    }

    [HttpPost("login-form")]
    [EnableRateLimiting("auth")]
    // Antiforgery is intentionally bypassed: this is the pre-authentication login form, so the
    // caller has no antiforgery token or session yet. The action only authenticates (sets the auth
    // cookie) and does nothing else state-changing; brute force is covered by the rate limiter and
    // progressive lockout.
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LoginForm(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] bool remember = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Form login attempt with missing credentials for email: {Email}", email);

                return Redirect("/login?error=Invalid email or password");
            }

            var identifier = BuildAttemptIdentifier(email);
            if (_attemptTracker.IsBlocked(identifier))
            {
                _logger.LogWarning("Form login blocked for email {Email} due to too many failed attempts", email);

                return Redirect("/login?error=Too many failed attempts. Please try again later.");
            }

            _logger.LogInformation("Looking up user with email: {Email}", email);
            var user = await _userService.FindByEmail(email, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Form login attempt with non-existent email: {Email}", email);
                _attemptTracker.RecordFailedAttempt(identifier);

                return Redirect("/login?error=Invalid email or password");
            }

            _logger.LogInformation("User found: {UserId}, verifying password", user.Id);
            if (!_passwordHasher.Verify(password, user.PasswordHash))
            {
                _logger.LogWarning("Form login attempt with invalid password for email: {Email}", email);
                _attemptTracker.RecordFailedAttempt(identifier);

                return Redirect("/login?error=Invalid email or password");
            }

            _attemptTracker.RecordSuccessfulAttempt(identifier);
            await _authService.SignInAsync(HttpContext, user, remember);
            
            // Log cookie information
            var setCookieHeader = HttpContext.Response.Headers["Set-Cookie"].FirstOrDefault();

            return Redirect("/");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during form login for email: {Email}", email);

            return Redirect("/login?error=An error occurred during login");
        }
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
    public async Task<IActionResult> Logout()
    {
        var userId = _authService.GetUserId(User);
        await _authService.SignOut(HttpContext);
        _logger.LogInformation("User {UserId} logged out", userId);

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = _authService.GetUserId(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _userService.Get(userId.Value, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        var userDto = new CurrentUserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsAdmin,
            user.Culture,
            user.TimeZone,
            ActivityFilterPreferences.NormalizeDisplayType(user.ActivityDisplayType),
            ActivityFilterPreferences.NormalizeActivitySortOrder(user.ActivitySortOrder),
            ActivityFilterPreferences.NormalizePeriodSort(user.ActivityPeriodSort));

        return Ok(userDto);
    }

    [HttpGet("csrf")]
    [Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        var token = new CsrfTokenDto(tokens.RequestToken!);

        return Ok(token);
    }

}
