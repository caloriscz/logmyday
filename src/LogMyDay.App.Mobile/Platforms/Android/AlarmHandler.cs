using Android.App;
using Android.Content;
using LogMyDay.App.Mobile.Services;
using LogMyDay.App.Mobile.Services.Diagnostics;
using LogMyDay.Shared.Interfaces;

namespace LogMyDay.App.Mobile.Platforms.Android;

[BroadcastReceiver(Enabled = true, Label = "Local Notifications Broadcast Receiver")]
public class AlarmHandler : BroadcastReceiver
{
    // How long the fire-time server check may take before we give up and show anyway.
    // Kept well under the BroadcastReceiver.GoAsync budget (~10s).
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(4);

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Extras == null)
        {
            return;
        }

        string? title = intent.GetStringExtra(NotificationManagerService.TitleKey);
        string? message = intent.GetStringExtra(NotificationManagerService.MessageKey);

        if (title == null || message == null)
        {
            return;
        }

        NotificationManagerService manager = NotificationManagerService.Instance ?? new NotificationManagerService();
        var payload = NotificationManagerService.BuildPayloadFromIntent(intent);

        // Non-reminder notifications have no server-side completion state — show immediately.
        if (payload?.TodoItemId is not int todoItemId)
        {
            manager.Show(title, message, payload);

            return;
        }

        // Reminder alarm: record the fire, then verify the reminder is still active for today before
        // surfacing it. This catches the cross-surface case (completed in the web app) where the local
        // alarm was never cancelled. GoAsync keeps the process alive for the async server check.
        ReminderNotificationScheduler.Instance?.LogFired(todoItemId);

        var pendingResult = GoAsync();
        _ = VerifyAndShowAsync(manager, title, message, payload, todoItemId, pendingResult);
    }

    /// <summary>
    /// Asks the server whether the reminder is still active today; suppresses the notification when it
    /// is done/skipped/absent. Fail-open: any error, timeout, or missing session shows the notification,
    /// so a flaky check can never hide a legitimate reminder. Mirrors the cancel predicate used by
    /// <see cref="ReminderNotificationScheduler.ScheduleAll"/> so fire-time behavior equals a reconcile.
    /// </summary>
    private static async Task VerifyAndShowAsync(
        NotificationManagerService manager,
        string title,
        string message,
        NotificationPayload payload,
        int todoItemId,
        BroadcastReceiver.PendingResult? pendingResult)
    {
        var show = true; // fail-open default
        string? suppressReason = null;
        string? failReason = null;

        try
        {
            var services = Microsoft.Maui.IPlatformApplication.Current?.Services;
            var provider = services?.GetService(typeof(IApiClientProvider)) as IApiClientProvider;

            if (provider == null)
            {
                failReason = "no-services";
            }
            else
            {
                IReminderApi? api = null;
                try
                {
                    // Throws when the server/credentials aren't configured (pre-login, or a killed
                    // process the alarm just woke — credentials are in-memory only).
                    api = provider.Reminder;
                }
                catch (Exception)
                {
                    failReason = "not-configured";
                }

                if (api != null)
                {
                    using var cts = new CancellationTokenSource(CheckTimeout);
                    var today = DateTime.Now.ToString("yyyy-MM-dd");
                    var reminders = await api.GetReminders(today).WaitAsync(cts.Token).ConfigureAwait(false);
                    var item = reminders?.FirstOrDefault(r => r.Id == todoItemId);

                    if (item == null)
                    {
                        show = false;
                        suppressReason = "absent";
                    }
                    else if (item.IsDone || item.IsSkipped)
                    {
                        show = false;
                        suppressReason = item.IsDone ? "done" : "skipped";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Fail-open — never hide a legitimate reminder because the check failed.
            failReason = ex.GetType().Name;
            show = true;
        }

        try
        {
            if (show)
            {
                if (failReason != null)
                {
                    DiagnosticStore.Instance?.Record("reminder-diag",
                        $"event=fire-check-failed itemId={todoItemId} reason={failReason} surface=mobile");
                }

                manager.Show(title, message, payload);
            }
            else
            {
                DiagnosticStore.Instance?.Record("reminder-diag",
                    $"event=fire-suppressed itemId={todoItemId} reason={suppressReason} surface=mobile");

                // Drop the now-stale local alarm/snapshot so it can't fire again.
                ReminderNotificationScheduler.Instance?.CancelItem(todoItemId);
            }
        }
        finally
        {
            pendingResult?.Finish();
        }
    }
}
