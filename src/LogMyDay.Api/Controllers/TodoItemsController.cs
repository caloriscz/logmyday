using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/todo-items")]
public class TodoItemsController : BaseApiController
{
    private readonly ITodoItemService _todoItemService;
    private readonly ILogger<TodoItemsController> _logger;

    public TodoItemsController(
        ITodoItemService todoItemService,
        IAuthService authService,
        ILogger<TodoItemsController> logger) : base(authService)
    {
        _todoItemService = todoItemService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<TodoItemResponse>> Create(TodoItemRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var item = await _todoItemService.Create(request, userId);

            return Created(string.Empty, item);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation failed creating todo item for user {UserId}", userId);

            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo list not found for user {UserId}", userId);

            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TodoItemRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _todoItemService.Update(id, request, userId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation failed updating todo item {ItemId} for user {UserId}", id, userId);

            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo item {ItemId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _todoItemService.Delete(id, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo item {ItemId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<TodoItemResponse>> Complete(int id, TodoItemCompleteRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var item = await _todoItemService.Complete(id, request, userId);

            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo item {ItemId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot complete todo item {ItemId} for user {UserId}", id, userId);

            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id:int}/reopen")]
    public async Task<ActionResult<TodoItemResponse>> Reopen(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            var item = await _todoItemService.Reopen(id, userId);

            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo item {ItemId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id:int}/skip")]
    public async Task<ActionResult<TodoItemResponse>> Skip(int id, [FromQuery] string? date = null)
    {
        var userId = GetCurrentUserId();
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        try
        {
            var item = await _todoItemService.Skip(id, userId, parsedDate);

            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo item {ItemId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id:int}/unskip")]
    public async Task<ActionResult<TodoItemResponse>> Unskip(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            var item = await _todoItemService.Unskip(id, userId);

            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo item {ItemId} not found for user {UserId}", id, userId);

            return NotFound(ex.Message);
        }
    }

    [HttpPatch("/api/todo-lists/{listId:int}/items/reorder")]
    public async Task<IActionResult> Reorder(int listId, [FromBody] IList<TodoItemReorderRequest> items)
    {
        var userId = GetCurrentUserId();

        try
        {
            await _todoItemService.Reorder(listId, items, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Todo list {ListId} not found for user {UserId}", listId, userId);

            return NotFound(ex.Message);
        }
    }
}
