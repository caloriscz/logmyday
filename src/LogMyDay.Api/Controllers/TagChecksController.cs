using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

/// <summary>Tag Activity Relations checks and the Rule review. Validation failures return 400
/// with a message the editor shows as is; tags or checks the caller does not own return 404.</summary>
[ApiController]
[Route("api/tag-checks")]
public class TagChecksController : BaseApiController
{
    private readonly ITagCheckService _tagCheckService;
    private readonly ILogger<TagChecksController> _logger;

    public TagChecksController(
        ITagCheckService tagCheckService,
        IAuthService authService,
        ILogger<TagChecksController> logger) : base(authService)
    {
        _tagCheckService = tagCheckService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<TagCheckResponse>>> GetAll()
    {
        var userId = GetCurrentUserId();

        return Ok(await _tagCheckService.GetAll(userId));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagCheckResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _tagCheckService.GetById(id, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<TagCheckResponse>> Create(TagCheckRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var check = await _tagCheckService.Create(request, userId);

            return CreatedAtAction(nameof(GetById), new { id = check.Id }, check);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Check create rejected for user {UserId}: {Reason}", userId, ex.Message);

            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TagCheckResponse>> Update(int id, TagCheckRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _tagCheckService.Update(id, request, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Check {CheckId} update rejected for user {UserId}: {Reason}", id, userId, ex.Message);

            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _tagCheckService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>The days that break an active check, newest first; nothing is stored. Defaults:
    /// the last 30 days up to today.</summary>
    [HttpGet("review")]
    public async Task<ActionResult<TagCheckReviewResponse>> Review(string? from, string? to)
    {
        if (!TryParseDay(from, out var fromDay) || !TryParseDay(to, out var toDay))
        {
            return BadRequest("Use yyyy-MM-dd for from and to.");
        }

        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _tagCheckService.Review(fromDay, toDay, userId));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
