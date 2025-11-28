namespace LogMyDay.Shared.DTOs;

public class NotificationDeliveryRequest
{
    /// <summary>
    /// Exact timestamp (UTC) when the notification was displayed to the user.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>
    /// Local date (based on the user's app context) the notification belongs to.
    /// Used for daily reset of delivery counters.
    /// </summary>
    public DateOnly LocalDate { get; set; }

    /// <summary>
    /// Total number of deliveries that have occurred on <see cref="LocalDate"/> including this one.
    /// Enables idempotent updates across multiple clients.
    /// </summary>
    public int DeliveriesOnDate { get; set; }

    /// <summary>
    /// Optional UTC timestamp indicating when the client believes the next delivery is allowed.
    /// The server will sanitize this against minimum intervals and schedule limits.
    /// </summary>
    public DateTime? NextEligibleSendAfterUtc { get; set; }
}
