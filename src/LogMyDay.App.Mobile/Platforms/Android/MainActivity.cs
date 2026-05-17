using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | 
                          ConfigChanges.UiMode | ConfigChanges.ScreenLayout | 
                          ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android 13+ (API 33) requires POST_NOTIFICATIONS to be granted at runtime.
        // Without this, NotificationManagerCompat.Notify() silently drops every notification.
        RequestNotificationPermission();

        // Android 12 / 12L (API 31-32) requires the user to enable exact alarms in Settings
        // (USE_EXACT_ALARM covers API 33+ and is auto-granted for calendar/reminder apps).
        RequestExactAlarmPermission();

        CreateNotificationFromIntent(Intent);
    }

    private void RequestNotificationPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
        {
            return;
        }

        if (CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != Permission.Granted)
        {
            RequestPermissions(["android.permission.POST_NOTIFICATIONS"], 100);
        }
    }

    private void RequestExactAlarmPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
        {
            return;
        }

        var alarmManager = GetSystemService(Context.AlarmService) as AlarmManager;
        if (alarmManager?.CanScheduleExactAlarms() == true)
        {
            return;
        }

        try
        {
            // Opens the per-app "Alarms & Reminders" settings page so the user can grant exact alarm scheduling.
            StartActivity(new Intent(Settings.ActionRequestScheduleExactAlarm));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open exact alarm settings: {ex.Message}");
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        CreateNotificationFromIntent(intent);
    }

    static void CreateNotificationFromIntent(Intent? intent)
    {
        if (intent?.Extras != null)
        {
            string? title = intent.GetStringExtra(NotificationManagerService.TitleKey);
            string? message = intent.GetStringExtra(NotificationManagerService.MessageKey);

            if (title != null && message != null)
            {
                var services = IPlatformApplication.Current?.Services;
                var payload = NotificationManagerService.BuildPayloadFromIntent(intent);

                services?.GetService<INotificationManagerService>()?.ReceiveNotification(title, message, payload);

                // INotificationManagerService resolves to the Android platform service whose
                // ReceiveNotification only fires a local event. NotificationService (the wrapper
                // that calls SetPendingIntent) may not be instantiated yet, so call SetPendingIntent
                // directly to guarantee navigation always works.
                if (payload != null)
                {
                    services?.GetService<NotificationNavigationService>()?.SetPendingIntent(payload);
                }
            }
        }
    }
}
