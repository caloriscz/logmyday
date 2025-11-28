using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[Route("api/[controller]")]
public class TagOptionListsController : BaseApiController
{
    private readonly ITagOptionListService _service;
    private readonly ILogger<TagOptionListsController> _logger;

    public TagOptionListsController(
        ITagOptionListService service,
        ILogger<TagOptionListsController> logger,
        IAuthService authService)
        : base(authService)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TagOptionListResponse>>> GetAll()
    {
        var userId = GetCurrentUserId();
        var lists = await _service.GetAll(userId);
        
        return Ok(lists);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagOptionListResponse>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        var list = await _service.GetById(id, userId);
        
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create(TagOptionListRequest request)
    {
        var userId = GetCurrentUserId();
        var id = await _service.Create(request, userId);
        _logger.LogInformation("Created option list {ListId} for user {UserId}", id, userId);
        
        return Ok(id);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TagOptionListRequest request)
    {
        var userId = GetCurrentUserId();
        await _service.Update(id, request, userId);
        
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        await _service.Delete(id, userId);
        
        return NoContent();
    }
}
