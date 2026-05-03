using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

public class TodoItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int ListId { get; set; }

    [ForeignKey(nameof(ListId))]
    public TodoList List { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }

    public string? Notes { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public TimeOnly? NotifyAt { get; set; }

    public bool IsDone { get; set; } = false;

    public DateTime? DoneAt { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;

    public AutoLogMode AutoLogMode { get; set; } = AutoLogMode.Add;

    public int? CompletionTagId { get; set; }

    [ForeignKey(nameof(CompletionTagId))]
    public Tag? CompletionTag { get; set; }

    public int? MonitorDaysBack { get; set; }

    public DateOnly? MonitorFromDate { get; set; }

    public DateOnly? MonitorToDate { get; set; }
}
