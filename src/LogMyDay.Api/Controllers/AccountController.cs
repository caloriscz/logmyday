using LogMyDay.Api.Application.Helpers;
using LogMyDay.Api.Application.Interfaces;
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

    [HttpPost("password/change")]
    [Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
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
    /// Changes the password of the web app's user. The browser posts this form itself, so that it
    /// gets the new cookie: the change ends the sign-ins made with the old password, and this one
    /// goes on. The profile page shows the result by its key.
    /// </summary>
    [HttpPost("password/change-form")]
    [Authorize(AuthenticationSchemes = "lmd-cookie")]
    [EnableRateLimiting("auth")]
    // SameSite=Lax keeps the cookie from a form on another site, which wouldn't know the current
    // password either.
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ChangePasswordForm([FromForm] string? currentPassword, [FromForm] string? newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 10)
        {
            return Redirect("/profile?password=short");
        }

        if (string.IsNullOrEmpty(currentPassword))
        {
            return Redirect("/profile?password=incorrect");
        }

        var userId = _authService.GetUserId(User);
        if (userId == null)
        {
            return Redirect("/login");
        }

        try
        {
            await _userService.ChangePassword(userId.Value, currentPassword, newPassword, userId.Value, cancellationToken);

            var user = await _userService.Get(userId.Value, cancellationToken)
                ?? throw new InvalidOperationException($"The account {userId} is gone.");
            await _authService.SignInAsync(HttpContext, user);

            return Redirect("/profile?password=changed");
        }
        catch (UnauthorizedAccessException)
        {
            return Redirect("/profile?password=incorrect");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");

            return Redirect("/profile?password=failed");
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
