using Android.App;
using Android.Content;
using LogMyDay.App.Mobile.Services;
using LogMyDay.App.Mobile.Services.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace LogMyDay.App.Mobile.Platforms.Android;

/// <summary>
/// Re-arms reminder alarms after a device reboot. Android clears every scheduled alarm on boot and
/// the app holds credentials in memory only, so it cannot re-fetch reminders this early. We re-arm
/// from <see cref="ArmedAlarmStore"/> — a durable local snapshot of what was scheduled — which needs
/// no network and no login. Re-arming touches only OS alarm registrations; no reminder data changes.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[]
{
    Intent.ActionBootCompleted,
    Intent.ActionLockedBootCompleted,
    "android.intent.action.QUICKBOOT_POWERON"
})]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var action = intent?.Action;
        if (action != Intent.ActionBootCompleted
            && action != Intent.ActionLockedBootCompleted
            && action != "android.intent.action.QUICKBOOT_POWERON")
        {
            return;
        }

        var services = IPlatformApplication.Current?.Services;
        var diag = services?.GetService<IDiagnosticStore>() ?? DiagnosticStore.Instance;

        try
        {
            var snapshot = ArmedAlarmStore.GetAll();
            var nowUtc = DateTime.UtcNow;

            var notifier = services?.GetService<INotificationManagerService>()
                ?? NotificationManagerService.Instance
                ?? new NotificationManagerService();

            var rearmed = 0;
            foreach (var a in snapshot)
            {
                if (a.FireAtUtc <= nowUtc)
                {
                    // Its time already passed during downtime — drop it rather than fire late.
                    ArmedAlarmStore.Remove(a.ItemId);

                    continue;
                }

                var payload = new NotificationPayload
                {
                    NotificationId = a.ItemId,
                    TodoItemId = a.ItemId,
                    AutoComplete = true
                };

                notifier.SendNotification(a.Title, a.Notes ?? string.Empty, a.FireAtUtc, payload);
                rearmed++;
            }

            diag?.Record("reminder-diag", $"event=boot surface=mobile rearmed={rearmed} snapshot={snapshot.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BootReceiver: re-arm failed — {ex.Message}");
            diag?.Record("reminder-diag", $"event=boot surface=mobile error=\"{ex.Message}\"");
        }
    }
}
