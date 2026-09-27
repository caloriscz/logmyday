using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

/// <summary>Tag Activity Relations rules. Validation failures return 400 with a message the
/// editor shows as is; tags or rules the caller does not own return 404.</summary>
[ApiController]
[Route("api/tag-rules")]
public class TagRulesController : BaseApiController
{
    private readonly ITagRuleService _tagRuleService;
    private readonly ILogger<TagRulesController> _logger;

    public TagRulesController(
        ITagRuleService tagRuleService,
        IAuthService authService,
        ILogger<TagRulesController> logger) : base(authService)
    {
        _tagRuleService = tagRuleService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<TagRuleResponse>>> GetAll()
    {
        var userId = GetCurrentUserId();

        return Ok(await _tagRuleService.GetAll(userId));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagRuleResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _tagRuleService.GetById(id, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<TagRuleResponse>> Create(TagRuleRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var rule = await _tagRuleService.Create(request, userId);

            return CreatedAtAction(nameof(GetById), new { id = rule.Id }, rule);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Rule create rejected for user {UserId}: {Reason}", userId, ex.Message);

            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TagRuleResponse>> Update(int id, TagRuleRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            return Ok(await _tagRuleService.Update(id, request, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Rule {RuleId} update rejected for user {UserId}: {Reason}", id, userId, ex.Message);

            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _tagRuleService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
