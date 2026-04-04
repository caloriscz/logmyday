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

    [HttpGet]
    public async Task<IActionResult> GetEventLogs([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, [FromQuery] string? level = null)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.Claims.Any(c => c.Type == "is_admin" && c.Value == "true");

        EventLogLevel? levelFilter = null;
        if (!string.IsNullOrEmpty(level) && Enum.TryParse<EventLogLevel>(level, true, out var parsed))
        {
            levelFilter = parsed;
        }

        var result = await _eventLogService.GetPaged(pageNumber, pageSize, userId, isAdmin, levelFilter);

        return Ok(result);
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetCount([FromQuery] string? level = null)
    {
        var userId = GetCurrentUserId();

        EventLogLevel? levelFilter = null;
        if (!string.IsNullOrEmpty(level) && Enum.TryParse<EventLogLevel>(level, true, out var parsed))
        {
            levelFilter = parsed;
        }

        var count = await _eventLogService.GetCount(userId, levelFilter);

        return Ok(count);
    }
}
