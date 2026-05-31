using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TodoItemResponse
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public required string Title { get; set; }
    public string? Notes { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public TimeOnly? NotifyAt { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
    public bool IsSkipped { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
    public RecurrenceType RecurrenceType { get; set; }
    public AutoLogMode AutoLogMode { get; set; }
    public int? CompletionTagId { get; set; }
    public string? CompletionTagName { get; set; }
    public int? CompletionTagInputTypeId { get; set; }

    // Compat shims — Reminder-only fields moved to ReminderResponse. These properties
    // remain as source-compat for razor markup not yet stripped; the server never sets them.
    public int? MonitorDaysBack { get; set; }
    public DateOnly? MonitorFromDate { get; set; }
    public DateOnly? MonitorToDate { get; set; }
    public bool AllowUnfilled { get; set; }
}
