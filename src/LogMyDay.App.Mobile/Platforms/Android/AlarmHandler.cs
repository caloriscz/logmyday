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
    // Kept under the BroadcastReceiver.GoAsync budget (~10s), but generous enough for the cold
    // path this check normally runs on: process start, keystore read, then a TLS handshake over a
    // radio that was asleep. At 4s that routinely timed out and fell through to fail-open.
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(7);

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
                // An alarm usually wakes a process that never rendered the UI, so nothing has
                // configured ApiContext and every client throws. Restore the stored session first
                // — without it this check always fails open, which is why reminders completed on
                // the web still fired here.
                var restored = false;
                if (services?.GetService(typeof(IApiContext)) is IApiContext ctx)
                {
                    restored = await StoredSession.TryRestore(ctx).ConfigureAwait(false);
                }

                IReminderApi? api = null;
                try
                {
                    api = provider.Reminder;
                }
                catch (Exception)
                {
                    failReason = restored ? "not-configured" : "no-stored-session";
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
                    else if (!item.IsWithinMonitoringWindow(DateOnly.FromDateTime(DateTime.Now)))
                    {
                        // The API reports reminders outside their monitoring window as live, so
                        // "present and not done" is not enough to justify showing one.
                        show = false;
                        suppressReason = "out-of-window";
                    }
                    else
                    {
                        // Title/notes were snapshotted into the intent when the alarm was armed —
                        // a day earlier, or much longer if the app was never reopened. Renaming a
                        // reminder (a changed dosage) would otherwise keep firing the old text.
                        title = item.Title;
                        message = item.Notes ?? string.Empty;
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
