using Android.App;
using Android.Content;
using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile.Platforms.Android;

[BroadcastReceiver(Enabled = true, Label = "Local Notifications Broadcast Receiver")]
public class AlarmHandler : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Extras != null)
        {
            string? title = intent.GetStringExtra(NotificationManagerService.TitleKey);
            string? message = intent.GetStringExtra(NotificationManagerService.MessageKey);

            if (title != null && message != null)
            {
                NotificationManagerService? manager = NotificationManagerService.Instance ?? new NotificationManagerService();
                manager.Show(title, message);
            }
        }
    }
}
