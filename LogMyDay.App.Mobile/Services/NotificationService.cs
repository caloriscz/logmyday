using System.Timers;

namespace LogMyDay.App.Mobile.Services;

public class NotificationService : INotificationManagerService
{
    private readonly INotificationManagerService _platformService;
    private System.Timers.Timer? _notificationTimer;
    private int _notificationCount = 0;
    
    public event EventHandler<NotificationEventArgs>? NotificationReceived;

    public NotificationService(INotificationManagerService platformService)
    {
        _platformService = platformService;
        _platformService.NotificationReceived += OnPlatformNotificationReceived;
    }

    public void SendNotification(string title, string message, DateTime? notifyTime = null)
    {
        System.Diagnostics.Debug.WriteLine($"NotificationService.SendNotification called: {title} - {message}");
        _platformService.SendNotification(title, message, notifyTime);
    }

    public void ReceiveNotification(string title, string message)
    {
        NotificationReceived?.Invoke(this, new NotificationEventArgs 
        { 
            Title = title, 
            Message = message 
        });
    }

    public void StartPeriodicNotifications()
    {
        System.Diagnostics.Debug.WriteLine("NotificationService.StartPeriodicNotifications called");
        
        // Send initial notification
        _notificationCount++;
        SendNotification("LogMyDay Periodic", $"Periodic notification #{_notificationCount} - Timer started");
        
        // Show toast that periodic notifications are starting
        ShowToastNotification($"📱 Periodic notifications started - every 1 minute");

        // Set up timer for every 1 minute (60,000 milliseconds)
        _notificationTimer = new System.Timers.Timer(60000); // Changed from 120000 to 60000
        _notificationTimer.Elapsed += OnTimerElapsed;
        _notificationTimer.AutoReset = true;
        _notificationTimer.Enabled = true;
        
        System.Diagnostics.Debug.WriteLine("Notification timer started - 1 minute intervals");
    }

    public void StopPeriodicNotifications()
    {
        if (_notificationTimer != null)
        {
            _notificationTimer.Stop();
            _notificationTimer.Dispose();
            _notificationTimer = null;
        }
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _notificationCount++;
        System.Diagnostics.Debug.WriteLine($"Timer elapsed - sending notification #{_notificationCount}");
        
        // Send system notification (to notification panel)
        SendNotification("LogMyDay Timer", $"⏰ Periodic notification #{_notificationCount} - {DateTime.Now:HH:mm:ss}");
        
        // Send toast notification (in-app message)
        ShowToastNotification($"🔔 Timer #{_notificationCount} - {DateTime.Now:HH:mm:ss}");
    }

    private void ShowToastNotification(string message)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
#if ANDROID
                var context = Platform.AppContext;
                if (context != null)
                {
                    var toast = Android.Widget.Toast.MakeText(context, message, Android.Widget.ToastLength.Short);
                    toast?.Show();
                    System.Diagnostics.Debug.WriteLine($"Toast shown: {message}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Cannot show toast - Platform.AppContext is null");
                }
#endif
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
        }
    }

    private void OnPlatformNotificationReceived(object? sender, NotificationEventArgs e)
    {
        NotificationReceived?.Invoke(this, e);
    }
}
