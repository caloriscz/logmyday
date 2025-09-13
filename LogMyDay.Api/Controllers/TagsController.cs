using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Shared.DTOs;
using LogMyDay.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[Authorize(AuthenticationSchemes = "lmd-cookie")]
[ApiController]
[Route("api/[controller]")]
public class TagsController : BaseApiController
{
    private readonly ITagService _tagService;
    private readonly ILogger<TagsController> _logger;
    private readonly LogMyDayDbContext _context;

    public TagsController(ITagService tagsService, ILogger<TagsController> logger, LogMyDayDbContext context, IAuthService authService) : base(authService)
    {
        _tagService = tagsService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Create a tag for given user
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>    
    [HttpPost]
    public async Task<IActionResult> Create(TagRequest model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for tag creation: {@ModelState}", ModelState);
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Creating tag with data: {@TagRequest}", model);
            var userId = GetCurrentUserId();
            return Ok(await _tagService.Create(model, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tag. Tag: {Tag}, TypeId: {TypeId}, IsRepeatable: {IsRepeatable}, TimeGranularity: {TimeGranularity}, IsRange: {IsRange}", 
                model.Tag, model.TypeId, model.IsRepeatable, model.TimeGranularity, model.IsRange);

            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates tag name identified by its id
    /// </summary>
    /// <param name="id"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TagRequest model)
    {
        var userId = GetCurrentUserId();
        await _tagService.Update(id, model, userId);

        return NoContent();
    }

    /// <summary>
    /// Lists all tags
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetCurrentUserId();
        return Ok(await _tagService.GetAll(userId));
    }

    /// <summary>
    /// Lists paged, sorted, and filtered tags
    /// </summary>
    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] string orderBy = "asc", [FromQuery] string? filter = null, [FromQuery] string? filterType = null)
    {
        var userId = GetCurrentUserId();
        return Ok(await _tagService.GetPaged(pageNumber, pageSize, orderBy, userId, filter, filterType));
    }

    /// <summary>
    /// Get tag by its id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTagById(int id)
    {
        var userId = GetCurrentUserId();
        return Ok(await _tagService.GetTagById(id, userId));
    }

    /// <summary>
    /// Delete tag by its id
    /// </summary>
    /// <param name="id"></param>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        await _tagService.Delete(id, userId);
        return NoContent();
    }

    /// <summary>
    /// Get all input types
    /// </summary>
    /// <returns></returns>
    [HttpGet("inputtypes")]
    public async Task<ActionResult<List<InputTypeDto>>> GetInputTypes()
    {
        var inputTypes = await _context.Set<InputType>().Select(x => new InputTypeDto { Id = x.Id, Name = x.Name }).ToListAsync();
        return Ok(inputTypes);
    }
}
