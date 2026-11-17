namespace LogMyDay.Shared.DTOs;

/// <summary>A reminder a save created through one of the user's triggers: logging the trigger's
/// tag set a one-day reminder to log another one.</summary>
public class TriggeredReminder
{
    public int ReminderId { get; set; }
    public int TriggerId { get; set; }
    public string TriggerName { get; set; } = string.Empty;

    /// <summary>The tag or record type the reminder asks to log, e.g. "sleep: quality".</summary>
    public string PromptTagName { get; set; } = string.Empty;

    /// <summary>The day of the entry that set it.</summary>
    public DateOnly EntryDay { get; set; }

    /// <summary>When it rings: that day or the next, local time.</summary>
    public DateOnly Day { get; set; }
    public TimeOnly Time { get; set; }
}
