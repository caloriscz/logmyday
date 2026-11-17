using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

public class Reminder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }

    public string? Notes { get; set; }

    public TimeOnly? NotifyAt { get; set; }

    public bool IsDone { get; set; } = false;

    public DateTime? DoneAt { get; set; }

    public DateTime? SkippedAt { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;

    public AutoLogMode AutoLogMode { get; set; } = AutoLogMode.Add;

    public int? CompletionTagId { get; set; }

    [ForeignKey(nameof(CompletionTagId))]
    public Tag? CompletionTag { get; set; }

    public DateOnly? MonitorFromDate { get; set; }

    public DateOnly? MonitorToDate { get; set; }

    public bool AllowUnfilled { get; set; } = false;

    /// <summary>The trigger that created this reminder, if any. Such a reminder is limited to its
    /// one day by the monitoring window.</summary>
    public int? TriggerId { get; set; }

    [ForeignKey(nameof(TriggerId))]
    public TagTrigger? Trigger { get; set; }

    /// <summary>The day of the trigger's first tag that created this reminder; one reminder per
    /// trigger and day.</summary>
    public DateOnly? TriggerDay { get; set; }

    public ICollection<ReminderDay> Days { get; set; } = new List<ReminderDay>();
}
