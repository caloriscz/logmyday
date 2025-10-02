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

    /// <summary>
    /// The last date (in the user's local context) when this notification was delivered.
    /// Used to reset delivery counts daily across clients.
    /// </summary>
    public DateOnly? LastDeliveryDate { get; set; }

    /// <summary>
    /// Number of deliveries that have occurred on <see cref="LastDeliveryDate"/>.
    /// Includes the initial send and any nudges so clients stay in sync.
    /// </summary>
    public int DeliveriesOnLastDate { get; set; }

    /// <summary>
    /// Absolute timestamp (UTC) of the most recent delivery, used for auditing.
    /// </summary>
    public DateTime? LastDeliverySentAtUtc { get; set; }

    /// <summary>
    /// Next eligible send timestamp (UTC). Schedulers must not fire before this moment
    /// to respect nudge intervals across devices.
    /// </summary>
    public DateTime? NextEligibleSendAfterUtc { get; set; }
}
