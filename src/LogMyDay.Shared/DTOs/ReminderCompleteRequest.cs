namespace LogMyDay.Shared.DTOs;

public class ReminderCompleteRequest
{
    public DateTime DoneAt { get; set; } = DateTime.UtcNow;
    public string? CompletionValue { get; set; }
}
