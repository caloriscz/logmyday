using System.Timers;

namespace LogMyDay.App.Mobile.Services;

public class NotificationService : INotificationManagerService
{
    private readonly INotificationManagerService _platformService;
    private readonly NotificationNavigationService _navigationService;
    
    public event EventHandler<NotificationEventArgs>? NotificationReceived;

    public NotificationService(INotificationManagerService platformService, NotificationNavigationService navigationService)
    {
        _platformService = platformService;
        _navigationService = navigationService;
        _platformService.NotificationReceived += OnPlatformNotificationReceived;
    }

    public void SendNotification(string title, string message, DateTime? notifyTime = null, NotificationPayload? payload = null)
    {
        System.Diagnostics.Debug.WriteLine($"NotificationService.SendNotification called: {title} - {message}");
        _platformService.SendNotification(title, message, notifyTime, payload);
    }

    public void ReceiveNotification(string title, string message, NotificationPayload? payload = null)
    {
        NotificationReceived?.Invoke(this, new NotificationEventArgs 
        { 
            Title = title, 
            Message = message,
            Payload = payload
        });

        if (payload != null)
        {
            _navigationService.SetPendingIntent(payload);
        }
    }

    public void CancelAlarmsForNotification(int notificationId)
        => _platformService.CancelAlarmsForNotification(notificationId);

    public void CancelAlarmsForTag(int tagId)
        => _platformService.CancelAlarmsForTag(tagId);

    public void CancelReminderAlarm(int todoItemId)
        => _platformService.CancelReminderAlarm(todoItemId);

    public void DismissReminderNotification(int todoItemId)
        => _platformService.DismissReminderNotification(todoItemId);

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

        if (e.Payload != null)
        {
            _navigationService.SetPendingIntent(e.Payload);
        }
    }
}
