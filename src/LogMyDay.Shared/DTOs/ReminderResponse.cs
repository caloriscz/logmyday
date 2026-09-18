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

    /// <summary>
    /// True when <paramref name="date"/> falls inside the reminder's monitoring window. Both bounds
    /// are independent — an end date alone still closes the window.
    ///
    /// The API returns every reminder regardless of its window and each surface filters locally, so
    /// notification scheduling must apply this too. Without it, a reminder whose window has ended
    /// keeps arming alarms and firing while being invisible in the UI.
    /// </summary>
    public bool IsWithinMonitoringWindow(DateOnly date)
    {
        if (MonitorFromDate.HasValue && date < MonitorFromDate.Value)
        {
            return false;
        }

        if (MonitorToDate.HasValue && date > MonitorToDate.Value)
        {
            return false;
        }

        return true;
    }
}
