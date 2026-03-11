using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/scan-mappings")]
public class ScanMappingsController : BaseApiController
{
    private readonly IScanMappingService _scanMappingService;
    private readonly ILogger<ScanMappingsController> _logger;

    public ScanMappingsController(
        IScanMappingService scanMappingService,
        IAuthService authService,
        ILogger<ScanMappingsController> logger) : base(authService)
    {
        _scanMappingService = scanMappingService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<ScanMappingResponse>>> GetAll()
    {
        var userId = GetCurrentUserId();
        var mappings = await _scanMappingService.GetAll(userId);

        return Ok(mappings);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ScanMappingResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            var mapping = await _scanMappingService.GetById(id, userId);

            return Ok(mapping);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Scan mapping {MappingId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<ScanLookupResponse>> Lookup([FromQuery] string codeValue)
    {
        if (string.IsNullOrWhiteSpace(codeValue))
        {
            return BadRequest("Code value is required");
        }

        var userId = GetCurrentUserId();
        var result = await _scanMappingService.Lookup(codeValue, userId);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ScanMappingResponse>> Create(ScanMappingRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var created = await _scanMappingService.Create(request, userId);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Duplicate scan mapping for code '{CodeValue}' by user {UserId}", request.CodeValue, userId);

            return Conflict(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tag {TagId} not found when creating scan mapping", request.TagId);

            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ScanMappingResponse>> Update(int id, ScanMappingRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var updated = await _scanMappingService.Update(id, request, userId);

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Duplicate scan mapping for code '{CodeValue}' by user {UserId}", request.CodeValue, userId);

            return Conflict(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Scan mapping {MappingId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _scanMappingService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Scan mapping {MappingId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }
}
