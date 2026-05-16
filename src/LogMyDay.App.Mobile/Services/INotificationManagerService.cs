namespace LogMyDay.App.Mobile.Services;

public interface INotificationManagerService
{
    event EventHandler<NotificationEventArgs>? NotificationReceived;
    void SendNotification(string title, string message, DateTime? notifyTime = null, NotificationPayload? payload = null);
    void ReceiveNotification(string title, string message, NotificationPayload? payload = null);
    void StartPeriodicNotifications();
    void StopPeriodicNotifications();
    void CancelAlarmsForNotification(int notificationId);
    void CancelAlarmsForTag(int tagId);
    void CancelReminderAlarm(int todoItemId);
}

public class NotificationEventArgs : EventArgs
{
    public required string Title { get; set; }
    public required string Message { get; set; }
    public NotificationPayload? Payload { get; set; }
}
