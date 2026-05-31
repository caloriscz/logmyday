using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

/// <summary>
/// Per-tag-per-day reversible "value is closed/finite for today" lock.
/// When set, new <see cref="Activity"/> attempts for the same (UserId, TagId, Date) are
/// rejected (HTTP 409). Auto-set on first activity for non-repeatable tags; repeatable
/// tags require the user to lock manually. Reminder skip semantics (Phase 4) honor this
/// lock as sticky for the date.
/// </summary>
public class TagDayLock
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public int TagId { get; set; }

    [ForeignKey(nameof(TagId))]
    public Tag? Tag { get; set; }

    public DateOnly Date { get; set; }

    public bool IsLocked { get; set; }

    public DateTime SetAt { get; set; } = DateTime.UtcNow;

    public DayLockSetBy SetBy { get; set; } = DayLockSetBy.User;

    [MaxLength(200)]
    public string? Reason { get; set; }
}
