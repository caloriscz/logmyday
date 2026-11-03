namespace LogMyDay.Shared.DTOs;

public class ReminderCompleteRequest
{
    public DateTime DoneAt { get; set; } = DateTime.UtcNow;
    public string? CompletionValue { get; set; }

    /// <summary>For a reminder whose completion target is a record type: the record logged for
    /// it. The reminder completes only with a saved record of that type.</summary>
    public int? RecordId { get; set; }
}
