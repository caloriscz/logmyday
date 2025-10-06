using LogMyDay.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LogMyDay.Api.Controllers;

/// <summary>
/// Base controller for API endpoints that require user authentication and provide user context.
/// Uses smart authentication that automatically selects between cookie and basic auth.
/// </summary>
[Authorize]
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    private readonly IAuthService _authService;

    protected BaseApiController(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    /// <summary>
    /// Gets the current authenticated user's ID from claims.
    /// </summary>
    /// <returns>The current user's ID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated or user ID cannot be determined</exception>
    protected Guid GetCurrentUserId()
    {
        var userId = _authService.GetUserId(User);
        if (userId == null)
        {
            throw new UnauthorizedAccessException("User ID not found in claims. User may not be properly authenticated.");
        }
        return userId.Value;
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