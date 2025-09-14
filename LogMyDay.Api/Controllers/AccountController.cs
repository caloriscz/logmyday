using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

            await _userService.ChangePasswordAsync(
                userId.Value,
                request.CurrentPassword,
                request.NewPassword,
                userId.Value,
                cancellationToken);

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

            await _userService.AdminResetPasswordAsync(id, request.NewPassword, actorId.Value, cancellationToken);
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

            if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
            {
                return BadRequest("Valid email is required.");
            }

            var token = await _userService.BeginForgotAsync(request.Email, cancellationToken);
            
            // In v1, we return the token directly. In production, this would be sent via email.
            var response = new ForgotResponseDto(token);
            return Ok(response);
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

            await _userService.CompleteForgotAsync(request.Token, request.NewPassword, cancellationToken);
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
