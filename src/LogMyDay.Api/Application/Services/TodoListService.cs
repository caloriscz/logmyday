using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class TodoListService : ITodoListService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<TodoListService> _logger;

    public TodoListService(LogMyDayDbContext context, ILogger<TodoListService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IList<TodoListResponse>> GetAll(Guid userId)
    {
        var lists = await _context.TodoLists
            .AsNoTracking()
            .Include(l => l.CompletionTag)
            .Include(l => l.Items)
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.DisplayOrder)
            .ThenBy(l => l.DateCreated)
            .ToListAsync();

        return lists.Select(MapToResponse).ToList();
    }

    public async Task<TodoListResponse> GetById(int id, Guid userId)
    {
        var list = await _context.TodoLists
            .AsNoTracking()
            .Include(l => l.CompletionTag)
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Todo list not found");
        }

        return MapToResponse(list);
    }

    public async Task<TodoListResponse> Create(TodoListRequest request, Guid userId)
    {
        var list = new TodoList
        {
            UserId = userId,
            Name = request.Name,
            CompletionTagId = request.CompletionTagId,
            DisplayOrder = request.DisplayOrder,
            DateCreated = DateTime.UtcNow
        };

        _context.TodoLists.Add(list);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created todo list {ListId} for user {UserId}", list.Id, userId);

        return await GetById(list.Id, userId);
    }

    public async Task Update(int id, TodoListRequest request, Guid userId)
    {
        var list = await _context.TodoLists.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Todo list not found");
        }

        list.Name = request.Name;
        list.CompletionTagId = request.CompletionTagId;
        list.DisplayOrder = request.DisplayOrder;

        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id, Guid userId)
    {
        var list = await _context.TodoLists.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Todo list not found");
        }

        _context.TodoLists.Remove(list);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted todo list {ListId} for user {UserId}", id, userId);
    }

    private static TodoListResponse MapToResponse(TodoList list) =>
        new()
        {
            Id = list.Id,
            Name = list.Name,
            CompletionTagId = list.CompletionTagId,
            CompletionTagName = list.CompletionTag?.TagName,
            CompletionTagIsRepeatable = list.CompletionTag?.IsRepeatable ?? true,
            DisplayOrder = list.DisplayOrder,
            DateCreated = list.DateCreated,
            Items = list.Items
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.DueDate)
                .ThenBy(i => i.DateCreated)
                .Select(MapItemToResponse)
                .ToList()
        };

    private static TodoItemResponse MapItemToResponse(TodoItem item) =>
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
