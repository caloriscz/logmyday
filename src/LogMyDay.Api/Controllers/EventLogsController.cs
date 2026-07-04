using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LogMyDay.Api.Controllers;

[Route("api/[controller]")]
public class EventLogsController : BaseApiController
{
    private readonly IEventLogService _eventLogService;

    public EventLogsController(IEventLogService eventLogService, IAuthService authService)
        : base(authService)
    {
        _eventLogService = eventLogService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateEventLog([FromBody] LogMyDay.Shared.DTOs.EventLogRequest request)
    {
        var userId = GetCurrentUserId();

        LogMyDay.Domain.Enums.EventLogLevel level = LogMyDay.Domain.Enums.EventLogLevel.Error;
        if (!string.IsNullOrEmpty(request.Level) &&
            Enum.TryParse<LogMyDay.Domain.Enums.EventLogLevel>(request.Level, true, out var parsedLevel))
        {
            level = parsedLevel;
        }

        await _eventLogService.Log(userId, level, request.Message);

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetEventLogs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? level = null,
        [FromQuery] string? message = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string sortBy = "time",
        [FromQuery] bool sortDesc = true,
        [FromQuery] LogMyDay.Shared.DTOs.EventLogCategoryFilter category = LogMyDay.Shared.DTOs.EventLogCategoryFilter.All)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.Claims.Any(c => c.Type == "is_admin" && c.Value == "true");

        EventLogLevel? levelFilter = null;
        if (!string.IsNullOrEmpty(level) && Enum.TryParse<EventLogLevel>(level, true, out var parsed))
        {
            levelFilter = parsed;
        }

        var result = await _eventLogService.GetPaged(pageNumber, pageSize, userId, isAdmin, levelFilter, message, dateFrom, dateTo, sortBy, sortDesc, category);

        return Ok(result);
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetCount(
        [FromQuery] string? level = null,
        [FromQuery] string? message = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] LogMyDay.Shared.DTOs.EventLogCategoryFilter category = LogMyDay.Shared.DTOs.EventLogCategoryFilter.All)
    {
        var userId = GetCurrentUserId();

        EventLogLevel? levelFilter = null;
        if (!string.IsNullOrEmpty(level) && Enum.TryParse<EventLogLevel>(level, true, out var parsed))
        {
            levelFilter = parsed;
        }

        var count = await _eventLogService.GetCount(userId, levelFilter, message, dateFrom, dateTo, category);

        return Ok(count);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteEventLogs([FromQuery] int? olderThanDays = null)
    {
        var userId = GetCurrentUserId();
        await _eventLogService.DeleteEvents(userId, olderThanDays);

        return NoContent();
    }
}