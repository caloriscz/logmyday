using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogMyDay.App.Mobile.Services;

/// <summary>
/// Schedules and cancels native Android alarms for Reminder-type TodoItems that have a NotifyAt time.
/// Uses TodoItem.Id as the deterministic PendingIntent request code so alarms survive app restarts.
///
/// Maintains an in-memory <see cref="_activeAlarms"/> map so the diagnostic event stream only fires
/// on real state transitions — re-running <see cref="ScheduleAll"/> with the same lists no longer
/// spams the log with redundant cancel/schedule rows.
/// </summary>
public class ReminderNotificationScheduler
{
    private readonly INotificationManagerService _notificationService;
    private readonly IEventLogApi _eventLog;
    private readonly ILogger<ReminderNotificationScheduler> _logger;

    /// <summary>itemId → currently-scheduled fire time (UTC). Source of truth for diag emit.</summary>
    private readonly Dictionary<int, DateTime> _activeAlarms = new();
    private readonly object _lock = new();

    /// <summary>Singleton accessor so the Android <c>AlarmHandler</c> can post a diagnostic
    /// fire-event from the broadcast receiver (which is constructed by the OS, not DI).</summary>
    public static ReminderNotificationScheduler? Instance { get; private set; }

    public ReminderNotificationScheduler(
        INotificationManagerService notificationService,
        IEventLogApi eventLog,
        ILogger<ReminderNotificationScheduler> logger)
    {
        _notificationService = notificationService;
        _eventLog = eventLog;
        _logger = logger;
        Instance = this;
    }

    public void ScheduleAll(IList<TodoListResponse> lists)
    {
        // Track how many items share the same base fire time so we can stagger them.
        var seenFireTimes = new Dictionary<DateTime, int>();

        foreach (var list in lists)
        {
            foreach (var item in list.Items.Where(i => i.NotifyAt.HasValue))
            {
                if (item.IsDone || item.IsSkipped)
                {
                    _notificationService.CancelReminderAlarm(item.Id);

                    bool removed;
                    lock (_lock) { removed = _activeAlarms.Remove(item.Id); }

                    if (removed)
                    {
                        LogDiag($"event=cancelled itemId={item.Id} surface=mobile reason={(item.IsDone ? "done" : "skipped")}");
                    }

                    continue;
                }

                var baseFireTime = CalculateFireTime(item);
                if (baseFireTime == null)
                {
                    _logger.LogDebug("Skipping reminder notification for item {ItemId} '{Title}' — past due or fire time already passed", item.Id, item.Title);
                    continue;
                }

                var count = seenFireTimes.GetValueOrDefault(baseFireTime.Value, 0);
                var adjustedFireTime = baseFireTime.Value.AddSeconds(count * 30);
                seenFireTimes[baseFireTime.Value] = count + 1;

                ScheduleItemAt(item, list.Name, adjustedFireTime);
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

        ScheduleItemAt(item, listName, fireTime.Value);
    }

    private void ScheduleItemAt(TodoItemResponse item, string listName, DateTime fireTimeUtc)
    {
        // Cancel any existing alarm for this item before rescheduling (idempotent at the OS level).
        _notificationService.CancelReminderAlarm(item.Id);

        var payload = new NotificationPayload
        {
            NotificationId = item.Id,
            TodoItemId = item.Id,
            AutoComplete = true
        };

        _notificationService.SendNotification(item.Title, $"from {listName}", fireTimeUtc, payload);

        bool changed;
        lock (_lock)
        {
            changed = !_activeAlarms.TryGetValue(item.Id, out var existing) || existing != fireTimeUtc;
            _activeAlarms[item.Id] = fireTimeUtc;
        }

        _logger.LogDebug("Scheduled reminder notification for item {ItemId} '{Title}' at {FireTime:HH:mm} UTC", item.Id, item.Title, fireTimeUtc);

        if (changed)
        {
            LogDiag($"event=scheduled itemId={item.Id} surface=mobile fireAtUtc={fireTimeUtc:o} notifyAt={item.NotifyAt?.ToString("HH:mm")} title=\"{item.Title}\"");
        }
    }

    public void CancelItem(int todoItemId)
    {
        _notificationService.CancelReminderAlarm(todoItemId);

        bool removed;
        lock (_lock) { removed = _activeAlarms.Remove(todoItemId); }

        _logger.LogDebug("Cancelled reminder notification for item {ItemId}", todoItemId);

        if (removed)
        {
            LogDiag($"event=cancelled itemId={todoItemId} surface=mobile reason=explicit");
        }
    }

    public void DismissItem(int todoItemId)
    {
        _notificationService.DismissReminderNotification(todoItemId);
    }

    /// <summary>Called from <c>AlarmHandler.OnReceive</c> when the OS delivers a reminder alarm.</summary>
    public void LogFired(int todoItemId)
    {
        lock (_lock) { _activeAlarms.Remove(todoItemId); }

        LogDiag($"event=fired itemId={todoItemId} surface=mobile firedAt={DateTime.UtcNow:o}");
    }

    private void LogDiag(string body)
    {
        var message = $"[reminder-diag] {body}";

        _ = Task.Run(async () =>
        {
            try
            {
                await _eventLog.LogEvent(new EventLogRequest { Level = "Info", Message = message });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to post reminder-diag event: {Message}", message);
            }
        });
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
