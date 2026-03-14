using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/tag-groups")]
public class TagGroupsController : BaseApiController
{
    private readonly ITagGroupService _tagGroupService;
    private readonly ILogger<TagGroupsController> _logger;

    public TagGroupsController(
        ITagGroupService tagGroupService,
        IAuthService authService,
        ILogger<TagGroupsController> logger) : base(authService)
    {
        _tagGroupService = tagGroupService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<TagGroupResponse>>> GetAll()
    {
        var userId = GetCurrentUserId();
        var groups = await _tagGroupService.GetAll(userId);

        return Ok(groups);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagGroupResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            var group = await _tagGroupService.GetById(id, userId);

            return Ok(group);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tag group {GroupId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create(TagGroupRequest request)
    {
        var userId = GetCurrentUserId();
        var id = await _tagGroupService.Create(request, userId);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TagGroupRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _tagGroupService.Update(id, request, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tag group {GroupId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _tagGroupService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tag group {GroupId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }
}
