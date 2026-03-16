using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Preferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        IAuthService authService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _authService = authService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        try
        {
            var users = await _userService.List(cancellationToken);
            var userDtos = users
                .Select(u => new UserDto(
                    u.Id,
                    u.Email,
                    u.DisplayName,
                    u.IsAdmin,
                    u.CreatedUtc,
                    u.UpdatedUtc,
                    u.Culture,
                    u.TimeZone,
                    ActivityFilterPreferences.NormalizeDisplayType(u.ActivityDisplayType),
                    ActivityFilterPreferences.NormalizeActivitySortOrder(u.ActivitySortOrder),
                    ActivityFilterPreferences.NormalizePeriodSort(u.ActivityPeriodSort)))
                .ToList();
            
            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            
            return StatusCode(500, "An error occurred while retrieving users.");
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto request, CancellationToken cancellationToken)
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

            if (string.IsNullOrWhiteSpace(request.Culture))
            {
                return BadRequest("Culture is required.");
            }

            if (string.IsNullOrWhiteSpace(request.TimeZone))
            {
                return BadRequest("Time zone is required.");
            }

            var actorId = _authService.GetUserId(User);
            
            if (actorId == null)
            {
                return Unauthorized();
            }

            var user = await _userService.CreateUser(
                request.Email,
                request.Password,
                request.DisplayName,
                request.IsAdmin,
                request.Culture.Trim(),
                request.TimeZone.Trim(),
                actorId.Value,
                cancellationToken);

            var userDto = new UserDto(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsAdmin,
                user.CreatedUtc,
                user.UpdatedUtc,
                user.Culture,
                user.TimeZone,
                ActivityFilterPreferences.NormalizeDisplayType(user.ActivityDisplayType),
                ActivityFilterPreferences.NormalizeActivitySortOrder(user.ActivitySortOrder),
                ActivityFilterPreferences.NormalizePeriodSort(user.ActivityPeriodSort));
            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, userDto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");

            return StatusCode(500, "An error occurred while creating the user.");
        }
    }

    [HttpPatch("{id}")]
    [Authorize] // Requires authentication, but users can update their own profile
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("🎯 UsersController.UpdateUser: Invalid model state");
                return BadRequest(ModelState);
            }

            if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
            {
                return BadRequest("Valid email is required.");
            }

            if (request.Culture != null && string.IsNullOrWhiteSpace(request.Culture))
            {
                return BadRequest("Culture is required.");
            }

            if (request.TimeZone != null && string.IsNullOrWhiteSpace(request.TimeZone))
            {
                return BadRequest("Time zone is required.");
            }

            var actorId = _authService.GetUserId(User);
            if (actorId == null)
            {
                return Unauthorized();
            }

            var user = await _userService.Update(
                id,
                request.Email,
                request.DisplayName,
                request.IsAdmin,
                request.Culture?.Trim(),
                request.TimeZone?.Trim(),
                actorId.Value,
                cancellationToken,
                request.ActivityDisplayType?.Trim(),
                request.ActivitySortOrder?.Trim(),
                request.ActivityPeriodSort?.Trim());

            var userDto = new UserDto(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsAdmin,
                user.CreatedUtc,
                user.UpdatedUtc,
                user.Culture,
                user.TimeZone,
                ActivityFilterPreferences.NormalizeDisplayType(user.ActivityDisplayType),
                ActivityFilterPreferences.NormalizeActivitySortOrder(user.ActivitySortOrder),
                ActivityFilterPreferences.NormalizePeriodSort(user.ActivityPeriodSort));
            return Ok(userDto);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);

            return StatusCode(500, "An error occurred while updating the user.");
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var actorId = _authService.GetUserId(User);
            if (actorId == null)
            {
                return Unauthorized();
            }

            await _userService.Delete(id, actorId.Value, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            
            return StatusCode(500, "An error occurred while deleting the user.");
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
