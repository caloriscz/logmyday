using LogMyDay.Shared.DTOs;

namespace LogMyDay.UI.Services;

/// <summary>Hands what a save reported to the notice in the layout, so it stays on screen after the
/// form that saved closes: the Warn checks it left broken and the reminders its triggers set. One per
/// circuit on the web, one per app on mobile.</summary>
public class SaveNotices
{
    public IReadOnlyList<TagCheckWarning> Warnings { get; private set; } = [];
    public IReadOnlyList<TriggeredReminder> Reminders { get; private set; } = [];

    public bool IsOpen => Warnings.Count > 0 || Reminders.Count > 0;

    public event Action? Changed;

    /// <summary>Raised when a save set reminders; the phone arms them at once.</summary>
    public event Action<IReadOnlyList<TriggeredReminder>>? RemindersSet;

    /// <summary>Shows what the latest save reported; a save that reported nothing leaves an open
    /// notice as it is.</summary>
    public void Show(IReadOnlyList<TagCheckWarning>? warnings, IReadOnlyList<TriggeredReminder>? reminders)
    {
        if (warnings is not { Count: > 0 } && reminders is not { Count: > 0 })
        {
            return;
        }

        Warnings = warnings ?? [];
        Reminders = reminders ?? [];
        Changed?.Invoke();

        if (Reminders.Count > 0)
        {
            RemindersSet?.Invoke(Reminders);
        }
    }

    public void Dismiss()
    {
        Warnings = [];
        Reminders = [];
        Changed?.Invoke();
    }
}
