namespace LogMyDay.Shared.DTOs;

public class TodoItemResponse
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public required string Title { get; set; }
    public string? Notes { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public TimeOnly? NotifyAt { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
}
