using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using LogMyDay.UI.Services;
using Microsoft.Extensions.Logging;

namespace LogMyDay.App.Mobile.Services;

/// <summary>Arms the alarm of a reminder that a save on this phone set through a trigger at once,
/// instead of on the next visit to Reminders, which is otherwise the only place alarms are armed.</summary>
public sealed class SavedReminderArming
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SavedReminderArming> _logger;

    // Created at startup, so it takes nothing that needs a signed-in user or the platform yet.
    public SavedReminderArming(SaveNotices notices, IServiceProvider services, ILogger<SavedReminderArming> logger)
    {
        _services = services;
        _logger = logger;
        notices.RemindersSet += OnRemindersSet;
    }

    // An event handler; a failure only leaves the alarm to the next Reminders load.
    private async void OnRemindersSet(IReadOnlyList<TriggeredReminder> reminders)
    {
        try
        {
            await Arm();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not arm the reminders a save set; the Reminders page arms them on its next load");
        }
    }

    /// <summary>Re-arms from the server's reminder list, as the Reminders page does on load.</summary>
    private async Task Arm()
    {
        var api = _services.GetRequiredService<IReminderApi>();
        var all = await api.GetReminders(DateTime.Today.ToString("yyyy-MM-dd"));
        _services.GetRequiredService<ReminderNotificationScheduler>().ScheduleAll(all.ToList(), "save");
    }
}
