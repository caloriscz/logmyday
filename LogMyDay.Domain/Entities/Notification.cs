using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    public required int TagId { get; set; }

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;

    // If user does not fill in text, prefilled text from app will be used
    public string? NotificationText { get; set; }

    // Optional daily window constraints (e.g., earliest 06:00, latest 07:00)
    public TimeSpan? NotBeforeTime { get; set; }
    public TimeSpan? NotAfterTime { get; set; }

    public int MaxNudges { get; set; } = 3;  // extra "re-nudges" if ignored
    public TimeSpan? NudgeInterval { get; set; } = TimeSpan.FromMinutes(15);

    public bool IsActive { get; set; } = true;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}
