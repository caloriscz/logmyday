using Android.App;
using AndroidX.Core.App;
using Android.Content;

namespace LogMyDay.App.Mobile.Tests;

/// <summary>
/// Simple test class to verify notification functionality
/// Call this from MainActivity or any Android context
/// </summary>
public static class NotificationTester
{
    public static void TestNotification()
    {
        try
        {
            var context = Platform.AppContext;
            if (context == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Platform.AppContext is null");

                return;
            }
            
            CreateNotificationChannel(context);
            
            // Test NotificationManagerCompat
            var compatManager = NotificationManagerCompat.From(context);
            if (compatManager == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: NotificationManagerCompat.From returned null");

                return;
            }
            

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== NOTIFICATION TEST FAILED ===");
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
        }
    }
    
    private static void CreateNotificationChannel(Context context)
    {
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            var channelId = "test_channel";
            var channelName = "Test Notifications";
            var channel = new NotificationChannel(channelId, channelName, NotificationImportance.Default)
            {
                Description = "Test notification channel"
            };
            
            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);
#pragma warning restore CA1416
            
            System.Diagnostics.Debug.WriteLine("Notification channel created");
        }
    }
}
