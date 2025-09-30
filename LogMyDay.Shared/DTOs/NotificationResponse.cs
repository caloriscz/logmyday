namespace LogMyDay.Shared.DTOs;

public class NotificationResponse
{
    public int Id { get; set; }
    public int TagId { get; set; }
    public string? TagName { get; set; }
    public string? NotificationText { get; set; }
    public TimeSpan? NotBeforeTime { get; set; }
    public TimeSpan? NotAfterTime { get; set; }
    public int MaxNudges { get; set; }
    public TimeSpan? NudgeInterval { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}
