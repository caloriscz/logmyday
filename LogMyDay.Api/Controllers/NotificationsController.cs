using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        IAuthService authService,
        ILogger<NotificationsController> logger) : base(authService)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet("tag/{tagId:int}")]
    public async Task<ActionResult<IList<NotificationResponse>>> GetByTag(int tagId)
    {
        var userId = GetCurrentUserId();
        try
        {
            var notifications = await _notificationService.GetByTagAsync(tagId, userId);
            return Ok(notifications);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tag {TagId} not found for user {UserId}", tagId, userId);
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NotificationResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            var notification = await _notificationService.GetByIdAsync(id, userId);
            return Ok(notification);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Notification {NotificationId} not found for user {UserId}", id, userId);
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<NotificationResponse>> Create(NotificationRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var created = await _notificationService.CreateAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid notification create request for tag {TagId}", request.TagId);
            return BadRequest(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Invalid notification create request for tag {TagId}", request.TagId);
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tag {TagId} not found when creating notification", request.TagId);
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<NotificationResponse>> Update(int id, NotificationRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var updated = await _notificationService.UpdateAsync(id, request, userId);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid notification update request for notification {NotificationId}", id);
            return BadRequest(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Invalid notification update request for notification {NotificationId}", id);
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Notification {NotificationId} not found for user {UserId}", id, userId);
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        await _notificationService.DeleteAsync(id, userId);
        return NoContent();
    }
}
