using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
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

    public async Task<ReminderResponse> Create(ReminderRequest request, Guid userId)
    {
        var list = await _context.ReminderLists
            .FirstOrDefaultAsync(l => l.Id == request.ReminderListId && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Reminder list not found");
        }

        Domain.Entities.Tag? completionTag = null;
        if (request.CompletionTagId.HasValue)
        {
            completionTag = await _context.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.CompletionTagId.Value);
        }

        var item = new Domain.Entities.Reminder
        {
            ReminderListId = request.ReminderListId,
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

        return ReminderListService.MapItemToResponse(item, null);
    }

    public async Task Update(int id, ReminderRequest request, Guid userId)
    {
        var item = await _context.Reminders
            .Include(i => i.List)
            .Include(i => i.CompletionTag)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

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
            .Include(i => i.List)
            .Include(i => i.CompletionTag)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Reminder not found");
        }

        item.IsDone = true;
        item.DoneAt = request.DoneAt;

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

        await _context.SaveChangesAsync();

        return ReminderListService.MapItemToResponse(item, null);
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
        item.IsDone = false;
        item.DoneAt = null;
        await _context.SaveChangesAsync();
        return ReminderListService.MapItemToResponse(item, null);
    }

    public async Task<ReminderResponse> Skip(int id, Guid userId, DateOnly? date = null)
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

        _logger.LogInformation(
            "[reminder-diag] event=skip reminderId={ItemId} userId={UserId} skippedAt={SkippedAt:o} recurrence={Recurrence}",
            item.Id, userId, item.SkippedAt, item.RecurrenceType);

        return ReminderListService.MapItemToResponse(item, null);
    }

    public async Task<ReminderResponse> Unskip(int id, Guid userId)
    {
        var item = await LoadItemForUser(id, userId);
        item.SkippedAt = null;
        await _context.SaveChangesAsync();
        return ReminderListService.MapItemToResponse(item, null);
    }

    public async Task Reorder(int listId, IList<ReminderReorderRequest> items, Guid userId)
    {
        var list = await _context.ReminderLists.FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId);

        if (list == null)
        {
            throw new KeyNotFoundException("Reminder list not found");
        }

        var itemIds = items.Select(i => i.Id).ToList();
        var dbItems = await _context.Reminders
            .Where(i => i.ReminderListId == listId && itemIds.Contains(i.Id))
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
            .Include(i => i.List)
            .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

        if (item == null)
        {
            throw new KeyNotFoundException("Reminder not found");
        }

        return item;
    }
}
