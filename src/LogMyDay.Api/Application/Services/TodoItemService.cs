using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// Basic todo items only. Reminder items moved to <see cref="ReminderService"/> in the
/// 2026-05-24 entity split; Reminder-only fields (MonitorDaysBack/From/To, AllowUnfilled)
/// were dropped from <see cref="Domain.Entities.TodoItem"/>.
/// </summary>
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
        var list = await _context.TodoLists
            .FirstOrDefaultAsync(l => l.Id == request.ListId && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Todo list not found");
        }

        Domain.Entities.Tag? completionTag = null;
        if (request.CompletionTagId.HasValue)
        {
            completionTag = await _context.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.CompletionTagId.Value);
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
            RecurrenceType = request.RecurrenceType,
            AutoLogMode = request.AutoLogMode,
            CompletionTagId = request.CompletionTagId,
            DateCreated = DateTime.UtcNow
        };

        _context.TodoItems.Add(item);
        await _context.SaveChangesAsync();

        item.CompletionTag = completionTag;

        return MapToResponse(item);
    }

    public async Task Update(int id, TodoItemRequest request, Guid userId)
    {
        var item = await _context.TodoItems
            .Include(i => i.List)
            .Include(i => i.CompletionTag)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Todo item not found");
        }

        item.Title = request.Title;
        item.Notes = request.Notes;
        item.StartDate = request.StartDate;
        item.DueDate = request.DueDate;
        item.NotifyAt = request.NotifyAt;
        item.DisplayOrder = request.DisplayOrder;
        item.RecurrenceType = request.RecurrenceType;
        item.AutoLogMode = request.AutoLogMode;
        item.CompletionTagId = request.CompletionTagId;

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
            .Include(i => i.CompletionTag)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Todo item not found");
        }

        item.IsDone = true;
        item.DoneAt = request.DoneAt;

        if (item.CompletionTagId.HasValue)
        {
            if (item.AutoLogMode == AutoLogMode.ResetIfExists)
            {
                var (windowStart, windowEnd) = ComputeMonitoringWindow(item);

                var existing = await _context.Activities
                    .FirstOrDefaultAsync(a =>
                        a.TagId == item.CompletionTagId.Value &&
                        a.UserId == userId &&
                        a.DateStarted >= windowStart &&
                        a.DateStarted < windowEnd);

                if (existing != null)
                {
                    existing.DateStarted = request.DoneAt;
                    existing.Description = item.Title;
                    _logger.LogInformation("Reset activity {ActivityId} for tag {TagId} on todo item {ItemId} completion", existing.Id, item.CompletionTagId.Value, id);
                }
                else
                {
                    await LogActivityAsync(item, request.DoneAt, userId);
                }
            }
            else
            {
                await LogActivityAsync(item, request.DoneAt, userId);
            }
        }

        await _context.SaveChangesAsync();

        return MapToResponse(item);
    }

    private async Task LogActivityAsync(Domain.Entities.TodoItem item, DateTime doneAt, Guid userId)
    {
        var activityRequest = new ActivityRequest
        {
            PrimaryTagId = item.CompletionTagId!.Value,
            Description = item.Title,
            DateStarted = doneAt
        };

        await _activityService.Create(activityRequest, userId);
        _logger.LogInformation("Auto-logged activity for tag {TagId} on todo item {ItemId} completion", item.CompletionTagId.Value, item.Id);
    }

    /// <summary>Window for <c>AutoLogMode.ResetIfExists</c>. Basic items don't carry the
    /// Reminder-style explicit Monitor* fields, so the window is derived from RecurrenceType
    /// only: Weekly → current Monday-anchored week; everything else → today.</summary>
    private static (DateTime Start, DateTime End) ComputeMonitoringWindow(Domain.Entities.TodoItem item)
    {
        var todayUtc = DateTime.UtcNow.Date;

        if (item.RecurrenceType == RecurrenceType.Weekly)
        {
            var daysFromMonday = ((int)todayUtc.DayOfWeek + 6) % 7;
            var weekStart = todayUtc.AddDays(-daysFromMonday);

            return (weekStart, weekStart.AddDays(7));
        }

        return (todayUtc, todayUtc.AddDays(1));
    }

    public async Task<TodoItemResponse> Reopen(int id, Guid userId)
    {
        var item = await LoadItemForUser(id, userId);

        item.IsDone = false;
        item.DoneAt = null;

        await _context.SaveChangesAsync();

        return MapToResponse(item);
    }

    public async Task<TodoItemResponse> Skip(int id, Guid userId, DateOnly? date = null)
    {
        var item = await LoadItemForUser(id, userId);

        if (date.HasValue)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "UTC");
            }
            catch (TimeZoneNotFoundException)
            {
                tz = TimeZoneInfo.Utc;
            }

            var localMidnight = date.Value.ToDateTime(TimeOnly.MinValue);
            item.SkippedAt = TimeZoneInfo.ConvertTimeToUtc(localMidnight, tz);
        }
        else
        {
            item.SkippedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return MapToResponse(item);
    }

    public async Task<TodoItemResponse> Unskip(int id, Guid userId)
    {
        var item = await LoadItemForUser(id, userId);

        item.SkippedAt = null;

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
            IsSkipped = item.SkippedAt != null && item.RecurrenceType != RecurrenceType.None,
            DisplayOrder = item.DisplayOrder,
            DateCreated = item.DateCreated,
            RecurrenceType = item.RecurrenceType,
            AutoLogMode = item.AutoLogMode,
            CompletionTagId = item.CompletionTagId,
            CompletionTagName = item.CompletionTag?.TagName,
            CompletionTagInputTypeId = item.CompletionTag?.InputTypeId
        };
}
