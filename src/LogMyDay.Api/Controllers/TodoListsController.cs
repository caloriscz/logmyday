using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/todo-lists")]
public class TodoListsController : BaseApiController
{
    private readonly ITodoListService _todoListService;
    private readonly ILogger<TodoListsController> _logger;

    public TodoListsController(
        ITodoListService todoListService,
        IAuthService authService,
        ILogger<TodoListsController> logger) : base(authService)
    {
        _todoListService = todoListService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<TodoListResponse>>> GetAll([FromQuery] string? date = null)
    {
        var userId = GetCurrentUserId();
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        var lists = await _todoListService.GetAll(userId, parsedDate);

        return Ok(lists);
    }

    [HttpPost]
    public async Task<ActionResult<TodoListResponse>> Create(TodoListRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var list = await _todoListService.Create(request, userId);

            return Created(string.Empty, list);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid todo list create request for user {UserId}", userId);

            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TodoListRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _todoListService.Update(id, request, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo list {ListId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid todo list update request for list {ListId}, user {UserId}", id, userId);

            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _todoListService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo list {ListId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }
}
