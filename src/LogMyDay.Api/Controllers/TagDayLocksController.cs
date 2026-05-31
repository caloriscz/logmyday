using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/tag-day-locks")]
public class TagDayLocksController : BaseApiController
{
    private readonly ITagDayLockService _service;
    private readonly ILogger<TagDayLocksController> _logger;

    public TagDayLocksController(
        ITagDayLockService service,
        IAuthService authService,
        ILogger<TagDayLocksController> logger) : base(authService)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<TagDayLockResponse>>> GetForDate([FromQuery] string date)
    {
        var userId = GetCurrentUserId();

        if (!DateOnly.TryParse(date, out var parsed))
        {
            return BadRequest("Invalid date format. Expected YYYY-MM-DD.");
        }

        var rows = await _service.GetForDate(userId, parsed);

        return Ok(rows);
    }

    [HttpPost]
    public async Task<ActionResult<TagDayLockResponse>> Upsert([FromBody] TagDayLockRequest request)
    {
        var userId = GetCurrentUserId();

        var result = await _service.Upsert(userId, request, DayLockSetBy.User);

        _logger.LogInformation(
            "[reminder-diag] event=tag-day-lock {Verb} tagId={TagId} date={Date} setBy=User",
            request.IsLocked ? "set" : "cleared", request.TagId, request.Date);

        return Ok(result);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] int tagId, [FromQuery] string date)
    {
        var userId = GetCurrentUserId();

        if (!DateOnly.TryParse(date, out var parsed))
        {
            return BadRequest("Invalid date format. Expected YYYY-MM-DD.");
        }

        await _service.Delete(userId, tagId, parsed);

        _logger.LogInformation(
            "[reminder-diag] event=tag-day-lock removed tagId={TagId} date={Date}",
            tagId, parsed);

        return NoContent();
    }
}
