using System.Timers;

namespace LogMyDay.App.Mobile.Services;

public class NotificationService : INotificationManagerService
{
    private readonly INotificationManagerService _platformService;
    
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

    // Deprecated methods - use SystemNotificationService instead
    public void StartPeriodicNotifications()
    {
        System.Diagnostics.Debug.WriteLine("NotificationService.StartPeriodicNotifications is deprecated - use SystemNotificationService");
    }

    public void StopPeriodicNotifications()
    {
        System.Diagnostics.Debug.WriteLine("NotificationService.StopPeriodicNotifications is deprecated - use SystemNotificationService");
    }

    private void OnPlatformNotificationReceived(object? sender, NotificationEventArgs e)
    {
        NotificationReceived?.Invoke(this, e);
    }
}
