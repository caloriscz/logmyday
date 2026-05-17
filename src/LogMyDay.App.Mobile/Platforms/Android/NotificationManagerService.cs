using System;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using AndroidX.Core.App;
using LogMyDay.App.Mobile.Services;
using AndroidX.Core.Content;

namespace LogMyDay.App.Mobile.Platforms.Android;

public class NotificationManagerService : INotificationManagerService
{
    const string channelId = "logmyday_notifications";
    const string channelName = "LogMyDay Notifications";
    const string channelDescription = "Notifications for LogMyDay app.";

    const string periodicChannelId = "logmyday_periodic";
    const string periodicChannelName = "LogMyDay Periodic";
    const string periodicChannelDescription = "Periodic notifications from LogMyDay app.";

    const string reminderChannelId = "logmyday_reminders";
    const string reminderChannelName = "LogMyDay Reminders";
    const string reminderChannelDescription = "Reminder alarm notifications from LogMyDay app.";

    public const string TitleKey = "title";
    public const string MessageKey = "message";
    public const string NotificationIdKey = "notification_id";
    public const string TagIdKey = "tag_id";
    public const string TagNameKey = "tag_name";
    public const string LocalDateKey = "local_date";
    public const string TodoItemIdKey = "todo_item_id";
    public const string AutoCompleteKey = "auto_complete";

    bool channelInitialized = false;
    bool periodicChannelInitialized = false;
    bool reminderChannelInitialized = false;
    int messageId = 0;
    int pendingIntentId = 0;
    static int _notificationCount = 0; // Track periodic notifications

    // Keys: notificationId → list of (requestCode, tagId) for scheduled alarms.
    private readonly Dictionary<int, List<(int requestCode, int tagId)>> _scheduledAlarms = new();

    NotificationManagerCompat? compatManager;

    public event EventHandler<NotificationEventArgs>? NotificationReceived;

    public static NotificationManagerService? Instance { get; private set; }

    public NotificationManagerService()
    {
        System.Diagnostics.Debug.WriteLine("NotificationManagerService constructor called");

        if (Instance == null)
        {
            System.Diagnostics.Debug.WriteLine("Initializing NotificationManagerService instance");
            CreateNotificationChannel();

            if (Platform.AppContext != null)
            {
                compatManager = NotificationManagerCompat.From(Platform.AppContext);
            }

            Instance = this;
        }
    }

    public void SendNotification(string title, string message, DateTime? notifyTime = null, NotificationPayload? payload = null)
    {
        System.Diagnostics.Debug.WriteLine($"Android NotificationManagerService.SendNotification called: {title} - {message}");

        if (!channelInitialized)
        {
            CreateNotificationChannel();
        }

        if (notifyTime != null)
        {
            Intent intent = new Intent(Platform.AppContext, typeof(AlarmHandler));
            intent.PutExtra(TitleKey, title);
            intent.PutExtra(MessageKey, message);
            if (payload != null)
            {
                intent.PutExtra(NotificationIdKey, payload.NotificationId);
                if (payload.TagId.HasValue)
                {
                    intent.PutExtra(TagIdKey, payload.TagId.Value);
                }
                intent.PutExtra(TagNameKey, payload.TagName ?? string.Empty);
                if (payload.LocalDate.HasValue)
                {
                    intent.PutExtra(LocalDateKey, payload.LocalDate.Value.ToString("yyyy-MM-dd"));
                }
                if (payload.TodoItemId.HasValue)
                {
                    intent.PutExtra(TodoItemIdKey, payload.TodoItemId.Value);
                }
            }
            intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                ? PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable
                : PendingIntentFlags.CancelCurrent;

            // Use TodoItemId as a deterministic request code when available so alarms
            // can be cancelled across app restarts without an in-memory lookup.
            int requestCode = payload?.TodoItemId ?? pendingIntentId++;
            PendingIntent? pendingIntent = PendingIntent.GetBroadcast(Platform.AppContext, requestCode, intent, pendingIntentFlags);
            if (pendingIntent != null)
            {
                long triggerTime = GetNotifyTime(notifyTime.Value);
                AlarmManager? alarmManager = Platform.AppContext.GetSystemService(Context.AlarmService) as AlarmManager;
                ScheduleExactAlarm(alarmManager, triggerTime, pendingIntent);

                // Also track in the in-memory map for non-reminder alarms.
                if (payload?.NotificationId is int nid && !payload.TodoItemId.HasValue)
                {
                    var tagId = payload.TagId ?? 0;
                    if (!_scheduledAlarms.TryGetValue(nid, out var list))
                    {
                        list = new List<(int, int)>();
                        _scheduledAlarms[nid] = list;
                    }
                    list.Add((requestCode, tagId));
                }
            }
        }
        else
        {
            Show(title, message, payload);
        }
    }

    public void ReceiveNotification(string title, string message, NotificationPayload? payload = null)
    {
        var args = new NotificationEventArgs()
        {
            Title = title,
            Message = message,
            Payload = payload,
        };
        NotificationReceived?.Invoke(null, args);
    }

    public void StartPeriodicNotifications()
    {
        // This will be handled by the cross-platform NotificationService
    }

    public void StopPeriodicNotifications()
    {
        // This will be handled by the cross-platform NotificationService
    }

    public void CancelAlarmsForNotification(int notificationId)
    {
        if (!_scheduledAlarms.TryGetValue(notificationId, out var alarms))
        {
            return;
        }

        AlarmManager? alarmManager = Platform.AppContext.GetSystemService(Context.AlarmService) as AlarmManager;

        foreach (var (requestCode, _) in alarms)
        {
            var cancelIntent = new Intent(Platform.AppContext, typeof(AlarmHandler));
            var flags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                ? PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable
                : PendingIntentFlags.NoCreate;
            var pending = PendingIntent.GetBroadcast(Platform.AppContext, requestCode, cancelIntent, flags);
            if (pending != null)
            {
                alarmManager?.Cancel(pending);
            }
        }

        _scheduledAlarms.Remove(notificationId);
    }

    public void CancelAlarmsForTag(int tagId)
    {
        var idsForTag = _scheduledAlarms
            .Where(kvp => kvp.Value.Any(a => a.tagId == tagId))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var notificationId in idsForTag)
        {
            CancelAlarmsForNotification(notificationId);
        }
    }

    public void CancelReminderAlarm(int todoItemId)
    {
        AlarmManager? alarmManager = Platform.AppContext.GetSystemService(Context.AlarmService) as AlarmManager;
        var cancelIntent = new Intent(Platform.AppContext, typeof(AlarmHandler));
        var flags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable
            : PendingIntentFlags.NoCreate;
        var pending = PendingIntent.GetBroadcast(Platform.AppContext, todoItemId, cancelIntent, flags);
        if (pending != null)
        {
            alarmManager?.Cancel(pending);
            pending.Cancel();
        }
    }

    public void Show(string title, string message, NotificationPayload? payload = null)
    {
        System.Diagnostics.Debug.WriteLine($"Android NotificationManagerService.Show called: {title} - {message}");

        var context = Platform.AppContext;
        if (context == null || compatManager == null)
        {
            System.Diagnostics.Debug.WriteLine("Context or compatManager is null");
            return;
        }

        // Ensure notification channel is created
        bool isReminderNotification = payload?.TodoItemId.HasValue == true;
        bool isPeriodicNotification = !isReminderNotification && (title?.Contains("Timer") == true || title?.Contains("Periodic") == true);
        string notificationChannelId = isReminderNotification ? reminderChannelId
            : isPeriodicNotification ? periodicChannelId
            : channelId;

        if (isReminderNotification && !reminderChannelInitialized)
        {
            CreateReminderNotificationChannel();
        }
        else if (isPeriodicNotification && !periodicChannelInitialized)
        {
            CreatePeriodicNotificationChannel();
        }
        else if (!isReminderNotification && !isPeriodicNotification && !channelInitialized)
        {
            CreateNotificationChannel();
        }

        Intent intent = new Intent(context, typeof(MainActivity));
        intent.PutExtra(TitleKey, title ?? string.Empty);
        intent.PutExtra(MessageKey, message ?? string.Empty);
        if (payload != null)
        {
            intent.PutExtra(NotificationIdKey, payload.NotificationId);
            if (payload.TagId.HasValue)
            {
                intent.PutExtra(TagIdKey, payload.TagId.Value);
            }
            intent.PutExtra(TagNameKey, payload.TagName ?? string.Empty);
            if (payload.LocalDate.HasValue)
            {
                intent.PutExtra(LocalDateKey, payload.LocalDate.Value.ToString("yyyy-MM-dd"));
            }
            if (payload.TodoItemId.HasValue)
            {
                intent.PutExtra(TodoItemIdKey, payload.TodoItemId.Value);
            }
        }
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        PendingIntent? pendingIntent = PendingIntent.GetActivity(context, pendingIntentId++, intent, pendingIntentFlags);
        if (pendingIntent == null)
            return;

        var builder = new NotificationCompat.Builder(context, notificationChannelId);
        builder.SetContentIntent(pendingIntent);
        builder.SetContentTitle(title ?? "LogMyDay");
        builder.SetContentText(message ?? "Notification");

        // Reminder notifications get high priority (heads-up) + vibration + action button.
        if (isReminderNotification)
        {
            builder.SetPriority(NotificationCompat.PriorityHigh);
            // Vibration is configured on the channel (API 26+); SetVibrate is for API < 26.
            builder.SetVibrate(new long[] { 0, 300, 100, 300 });
            // Give each reminder its own group key so Android never auto-collapses them.
            builder.SetGroup("lmd_reminder_" + payload!.TodoItemId!.Value);
            builder.SetGroupSummary(false);

            // "Mark Done" action — opens the app and auto-completes the item.
            // Wrapped in try-catch: a failure here must not prevent the notification from showing.
            try
            {
                var doneIntent = new Intent(context, typeof(MainActivity));
                doneIntent.PutExtra(TitleKey, title ?? string.Empty);
                doneIntent.PutExtra(MessageKey, message ?? string.Empty);
                doneIntent.PutExtra(NotificationIdKey, payload!.NotificationId);
                doneIntent.PutExtra(TodoItemIdKey, payload.TodoItemId!.Value);
                doneIntent.PutExtra(AutoCompleteKey, true);
                doneIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
                var donePendingIntent = PendingIntent.GetActivity(context, pendingIntentId++, doneIntent, pendingIntentFlags);
                if (donePendingIntent != null)
                {
                    builder.AddAction(0, "Mark Done", donePendingIntent);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Show: failed to create Mark Done action — {ex.Message}");
            }
        }
        else if (isPeriodicNotification)
        {
            builder.SetPriority(NotificationCompat.PriorityHigh);
            builder.SetVibrate(new long[] { 0, 250, 250, 250 }); // Vibration pattern
            builder.SetLights(unchecked((int)0xFF0000FF), 1000, 1000); // Blue LED flash
            System.Diagnostics.Debug.WriteLine("Set high priority for periodic notification");
        }
        else
        {
            builder.SetPriority(NotificationCompat.PriorityDefault);
        }

        // Use the custom notification icon
        var iconResourceId = context.Resources?.GetIdentifier("notification_icon", "drawable", context.PackageName);
        System.Diagnostics.Debug.WriteLine($"Notification icon resource ID: {iconResourceId}");

        if (iconResourceId != null && iconResourceId > 0)
        {
            builder.SetSmallIcon(iconResourceId.Value);
            System.Diagnostics.Debug.WriteLine("Using custom notification icon");
        }
        else
        {
            // Fallback to Android system icon using resource ID
            builder.SetSmallIcon(17301659); // android.R.drawable.ic_dialog_info
            System.Diagnostics.Debug.WriteLine("Using system icon for notification");
        }

        builder.SetAutoCancel(true);

        var notification = builder.Build();

        // Use a deterministic ID for reminder notifications so they can be dismissed programmatically.
        int notificationId;
        if (payload?.TodoItemId.HasValue == true)
        {
            notificationId = payload.TodoItemId.Value;
        }
        else if (title?.Contains("Timer") == true || title?.Contains("Periodic") == true)
        {
            _notificationCount++;
            notificationId = 2000 + _notificationCount; // Use different range for periodic notifications
        }
        else
        {
            notificationId = messageId++;
        }

        System.Diagnostics.Debug.WriteLine($"Built notification, about to notify with ID: {notificationId}");
        compatManager.Notify(notificationId, notification);
        System.Diagnostics.Debug.WriteLine("Notification sent successfully");
    }

    internal static NotificationPayload? BuildPayloadFromIntent(Intent intent)
    {
        if (!intent.HasExtra(NotificationIdKey))
        {
            return null;
        }

        var notificationId = intent.GetIntExtra(NotificationIdKey, -1);
        if (notificationId < 0)
        {
            return null;
        }

        int? tagId = null;
        if (intent.HasExtra(TagIdKey))
        {
            tagId = intent.GetIntExtra(TagIdKey, 0);
        }

        string? tagName = intent.GetStringExtra(TagNameKey);
        var localDateRaw = intent.GetStringExtra(LocalDateKey);
        DateOnly? localDate = null;
        if (!string.IsNullOrWhiteSpace(localDateRaw) && DateOnly.TryParse(localDateRaw, out var parsed))
        {
            localDate = parsed;
        }

        int? todoItemId = null;
        if (intent.HasExtra(TodoItemIdKey))
        {
            var raw = intent.GetIntExtra(TodoItemIdKey, -1);
            if (raw >= 0)
            {
                todoItemId = raw;
            }
        }

        return new NotificationPayload
        {
            NotificationId = notificationId,
            TagId = tagId,
            TagName = string.IsNullOrWhiteSpace(tagName) ? null : tagName,
            LocalDate = localDate,
            TodoItemId = todoItemId,
            AutoComplete = intent.GetBooleanExtra(AutoCompleteKey, false)
        };
    }

    void CreateNotificationChannel()
    {
        // Create the notification channel, but only on API 26+.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            var channelNameJava = new Java.Lang.String(channelName);
            var channel = new NotificationChannel(channelId, channelNameJava, NotificationImportance.Default)
            {
                Description = channelDescription
            };
            // Register the channel
            NotificationManager? manager = (NotificationManager?)Platform.AppContext?.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);
#pragma warning restore CA1416
            System.Diagnostics.Debug.WriteLine("Notification channel created for API 26+");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Notification channel not needed for API < 26");
        }

        // Mark as initialized regardless of API level
        channelInitialized = true;
    }

    void CreatePeriodicNotificationChannel()
    {
        // Create the periodic notification channel, but only on API 26+.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            var channelNameJava = new Java.Lang.String(periodicChannelName);
            var channel = new NotificationChannel(periodicChannelId, channelNameJava, NotificationImportance.High)
            {
                Description = periodicChannelDescription
            };
            // Register the channel
            NotificationManager? manager = (NotificationManager?)Platform.AppContext?.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);
#pragma warning restore CA1416
            System.Diagnostics.Debug.WriteLine("Periodic notification channel created for API 26+");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Periodic notification channel not needed for API < 26");
        }

        // Mark as initialized regardless of API level
        periodicChannelInitialized = true;
    }

    void CreateReminderNotificationChannel()
    {
        // High importance so reminders appear as heads-up banners and are not silenced.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            var channelNameJava = new Java.Lang.String(reminderChannelName);
            var channel = new NotificationChannel(reminderChannelId, channelNameJava, NotificationImportance.High)
            {
                Description = reminderChannelDescription
            };
            channel.EnableVibration(true);
            channel.SetVibrationPattern(new long[] { 0, 300, 100, 300 });
            NotificationManager? manager = (NotificationManager?)Platform.AppContext?.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);
#pragma warning restore CA1416
            System.Diagnostics.Debug.WriteLine("Reminder notification channel created for API 26+");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Reminder notification channel not needed for API < 26");
        }

        reminderChannelInitialized = true;
    }

    public void DismissReminderNotification(int todoItemId)
    {
        compatManager?.Cancel(todoItemId);
    }

    static void ScheduleExactAlarm(AlarmManager? alarmManager, long triggerTimeMs, PendingIntent pendingIntent)
    {
        if (alarmManager == null)
        {
            return;
        }

        // Android 12+ (API 31+): exact alarms require SCHEDULE_EXACT_ALARM / USE_EXACT_ALARM.
        // Check at runtime — USE_EXACT_ALARM (API 33+) is auto-granted; SCHEDULE_EXACT_ALARM
        // can be revoked by the user on API 31–32.
#pragma warning disable CA1416
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S && !alarmManager.CanScheduleExactAlarms())
#pragma warning restore CA1416
        {
            // Permission not granted — fall back to inexact (better than nothing).
            alarmManager.Set(AlarmType.RtcWakeup, triggerTimeMs, pendingIntent);
            System.Diagnostics.Debug.WriteLine("ScheduleExactAlarm: exact alarm permission not granted, using inexact Set()");

            return;
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            // API 23+: fires even in Doze mode.
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerTimeMs, pendingIntent);
            System.Diagnostics.Debug.WriteLine("ScheduleExactAlarm: SetExactAndAllowWhileIdle scheduled");
        }
        else
        {
            alarmManager.Set(AlarmType.RtcWakeup, triggerTimeMs, pendingIntent);
            System.Diagnostics.Debug.WriteLine("ScheduleExactAlarm: fallback Set() (API < 23)");
        }
    }

    long GetNotifyTime(DateTime notifyTime)
    {
        DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(notifyTime);
        double epochDiff = (new DateTime(1970, 1, 1) - DateTime.MinValue).TotalSeconds;
        long utcAlarmTime = utcTime.AddSeconds(-epochDiff).Ticks / 10000;
        return utcAlarmTime; // milliseconds
    }
}
