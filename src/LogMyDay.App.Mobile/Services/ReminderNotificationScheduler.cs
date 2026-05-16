using LogMyDay.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace LogMyDay.App.Mobile.Services;

/// <summary>
/// Schedules and cancels native Android alarms for Reminder-type TodoItems that have a NotifyAt time.
/// Uses TodoItem.Id as the deterministic PendingIntent request code so alarms survive app restarts.
/// </summary>
public class ReminderNotificationScheduler
{
    private readonly INotificationManagerService _notificationService;
    private readonly ILogger<ReminderNotificationScheduler> _logger;

    public ReminderNotificationScheduler(
        INotificationManagerService notificationService,
        ILogger<ReminderNotificationScheduler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public void ScheduleAll(IList<TodoListResponse> lists)
    {
        foreach (var list in lists)
        {
            foreach (var item in list.Items.Where(i => !i.IsDone && !i.IsSkipped && i.NotifyAt.HasValue))
            {
                ScheduleItem(item, list.Name);
            }
        }
    }

    public void ScheduleItem(TodoItemResponse item, string listName)
    {
        if (item.IsDone || item.IsSkipped || !item.NotifyAt.HasValue)
        {
            return;
        }

        var fireTime = CalculateFireTime(item);

        if (fireTime == null)
        {
            _logger.LogDebug("Skipping reminder notification for item {ItemId} '{Title}' — past due or fire time already passed", item.Id, item.Title);

            return;
        }

        // Cancel any existing alarm for this item before rescheduling (idempotent).
        _notificationService.CancelReminderAlarm(item.Id);

        var payload = new NotificationPayload
        {
            NotificationId = item.Id,
            TodoItemId = item.Id
        };

        _notificationService.SendNotification(item.Title, $"from {listName}", fireTime.Value, payload);

        _logger.LogDebug("Scheduled reminder notification for item {ItemId} '{Title}' at {FireTime:HH:mm} UTC", item.Id, item.Title, fireTime.Value);
    }

    public void CancelItem(int todoItemId)
    {
        _notificationService.CancelReminderAlarm(todoItemId);
        _logger.LogDebug("Cancelled reminder notification for item {ItemId}", todoItemId);
    }

    public void DismissItem(int todoItemId)
    {
        _notificationService.DismissReminderNotification(todoItemId);
    }

    private static DateTime? CalculateFireTime(TodoItemResponse item)
    {
        var notifyAt = item.NotifyAt!.Value;

        DateTime localFireTime;

        if (item.DueDate.HasValue)
        {
            // Past-due items don't get a notification.
            if (item.DueDate.Value.Date < DateTime.Today)
            {
                return null;
            }

            localFireTime = item.DueDate.Value.Date + notifyAt.ToTimeSpan();
        }
        else
        {
            // No due date: fire today at the specified time.
            localFireTime = DateTime.Today + notifyAt.ToTimeSpan();
        }

        var fireTimeUtc = DateTime.SpecifyKind(localFireTime, DateTimeKind.Local).ToUniversalTime();

        // Already passed — caller will log and skip.
        if (fireTimeUtc <= DateTime.UtcNow)
        {
            return null;
        }

        return fireTimeUtc;
    }
}
