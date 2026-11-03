namespace LogMyDay.Shared.DTOs;

public class TodoItemCompleteRequest
{
    public DateTime DoneAt { get; set; } = DateTime.UtcNow;
    public string? CompletionValue { get; set; }

    /// <summary>For an item whose list's completion target is a record type: the record logged
    /// for it. The item completes only with a saved record of that type.</summary>
    public int? RecordId { get; set; }
}
