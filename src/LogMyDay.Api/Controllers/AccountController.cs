using LogMyDay.Api.Application.Helpers;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IUserService userService,
        IAuthService authService,
        ILogger<AccountController> logger)
    {
        _userService = userService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Changes your own password, which ends the web sign-ins made with the old one. A browser that
    /// calls this itself gets a new cookie and stays signed in.
    /// </summary>
    [HttpPost("password/change")]
    [Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 10)
            {
                return BadRequest("New password must be at least 10 characters long.");
            }

            var userId = _authService.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized();
            }

            await _userService.ChangePassword(userId.Value, request.CurrentPassword, request.NewPassword, userId.Value, cancellationToken);

            if (User.Identity?.AuthenticationType == CookieSession.AuthenticationScheme)
            {
                await KeepThisBrowserSignedIn(userId.Value, cancellationToken);
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");

            return StatusCode(500, "An error occurred while changing the password.");
        }
    }

    /// <summary>
    /// Gives the browser a cookie of the new password. The password is changed by now, so a
    /// failure here doesn't fail the change: the browser's old cookie is refused, and the user
    /// signs in again with the new password.
    /// </summary>
    private async Task KeepThisBrowserSignedIn(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            if (await _userService.Get(userId, cancellationToken) is { } user)
            {
                await _authService.SignInAsync(HttpContext, user);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The password of user {UserId} was changed, but the browser was not signed in again", userId);
        }
    }

    [HttpPost("password/reset/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] AdminResetPasswordDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 10)
            {
                return BadRequest("New password must be at least 10 characters long.");
            }

            var actorId = _authService.GetUserId(User);
            if (actorId == null)
            {
                return Unauthorized();
            }

            await _userService.AdminResetPassword(id, request.NewPassword, actorId.Value, cancellationToken);
            return NoContent();
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
            _logger.LogError(ex, "Error resetting password for user {UserId}", id);
            
            return StatusCode(500, "An error occurred while resetting the password.");
        }
    }

    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotDto request, CancellationToken cancellationToken)
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

            await _userService.BeginForgot(request.Email, $"{Request.Scheme}://{Request.Host}", cancellationToken);

            var response = new ForgotResponseDto("If an account exists for the provided email, a password reset link has been sent.");
            
            return Accepted(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request");
            
            return StatusCode(500, "An error occurred while processing the forgot password request.");
        }
    }

    [HttpPost("password/forgot/confirm")]
    public async Task<IActionResult> ConfirmForgotPassword([FromBody] ForgotConfirmDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 10)
            {
                return BadRequest("New password must be at least 10 characters long.");
            }

            await _userService.CompleteForgot(request.Token, request.NewPassword, cancellationToken);
            
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming forgot password");
            return StatusCode(500, "An error occurred while confirming the forgot password.");
        }
    }

}
