using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

/// <summary>
/// Per-reminder, per-period-day state. One row per <c>(ReminderId, Date)</c>, where <see cref="Date"/>
/// is the local period day — the calendar day for Daily/None reminders, the week-start date for
/// Weekly. Holds that day's done/skip state and recorded value independently of every other day,
/// replacing the single <c>DoneAt</c>/<c>SkippedAt</c> scalars on <see cref="Reminder"/> (a remnant
/// of the era when Basic todos and Reminders shared one table). Created lazily — no row means nothing
/// was done or skipped that day.
/// </summary>
public class ReminderDay
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public int ReminderId { get; set; }

    [ForeignKey(nameof(ReminderId))]
    public Reminder? Reminder { get; set; }

    public DateOnly Date { get; set; }

    public bool IsDone { get; set; }

    public DateTime? DoneAt { get; set; }

    public bool IsSkipped { get; set; }

    public DateTime? SkippedAt { get; set; }

    public string? CompletionValue { get; set; }
}
