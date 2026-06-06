using System.Globalization;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class ReminderService : IReminderService
{
    private readonly LogMyDayDbContext _context;
    private readonly IActivityService _activityService;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(LogMyDayDbContext context, IActivityService activityService, ILogger<ReminderService> logger)
    {
        _context = context;
        _activityService = activityService;
        _logger = logger;
    }

    public async Task<IList<ReminderResponse>> GetAll(Guid userId, DateOnly? date = null)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        var reminders = await _context.Reminders
            .AsNoTracking()
            .Include(i => i.CompletionTag)
            .Where(i => i.UserId == userId)
            .ToListAsync();

        var lockedTagIds = await GetLockedTagIds(userId, date, user);

        var refDate = ReferenceLocalDate(date, user);
        var weekStart = GetWeekStart(refDate, ResolveFirstDayOfWeek(user));
        var reminderIds = reminders.Select(r => r.Id).ToList();

        var dayRows = await _context.ReminderDays
            .AsNoTracking()
            .Where(d => reminderIds.Contains(d.ReminderId) && (d.Date == refDate || d.Date == weekStart))
            .ToListAsync();

        var dayLookup = dayRows.ToDictionary(d => (d.ReminderId, d.Date));

        return reminders
            .OrderBy(i => i.NotifyAt ?? TimeOnly.MaxValue)
            .ThenBy(i => i.DisplayOrder)
            .ThenBy(i => i.DateCreated)
            .Select(i =>
            {
                dayLookup.TryGetValue((i.Id, PeriodDate(i, refDate, user)), out var day);
                return MapItemToResponse(i, day, lockedTagIds);
            })
            .ToList();
    }

    public async Task<ReminderResponse> Create(ReminderRequest request, Guid userId)
    {
        Domain.Entities.Tag? completionTag = null;
        if (request.CompletionTagId.HasValue)
        {
            completionTag = await _context.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.CompletionTagId.Value);
        }

        var item = new Domain.Entities.Reminder
        {
            UserId = userId,
            Title = request.Title,
            Notes = request.Notes,
            NotifyAt = request.NotifyAt,
            DisplayOrder = request.DisplayOrder,
            RecurrenceType = request.RecurrenceType,
            AutoLogMode = request.AutoLogMode,
            MonitorDaysBack = request.MonitorDaysBack,
            MonitorFromDate = request.MonitorFromDate,
            MonitorToDate = request.MonitorToDate,
            CompletionTagId = request.CompletionTagId,
            AllowUnfilled = request.AllowUnfilled,
            DateCreated = DateTime.UtcNow
        };

        _context.Reminders.Add(item);
        await _context.SaveChangesAsync();

        item.CompletionTag = completionTag;

        return MapItemToResponse(item);
    }

    public async Task Update(int id, ReminderRequest request, Guid userId)
    {
        var item = await _context.Reminders
            .Include(i => i.CompletionTag)
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Reminder not found");
        }

        var oldNotifyAt = item.NotifyAt;
        var oldRecurrence = item.RecurrenceType;
        var oldIsDone = item.IsDone;

        item.Title = request.Title;
        item.Notes = request.Notes;
        item.NotifyAt = request.NotifyAt;
        item.DisplayOrder = request.DisplayOrder;
        item.RecurrenceType = request.RecurrenceType;
        item.AutoLogMode = request.AutoLogMode;
        item.MonitorDaysBack = request.MonitorDaysBack;
        item.MonitorFromDate = request.MonitorFromDate;
        item.MonitorToDate = request.MonitorToDate;
        item.CompletionTagId = request.CompletionTagId;
        item.AllowUnfilled = request.AllowUnfilled;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "[reminder-diag] event=update reminderId={ItemId} userId={UserId} oldNotifyAt={OldNotifyAt} newNotifyAt={NewNotifyAt} oldRecurrence={OldRecurrence} newRecurrence={NewRecurrence} oldIsDone={OldIsDone}",
            item.Id, userId,
            oldNotifyAt?.ToString("HH:mm") ?? "null", item.NotifyAt?.ToString("HH:mm") ?? "null",
            oldRecurrence, item.RecurrenceType, oldIsDone);
    }

    public async Task Delete(int id, Guid userId)
    {
        var item = await LoadItemForUser(id, userId);
        _context.Reminders.Remove(item);
        await _context.SaveChangesAsync();
    }

    public async Task<ReminderResponse> Complete(int id, ReminderCompleteRequest request, Guid userId)
    {
        var item = await _context.Reminders
            .Include(i => i.CompletionTag)
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Reminder not found");
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        // Legacy scalars kept in sync for rollback safety; the read path uses the day row.
        item.IsDone = true;
        item.DoneAt = request.DoneAt;

        var doneLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(request.DoneAt, ResolveTimeZone(user)));
        var day = await GetOrCreateDay(item.Id, userId, PeriodDate(item, doneLocalDate, user));
        day.IsDone = true;
        day.DoneAt = request.DoneAt;
        day.IsSkipped = false;
        day.SkippedAt = null;
        day.CompletionValue = request.CompletionValue;

        _logger.LogInformation(
            "[reminder-diag] event=complete reminderId={ItemId} userId={UserId} doneAt={DoneAt:o} notifyAt={NotifyAt} recurrence={Recurrence}",
            item.Id, userId, request.DoneAt, item.NotifyAt?.ToString("HH:mm") ?? "null", item.RecurrenceType);

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
                    existing.Description = request.CompletionValue ?? item.Notes;
                    _logger.LogInformation("Reset activity {ActivityId} for tag {TagId} on reminder {ItemId} completion", existing.Id, item.CompletionTagId.Value, id);
                }
                else
                {
                    await LogActivityAsync(item, request.CompletionValue, request.DoneAt, userId);
                }
            }
            else
            {
                await LogActivityAsync(item, request.CompletionValue, request.DoneAt, userId);
            }
        }

        await PruneOldDays(item, user);
        await _context.SaveChangesAsync();

        return MapItemToResponse(item, day);
    }

    private async Task LogActivityAsync(Domain.Entities.Reminder item, string? completionValue, DateTime doneAt, Guid userId)
    {
        var activityRequest = new ActivityRequest
        {
            PrimaryTagId = item.CompletionTagId!.Value,
            Description = completionValue ?? item.Notes,
            DateStarted = doneAt
        };

        await _activityService.Create(activityRequest, userId);
        _logger.LogInformation("Auto-logged activity for tag {TagId} on reminder {ItemId} completion", item.CompletionTagId.Value, item.Id);
    }

    private static (DateTime Start, DateTime End) ComputeMonitoringWindow(Domain.Entities.Reminder item)
    {
        var todayUtc = DateTime.UtcNow.Date;

        if (item.MonitorDaysBack.HasValue)
        {
            return (todayUtc.AddDays(-item.MonitorDaysBack.Value), todayUtc.AddDays(1));
        }

        if (item.MonitorFromDate.HasValue && item.MonitorToDate.HasValue)
        {
            return (
                item.MonitorFromDate.Value.ToDateTime(TimeOnly.MinValue),
                item.MonitorToDate.Value.ToDateTime(TimeOnly.MinValue).AddDays(1)
            );
        }

        if (item.RecurrenceType == RecurrenceType.Weekly)
        {
            var daysFromMonday = ((int)todayUtc.DayOfWeek + 6) % 7;
            var weekStart = todayUtc.AddDays(-daysFromMonday);
            return (weekStart, weekStart.AddDays(7));
        }

        return (todayUtc, todayUtc.AddDays(1));
    }

    public async Task<ReminderResponse> Reopen(int id, Guid userId)
    {
        var item = await LoadItemForUser(id, userId);
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        item.IsDone = false;
        item.DoneAt = null;

        var periodDate = PeriodDate(item, ReferenceLocalDate(null, user), user);
        var day = await _context.ReminderDays
            .FirstOrDefaultAsync(d => d.ReminderId == id && d.Date == periodDate);
        if (day != null)
        {
            day.IsDone = false;
            day.DoneAt = null;
            day.CompletionValue = null;
        }

        await _context.SaveChangesAsync();

        return MapItemToResponse(item, day);
    }

    public async Task<ReminderResponse> Skip(int id, Guid userId, DateOnly? date = null)
    {
        var item = await LoadItemForUser(id, userId);
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        var skippedAt = DateTime.UtcNow;
        if (date.HasValue)
        {
            var localMidnight = date.Value.ToDateTime(TimeOnly.MinValue);
            item.SkippedAt = TimeZoneInfo.ConvertTimeToUtc(localMidnight, ResolveTimeZone(user));
        }
        else
        {
            item.SkippedAt = skippedAt;
        }

        var day = await GetOrCreateDay(item.Id, userId, PeriodDate(item, ReferenceLocalDate(date, user), user));
        day.IsSkipped = true;
        day.SkippedAt = skippedAt;

        await PruneOldDays(item, user);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "[reminder-diag] event=skip reminderId={ItemId} userId={UserId} skippedAt={SkippedAt:o} recurrence={Recurrence}",
            item.Id, userId, item.SkippedAt, item.RecurrenceType);

        return MapItemToResponse(item, day);
    }

    public async Task<ReminderResponse> Unskip(int id, Guid userId, DateOnly? date = null)
    {
        var item = await LoadItemForUser(id, userId);
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        item.SkippedAt = null;

        var periodDate = PeriodDate(item, ReferenceLocalDate(date, user), user);
        var day = await _context.ReminderDays
            .FirstOrDefaultAsync(d => d.ReminderId == id && d.Date == periodDate);
        if (day != null)
        {
            day.IsSkipped = false;
            day.SkippedAt = null;
        }

        await _context.SaveChangesAsync();

        return MapItemToResponse(item, day);
    }

    public async Task Reorder(IList<ReminderReorderRequest> items, Guid userId)
    {
        var itemIds = items.Select(i => i.Id).ToList();
        var dbItems = await _context.Reminders
            .Where(i => i.UserId == userId && itemIds.Contains(i.Id))
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

    private async Task<Domain.Entities.Reminder> LoadItemForUser(int id, Guid userId)
    {
        var item = await _context.Reminders
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Reminder not found");
        }

        return item;
    }

    private async Task<ReminderDay> GetOrCreateDay(int reminderId, Guid userId, DateOnly date)
    {
        var day = await _context.ReminderDays
            .FirstOrDefaultAsync(d => d.ReminderId == reminderId && d.Date == date);

        if (day == null)
        {
            day = new ReminderDay { ReminderId = reminderId, UserId = userId, Date = date };
            _context.ReminderDays.Add(day);
        }

        return day;
    }

    // Drop ReminderDay rows that have aged out of the reminder's window: its monitoring window when
    // set, otherwise 365 days. Never touches the current period's row.
    private async Task PruneOldDays(Domain.Entities.Reminder item, User? user)
    {
        var today = ReferenceLocalDate(null, user);
        var currentPeriod = PeriodDate(item, today, user);

        var cutoff = item.MonitorDaysBack.HasValue ? today.AddDays(-item.MonitorDaysBack.Value)
            : item.MonitorFromDate ?? today.AddDays(-365);

        if (cutoff > currentPeriod)
        {
            cutoff = currentPeriod;
        }

        var stale = await _context.ReminderDays
            .Where(d => d.ReminderId == item.Id && d.Date < cutoff)
            .ToListAsync();

        if (stale.Count > 0)
        {
            _context.ReminderDays.RemoveRange(stale);
        }
    }

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

    internal static ReminderResponse MapItemToResponse(Domain.Entities.Reminder item, ReminderDay? day = null, HashSet<int>? lockedTagIds = null)
    {
        var isTagLocked = item.CompletionTagId.HasValue
            && lockedTagIds != null
            && lockedTagIds.Contains(item.CompletionTagId.Value);

        // Non-recurring reminders are one-shot: their done state lives on the reminder itself.
        // Recurring reminders read this period's state from its ReminderDay row.
        var isNone = item.RecurrenceType == RecurrenceType.None;
        var isDone = isNone ? item.IsDone : (day?.IsDone ?? false);
        var doneAt = isNone ? item.DoneAt : day?.DoneAt;
        var isSkipped = !isNone && (day?.IsSkipped ?? false);

        return new ReminderResponse
        {
            Id = item.Id,
            Title = item.Title,
            Notes = item.Notes,
            NotifyAt = item.NotifyAt,
            IsDone = isDone,
            DoneAt = doneAt,
            IsSkipped = isTagLocked || isSkipped,
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

    private static DateOnly ReferenceLocalDate(DateOnly? date, User? user)
        => date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ResolveTimeZone(user)));

    // The period day a ReminderDay row is keyed by: the calendar day for Daily/None,
    // the week-start date for Weekly.
    private static DateOnly PeriodDate(Domain.Entities.Reminder item, DateOnly localDate, User? user)
        => item.RecurrenceType == RecurrenceType.Weekly
            ? GetWeekStart(localDate, ResolveFirstDayOfWeek(user))
            : localDate;

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
