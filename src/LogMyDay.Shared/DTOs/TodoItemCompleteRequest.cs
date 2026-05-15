namespace LogMyDay.Shared.DTOs;

public class TodoItemCompleteRequest
{
    public DateTime DoneAt { get; set; } = DateTime.UtcNow;
    public string? CompletionValue { get; set; }
}
