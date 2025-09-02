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
            System.Diagnostics.Debug.WriteLine("=== NOTIFICATION TEST STARTED ===");
            
            var context = Platform.AppContext;
            if (context == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Platform.AppContext is null");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"Context available: {context.GetType().Name}");
            
            // Show a toast message as visual feedback
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
#if ANDROID
                    var toast = Android.Widget.Toast.MakeText(context, "Testing notifications...", Android.Widget.ToastLength.Long);
                    toast?.Show();
                    System.Diagnostics.Debug.WriteLine("Toast message shown");
#endif
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
                }
            });
            
            // Create notification channel first
            CreateNotificationChannel(context);
            
            // Test NotificationManagerCompat
            var compatManager = NotificationManagerCompat.From(context);
            if (compatManager == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: NotificationManagerCompat.From returned null");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"NotificationManagerCompat created: {compatManager.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Notifications enabled: {compatManager.AreNotificationsEnabled()}");
            
            // Build a very basic notification
            var builder = new NotificationCompat.Builder(context, "test_channel");
            builder.SetContentTitle("🎉 LogMyDay Test");
            builder.SetContentText($"Direct test notification - {DateTime.Now:HH:mm:ss}");
            builder.SetSmallIcon(17301659); // system icon
            builder.SetAutoCancel(true);
            builder.SetPriority(NotificationCompat.PriorityHigh); // Make it more visible
            builder.SetVibrate(new long[] { 0, 500 }); // Short vibration
            builder.SetOnlyAlertOnce(false); // Always alert even if similar notification exists
            
            var notification = builder.Build();
            compatManager.Notify(998, notification); // Use unique ID
            
            // Send a second test notification after delay
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                var builder2 = new NotificationCompat.Builder(context, "test_channel");
                builder2.SetContentTitle("🔔 LogMyDay Timer Test");
                builder2.SetContentText($"Second test notification - {DateTime.Now:HH:mm:ss}");
                builder2.SetSmallIcon(17301659);
                builder2.SetAutoCancel(false); // Don't auto-cancel this one
                builder2.SetPriority(NotificationCompat.PriorityMax); // Highest priority
                builder2.SetOngoing(false);
                
                var notification2 = builder2.Build();
                compatManager.Notify(997, notification2); // Different ID
                System.Diagnostics.Debug.WriteLine("Second test notification sent");
            });
            
            System.Diagnostics.Debug.WriteLine("Direct notification sent successfully");
            System.Diagnostics.Debug.WriteLine("=== NOTIFICATION TEST COMPLETED ===");
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
