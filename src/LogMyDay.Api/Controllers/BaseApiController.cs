using System.Globalization;
using LogMyDay.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LogMyDay.Api.Controllers;

/// <summary>
/// Base controller for API endpoints that require user authentication and provide user context.
/// Uses smart authentication that automatically selects between cookie and basic auth.
/// Rate limited at 100 requests per minute per signed-in user and client address (per address when anonymous).
/// </summary>
[Authorize]
[ApiController]
[EnableRateLimiting("api")]
public abstract class BaseApiController : ControllerBase
{
    private readonly IAuthService _authService;

    protected BaseApiController(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    /// <summary>A day query parameter in yyyy-MM-dd form only; empty is no day. Model binding
    /// would also take culture-dependent forms such as 6.5.2026.</summary>
    protected static bool TryParseDay(string? value, out DateOnly? day)
    {
        day = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        day = parsed;

        return true;
    }

    /// <summary>
    /// Gets the current authenticated user's ID from claims.
    /// </summary>
    /// <returns>The current user's ID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated or user ID cannot be determined</exception>
    protected Guid GetCurrentUserId()
    {
        var userId = _authService.GetUserId(User);
        return userId == null
            ? throw new UnauthorizedAccessException("User ID not found in claims. User may not be properly authenticated.")
            : userId.Value;
    }

    /// <summary>
    /// Gets the current authenticated user's ID from claims, returns null if not found.
    /// </summary>
    /// <returns>The current user's ID or null if not authenticated</returns>
    protected Guid? GetCurrentUserIdOrNull()
    {
        return _authService.GetUserId(User);
    }
}