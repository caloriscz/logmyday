using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Security;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        IAuthService authService,
        IPasswordHasher passwordHasher,
        IAntiforgery antiforgery,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _authService = authService;
        _passwordHasher = passwordHasher;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    [HttpPost("register-first")]
    public async Task<IActionResult> RegisterFirstAdmin([FromBody] RegisterFirstDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
            {
                return BadRequest("Valid email is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10)
            {
                return BadRequest("Password must be at least 10 characters long.");
            }

            var user = await _userService.CreateFirstAdminAsync(request.Email, request.Password, request.DisplayName, cancellationToken);
            return CreatedAtAction(nameof(GetCurrentUser), new { }, new { message = "First admin user created successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating first admin user");
            return StatusCode(500, "An error occurred while creating the admin user.");
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Login attempt with invalid model state for email: {Email}", request.Email);
                return BadRequest(ModelState);
            }

            var user = await _userService.FindByEmailAsync(request.Email, cancellationToken);
            if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login attempt with invalid credentials for email: {Email}", request.Email);
                return StatusCode(401, "Invalid email or password.");
            }

            // This should integrate with the existing AuthAttemptTracker
            // For now, we'll implement basic verification
            // TODO: Add rate limiting integration

            _logger.LogInformation("User {UserId} ({Email}) authenticated successfully, signing in...", user.Id, user.Email);
            await _authService.SignInAsync(HttpContext, user);
            _logger.LogInformation("User {UserId} ({Email}) signed in successfully with cookie authentication", user.Id, user.Email);
            
            // Log cookie information for debugging
            var cookieValue = HttpContext.Response.Headers["Set-Cookie"].FirstOrDefault();
            _logger.LogDebug("Set-Cookie header: {CookieHeader}", cookieValue);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
            return StatusCode(500, "An error occurred during login.");
        }
    }

    [HttpPost("login-form")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LoginForm([FromForm] string email, [FromForm] string password, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Form login attempt for email: {Email}", email);
            
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Form login attempt with missing credentials for email: {Email}", email);
                return Redirect("/login?error=Invalid email or password");
            }

            var user = await _userService.FindByEmailAsync(email, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Form login attempt with non-existent email: {Email}", email);
                return Redirect("/login?error=Invalid email or password");
            }
            
            if (!_passwordHasher.Verify(password, user.PasswordHash))
            {
                _logger.LogWarning("Form login attempt with invalid password for email: {Email}", email);
                return Redirect("/login?error=Invalid email or password");
            }

            _logger.LogInformation("User {UserId} ({Email}) authenticated successfully via form, signing in...", user.Id, user.Email);
            
            // Log cookie settings before sign in
            _logger.LogInformation("Cookie authentication settings: Scheme=lmd-cookie, Path={Path}, Domain={Domain}", 
                HttpContext.Request.PathBase, HttpContext.Request.Host.Host);
            
            await _authService.SignInAsync(HttpContext, user);
            
            // Check if authentication was successful
            var isAuthenticated = HttpContext.User?.Identity?.IsAuthenticated == true;
            _logger.LogInformation("User {UserId} ({Email}) sign in complete. IsAuthenticated: {IsAuthenticated}, User: {UserName}", 
                user.Id, user.Email, isAuthenticated, HttpContext.User?.Identity?.Name ?? "null");
            
            // Log cookie information
            var setCookieHeader = HttpContext.Response.Headers["Set-Cookie"].FirstOrDefault();
            _logger.LogInformation("Set-Cookie header: {CookieHeader}", setCookieHeader ?? "No cookie set");
            
            if (!isAuthenticated)
            {
                _logger.LogError("Authentication failed even after successful sign in for user {UserId} ({Email})", user.Id, user.Email);
                return Redirect("/login?error=Authentication system error");
            }
            
            _logger.LogInformation("User {UserId} ({Email}) signed in successfully, redirecting to home", user.Id, user.Email);
            return Redirect("/");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during form login for email: {Email}", email);
            return Redirect("/login?error=An error occurred during login");
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = _authService.GetUserId(User);
            await _authService.SignOutAsync(HttpContext);
            _logger.LogInformation("User {UserId} logged out", userId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, "An error occurred during logout.");
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        try
        {
            var userId = _authService.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _userService.GetAsync(userId.Value, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            var userDto = new CurrentUserDto(user.Id, user.Email, user.DisplayName, user.IsAdmin);
            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, "An error occurred while retrieving user information.");
        }
    }

    [HttpGet("csrf")]
    [Authorize]
    public IActionResult GetCsrfToken()
    {
        try
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            var token = new CsrfTokenDto(tokens.RequestToken!);
            return Ok(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSRF token");
            return StatusCode(500, "An error occurred while generating CSRF token.");
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
