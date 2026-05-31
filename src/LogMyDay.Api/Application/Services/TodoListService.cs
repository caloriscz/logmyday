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
    private readonly ILogger<TodoListService> _logger;

    public TodoListService(LogMyDayDbContext context, ILogger<TodoListService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IList<TodoListResponse>> GetAll(Guid userId, DateOnly? date = null)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        var lists = await _context.TodoLists
            .AsNoTracking()
            .Include(l => l.Items)
            .ThenInclude(i => i.CompletionTag)
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
            .Include(l => l.Items)
            .ThenInclude(i => i.CompletionTag)
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
        list.DisplayOrder = request.DisplayOrder;
        list.ShowOnHomepage = request.ShowOnHomepage;

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

    private static TodoListResponse MapToResponse(TodoList list, User? user, DateOnly? date = null) =>
        new()
        {
            Id = list.Id,
            Name = list.Name,
            DisplayOrder = list.DisplayOrder,
            ShowOnHomepage = list.ShowOnHomepage,
            DateCreated = list.DateCreated,
            Items = list.Items
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.DueDate)
                .ThenBy(i => i.DateCreated)
                .Select(i => MapItemToResponse(i, user, date))
                .ToList()
        };

    private static TodoItemResponse MapItemToResponse(TodoItem item, User? user, DateOnly? date = null) =>
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
            AutoLogMode = item.AutoLogMode,
            CompletionTagId = item.CompletionTagId,
            CompletionTagName = item.CompletionTag?.TagName,
            CompletionTagInputTypeId = item.CompletionTag?.InputTypeId
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
