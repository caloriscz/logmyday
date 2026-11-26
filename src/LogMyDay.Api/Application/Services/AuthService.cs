using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace LogMyDay.Api.Application.Services;

public sealed class AuthService : IAuthService
{
    private const string AuthenticationScheme = CookieSession.AuthenticationScheme;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger;
    }
    public async Task SignInAsync(HttpContext httpContext, User user, bool rememberMe)
    {
        _logger.LogInformation("AuthService: Starting sign-in for User {UserId} ({Email})", user.Id, user.Email);

        var principal = CookieSession.Create(user);

        _logger.LogInformation("AuthService.SignInAsync: Created claims for User {UserId}: {Claims}",
            user.Id, string.Join(", ", principal.Claims.Where(c => c.Type != CookieSession.StampClaimType).Select(c => $"{c.Type}={c.Value}")));

        // Always set IsPersistent to true for proper cookie behavior with 30-day sliding expiration
        // "Remember Me" checkbox preserved for UX but both options now use persistent cookies
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,  // Always true to enable 30-day sliding expiration
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)  // 30-day expiration for all logins
        };

        await httpContext.SignInAsync(AuthenticationScheme, principal, authProperties);
    }

    public async Task SignOut(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(AuthenticationScheme);
        _logger.LogInformation("AuthService: Successfully signed out user");
    }

    public Guid? GetUserId(ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.TryParse(userIdClaim, out var id) ? (Guid?)id : null;

        return userId;
    }
}
