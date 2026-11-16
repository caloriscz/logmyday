using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

/// <summary>Tag Activity Relations triggers. Validation failures return 400 with a message the
/// editor shows as is; tags or triggers the caller does not own return 404.</summary>
[ApiController]
[Route("api/tag-triggers")]
public class TagTriggersController : BaseApiController
{
    private readonly ITagTriggerService _tagTriggerService;
    private readonly ILogger<TagTriggersController> _logger;

    public TagTriggersController(
        ITagTriggerService tagTriggerService,
        IAuthService authService,
        ILogger<TagTriggersController> logger) : base(authService)
    {
        _tagTriggerService = tagTriggerService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<TagTriggerResponse>>> GetAll()
    {
        var userId = GetCurrentUserId();

        return Ok(await _tagTriggerService.GetAll(userId));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagTriggerResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _tagTriggerService.GetById(id, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<TagTriggerResponse>> Create(TagTriggerRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var trigger = await _tagTriggerService.Create(request, userId);

            return CreatedAtAction(nameof(GetById), new { id = trigger.Id }, trigger);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Trigger create rejected for user {UserId}: {Reason}", userId, ex.Message);

            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TagTriggerResponse>> Update(int id, TagTriggerRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _tagTriggerService.Update(id, request, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Trigger {TriggerId} update rejected for user {UserId}: {Reason}", id, userId, ex.Message);

            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _tagTriggerService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
