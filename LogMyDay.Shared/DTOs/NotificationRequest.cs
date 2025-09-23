namespace LogMyDay.Shared.DTOs;

public class NotificationRequest
{
    public int TagId { get; set; }
    public string? NotificationText { get; set; }
    public TimeSpan? NotBeforeTime { get; set; }
    public TimeSpan? NotAfterTime { get; set; }
    public int MaxNudges { get; set; } = 3;
    public TimeSpan? NudgeInterval { get; set; } = TimeSpan.FromMinutes(15);
    public bool IsActive { get; set; } = true;
}
