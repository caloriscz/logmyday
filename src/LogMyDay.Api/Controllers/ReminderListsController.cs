using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/reminder-lists")]
public class ReminderListsController : BaseApiController
{
    private readonly IReminderListService _service;
    private readonly ILogger<ReminderListsController> _logger;

    public ReminderListsController(IReminderListService service, IAuthService authService, ILogger<ReminderListsController> logger)
        : base(authService)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<ReminderListResponse>>> GetAll([FromQuery] string? date = null)
    {
        var userId = GetCurrentUserId();
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        return Ok(await _service.GetAll(userId, parsedDate));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReminderListResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _service.GetById(id, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<ReminderListResponse>> Create(ReminderListRequest request)
    {
        var userId = GetCurrentUserId();
        var created = await _service.Create(request, userId);
        return Created(string.Empty, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ReminderListRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _service.Update(id, request, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
