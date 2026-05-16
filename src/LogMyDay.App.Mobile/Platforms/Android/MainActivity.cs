using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
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
        
        CreateNotificationFromIntent(Intent);
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
