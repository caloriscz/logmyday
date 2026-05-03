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

    public DateOnly? StartDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public TimeOnly? NotifyAt { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;

    public AutoLogMode AutoLogMode { get; set; } = AutoLogMode.Add;

    public int? MonitorDaysBack { get; set; }

    public DateOnly? MonitorFromDate { get; set; }

    public DateOnly? MonitorToDate { get; set; }

    public int? CompletionTagId { get; set; }
}
