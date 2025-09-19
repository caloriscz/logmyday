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

    public const string TitleKey = "title";
    public const string MessageKey = "message";

    bool channelInitialized = false;
    bool periodicChannelInitialized = false;
    int messageId = 0;
    int pendingIntentId = 0;
    static int _notificationCount = 0; // Track periodic notifications

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

    public void SendNotification(string title, string message, DateTime? notifyTime = null)
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
            intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                ? PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable
                : PendingIntentFlags.CancelCurrent;

            PendingIntent? pendingIntent = PendingIntent.GetBroadcast(Platform.AppContext, pendingIntentId++, intent, pendingIntentFlags);
            if (pendingIntent != null)
            {
                long triggerTime = GetNotifyTime(notifyTime.Value);
                AlarmManager? alarmManager = Platform.AppContext.GetSystemService(Context.AlarmService) as AlarmManager;
                alarmManager?.Set(AlarmType.RtcWakeup, triggerTime, pendingIntent);
            }
        }
        else
        {
            Show(title, message);
        }
    }

    public void ReceiveNotification(string title, string message)
    {
        var args = new NotificationEventArgs()
        {
            Title = title,
            Message = message,
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

    public void Show(string title, string message)
    {
        System.Diagnostics.Debug.WriteLine($"Android NotificationManagerService.Show called: {title} - {message}");

        var context = Platform.AppContext;
        if (context == null || compatManager == null)
        {
            System.Diagnostics.Debug.WriteLine("Context or compatManager is null");
            return;
        }

        // Ensure notification channel is created
        bool isPeriodicNotification = title?.Contains("Timer") == true || title?.Contains("Periodic") == true;
        string notificationChannelId = isPeriodicNotification ? periodicChannelId : channelId;

        if (isPeriodicNotification && !periodicChannelInitialized)
        {
            CreatePeriodicNotificationChannel();
        }
        else if (!isPeriodicNotification && !channelInitialized)
        {
            CreateNotificationChannel();
        }

        Intent intent = new Intent(context, typeof(MainActivity));
        intent.PutExtra(TitleKey, title ?? string.Empty);
        intent.PutExtra(MessageKey, message ?? string.Empty);
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

        // Make notifications more visible
        if (title?.Contains("Timer") == true || title?.Contains("Periodic") == true)
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

        // Use different notification IDs to prevent Android from grouping them
        int notificationId = messageId++;
        if (title?.Contains("Timer") == true || title?.Contains("Periodic") == true)
        {
            _notificationCount++;
            notificationId = 2000 + _notificationCount; // Use different range for periodic notifications
        }

        System.Diagnostics.Debug.WriteLine($"Built notification, about to notify with ID: {notificationId}");
        compatManager.Notify(notificationId, notification);
        System.Diagnostics.Debug.WriteLine("Notification sent successfully");
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

    long GetNotifyTime(DateTime notifyTime)
    {
        DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(notifyTime);
        double epochDiff = (new DateTime(1970, 1, 1) - DateTime.MinValue).TotalSeconds;
        long utcAlarmTime = utcTime.AddSeconds(-epochDiff).Ticks / 10000;
        return utcAlarmTime; // milliseconds
    }
}
