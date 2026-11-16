using LogMyDay.Shared.DTOs;

namespace LogMyDay.UI.Services;

/// <summary>Hands the warnings a save returned to the notice in the layout, so they stay on screen
/// after the form that saved closes. One per circuit on the web, one per app on mobile.</summary>
public class CheckWarningNotices
{
    public IReadOnlyList<TagCheckWarning> Current { get; private set; } = [];

    public event Action? Changed;

    /// <summary>Shows the warnings of the latest save; a save without warnings leaves an open
    /// notice as it is.</summary>
    public void Show(IReadOnlyList<TagCheckWarning>? warnings)
    {
        if (warnings is not { Count: > 0 })
        {
            return;
        }

        Current = warnings;
        Changed?.Invoke();
    }

    public void Dismiss()
    {
        Current = [];
        Changed?.Invoke();
    }
}
