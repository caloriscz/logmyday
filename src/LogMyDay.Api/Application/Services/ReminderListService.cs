using System.Globalization;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class ReminderListService : IReminderListService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<ReminderListService> _logger;

    public ReminderListService(LogMyDayDbContext context, ILogger<ReminderListService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IList<ReminderListResponse>> GetAll(Guid userId, DateOnly? date = null)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        var lists = await _context.ReminderLists
            .AsNoTracking()
            .Include(l => l.Items)
            .ThenInclude(i => i.CompletionTag)
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.DisplayOrder)
            .ThenBy(l => l.DateCreated)
            .ToListAsync();

        var lockedTagIds = await GetLockedTagIds(userId, date, user);

        return lists.Select(l => MapToResponse(l, user, date, lockedTagIds)).ToList();
    }

    public async Task<ReminderListResponse> GetById(int id, Guid userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        var list = await _context.ReminderLists
            .AsNoTracking()
            .Include(l => l.Items)
            .ThenInclude(i => i.CompletionTag)
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Reminder list not found");
        }

        var lockedTagIds = await GetLockedTagIds(userId, null, user);

        return MapToResponse(list, user, null, lockedTagIds);
    }

    /// <summary>Locked tag IDs for the reference date (user TZ today if date is null).
    /// Reminder skip logic treats a locked tag as "this reminder is finished for the day".</summary>
    private async Task<HashSet<int>> GetLockedTagIds(Guid userId, DateOnly? date, User? user)
    {
        var referenceDate = date ?? DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ResolveTimeZone(user)));

        return await _context.TagDayLocks
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.Date == referenceDate && l.IsLocked)
            .Select(l => l.TagId)
            .ToHashSetAsync();
    }

    public async Task<ReminderListResponse> Create(ReminderListRequest request, Guid userId)
    {
        var list = new ReminderList
        {
            UserId = userId,
            Name = request.Name,
            DisplayOrder = request.DisplayOrder,
            ShowOnHomepage = request.ShowOnHomepage,
            DateCreated = DateTime.UtcNow
        };

        _context.ReminderLists.Add(list);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created reminder list {ListId} for user {UserId}", list.Id, userId);

        return await GetById(list.Id, userId);
    }

    public async Task Update(int id, ReminderListRequest request, Guid userId)
    {
        var list = await _context.ReminderLists.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Reminder list not found");
        }

        list.Name = request.Name;
        list.DisplayOrder = request.DisplayOrder;
        list.ShowOnHomepage = request.ShowOnHomepage;

        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id, Guid userId)
    {
        var list = await _context.ReminderLists.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Reminder list not found");
        }

        _context.ReminderLists.Remove(list);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted reminder list {ListId} for user {UserId}", id, userId);
    }

    internal static ReminderListResponse MapToResponse(ReminderList list, User? user, DateOnly? date = null, HashSet<int>? lockedTagIds = null) =>
        new()
        {
            Id = list.Id,
            Name = list.Name,
            DisplayOrder = list.DisplayOrder,
            ShowOnHomepage = list.ShowOnHomepage,
            DateCreated = list.DateCreated,
            Items = list.Items
                .OrderBy(i => i.NotifyAt ?? TimeOnly.MaxValue)
                .ThenBy(i => i.DisplayOrder)
                .ThenBy(i => i.DateCreated)
                .Select(i => MapItemToResponse(i, user, date, lockedTagIds))
                .ToList()
        };

    internal static ReminderResponse MapItemToResponse(Reminder item, User? user, DateOnly? date = null, HashSet<int>? lockedTagIds = null)
    {
        var isTagLocked = item.CompletionTagId.HasValue
            && lockedTagIds != null
            && lockedTagIds.Contains(item.CompletionTagId.Value);

        return new ReminderResponse
        {
            Id = item.Id,
            ReminderListId = item.ReminderListId,
            Title = item.Title,
            Notes = item.Notes,
            NotifyAt = item.NotifyAt,
            IsDone = ComputeEffectiveIsDone(item, user, date),
            DoneAt = item.DoneAt,
            // TagDayLock takes precedence over SkippedAt — when the tag is locked for the
            // referenced date, the reminder is effectively skipped (sticky for the day).
            IsSkipped = isTagLocked || ComputeEffectiveIsSkipped(item, user, date),
            IsTagDayLocked = isTagLocked,
            DisplayOrder = item.DisplayOrder,
            DateCreated = item.DateCreated,
            RecurrenceType = item.RecurrenceType,
            AutoLogMode = item.AutoLogMode,
            MonitorDaysBack = item.MonitorDaysBack,
            MonitorFromDate = item.MonitorFromDate,
            MonitorToDate = item.MonitorToDate,
            CompletionTagId = item.CompletionTagId,
            CompletionTagName = item.CompletionTag?.TagName,
            CompletionTagInputTypeId = item.CompletionTag?.InputTypeId,
            AllowUnfilled = item.AllowUnfilled
        };
    }

    private static bool ComputeEffectiveIsSkipped(Reminder item, User? user, DateOnly? date = null)
    {
        if (item.SkippedAt == null || item.RecurrenceType == RecurrenceType.None)
        {
            return false;
        }

        var tz = ResolveTimeZone(user);
        var skippedLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(item.SkippedAt.Value, tz));
        var referenceDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));

        if (item.RecurrenceType == RecurrenceType.Daily)
        {
            return skippedLocalDate == referenceDate;
        }

        if (item.RecurrenceType == RecurrenceType.Weekly)
        {
            var firstDay = ResolveFirstDayOfWeek(user);
            return GetWeekStart(skippedLocalDate, firstDay) == GetWeekStart(referenceDate, firstDay);
        }

        return false;
    }

    private static bool ComputeEffectiveIsDone(Reminder item, User? user, DateOnly? date = null)
    {
        if (!item.IsDone || item.DoneAt == null || item.RecurrenceType == RecurrenceType.None)
        {
            return item.IsDone;
        }

        var tz = ResolveTimeZone(user);
        var doneLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(item.DoneAt.Value, tz));
        var referenceDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));

        if (item.RecurrenceType == RecurrenceType.Daily)
        {
            return doneLocalDate == referenceDate;
        }

        if (item.RecurrenceType == RecurrenceType.Weekly)
        {
            var firstDay = ResolveFirstDayOfWeek(user);
            return GetWeekStart(doneLocalDate, firstDay) == GetWeekStart(referenceDate, firstDay);
        }

        return item.IsDone;
    }

    private static TimeZoneInfo ResolveTimeZone(User? user)
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

    private static DayOfWeek ResolveFirstDayOfWeek(User? user)
    {
        try
        {
            return new CultureInfo(user?.Culture ?? "en-US").DateTimeFormat.FirstDayOfWeek;
        }
        catch (CultureNotFoundException)
        {
            return DayOfWeek.Monday;
        }
    }

    private static DateOnly GetWeekStart(DateOnly date, DayOfWeek firstDay)
    {
        var diff = ((int)date.DayOfWeek - (int)firstDay + 7) % 7;
        return date.AddDays(-diff);
    }
}
