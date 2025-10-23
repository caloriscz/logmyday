namespace LogMyDay.Shared.DTOs;

public class NotificationBackup
{
    public string TagKey { get; set; } = string.Empty; // Reference to TagBackup.TagName
    public string? NotificationText { get; set; }
    public TimeSpan? NotBeforeTime { get; set; }
    public TimeSpan? NotAfterTime { get; set; }
    public int MaxNudges { get; set; } = 3;
    public TimeSpan? NudgeInterval { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime DateCreated { get; set; }
    public DateOnly? LastDeliveryDate { get; set; }
    public int DeliveriesOnLastDate { get; set; }
}
