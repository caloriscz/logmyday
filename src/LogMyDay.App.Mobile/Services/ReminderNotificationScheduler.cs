using LogMyDay.App.Mobile.Services.Diagnostics;
using LogMyDay.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

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
    private readonly IDiagnosticStore _diag;
    private readonly ILogger<ReminderNotificationScheduler> _logger;

    /// <summary>itemId → currently-scheduled fire time (UTC). Source of truth for diag emit.</summary>
    private readonly Dictionary<int, DateTime> _activeAlarms = new();
    private readonly object _lock = new();

    /// <summary>Every TodoItem.Id this device has ever scheduled an alarm for (persistent across
    /// restarts via <see cref="Preferences"/>). The ghost-alarm sweep walks this set and cancels
    /// any code that isn't in the current live reminder set — catches orphans left over from
    /// older builds (e.g., the removed tag-notification path's <c>NudgeInterval=15min</c> alarms).</summary>
    private readonly HashSet<int> _everScheduledIds = new();

    private const string EverScheduledIdsPrefsKey = "reminder-diag.ever-scheduled-ids";

    /// <summary>Singleton accessor so the Android <c>AlarmHandler</c> can post a diagnostic
    /// fire-event from the broadcast receiver (which is constructed by the OS, not DI).</summary>
    public static ReminderNotificationScheduler? Instance { get; private set; }

    public ReminderNotificationScheduler(
        INotificationManagerService notificationService,
        IDiagnosticStore diag,
        ILogger<ReminderNotificationScheduler> logger)
    {
        _notificationService = notificationService;
        _diag = diag;
        _logger = logger;
        Instance = this;
        LoadEverScheduledIds();
    }

    private void LoadEverScheduledIds()
    {
        var raw = Preferences.Get(EverScheduledIdsPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, out var id))
            {
                _everScheduledIds.Add(id);
            }
        }
    }

    private void SaveEverScheduledIds()
    {
        Preferences.Set(EverScheduledIdsPrefsKey, string.Join(",", _everScheduledIds));
    }

    private void RememberScheduledId(int id)
    {
        lock (_lock)
        {
            if (_everScheduledIds.Add(id))
            {
                SaveEverScheduledIds();
            }
        }
    }

    public void ScheduleAll(IList<ReminderResponse> reminders, string trigger = "app")
    {
        var seenFireTimes = new Dictionary<DateTime, int>();
        var armed = 0;

        foreach (var item in reminders.Where(i => i.NotifyAt.HasValue))
        {
            if (item.IsDone || item.IsSkipped)
            {
                _notificationService.CancelReminderAlarm(item.Id);
                ArmedAlarmStore.Remove(item.Id);

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

            ScheduleItemAt(item, adjustedFireTime);
            armed++;
        }

        // Heartbeat — lets a miss-day be classified: an "armed count=N" row means alarms existed
        // that day; its absence means nothing was ever scheduled (app never opened / reboot).
        LogDiag($"event=armed surface=mobile count={armed} trigger={trigger}");
    }

    public void ScheduleItem(ReminderResponse item)
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

        ScheduleItemAt(item, fireTime.Value);
    }

    private void ScheduleItemAt(ReminderResponse item, DateTime fireTimeUtc)
    {
        _notificationService.CancelReminderAlarm(item.Id);

        var payload = new NotificationPayload
        {
            NotificationId = item.Id,
            TodoItemId = item.Id,
            AutoComplete = true
        };

        _notificationService.SendNotification(item.Title, item.Notes ?? string.Empty, fireTimeUtc, payload);

        bool changed;
        lock (_lock)
        {
            changed = !_activeAlarms.TryGetValue(item.Id, out var existing) || existing != fireTimeUtc;
            _activeAlarms[item.Id] = fireTimeUtc;
        }

        RememberScheduledId(item.Id);
        // Durable snapshot so a reboot can re-arm this alarm without credentials or network.
        ArmedAlarmStore.Upsert(new ArmedAlarm(item.Id, fireTimeUtc, item.Title, item.Notes));

        _logger.LogDebug("Scheduled reminder notification for item {ItemId} '{Title}' at {FireTime:HH:mm} UTC", item.Id, item.Title, fireTimeUtc);

        if (changed)
        {
            LogDiag($"event=scheduled itemId={item.Id} surface=mobile fireAtUtc={fireTimeUtc:o} notifyAt={item.NotifyAt?.ToString("HH:mm")} title=\"{item.Title}\"");
        }
    }

    /// <summary>
    /// Cancel any PendingIntent that was ever scheduled by this device but isn't in the live
    /// reminder set. Returns the count cancelled. Idempotent — re-running with the same liveIds
    /// is a no-op after the first call.
    ///
    /// This catches alarms scheduled by older builds (e.g., the removed tag-notification path
    /// with its 15-min nudge interval) that the current cancellation code never touches because
    /// it only cancels by current TodoItemId. Symptom: notifications firing ~15 min after a
    /// reminder was checked or deleted.
    /// </summary>
    public int SweepGhostAlarms(IReadOnlyCollection<int> liveIds)
    {
        int[] ghostIds;

        lock (_lock)
        {
            ghostIds = _everScheduledIds.Where(id => !liveIds.Contains(id)).ToArray();
        }

        if (ghostIds.Length == 0)
        {
            return 0;
        }

        foreach (var id in ghostIds)
        {
            _notificationService.CancelReminderAlarm(id);
            ArmedAlarmStore.Remove(id);
            LogDiag($"event=ghost-cancel itemId={id} surface=mobile reason=stale");
        }

        lock (_lock)
        {
            foreach (var id in ghostIds)
            {
                _everScheduledIds.Remove(id);
                _activeAlarms.Remove(id);
            }
            SaveEverScheduledIds();
        }

        return ghostIds.Length;
    }

    public void CancelItem(int todoItemId)
    {
        _notificationService.CancelReminderAlarm(todoItemId);
        ArmedAlarmStore.Remove(todoItemId);

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
        ArmedAlarmStore.Remove(todoItemId);

        LogDiag($"event=fired itemId={todoItemId} surface=mobile firedAt={DateTime.UtcNow:o}");
    }

    private void LogDiag(string body)
    {
        // Local-first, durable, synchronous. The outbox (DiagnosticStore.FlushAsync) syncs to the
        // server later — so events survive Doze / app-kill that a fire-and-forget HTTP POST loses.
        _diag.Record("reminder-diag", body);
    }

    private static DateTime? CalculateFireTime(ReminderResponse item)
    {
        var notifyAt = item.NotifyAt!.Value;

        // Reminders don't carry DueDate — fire today at the specified time.
        var localFireTime = DateTime.Today + notifyAt.ToTimeSpan();

        var fireTimeUtc = DateTime.SpecifyKind(localFireTime, DateTimeKind.Local).ToUniversalTime();

        // Already passed — caller will log and skip.
        if (fireTimeUtc <= DateTime.UtcNow)
        {
            return null;
        }

        return fireTimeUtc;
    }
}
