using System.ComponentModel.DataAnnotations;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TodoItemRequest
{
    public int ListId { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }

    public string? Notes { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? DueDate { get; set; }

    public TimeOnly? NotifyAt { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;

    public AutoLogMode AutoLogMode { get; set; } = AutoLogMode.Add;

    public int? CompletionTagId { get; set; }

    // Compat shims — ignored by the server (TodoItem no longer has these columns). See ReminderRequest.
    public int? MonitorDaysBack { get; set; }
    public DateOnly? MonitorFromDate { get; set; }
    public DateOnly? MonitorToDate { get; set; }
    public bool AllowUnfilled { get; set; }
}
