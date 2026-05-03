using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TodoListBackup
{
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public TodoListType ListType { get; set; }
    public DateTime DateCreated { get; set; }
    public List<TodoItemBackup> Items { get; set; } = new();
}

public class TodoItemBackup
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public TimeOnly? NotifyAt { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
    public RecurrenceType RecurrenceType { get; set; }
    public AutoLogMode AutoLogMode { get; set; }
    public string? CompletionTagName { get; set; }
    public int? MonitorDaysBack { get; set; }
    public DateOnly? MonitorFromDate { get; set; }
    public DateOnly? MonitorToDate { get; set; }
}
