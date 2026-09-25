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
    private readonly IEventLogService _eventLogService;
    private readonly ILogger<TodoItemService> _logger;

    public TodoItemService(LogMyDayDbContext context, IActivityService activityService, IEventLogService eventLogService, ILogger<TodoItemService> logger)
    {
        _context = context;
        _activityService = activityService;
        _eventLogService = eventLogService;
        _logger = logger;
    }

    public async Task<TodoItemResponse> Create(TodoItemRequest request, Guid userId)
    {
        var list = await _context.TodoLists
            .Include(l => l.CompletionTag)
            .FirstOrDefaultAsync(l => l.Id == request.ListId && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Todo list not found");
        }

        // The auto-log tag/mode live on the list now; the item carries neither.
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
            DateCreated = DateTime.UtcNow
        };

        _context.TodoItems.Add(item);
        await _context.SaveChangesAsync();

        item.List = list;

        return MapToResponse(item);
    }

    public async Task Update(int id, TodoItemRequest request, Guid userId)
    {
        var item = await _context.TodoItems
            .Include(i => i.List)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Todo item not found");
        }

        // Auto-log tag/mode are list-owned; the item request fields for them are ignored.
        item.Title = request.Title;
        item.Notes = request.Notes;
        item.StartDate = request.StartDate;
        item.DueDate = request.DueDate;
        item.NotifyAt = request.NotifyAt;
        item.DisplayOrder = request.DisplayOrder;
        item.RecurrenceType = request.RecurrenceType;

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
            .ThenInclude(l => l.CompletionTag)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Todo item not found");
        }

        item.IsDone = true;
        item.DoneAt = request.DoneAt;

        // DoneAt is UTC; the auto-logged activity is stored in naive local time like every other activity.
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        var doneLocal = ToUserLocalTime(request.DoneAt, user);

        // Auto-log against the parent list's tag, in the list's mode.
        var tagId = item.List.CompletionTagId;
        if (tagId.HasValue)
        {
            await LogActivityAsync(item, tagId.Value, doneLocal, userId);
        }

        await _context.SaveChangesAsync();

        return MapToResponse(item);
    }

    private async Task LogActivityAsync(Domain.Entities.TodoItem item, int tagId, DateTime doneAt, Guid userId)
    {
        var activityRequest = new ActivityRequest
        {
            PrimaryTagId = tagId,
            Description = item.Title,
            DateStarted = doneAt
        };

        // ResetIfExists replaces the entry of the completion's own local day only, so each day
        // keeps its own activity. It goes through ActivityService so it gets the same validation
        // and event log as any other write.
        if (item.List.AutoLogMode == AutoLogMode.ResetIfExists)
        {
            await _activityService.ReplaceForDay(activityRequest, userId);
        }
        else
        {
            await _activityService.Create(activityRequest, userId);
        }

        _logger.LogInformation("Auto-logged activity for tag {TagId} on todo item {ItemId} completion ({Mode})", tagId, item.Id, item.List.AutoLogMode);
    }

    /// <summary>A completion's DoneAt is UTC (an unspecified kind is read as UTC). Activities are
    /// stored in naive local time in the user's time zone, so the auto-logged row and the
    /// <c>AutoLogMode.ResetIfExists</c> same-day window use this local value.</summary>
    private static DateTime ToUserLocalTime(DateTime doneAt, Domain.Entities.User? user)
    {
        var utc = doneAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(doneAt, DateTimeKind.Utc)
            : doneAt.ToUniversalTime();

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, ResolveTimeZone(user)), DateTimeKind.Unspecified);
    }

    private static TimeZoneInfo ResolveTimeZone(Domain.Entities.User? user)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
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
            .ThenInclude(l => l.CompletionTag)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Todo item not found");
        }

        return item;
    }

    // Auto-log tag and mode are inherited from the parent list; callers that return this must load
    // item.List (and List.CompletionTag).
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
            AutoLogMode = item.List?.AutoLogMode ?? AutoLogMode.Add,
            CompletionTagId = item.List?.CompletionTagId,
            CompletionTagName = item.List?.CompletionTag?.TagName,
            CompletionTagInputTypeId = item.List?.CompletionTag?.InputTypeId
        };
}
