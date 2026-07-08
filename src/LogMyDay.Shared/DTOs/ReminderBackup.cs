using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class ReminderBackup
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public TimeOnly? NotifyAt { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
    public DateTime? SkippedAt { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
    public RecurrenceType RecurrenceType { get; set; }
    public AutoLogMode AutoLogMode { get; set; }
    public string? CompletionTagName { get; set; }
    public DateOnly? MonitorFromDate { get; set; }
    public DateOnly? MonitorToDate { get; set; }
    public bool AllowUnfilled { get; set; }
    public List<ReminderDayBackup> Days { get; set; } = new();
}

public class ReminderDayBackup
{
    public DateOnly Date { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
    public bool IsSkipped { get; set; }
    public DateTime? SkippedAt { get; set; }
    public string? CompletionValue { get; set; }
}
