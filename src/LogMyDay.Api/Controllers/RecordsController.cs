using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

/// <summary>Records: one measurement of a record type's fields. A value that does not fit its field
/// returns 400; a second record on a "once per day" type returns 409.</summary>
[ApiController]
[Route("api/records")]
public class RecordsController : BaseApiController
{
    private readonly IRecordService _recordService;
    private readonly ILogger<RecordsController> _logger;

    public RecordsController(
        IRecordService recordService,
        IAuthService authService,
        ILogger<RecordsController> logger) : base(authService)
    {
        _recordService = recordService;
        _logger = logger;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecordResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _recordService.GetById(id, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<RecordResponse>> Create(RecordRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var record = await _recordService.Create(request, userId);

            return CreatedAtAction(nameof(GetById), new { id = record.Id }, record);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Record create rejected for user {UserId}: {Reason}", userId, ex.Message);

            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecordResponse>> Update(int id, RecordRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _recordService.Update(id, request, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Record {RecordId} update rejected for user {UserId}: {Reason}", id, userId, ex.Message);

            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _recordService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
