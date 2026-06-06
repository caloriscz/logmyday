using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/reminders")]
public class RemindersController : BaseApiController
{
    private readonly IReminderService _service;
    private readonly IEventLogService _eventLogService;
    private readonly ILogger<RemindersController> _logger;

    public RemindersController(
        IReminderService service,
        IAuthService authService,
        IEventLogService eventLogService,
        ILogger<RemindersController> logger)
        : base(authService)
    {
        _service = service;
        _eventLogService = eventLogService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<ReminderResponse>>> GetAll([FromQuery] string? date = null)
    {
        var userId = GetCurrentUserId();
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        return Ok(await _service.GetAll(userId, parsedDate));
    }

    [HttpPost]
    public async Task<ActionResult<ReminderResponse>> Create(ReminderRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var item = await _service.Create(request, userId);
            return Created(string.Empty, item);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ReminderRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _service.Update(id, request, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _service.Delete(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ReminderResponse>> Complete(int id, ReminderCompleteRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _service.Complete(id, request, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (TagDayLockedException ex)
        {
            _logger.LogInformation(ex, "Reminder {ItemId} completion rejected: tag {TagId} locked for {Date}", id, ex.TagId, ex.Date);
            await _eventLogService.Log(userId, EventLogLevel.Error,
                $"Reminder completion rejected: tag {ex.TagId} locked for {ex.Date:yyyy-MM-dd}",
                $"ReminderId: {id}");

            return Conflict(new { code = "tag-day-locked", tagId = ex.TagId, date = ex.Date.ToString("yyyy-MM-dd") });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot complete reminder {ItemId} for user {UserId}", id, userId);
            await _eventLogService.Log(userId, EventLogLevel.Error,
                $"Reminder completion rejected: {ex.Message}",
                $"ReminderId: {id}");

            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id:int}/reopen")]
    public async Task<ActionResult<ReminderResponse>> Reopen(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _service.Reopen(id, userId));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:int}/skip")]
    public async Task<ActionResult<ReminderResponse>> Skip(int id, [FromQuery] string? date = null)
    {
        var userId = GetCurrentUserId();
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        try
        {
            return Ok(await _service.Skip(id, userId, parsedDate));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:int}/unskip")]
    public async Task<ActionResult<ReminderResponse>> Unskip(int id, [FromQuery] string? date = null)
    {
        var userId = GetCurrentUserId();
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        try
        {
            return Ok(await _service.Unskip(id, userId, parsedDate));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] IList<ReminderReorderRequest> items)
    {
        var userId = GetCurrentUserId();
        await _service.Reorder(items, userId);
        return NoContent();
    }
}
