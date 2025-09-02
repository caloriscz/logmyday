using LogMyDay.App.Mobile.Services;
#if ANDROID
using LogMyDay.App.Mobile.Platforms.Android;
#endif

namespace LogMyDay.App.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "LogMyDay" };
    }

    protected override void OnStart()
    {
        base.OnStart();
        
        System.Diagnostics.Debug.WriteLine("App.OnStart called");
        
        // Skip permission request for now - just test notifications directly
        System.Diagnostics.Debug.WriteLine("Skipping permission request - testing direct notifications");
        
        // Test direct platform service call
        try
        {
#if ANDROID
            System.Diagnostics.Debug.WriteLine("Testing direct platform service");
            var platformService = new LogMyDay.App.Mobile.Platforms.Android.NotificationManagerService();
            System.Diagnostics.Debug.WriteLine("Created platform service directly");
            platformService.SendNotification("LogMyDay Direct", "Direct platform test notification");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error with direct platform service: {ex.Message}");
        }
        
        // Try to get notification service from DI container
        try
        {
            System.Diagnostics.Debug.WriteLine("Attempting to get NotificationService from DI");
            var serviceProvider = IPlatformApplication.Current?.Services;
            System.Diagnostics.Debug.WriteLine($"ServiceProvider available: {serviceProvider != null}");
            
            var notificationService = serviceProvider?.GetService<NotificationService>();
            System.Diagnostics.Debug.WriteLine($"NotificationService from DI: {notificationService != null}");
            
            if (notificationService != null)
            {
                // Send startup notification
                System.Diagnostics.Debug.WriteLine("Sending app start notification");
                notificationService.SendNotification("LogMyDay App", "🚀 App started - Notification sent");
                
                // Start periodic notifications
                System.Diagnostics.Debug.WriteLine("Starting periodic notifications");
                notificationService.StartPeriodicNotifications();
                
                // Send a test periodic notification after 10 seconds to verify timer works
                Task.Run(async () =>
                {
                    await Task.Delay(10000); // 10 seconds
                    System.Diagnostics.Debug.WriteLine("Sending manual test periodic notification");
                    notificationService.SendNotification("LogMyDay Test", "🔔 Manual test notification after 10 seconds");
                });
                
                System.Diagnostics.Debug.WriteLine("Periodic notification system activated");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("NotificationService not found in DI container");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting notifications: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
        }
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        // Keep notifications running in background
    }

    protected override void OnResume()
    {
        base.OnResume();
        // Notifications should continue from where they left off
    }
}
