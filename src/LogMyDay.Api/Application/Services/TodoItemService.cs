using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class TodoItemService : ITodoItemService
{
    private readonly LogMyDayDbContext _context;
    private readonly IActivityService _activityService;
    private readonly ILogger<TodoItemService> _logger;

    public TodoItemService(LogMyDayDbContext context, IActivityService activityService, ILogger<TodoItemService> logger)
    {
        _context = context;
        _activityService = activityService;
        _logger = logger;
    }

    public async Task<TodoItemResponse> Create(TodoItemRequest request, Guid userId)
    {
        var list = await _context.TodoLists.FirstOrDefaultAsync(l => l.Id == request.ListId && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Todo list not found");
        }

        var item = new Domain.Entities.TodoItem
        {
            ListId = request.ListId,
            Title = request.Title,
            Notes = request.Notes,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            NotifyAt = request.NotifyAt,
            DisplayOrder = request.DisplayOrder,
            DateCreated = DateTime.UtcNow
        };

        _context.TodoItems.Add(item);
        await _context.SaveChangesAsync();

        return MapToResponse(item);
    }

    public async Task Update(int id, TodoItemRequest request, Guid userId)
    {
        var item = await LoadItemForUser(id, userId);

        item.Title = request.Title;
        item.Notes = request.Notes;
        item.StartDate = request.StartDate;
        item.DueDate = request.DueDate;
        item.NotifyAt = request.NotifyAt;
        item.DisplayOrder = request.DisplayOrder;

        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id, Guid userId)
    {
        var item = await LoadItemForUser(id, userId);

        _context.TodoItems.Remove(item);
        await _context.SaveChangesAsync();
    }

    public async Task<TodoItemResponse> Complete(int id, TodoItemCompleteRequest request, Guid userId)
    {
        var item = await _context.TodoItems
            .Include(i => i.List)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Todo item not found");
        }

        item.IsDone = true;
        item.DoneAt = request.DoneAt;

        if (item.List.CompletionTagId.HasValue)
        {
            var activityRequest = new ActivityRequest
            {
                PrimaryTagId = item.List.CompletionTagId.Value,
                Description = item.Title,
                DateStarted = request.DoneAt
            };

            await _activityService.Create(activityRequest, userId);
            _logger.LogInformation("Auto-logged activity for tag {TagId} on todo item {ItemId} completion", item.List.CompletionTagId.Value, id);
        }

        await _context.SaveChangesAsync();

        return MapToResponse(item);
    }

    public async Task<TodoItemResponse> Reopen(int id, Guid userId)
    {
        var item = await LoadItemForUser(id, userId);

        item.IsDone = false;
        item.DoneAt = null;

        await _context.SaveChangesAsync();

        return MapToResponse(item);
    }

    public async Task Reorder(int listId, IList<TodoItemReorderRequest> items, Guid userId)
    {
        var list = await _context.TodoLists.FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Todo list not found");
        }

        var itemIds = items.Select(i => i.Id).ToList();
        var dbItems = await _context.TodoItems
            .Where(i => i.ListId == listId && itemIds.Contains(i.Id))
            .ToListAsync();

        foreach (var dbItem in dbItems)
        {
            var req = items.FirstOrDefault(r => r.Id == dbItem.Id);

            if (req != null)
            {
                dbItem.DisplayOrder = req.DisplayOrder;
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task<Domain.Entities.TodoItem> LoadItemForUser(int id, Guid userId)
    {
        var item = await _context.TodoItems
            .Include(i => i.List)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Todo item not found");
        }

        return item;
    }

    private static TodoItemResponse MapToResponse(Domain.Entities.TodoItem item) =>
        new()
        {
            Id = item.Id,
            ListId = item.ListId,
            Title = item.Title,
            Notes = item.Notes,
            StartDate = item.StartDate,
            DueDate = item.DueDate,
            NotifyAt = item.NotifyAt,
            IsDone = item.IsDone,
            DoneAt = item.DoneAt,
            DisplayOrder = item.DisplayOrder,
            DateCreated = item.DateCreated
        };
}
