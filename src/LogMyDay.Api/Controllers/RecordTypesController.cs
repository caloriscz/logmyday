using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

/// <summary>Record types and their fields. Validation failures return 400 with a message the editor
/// shows as is; a field a rule reads cannot be removed (409); types the caller does not own return
/// 404.</summary>
[ApiController]
[Route("api/record-types")]
public class RecordTypesController : BaseApiController
{
    private readonly IRecordTypeService _recordTypeService;
    private readonly ILogger<RecordTypesController> _logger;

    public RecordTypesController(
        IRecordTypeService recordTypeService,
        IAuthService authService,
        ILogger<RecordTypesController> logger) : base(authService)
    {
        _recordTypeService = recordTypeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<RecordTypeResponse>>> GetAll()
    {
        var userId = GetCurrentUserId();

        return Ok(await _recordTypeService.GetAll(userId));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecordTypeResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _recordTypeService.GetById(id, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<RecordTypeResponse>> Create(RecordTypeRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var type = await _recordTypeService.Create(request, userId);

            return CreatedAtAction(nameof(GetById), new { id = type.Id }, type);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Record type create rejected for user {UserId}: {Reason}", userId, ex.Message);

            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecordTypeResponse>> Update(int id, RecordTypeRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _recordTypeService.Update(id, request, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Record type {RecordTypeId} update rejected for user {UserId}: {Reason}", id, userId, ex.Message);

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
            await _recordTypeService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
