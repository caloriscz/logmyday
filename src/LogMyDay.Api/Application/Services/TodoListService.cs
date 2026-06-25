using System.Globalization;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class TodoListService : ITodoListService
{
    private readonly LogMyDayDbContext _context;
    private readonly IEventLogService _eventLogService;
    private readonly ILogger<TodoListService> _logger;

    public TodoListService(LogMyDayDbContext context, IEventLogService eventLogService, ILogger<TodoListService> logger)
    {
        _context = context;
        _eventLogService = eventLogService;
        _logger = logger;
    }

    public async Task<IList<TodoListResponse>> GetAll(Guid userId, DateOnly? date = null)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        var lists = await _context.TodoLists
            .AsNoTracking()
            .Include(l => l.CompletionTag)
            .Include(l => l.Items)
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.DisplayOrder)
            .ThenBy(l => l.DateCreated)
            .ToListAsync();

        return lists.Select(l => MapToResponse(l, user, date)).ToList();
    }

    public async Task<TodoListResponse> GetById(int id, Guid userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        var list = await _context.TodoLists
            .AsNoTracking()
            .Include(l => l.CompletionTag)
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Todo list not found");
        }

        return MapToResponse(list, user);
    }

    public async Task<TodoListResponse> Create(TodoListRequest request, Guid userId)
    {
        var list = new TodoList
        {
            UserId = userId,
            Name = request.Name,
            DisplayOrder = request.DisplayOrder,
            ShowOnHomepage = request.ShowOnHomepage,
            CompletionTagId = request.CompletionTagId,
            AutoLogMode = request.AutoLogMode,
            DateCreated = DateTime.UtcNow
        };

        _context.TodoLists.Add(list);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created todo list {ListId} for user {UserId}", list.Id, userId);

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Todo list '{list.Name}' created (auto-log {list.AutoLogMode}, completionTag {await ResolveTagName(list.CompletionTagId) ?? Describe(list.CompletionTagId)})");

        return await GetById(list.Id, userId);
    }

    public async Task Update(int id, TodoListRequest request, Guid userId)
    {
        var list = await _context.TodoLists.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Todo list not found");
        }

        var oldAutoLogMode = list.AutoLogMode;
        var oldCompletionTagId = list.CompletionTagId;
        var oldName = list.Name;

        list.Name = request.Name;
        list.DisplayOrder = request.DisplayOrder;
        list.ShowOnHomepage = request.ShowOnHomepage;
        list.CompletionTagId = request.CompletionTagId;
        list.AutoLogMode = request.AutoLogMode;

        await _context.SaveChangesAsync();

        // Audit behaviour-affecting config changes to the (synced) event log.
        var changes = new List<string>();
        if (oldAutoLogMode != list.AutoLogMode)
        {
            changes.Add($"auto-log {oldAutoLogMode}→{list.AutoLogMode}");
        }
        if (oldCompletionTagId != list.CompletionTagId)
        {
            var oldTagName = await ResolveTagName(oldCompletionTagId) ?? Describe(oldCompletionTagId);
            var newTagName = await ResolveTagName(list.CompletionTagId) ?? Describe(list.CompletionTagId);
            changes.Add($"completionTag {oldTagName}→{newTagName}");
        }
        if (!string.Equals(oldName, list.Name, StringComparison.Ordinal))
        {
            changes.Add($"name '{oldName}'→'{list.Name}'");
        }

        if (changes.Count > 0)
        {
            await _eventLogService.Log(userId, EventLogLevel.Info,
                $"Todo list '{list.Name}' settings changed: {string.Join("; ", changes)}");
        }
    }

    private async Task<string?> ResolveTagName(int? tagId)
        => tagId.HasValue
            ? await _context.Tags.AsNoTracking().Where(t => t.Id == tagId.Value).Select(t => t.TagName).FirstOrDefaultAsync()
            : null;

    private static string Describe(int? value) => value?.ToString() ?? "none";

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

    private static TodoListResponse MapToResponse(TodoList list, User? user, DateOnly? date = null) =>
        new()
        {
            Id = list.Id,
            Name = list.Name,
            DisplayOrder = list.DisplayOrder,
            ShowOnHomepage = list.ShowOnHomepage,
            DateCreated = list.DateCreated,
            CompletionTagId = list.CompletionTagId,
            CompletionTagName = list.CompletionTag?.TagName,
            CompletionTagInputTypeId = list.CompletionTag?.InputTypeId,
            AutoLogMode = list.AutoLogMode,
            Items = list.Items
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.DueDate)
                .ThenBy(i => i.DateCreated)
                .Select(i => MapItemToResponse(i, list, user, date))
                .ToList()
        };

    // The item inherits its auto-log tag and mode from the parent list, so the completion UI can
    // render the right value prompt without the item carrying its own tag.
    private static TodoItemResponse MapItemToResponse(TodoItem item, TodoList list, User? user, DateOnly? date = null) =>
        new()
        {
            Id = item.Id,
            ListId = item.ListId,
            Title = item.Title,
            Notes = item.Notes,
            StartDate = item.StartDate,
            DueDate = item.DueDate,
            NotifyAt = item.NotifyAt,
            IsDone = ComputeEffectiveIsDone(item, user, date),
            DoneAt = item.DoneAt,
            IsSkipped = ComputeEffectiveIsSkipped(item, user, date),
            DisplayOrder = item.DisplayOrder,
            DateCreated = item.DateCreated,
            RecurrenceType = item.RecurrenceType,
            AutoLogMode = list.AutoLogMode,
            CompletionTagId = list.CompletionTagId,
            CompletionTagName = list.CompletionTag?.TagName,
            CompletionTagInputTypeId = list.CompletionTag?.InputTypeId
        };

    private static bool ComputeEffectiveIsSkipped(TodoItem item, User? user, DateOnly? date = null)
    {
        if (item.SkippedAt == null || item.RecurrenceType == RecurrenceType.None)
        {
            return false;
        }

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
        }

        var skippedLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(item.SkippedAt.Value, tz));
        var referenceDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));

        if (item.RecurrenceType == RecurrenceType.Daily)
        {
            return skippedLocalDate == referenceDate;
        }

        if (item.RecurrenceType == RecurrenceType.Weekly)
        {
            DayOfWeek firstDay;
            try
            {
                firstDay = new CultureInfo(user?.Culture ?? "en-US").DateTimeFormat.FirstDayOfWeek;
            }
            catch (CultureNotFoundException)
            {
                firstDay = DayOfWeek.Monday;
            }

            var skippedWeekStart = GetWeekStart(skippedLocalDate, firstDay);
            var referenceDateWeekStart = GetWeekStart(referenceDate, firstDay);

            return skippedWeekStart == referenceDateWeekStart;
        }

        return false;
    }

    private static bool ComputeEffectiveIsDone(TodoItem item, User? user, DateOnly? date = null)
    {
        if (!item.IsDone || item.DoneAt == null || item.RecurrenceType == RecurrenceType.None)
        {
            return item.IsDone;
        }

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
        }

        var doneLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(item.DoneAt.Value, tz));
        var referenceDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));

        if (item.RecurrenceType == RecurrenceType.Daily)
        {
            return doneLocalDate == referenceDate;
        }

        if (item.RecurrenceType == RecurrenceType.Weekly)
        {
            DayOfWeek firstDay;
            try
            {
                firstDay = new CultureInfo(user?.Culture ?? "en-US").DateTimeFormat.FirstDayOfWeek;
            }
            catch (CultureNotFoundException)
            {
                firstDay = DayOfWeek.Monday;
            }

            var doneWeekStart = GetWeekStart(doneLocalDate, firstDay);
            var referenceDateWeekStart = GetWeekStart(referenceDate, firstDay);

            return doneWeekStart == referenceDateWeekStart;
        }

        return item.IsDone;
    }

    private static DateOnly GetWeekStart(DateOnly date, DayOfWeek firstDay)
    {
        var diff = ((int)date.DayOfWeek - (int)firstDay + 7) % 7;

        return date.AddDays(-diff);
    }
}
