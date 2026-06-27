using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class ReminderResponse
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Notes { get; set; }
    public TimeOnly? NotifyAt { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
    public bool IsSkipped { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
    public RecurrenceType RecurrenceType { get; set; }
    public AutoLogMode AutoLogMode { get; set; }
    public DateOnly? MonitorFromDate { get; set; }
    public DateOnly? MonitorToDate { get; set; }
    public int? CompletionTagId { get; set; }
    public string? CompletionTagName { get; set; }
    public int? CompletionTagInputTypeId { get; set; }
    public bool AllowUnfilled { get; set; }

    public bool IsTagDayLocked { get; set; }
}
