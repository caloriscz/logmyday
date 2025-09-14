using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace LogMyDay.Api.Application.Services;

public sealed class AuthService : IAuthService
{
    private const string AuthenticationScheme = "lmd-cookie";
    private readonly ILogger<AuthService> _logger;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger;
    }

    public async Task SignInAsync(HttpContext httpContext, User user)
    {
        _logger.LogInformation("AuthService.SignInAsync: Starting sign-in for User {UserId} ({Email})", user.Id, user.Email);
        
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
            new Claim("is_admin", user.IsAdmin.ToString().ToLowerInvariant())
        };

        _logger.LogInformation("AuthService.SignInAsync: Created claims for User {UserId}: {Claims}", 
            user.Id, string.Join(", ", claims.Select(c => $"{c.Type}={c.Value}")));

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        _logger.LogInformation("AuthService.SignInAsync: Calling httpContext.SignInAsync with scheme '{Scheme}'", AuthenticationScheme);
        
        await httpContext.SignInAsync(AuthenticationScheme, principal);
        
        _logger.LogInformation("AuthService.SignInAsync: Successfully signed in User {UserId} ({Email})", user.Id, user.Email);
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        _logger.LogInformation("AuthService.SignOutAsync: Signing out user");
        await httpContext.SignOutAsync(AuthenticationScheme);
        _logger.LogInformation("AuthService.SignOutAsync: Successfully signed out user");
    }

    public Guid? GetUserId(ClaimsPrincipal principal)
    {
        _logger.LogDebug("🔍 DEBUG: AuthService.GetUserId called");
        _logger.LogDebug("🔍 DEBUG: Principal is null: {IsNull}", principal == null);
        if (principal != null)
        {
            _logger.LogDebug("🔍 DEBUG: Principal.Identity.IsAuthenticated: {IsAuthenticated}", principal.Identity?.IsAuthenticated);
            _logger.LogDebug("🔍 DEBUG: Principal claims count: {ClaimsCount}", principal.Claims.Count());
            foreach (var claim in principal.Claims)
            {
                _logger.LogDebug("🔍 DEBUG: Claim - Type: {Type}, Value: {Value}", claim.Type, claim.Value);
            }
        }
        
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.TryParse(userIdClaim, out var id) ? (Guid?)id : null;
        _logger.LogDebug("AuthService.GetUserId: UserIdClaim='{UserIdClaim}', ParsedUserId={UserId}", userIdClaim, userId);
        return userId;
    }
}
